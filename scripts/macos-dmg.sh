#!/usr/bin/env bash
# Makes the disk image of the macOS package: the program beside a shortcut to Applications.
#
#   scripts/macos-dmg.sh <path to the .app> <path of the .dmg> <volume name>
#
# Used by package-macos.sh and sign-and-notarize.sh.
set -euo pipefail

app="$1"
image="$2"
volume="$3"

stage="$(mktemp -d)"
trap 'rm -rf "$stage"' EXIT
cp -R "$app" "$stage/"
ln -s /Applications "$stage/Applications"
rm -f "$image"
hdiutil create -volname "$volume" -srcfolder "$stage" -format UDZO -ov -quiet "$image"
