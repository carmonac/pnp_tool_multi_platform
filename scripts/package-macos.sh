#!/usr/bin/env bash
# Builds "PNP Tool.app" and a disk image for macOS, without a developer signature.
#
#   scripts/package-macos.sh            both architectures
#   scripts/package-macos.sh arm64      Apple Silicon only
#   scripts/package-macos.sh x64        Intel only
#
# Results go to dist/macos-<architecture>/PNP Tool.app and dist/PnpTool-<version>-macos-<architecture>.dmg.
# The program carries its own .NET runtime, so nothing has to be installed to run it.
#
# The bundle is signed ad hoc (no identity), which Apple Silicon needs to run anything. That
# is enough on the machine that built it; a downloaded copy is stopped by Gatekeeper until it
# is signed with a Developer ID and notarised.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"

if [[ "$(uname -s)" != "Darwin" ]]; then
    echo "This script needs macOS (codesign, hdiutil)." >&2
    exit 1
fi

case "${1:-all}" in
    all) architectures=(arm64 x64) ;;
    arm64 | x64) architectures=("$1") ;;
    *) echo "Usage: $0 [arm64|x64|all]" >&2; exit 1 ;;
esac

version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props | head -n 1)"
if [[ -z "$version" ]]; then
    echo "No <Version> in Directory.Build.props." >&2
    exit 1
fi

name="PNP Tool"

for architecture in "${architectures[@]}"; do
    echo "== $name $version, macOS $architecture"
    work="$root/dist/macos-$architecture"
    app="$work/$name.app"
    image="$root/dist/PnpTool-$version-macos-$architecture.dmg"
    rm -rf "$work" "$image"
    mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"

    dotnet publish src/PnpTool.App/PnpTool.App.csproj \
        --configuration Release \
        --runtime "osx-$architecture" \
        --self-contained true \
        --output "$app/Contents/MacOS" \
        -p:DebugType=none \
        -p:DebugSymbols=false \
        --nologo --verbosity quiet

    # AppPaths.FindAssetsFolder looks for the bundled files in Contents/Resources/assets.
    cp -R assets "$app/Contents/Resources/assets"
    rm -f "$app/Contents/Resources/assets/README.md"
    cp packaging/macos/PnpTool.icns "$app/Contents/Resources/PnpTool.icns"

    # The oldest macOS the .NET runtime of this build starts on.
    minimum="$(vtool -show-build "$app/Contents/MacOS/PnpTool" 2>/dev/null | awk '$1 == "minos" { print $2; exit }')"
    sed -e "s/@VERSION@/$version/g" -e "s/@MINIMUM_SYSTEM@/${minimum:-12.0}/g" \
        packaging/macos/Info.plist > "$app/Contents/Info.plist"
    plutil -lint "$app/Contents/Info.plist" > /dev/null

    find "$app" -name .DS_Store -delete
    xattr -cr "$app"
    codesign --force --deep --sign - "$app"
    codesign --verify --deep --strict "$app"

    # The disk image: the program beside a shortcut to the Applications folder.
    stage="$work/dmg"
    mkdir -p "$stage"
    cp -R "$app" "$stage/"
    ln -s /Applications "$stage/Applications"
    hdiutil create -volname "$name $version" -srcfolder "$stage" -format UDZO -ov -quiet "$image"
    rm -rf "$stage"

    echo "   $app"
    echo "   $image"
done
