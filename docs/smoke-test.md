# Smoke test checklist

A walk through the program as packaged, to find what the automated tests cannot see: that
the packages start, that the native libraries load, that windows open and that the main
flows end in a file. Run it before a release and fill in the results.

Results: **ok**, **fail** (with a note), **n/r** not run, **n/a** does not apply.

## Run of 27 November 2026, version 0.1.0

| | macOS | Linux |
|---|---|---|
| Machine | Apple Silicon, macOS 27 | Ubuntu 24.04 in a container (arm64), X11 through Xvfb with openbox |
| Package | `PNP Tool.app` for arm64, signed with a Developer ID under the hardened runtime | `PnpTool-0.1.0-linux-arm64.tar.gz` |
| How | By hand, with real clicks and keys | Driven with `xdotool`, looked at through screenshots |

The Linux column is a first look, not a desktop: no Wayland, no desktop portal, no file
manager and no menu. What needs a real desktop is marked n/r.

### Starting

| Check | macOS | Linux |
|---|---|---|
| The package starts and shows the main window | ok | ok |
| Bundled files are found (styles, profiles, fonts) | ok | ok |
| Texts are drawn with their fonts, in the language of the settings | ok | ok |
| A `.pnp` handed over at startup opens (Finder / command line) | ok (`open -a`) | ok (argument) |
| A `.pnp` handed over while running opens | ok (`open -a`) | n/a |
| Double click on a `.pnp` in the file manager | n/r | n/r |
| Icon in the Dock / task bar, entry in the applications menu | n/r | n/r |
| Starts on an Intel Mac / on x64 Linux | n/r on real hardware (starts under Rosetta) | n/r on real hardware (starts emulated) |
| Starts from a downloaded, quarantined copy without warnings | n/r | n/a |
| AppImage by double click (FUSE) | n/a | n/r (starts with `--appimage-extract-and-run`) |
| `install.sh` adds the menu entry and the file types | n/a | ok for the files written; menu n/r |
| Update check at startup stays silent when there is nothing newer | ok | n/r |

### Projects and the PDF

| Check | macOS | Linux |
|---|---|---|
| Open a project made by the Windows version (`1-estandar.pnp`, relative image paths) | ok, 12 cards | ok, 12 cards |
| Project with missing images: they are listed and shown as missing | ok | n/r |
| Create the PDF: save dialog of the system, file written | ok | ok (GTK dialog) |
| The PDF has the pages and sheet size of the one made on Windows | ok (2 pages, A4) | ok (2 pages, A4) |
| The PDF opens when it is done | ok (Preview) | n/r |
| Preview | ok | n/r |
| Recent projects list | ok | n/r |
| Closing with unsaved changes asks; cancel keeps the program open; no closes it | ok | n/r |
| New, save, save as, pack | n/r | n/r |

### Cards panel

| Check | macOS | Linux |
|---|---|---|
| Thumbnails, sizes above each card, counts in the status line | ok | ok |
| Select a card, change its copies | ok | n/r |
| Context menu of a card | ok | n/r |
| Rotate from the context menu, and undo | ok | n/r |
| Drag to reorder, drop files from the file manager | n/r | n/r |
| Zoom slider | n/r | n/r |

### Ribbon tabs and windows

Each window was opened, looked at and closed; "used" means its main action was run.

| Check | macOS | Linux |
|---|---|---|
| File, Actions, Settings, Tools and Help tabs show their buttons | ok | ok (Actions, Tools) |
| Settings: group and template lists | ok | n/r |
| Settings: layout window | ok | n/r |
| Settings: save style | ok (dialog opens in the user styles folder) | n/r |
| Settings: front, header and footer, other parameters | say "not available yet" (tasks 44–46) | n/r |
| Tools: extract PDF, used with whole pages (PDFium) | ok, 2 pages at 300 dpi, added to the project | ok |
| Tools: folder (asks with the current layout) | ok | n/r |
| Tools: merge, modify images, cut, split fronts and backs | open | n/r |
| Tools: screen capture | opens; says the permission to record the screen is missing | n/r |
| Tools: book covers, modify PDF, advanced templates | open | n/r |
| Tools: import card PDF | says "not available yet" (task 39) | n/r |
| Tools: card search, poster, custom deck, sports deck | open | n/r |
| Tools: calculator | ok, opens Calculator | n/r |
| Help: links, Telegram | n/r | n/r |
| Help: about window, its pages | ok | n/r |
| Help: update notification list and search button, against GitHub | ok ("no update available") | n/r |
| Escape closes a tool window, a message and a question | ok after the fix below | n/r |

### Not run at all

- Screen capture and the eyedropper for real: the macOS permission (first run, denied,
  allowed), X11 and Wayland with and without the capture programs, two screens with different
  scaling.
- A real Intel Mac, a Mac other than the one that built the package, a real Linux desktop
  (GNOME and KDE, Wayland), Fedora with a display.
- Printing a PDF and measuring it.
- The tools used with real work: merge, cut, book covers, poster, decks, card search.

## Found in this run

Fixed:

- **Escape did not close anything.** Tool windows, messages and questions could only be
  closed with their buttons. Escape, and Command+W on macOS, now close every window but the
  main one, as the close button does (`App.CloseDialogOnKey`).
- **The program did not start on Linux** (found in task 35, when the packages were first
  run): the Skia library in the package was older than the one the program asks for.

Found then and fixed later:

- **Accessibility** (task 53). The buttons of the quick access bar had no name for assistive
  technology, and pressing a tab through the accessibility interface did not select it, so a
  screen reader user could not change tab. Buttons that show only a picture now take the name
  of their tooltip (`AccessibleNames`), and the tabs answer to being pressed
  (`RibbonTabControl`).
- **Icons on the dark theme** (task 53). New, Save As, Style and Other Parameters are dark
  icons, drawn for the light window of the original. On the dark theme every icon of the
  command bar now has a light halo round it.
- **Screen capture permission message** (task 53) was in English whatever the language; it
  is in Spanish for Spanish and Catalan now, as are the two messages of Linux.
- **macOS menu bar** only had the application menu; it has a menu for each tab since task 42.

## How to repeat it

macOS: build with `scripts/package-macos.sh`, start `dist/macos-<arch>/PNP Tool.app` and go
down the tables. To keep your own settings out of it, start it with another data folder:

```bash
open -n --env PNPTOOL_DATA_DIR=/tmp/pnptool-test "dist/macos-arm64/PNP Tool.app"
```

Linux without a desktop: in a container with `xvfb`, `openbox`, `xdotool`, `imagemagick` and
the libraries the README lists, start `Xvfb :9`, the program with `DISPLAY=:9`, click with
`xdotool mousemove X Y click 1` and look with `import -window root shot.png`. Test projects
with their images and the PDFs the Windows version made of them are in
`samples/referencia-windows` (not in the repository).
