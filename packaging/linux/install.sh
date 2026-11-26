#!/bin/sh
# Adds PNP Tool to the applications menu of this user and makes it the program that opens
# .pnp, .pnps and .pnpa files. Nothing is copied: the program stays in this folder, so run
# this again if the folder is moved. Needs no administrator rights. uninstall.sh undoes it.
set -eu

here="$(cd "$(dirname "$0")" && pwd)"
data="${XDG_DATA_HOME:-$HOME/.local/share}"

mkdir -p "$data/applications" "$data/icons/hicolor/256x256/apps" "$data/mime/packages"
cp "$here/linux/pnptool.png" "$data/icons/hicolor/256x256/apps/pnptool.png"
cp "$here/linux/pnptool.xml" "$data/mime/packages/pnptool.xml"
# The menu entry starts the program by its full path, quoted as desktop entries ask.
sed "s|^Exec=.*|Exec=\"$here/PnpTool\" %f|" "$here/linux/pnptool.desktop" > "$data/applications/pnptool.desktop"

if command -v update-mime-database > /dev/null 2>&1; then
    update-mime-database "$data/mime" > /dev/null 2>&1 || true
fi
if command -v update-desktop-database > /dev/null 2>&1; then
    update-desktop-database "$data/applications" > /dev/null 2>&1 || true
fi
if command -v gtk-update-icon-cache > /dev/null 2>&1; then
    gtk-update-icon-cache -q -t "$data/icons/hicolor" > /dev/null 2>&1 || true
fi

echo "PNP Tool is in the applications menu. To start it from here: $here/PnpTool"
