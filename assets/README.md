# Bundled assets

Files shipped with the application. They come from the `config/` folder of PNP Tool 20.11.1
for Windows, by Jupklass. `tests/PnpTool.Core.Tests/Files/BundledAssetsTests.cs` loads and
saves every style, template, deck and ticket here and checks the folder's contents.

| Folder | Original folder | Contents |
|---|---|---|
| `styles/default/` | `estilos/defecto/` | Card, token, paper and booklet styles (`.pnps`) |
| `styles/advanced/` | `estilos/avanzados/` | Advanced templates (`.pnpa`) |
| `styles/custom/` | `estilos/personalizados/` | Example of a user style |
| `tickets/` | `estilos/soriano/` | Default ticket project (`.pnpt`) |
| `custom-decks/` | `MazosPersonalizados/` | Poker, Spanish and Uno-style decks (`.pnpm`) and their images |
| `icc/` | `icc/` | ICC colour profiles |
| `fonts/` | `fonts/` | Liberation Serif, in place of Times New Roman |
| `images/` | `Imagenes/` | Stock images: blank card, missing image, calibration marks |
| `images/flags/` | `Imagenes/flags/` | Language flags |
| `images/suits/` | `Imagenes/barajas/` | Suit symbols and masks for the deck creator |

File names are unchanged, because project files and the program refer to them by name.
`custom-decks/imagenes/` keeps its Spanish name for the same reason: the decks store
`\imagenes\poker\1_picas.png`.

## Changes from the original

Left out:

- `estilos/soriano/*.pnps`, `Imagenes/Logosoriano.*` and `icc/Soriano_CoatedFOGRA39.icc`:
  printing-company presets and logo. The ICC profile is a byte-for-byte copy of
  `icc/CoatedFOGRA39.icc`.
- `fonts/times*.ttf`: Microsoft fonts, not redistributable. Liberation Serif, which has the
  same letter widths, replaces them (see `fonts/` below).
- `Imagenes/version.png`: a watermarked stock photo the program does not use.
- `Imagenes/º.png` (a copy of `Image_NotFound.png`) and `Imagenes/barajas/mascaras.psd`
  (working file).
- `webs/`, `core.txt`, `signature`, `CustomColors.txt`, `LastPNPFiles.txt`: help pages are
  handled with the help windows; the rest is run-time state or belongs to the removed updater.

Renamed so that names match on case-sensitive file systems (Linux). The decks and the
program are unchanged; the files they point to were renamed to the spelling they use:

- `styles/default/Uno.pnps` → `uno.pnps` (`Uno.pnpm` asks for `uno.pnps`)
- `custom-decks/imagenes/poker/N_Picas.png` → `N_picas.png`
- `custom-decks/imagenes/poker/N_diamantes.png` → `N_Diamantes.png`
- `custom-decks/imagenes/espanola/N_espadas.png` → `N_Espadas.png`

- `images/flags/`: nine files that began with a capital letter (`Cat_*.png`, `De_*.png`,
  `Es_*.png`, `ru_Uhd.png`) are now all lower case, as the program asks for them.

## Licences of third-party files

- `fonts/LiberationSerif-*.ttf`: Liberation Fonts 2.1.5, from
  <https://github.com/liberationfonts/liberation-fonts/releases/tag/2.1.5>, under the SIL Open
  Font License 1.1 (`fonts/LICENSE-Liberation.txt`).
- `icc/Adobe/`: Adobe ICC profiles, under the licence in `icc/Adobe/Color Profile EULA.pdf`.
- `icc/eciRGB_v2/`: European Color Initiative profiles, under `icc/eciRGB_v2/licence.rtf`.
- `icc/sRGB-IEC61966-2.1.icc`, `icc/sRGB2014.icc`: sRGB profiles (HP / International Color
  Consortium).
- `icc/CoatedFOGRA39.icc`: copy of the Adobe profile of the same name.
- `icc/APTEC_PC10_CardBoard_2023_v1.icc`, `icc/Coated_Fogra39L_VIGC_260.icc`,
  `icc/Coated_Fogra39L_VIGC_300.icc`: shipped with the Windows version; redistribution terms
  not yet confirmed.
