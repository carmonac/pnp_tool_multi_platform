# Publishing a version

A version is published by pushing a tag. `.github/workflows/release.yml` then runs every
test, builds the packages for macOS and Linux, signs and notarises the macOS ones, and puts
all of them on a release of the project on GitHub. Nothing has to be built by hand.

## Making a release

1. Make sure `main` is what you want to publish and that its tests pass (the *Tests*
   workflow runs on every push to `main`).
2. Tag it and push the tag:

   ```bash
   git tag v1.2.0
   ```

   ```bash
   git push origin v1.2.0
   ```

3. Watch the *Release* workflow under the **Actions** tab. It takes a quarter of an hour or
   more; most of it is Apple notarising the two macOS packages.

The tag is the version: `v1.2.0` gives packages of version 1.2.0, whatever
`Directory.Build.props` says (that number is only what a build on your own machine calls
itself). The tag must be `v` and three numbers. The program compares them as numbers to tell
its users of a newer version, and with the default setting it only tells when the first or
the second number changes.

A tag with a word after a hyphen, such as `v1.2.0-beta1`, is published as a **pre-release**:
only users who asked for test versions are told of it, and the download links of the README
keep pointing at the last final version.

### What the workflow does

| Step | Runs on | What |
|---|---|---|
| Tests | macOS and Linux | Every test project, twice: as built, and with `DOTNET_TieredCompilation=0` |
| Packages for Linux | Linux | `scripts/package-linux.sh`: `.tar.gz` and AppImage for x64 and arm64 |
| Packages for macOS | macOS | `scripts/package-macos.sh`, then `scripts/sign-and-notarize.sh` with the secrets below |
| Release | Linux | Creates the release of the tag with the six packages and their SHA-256 sums |

Each step waits for the ones before it. If a test fails nothing is built; if a package
cannot be made, or the macOS ones cannot be signed and notarised, nothing is published: there
is never a release with a part missing or with an unsigned macOS package.

The packages are published under names without the version, so that these addresses always
give the latest final version (they are the links of the README):

```text
https://github.com/<owner>/<repository>/releases/latest/download/PnpTool-macos-arm64.dmg
https://github.com/<owner>/<repository>/releases/latest/download/PnpTool-macos-x64.dmg
https://github.com/<owner>/<repository>/releases/latest/download/PnpTool-x86_64.AppImage
https://github.com/<owner>/<repository>/releases/latest/download/PnpTool-aarch64.AppImage
https://github.com/<owner>/<repository>/releases/latest/download/PnpTool-linux-x64.tar.gz
https://github.com/<owner>/<repository>/releases/latest/download/PnpTool-linux-arm64.tar.gz
```

After publishing a final version the workflow asks for each of them and fails if one does
not answer. The README itself is never changed by a release.

### If it fails

- **Tests**: fix, merge, delete the tag and tag again (below).
- **Secrets not set, or the certificate is not accepted**: see the next section.
- **Apple did not accept the package**: the log of the step shows Apple's reasons. The
  commonest are a certificate that is not a *Developer ID Application* one, and agreements
  waiting to be accepted at <https://developer.apple.com/account>.

To run it again for the same version, delete the tag and push it again. If the release was
already created, delete it first on the releases page.

```bash
git push origin --delete v1.2.0
```

```bash
git tag --delete v1.2.0
```

## The secrets

Signing needs your Developer ID certificate, and notarising needs to sign in to Apple. Both
are given to the workflow as **secrets** of the repository: GitHub keeps them encrypted, hands
them only to workflows of this repository, never to pull requests from forks, and hides them
in the logs. They are set at **Settings ▸ Secrets and variables ▸ Actions ▸ New repository
secret**.

| Secret | What it is |
|---|---|
| `MACOS_CERTIFICATE_P12` | The Developer ID Application certificate with its private key, as base64 |
| `MACOS_CERTIFICATE_PASSWORD` | The password of that `.p12` file |
| `NOTARY_API_KEY_P8` | An App Store Connect API key, as base64 |
| `NOTARY_API_KEY_ID` | The ID of that key |
| `NOTARY_API_ISSUER_ID` | The issuer ID of your team |

None of them is ever written in the repository. In the workflow the certificate goes into a
keychain made for that run and deleted at its end, and the key into a file that is deleted
too.

### The certificate

[macos-signing.md](macos-signing.md) explains how to get a *Developer ID Application*
certificate. Once it is in your keychain:

1. Open **Keychain Access**, *login* keychain, *My Certificates*.
2. Select "Developer ID Application: Your Name (TEAMID)". It must have a private key under
   it (the arrow at its left); a certificate alone cannot sign.
3. *File ▸ Export Items…*, format **Personal Information Exchange (.p12)**, and give it a
   password made for this. That password is `MACOS_CERTIFICATE_PASSWORD`.
4. Put the file in the clipboard as base64 and paste it as `MACOS_CERTIFICATE_P12`:

   ```bash
   base64 -i DeveloperID.p12 | pbcopy
   ```

5. Delete the `.p12` file, or keep it somewhere safe: with its password it is all that is
   needed to sign programs in your name.

### The key for notarisation

On your own Mac `sign-and-notarize.sh` signs in with a profile kept in the keychain
(`--profile`). A workflow has no keychain of yours, so it uses an **App Store Connect API
key**, which is a file and two identifiers.

1. Go to <https://appstoreconnect.apple.com/access/integrations/api> (*Users and Access ▸
   Integrations ▸ App Store Connect API ▸ Team Keys*). Only the Account Holder or an Admin
   can create keys; the first time, API access has to be requested there.
2. Press **+**, name it (say, `PNP Tool notarisation`) and give it the role **Developer**,
   which is enough to notarise.
3. Download the key. It is a file called `AuthKey_XXXXXXXXXX.p8` and can be downloaded
   **once**.
4. From that page take:
   - the **Key ID** of the key (the `XXXXXXXXXX` of the file name): `NOTARY_API_KEY_ID`;
   - the **Issuer ID** at the top of the page (a long identifier with hyphens):
     `NOTARY_API_ISSUER_ID`.
5. Put the file in the clipboard as base64 and paste it as `NOTARY_API_KEY_P8`:

   ```bash
   base64 -i AuthKey_XXXXXXXXXX.p8 | pbcopy
   ```

6. Delete the `.p8` file or keep it somewhere safe.

A key can be revoked on that same page at any time; the workflow then fails until a new one
is set.

### Trying the key on your own Mac

The script takes the same key, so it can be tried before the first release:

```bash
scripts/package-macos.sh arm64
```

```bash
scripts/sign-and-notarize.sh --identity "Developer ID Application" --key AuthKey_XXXXXXXXXX.p8 --key-id XXXXXXXXXX --issuer 00000000-0000-0000-0000-000000000000 arm64
```

## What has not been tried

The workflows were written without being run: GitHub Actions only runs them on GitHub. The
packaging and signing scripts they call were run on a Mac, with the version given from
outside as the workflow does, but the first tag is the first real test of the whole. In
particular the tests have never been run on Linux; if they fail there for a reason of the
test and not of the program, that has to be sorted out before the first release. Try the
whole with a pre-release tag (`v0.1.0-test1`) first: it publishes nothing that users are told
of, and the release and the tag can be deleted afterwards.
