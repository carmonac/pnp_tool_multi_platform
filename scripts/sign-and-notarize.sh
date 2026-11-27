#!/usr/bin/env bash
# Signs the macOS packages with a Developer ID, has Apple notarise them and staples the
# result, so that a downloaded copy opens without warnings. docs/macos-signing.md explains
# how to get the certificate and store the credentials.
#
#   scripts/sign-and-notarize.sh --identity "Developer ID Application: Name (TEAMID)" \
#                                --profile <keychain profile> [arm64|x64|all]
#
#   --identity   the signing identity, as `security find-identity -v -p codesigning` lists it:
#                the name between quotes, or the hash before it
#   --profile    the profile `xcrun notarytool store-credentials` saved in the keychain
#   --key, --key-id, --issuer
#                in place of --profile, an App Store Connect API key: the .p8 file, its key
#                ID and the issuer ID. For machines without a keychain profile, such as the
#                one that builds a release (.github/workflows/release.yml, docs/releasing.md)
#   --sign-only  sign and rebuild the disk image, without sending anything to Apple
#
# Works on what scripts/package-macos.sh left in dist/. Nothing secret is kept by this
# script, and with --profile none is given to it: the private key and the password stay in
# the keychain, which may ask for permission to use them. The .p8 file of --key is only
# handed to notarytool by its path.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"

identity=""
profile=""
key=""
key_id=""
issuer=""
notarize=true
which="all"
while [[ $# -gt 0 ]]; do
    case "$1" in
        --identity) identity="${2:-}"; shift 2 ;;
        --profile) profile="${2:-}"; shift 2 ;;
        --key) key="${2:-}"; shift 2 ;;
        --key-id) key_id="${2:-}"; shift 2 ;;
        --issuer) issuer="${2:-}"; shift 2 ;;
        --sign-only) notarize=false; shift ;;
        arm64 | x64 | all) which="$1"; shift ;;
        *) echo "Unknown argument: $1" >&2; sed -n '2,19p' "$0" >&2; exit 1 ;;
    esac
done

if [[ -z "$identity" ]]; then
    echo "--identity is needed." >&2
    exit 1
fi
# How notarytool is told who is asking: a keychain profile, or an API key.
credentials=()
if $notarize; then
    if [[ -n "$profile" ]]; then
        credentials=(--keychain-profile "$profile")
    elif [[ -n "$key" && -n "$key_id" && -n "$issuer" ]]; then
        if [[ ! -f "$key" ]]; then
            echo "The key file of --key is not there." >&2
            exit 1
        fi
        credentials=(--key "$key" --key-id "$key_id" --issuer "$issuer")
    else
        echo "--profile is needed, or --key with --key-id and --issuer (or --sign-only)." >&2
        exit 1
    fi
fi
# The identity may be given by name or by the 40 character hash that find-identity lists.
# codesign is given the hash: it does not always find a name with accented letters.
if [[ "$identity" != "-" ]]; then
    listed="$(security find-identity -v -p codesigning | grep -F "$identity" | head -n 1 || true)"
    if [[ -z "$listed" ]]; then
        echo "No signing identity matches \"$identity\". See: security find-identity -v -p codesigning" >&2
        exit 1
    fi
    if $notarize && [[ "$listed" != *'"Developer ID Application:'* ]]; then
        echo "Apple only notarises programs signed with a \"Developer ID Application\" identity." >&2
        exit 1
    fi
    identity="$(echo "$listed" | awk '{ print $2 }')"
elif $notarize; then
    echo "An ad hoc signature cannot be notarised: use --sign-only." >&2
    exit 1
fi

case "$which" in
    all) architectures=(arm64 x64) ;;
    *) architectures=("$which") ;;
esac

# The version of Directory.Build.props, unless PNPTOOL_VERSION gives another (a release
# takes it from its tag).
version="${PNPTOOL_VERSION:-$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props | head -n 1)}"
name="PNP Tool"
entitlements="$root/packaging/macos/PnpTool.entitlements"

# An ad hoc identity ("-", to try the script out) cannot have a timestamp from Apple.
timestamp="--timestamp"
if [[ "$identity" == "-" ]]; then
    timestamp="--timestamp=none"
fi

# Sends a file to Apple and waits for the answer; on a refusal, shows why.
submit() {
    local file="$1" output id
    output="$(xcrun notarytool submit "$file" "${credentials[@]}" --wait 2>&1)" || true
    echo "$output" | sed 's/^/     /'
    if ! echo "$output" | grep -q "status: Accepted"; then
        id="$(echo "$output" | awk '$1 == "id:" { print $2; exit }')"
        if [[ -n "$id" ]]; then
            xcrun notarytool log "$id" "${credentials[@]}" >&2 || true
        fi
        echo "Apple did not accept $file." >&2
        exit 1
    fi
}

for architecture in "${architectures[@]}"; do
    app="$root/dist/macos-$architecture/$name.app"
    image="$root/dist/PnpTool-$version-macos-$architecture.dmg"
    if [[ ! -d "$app" ]]; then
        echo "$app is missing: run scripts/package-macos.sh first." >&2
        exit 1
    fi

    echo "== Signing $name $version, macOS $architecture"
    xattr -cr "$app"

    # From the inside out: every file beside the program first, the bundle last. codesign
    # takes all that is in Contents/MacOS for code, the .NET assemblies too, and will not
    # sign the bundle while one of them is unsigned. The hardened runtime (--options runtime)
    # is what notarisation asks for.
    count=0
    while IFS= read -r -d '' file; do
        if [[ "$file" != "$app/Contents/MacOS/PnpTool" ]]; then
            codesign --force --options runtime $timestamp --sign "$identity" "$file"
            count=$((count + 1))
        fi
    done < <(find "$app/Contents/MacOS" -type f -print0)
    echo "   $count files signed"

    # The entitlements belong to the program itself; signing the bundle signs its executable.
    codesign --force --options runtime $timestamp --entitlements "$entitlements" --sign "$identity" "$app"
    codesign --verify --deep --strict "$app"

    if $notarize; then
        echo "== Notarising the program"
        archive="$(mktemp -d)/PnpTool.zip"
        ditto -c -k --keepParent "$app" "$archive"
        submit "$archive"
        rm -rf "$(dirname "$archive")"
        # The ticket goes inside the bundle, so that it opens on a Mac that is offline.
        xcrun stapler staple "$app"
    fi

    echo "== Disk image"
    scripts/macos-dmg.sh "$app" "$image" "$name $version"
    codesign --force $timestamp --sign "$identity" "$image"

    if $notarize; then
        submit "$image"
        xcrun stapler staple "$image"

        echo "== Checking"
        xcrun stapler validate "$app"
        xcrun stapler validate "$image"
        spctl --assess --type execute --verbose=2 "$app"
        spctl --assess --type open --context context:primary-signature --verbose=2 "$image"
    fi

    echo "   $app"
    echo "   $image"
done
