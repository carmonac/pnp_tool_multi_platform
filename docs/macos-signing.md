# Signing and notarising the macOS packages

`scripts/package-macos.sh` builds `PNP Tool.app` and a disk image that only carry an ad hoc
signature. They run on the Mac that built them, but a copy downloaded from the internet is
stopped by Gatekeeper ("Apple could not verify…"). For a download that opens normally, the
program has to be signed with a **Developer ID** and **notarised** by Apple.
`scripts/sign-and-notarize.sh` does both; this guide covers what it needs, which is done once.

## What you need

- A Mac with Xcode or its command line tools (`xcode-select --install`).
- Membership of the Apple Developer Program (paid, yearly). A free Apple account cannot
  create the certificate below.
- To be the *Account Holder* of the membership: only that role can create Developer ID
  certificates. With a personal membership that is you.

## 1. The Developer ID Application certificate

An "Apple Development" or "Apple Distribution" certificate is not enough: those are for
running on your own devices and for the App Store. Programs given out by download need a
**Developer ID Application** certificate.

With Xcode:

1. Xcode ▸ Settings ▸ Accounts, sign in with your Apple account if it is not listed.
2. Select the team, press *Manage Certificates…*
3. Press **+** and choose **Developer ID Application**.

Xcode makes the private key, asks Apple for the certificate and puts both in your login
keychain.

If Xcode answers "This operation can only be performed by the Account Holder" although the
membership is yours (it happened with an account that had been added to Xcode before joining
the program, and signing out and in again did not help), use the web site instead: it
accepted the same account.

Without Xcode: in Keychain Access, *Certificate Assistant ▸ Request a Certificate From a
Certificate Authority…*, saved to disk; upload that request at
<https://developer.apple.com/account/resources/certificates/add> choosing *Developer ID
Application*; download the `.cer` file and double-click it.

Check it. The script takes the name between quotes, a part of it that only matches this
certificate (`"Developer ID Application"` is enough when there is only one), or the hash
before it:

```bash
security find-identity -v -p codesigning
```

```text
1) 0123…CDEF "Developer ID Application: Your Name (AB12CD34EF)"
```

The ten characters in brackets are your team ID.

Keep a copy of the certificate with its private key (Keychain Access ▸ select it ▸ *Export*,
a `.p12` file with a password) somewhere safe. Apple lets you have few of these certificates
and the private key cannot be downloaded again. Note when it expires (the web site shows the
date): programs signed and notarised before then keep working, but new ones need a new
certificate.

## 2. Credentials for notarisation

Notarising is done by sending the program to Apple with `notarytool`, which signs in with
your Apple account and an *app-specific password*, not your normal one.

1. At <https://account.apple.com>, *Sign-In and Security ▸ App-Specific Passwords*, create
   one (call it, say, `notarytool`). It is shown once.
2. Save it in the keychain under a profile name:

   ```bash
   xcrun notarytool store-credentials "pnptool-notary" --apple-id "you@example.com" --team-id "AB12CD34EF"
   ```

   It asks for the app-specific password; type it there rather than on the command line, so
   that it does not stay in the shell history. It checks the credentials with Apple before
   saving them.

The profile name (`pnptool-notary` here) is all the script needs. The password stays in the
keychain.

## 3. Sign, notarise and staple

```bash
scripts/package-macos.sh
```

```bash
scripts/sign-and-notarize.sh --identity "Developer ID Application: Your Name (AB12CD34EF)" --profile pnptool-notary
```

Add `arm64` or `x64` at the end for only one of the two. The first time, the keychain asks
whether `codesign` may use the key: answer *Always Allow*, or it asks once per file.

For each architecture the script:

1. Signs every file in `Contents/MacOS` with the hardened runtime and a timestamp, then the
   bundle itself with the entitlements of `packaging/macos/PnpTool.entitlements`.
2. Sends the program to Apple and waits for the answer, usually a few minutes.
3. Staples the notarisation ticket into the `.app`, so that it opens on a Mac with no
   connection.
4. Builds the disk image again with the stapled program, signs it, has it notarised and
   staples it too.
