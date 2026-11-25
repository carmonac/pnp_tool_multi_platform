#!/usr/bin/env bash
# Decompiles the original Windows PNP Tool into reference/ (git-ignored).
#
#   reference/bundle/<App>/      files unpacked from the single-file .exe bundles
#   reference/decompiled/<Asm>/  one C# project per assembly, BAML converted to XAML
#
# Usage: scripts/decompile.sh [path-to-original-app]   (default: PNP_Tool_20.11.1)
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
app="${1:-$root/PNP_Tool_20.11.1}"
bundle="$root/reference/bundle"
out="$root/reference/decompiled"

# ilspycmd targets an older runtime than the one installed; let it roll forward.
export DOTNET_ROLL_FORWARD=Major
ilspy() { (cd "$root" && dotnet ilspycmd "$@"); }

[ -f "$app/PNPTool.exe" ] || { echo "Original app not found at: $app" >&2; exit 1; }

(cd "$root" && dotnet tool restore >/dev/null)

rm -rf "$bundle" "$out"
mkdir -p "$bundle" "$out"

echo "== Unpacking single-file bundles"
for exe in PNPTool Actualizador; do
  ilspy -d -o "$bundle/$exe" "$app/$exe.exe" >/dev/null
  echo "   $exe.exe -> $(find "$bundle/$exe" -type f | wc -l | tr -d ' ') files"
done

# Put the shipped PDBs beside their assemblies so original variable names are used.
for pdb in "$app"/*.pdb; do
  name="$(basename "$pdb" .pdb)"
  for dir in "$bundle"/*; do
    if [ -f "$dir/$name.dll" ]; then cp "$pdb" "$dir/"; fi
  done
done

decompile() { # <assembly path> <reference dir>
  local dll="$1" refs="$2" name
  name="$(basename "$dll" .dll)"
  echo "== Decompiling $name"
  ilspy -p --nested-directories -usepdb -r "$refs" -o "$out/$name" "$dll" >/dev/null
  echo "   $(find "$out/$name" -name '*.cs' | wc -l | tr -d ' ') .cs, $(find "$out/$name" -name '*.xaml' | wc -l | tr -d ' ') .xaml"
}

for asm in PNPTool ClasesGlobalesPNPTool ControlFuente Dsafa.WpfColorPicker LibreriaLogs; do
  decompile "$bundle/PNPTool/$asm.dll" "$bundle/PNPTool"
done
decompile "$bundle/Actualizador/Actualizador.dll" "$bundle/Actualizador"
# Bundled screen-capture helper (not a single-file bundle).
decompile "$app/tools/AutoCaptura/Autocaptura_WPF.dll" "$app/tools/AutoCaptura"

echo "== Extracting translated resources"
for sat in "$bundle"/PNPTool/*/ClasesGlobalesPNPTool.resources.dll; do
  lang="$(basename "$(dirname "$sat")")"
  ilspy -p -o "$out/_satellites/$lang" "$sat" >/dev/null
done
echo "   $(find "$out/_satellites" -name '*.resx' | wc -l | tr -d ' ') .resx in $(ls "$out/_satellites" | wc -l | tr -d ' ') languages"

echo "Done: $out"
