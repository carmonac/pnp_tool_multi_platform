#!/usr/bin/env bash
# Builds the Linux packages: a .tar.gz and an AppImage for each architecture.
#
#   scripts/package-linux.sh            x64 and arm64
#   scripts/package-linux.sh x64        Intel/AMD only
#   scripts/package-linux.sh arm64      ARM only
#
# Results: dist/PnpTool-<version>-linux-<arch>.tar.gz and dist/PnpTool-<version>-<arch>.AppImage.
# The program carries its own .NET runtime. Runs on Linux and on macOS.
#
# The AppImage needs two things; without them only the .tar.gz is made:
#   - mksquashfs (Debian/Ubuntu: apt install squashfs-tools; macOS: brew install squashfs)
#   - the AppImage runtime, the small program at the start of every AppImage. It is downloaded
#     once from the AppImage project into dist/cache; to use your own copies, put
#     runtime-x86_64 and runtime-aarch64 in a folder and set APPIMAGE_RUNTIME_DIR to it.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"

case "${1:-all}" in
    all) architectures=(x64 arm64) ;;
    x64 | arm64) architectures=("$1") ;;
    *) echo "Usage: $0 [x64|arm64|all]" >&2; exit 1 ;;
esac

version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props | head -n 1)"
if [[ -z "$version" ]]; then
    echo "No <Version> in Directory.Build.props." >&2
    exit 1
fi

runtime_source="https://github.com/AppImage/type2-runtime/releases/download/continuous"
runtime_folder="${APPIMAGE_RUNTIME_DIR:-$root/dist/cache}"

# macOS tar would add its own attributes to the archive, which Linux tar complains about or
# unpacks as "._name" files.
export COPYFILE_DISABLE=1
tar_options=()
if tar --version 2> /dev/null | grep -q bsdtar; then
    tar_options=(--no-xattrs --no-mac-metadata)
fi

# Prints the path of the AppImage runtime for a machine name, fetching it if needed.
appimage_runtime() {
    local machine="$1" file="$runtime_folder/runtime-$1"
    if [[ ! -s "$file" ]]; then
        if [[ -n "${APPIMAGE_RUNTIME_DIR:-}" ]]; then
            echo "runtime-$machine is not in $APPIMAGE_RUNTIME_DIR." >&2
            return 1
        fi
        mkdir -p "$runtime_folder"
        echo "   downloading $runtime_source/runtime-$machine" >&2
        curl --fail --location --silent --show-error --output "$file.part" "$runtime_source/runtime-$machine" || return 1
        mv "$file.part" "$file"
    fi
    echo "$file"
}

for architecture in "${architectures[@]}"; do
    echo "== PNP Tool $version, Linux $architecture"
    case "$architecture" in
        x64) machine="x86_64" ;;
        arm64) machine="aarch64" ;;
    esac
    work="$root/dist/linux-$architecture"
    folder="PnpTool-$version-linux-$architecture"
    program="$work/$folder"
    archive="$root/dist/$folder.tar.gz"
    image="$root/dist/PnpTool-$version-$machine.AppImage"
    rm -rf "$work" "$archive" "$image"
    mkdir -p "$program"

    dotnet publish src/PnpTool.App/PnpTool.App.csproj \
        --configuration Release \
        --runtime "linux-$architecture" \
        --self-contained true \
        --output "$program" \
        -p:DebugType=none \
        -p:DebugSymbols=false \
        --nologo --verbosity quiet

    # AppPaths.FindAssetsFolder looks for the bundled files beside the program first.
    cp -R assets "$program/assets"
    rm -f "$program/assets/README.md"
    find "$program" -name .DS_Store -delete
    chmod 755 "$program/PnpTool" "$program/createdump"
    # Only for tracing .NET itself with LTTng, and it asks for a library few systems have.
    rm -f "$program/libcoreclrtraceptprovider.so"

    # The .tar.gz: the program, and what install.sh needs to put it in the menu.
    mkdir -p "$program/linux"
    cp packaging/linux/pnptool.desktop packaging/linux/pnptool.xml "$program/linux/"
    cp packaging/icon-256.png "$program/linux/pnptool.png"
    cp packaging/linux/install.sh packaging/linux/uninstall.sh "$program/"
    cp LICENSE "$program/LICENSE"
    chmod 755 "$program/install.sh" "$program/uninstall.sh"
    tar "${tar_options[@]}" -czf "$archive" -C "$work" "$folder"
    echo "   $archive"

    # The AppImage: the same program in the folder layout AppImages use, packed into one
    # file that starts with the runtime that mounts it.
    if ! command -v mksquashfs > /dev/null 2>&1; then
        echo "   no AppImage: mksquashfs is not installed (see the top of this script)"
        continue
    fi
    if ! runtime="$(appimage_runtime "$machine")"; then
        echo "   no AppImage: the AppImage runtime could not be had (see the top of this script)"
        continue
    fi

    appdir="$work/AppDir"
    mkdir -p "$appdir/usr/lib" "$appdir/usr/share/applications" \
        "$appdir/usr/share/icons/hicolor/256x256/apps" "$appdir/usr/share/mime/packages"
    cp -R "$program" "$appdir/usr/lib/pnptool"
    rm -rf "$appdir/usr/lib/pnptool/linux" "$appdir/usr/lib/pnptool/install.sh" "$appdir/usr/lib/pnptool/uninstall.sh"
    cp packaging/linux/AppRun "$appdir/AppRun"
    chmod 755 "$appdir/AppRun"
    cp packaging/linux/pnptool.desktop "$appdir/pnptool.desktop"
    cp packaging/linux/pnptool.desktop "$appdir/usr/share/applications/pnptool.desktop"
    cp packaging/linux/pnptool.xml "$appdir/usr/share/mime/packages/pnptool.xml"
    cp packaging/icon-256.png "$appdir/pnptool.png"
    cp packaging/icon-256.png "$appdir/.DirIcon"
    cp packaging/icon-256.png "$appdir/usr/share/icons/hicolor/256x256/apps/pnptool.png"

    mksquashfs "$appdir" "$work/image.squashfs" -root-owned -noappend -no-xattrs -comp zstd -quiet -no-progress > /dev/null
    cat "$runtime" "$work/image.squashfs" > "$image"
    chmod 755 "$image"
    rm -rf "$appdir" "$work/image.squashfs"
    echo "   $image"
done