5. Checks the result with `stapler validate` and `spctl --assess`. The last lines should read
   `accepted` and `source=Notarized Developer ID`.

The signed and notarised files replace the ones in `dist/`:
`dist/PnpTool-<version>-macos-<arch>.dmg` is what goes on the releases page.

`--sign-only` stops before sending anything to Apple; it is there to try the signing step on
its own.

In place of `--profile` the script takes an App Store Connect API key (`--key`, `--key-id`,
`--issuer`), which is what the release workflow uses, since it has no keychain of yours:
[releasing.md](releasing.md).

### Trying it as a download

A file made on your own Mac is not put through Gatekeeper. To see what a user will see,
upload the disk image somewhere, download it with a browser and open the program from it; or
look at the verdict directly:

```bash
spctl --assess --type execute --verbose=2 "dist/macos-arm64/PNP Tool.app"
```

## Why it is done this way

- **Every file is signed, not only the libraries.** `codesign` takes whatever is in
  `Contents/MacOS` for code, the .NET assemblies (`.dll`) included, and refuses to sign the
  bundle while one of them is unsigned. `--deep` would do it in one step, but Apple advises
  against it for distribution: it gives the entitlements to every file and hides mistakes.
- **Hardened runtime and one entitlement.** Notarisation requires the hardened runtime. Under
  it a program may not make machine code while it runs, which is exactly what the .NET
  runtime does, so `com.apple.security.cs.allow-jit` is given to the program. Nothing else was
  needed: with that alone the program starts, opens a project and loads Skia, HarfBuzz,
  Avalonia's native library and ImageMagick.
- **No `disable-library-validation`.** Under the hardened runtime a program only loads
  libraries signed by the same team or by Apple. The script signs all of them with your
  identity, so they pass. (This is also why an *ad hoc* signature with the hardened runtime
  gives a program that does not start: there is no team.)
- **Two submissions.** A ticket can only be stapled to what was sent, or to something inside
  it that still exists outside. Stapling the `.app` before it goes into the disk image is what
  makes the copy dragged to Applications open without a connection.
- **No secrets in the repository or the script.** The private key and the app-specific
  password stay in the keychain; the script is given names that point to them.

## When something fails

| What you see | Cause |
|---|---|
| `no identity found` | The name given to `--identity` is not exactly as `security find-identity` shows it, or the certificate has no private key in this keychain (it was made on another Mac: import the `.p12`) |
| `The timestamp service is not available` | No connection, or Apple's server is busy; run the script again |
| `errSecInternalComponent` | The keychain is locked, typically over SSH: `security unlock-keychain login.keychain` |
| `HTTP status code: 401` from `notarytool` | Wrong Apple account, team ID or app-specific password: repeat `store-credentials` |
| `status: Invalid` | The script prints Apple's log with the file and the reason. The usual ones: a binary without hardened runtime or timestamp, or signed with another kind of certificate |
| `You must first sign the relevant contracts online` | Apple changed its agreements: accept them at <https://developer.apple.com/account> |
| `source=Unnotarized Developer ID` from `spctl` | Signed, but the ticket is missing: notarisation did not finish or `stapler staple` failed |
| The program does not start after signing | Run `"dist/macos-arm64/PNP Tool.app/Contents/MacOS/PnpTool"` in a terminal to read the error. "different Team IDs" means some file was not signed with your identity |

A submission can be looked at again later:

```bash
xcrun notarytool history --keychain-profile pnptool-notary
```

```bash
xcrun notarytool log <submission id> --keychain-profile pnptool-notary
```

## What has and has not been tried

The whole script was run for both architectures with a real Developer ID certificate, on an
Apple Silicon Mac: Apple accepted the four submissions at the first attempt, the program and
the disk image were stapled, and `spctl` answered `accepted`, `source=Notarized Developer ID`
for all of them. The signed programs start and open a project (the Intel one under Rosetta).

Not tried: downloading the disk image on another Mac, and making or opening a PDF with the
signed program, which loads PDFium under the hardened runtime.
