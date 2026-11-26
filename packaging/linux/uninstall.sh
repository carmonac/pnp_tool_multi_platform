#!/bin/sh
# Removes what install.sh added. The program folder and your settings (~/.config/PnpTool)
# are left alone.
set -eu

data="${XDG_DATA_HOME:-$HOME/.local/share}"
rm -f "$data/applications/pnptool.desktop" "$data/icons/hicolor/256x256/apps/pnptool.png" "$data/mime/packages/pnptool.xml"

if command -v update-mime-database > /dev/null 2>&1; then
    update-mime-database "$data/mime" > /dev/null 2>&1 || true
fi
if command -v update-desktop-database > /dev/null 2>&1; then
    update-desktop-database "$data/applications" > /dev/null 2>&1 || true
fi

echo "PNP Tool was removed from the applications menu."
