# PNP Tool for macOS and Linux

A cross-platform port of **PNP Tool**, the print-and-play utility for laying out
cards, tokens and booklets on printable PDF sheets.

The original PNP Tool is a Windows-only application written by **Jupklass**.
This project brings it to macOS and Linux. It aims
to produce the same PDF output and to read and write the same project files
(`.pnp`, `.pnps`, `.pnpa`, `.pnpc`, `.pnpm`, `.pnpt`) as the Windows version.

> **Status:** in development. The program runs and makes PDFs on macOS and Linux, but some
> windows of the original are not ported yet ([what is missing](#not-ported-yet)) and it has
> had little use on real machines. Keep a copy of your projects.

## Installing

Packages are on the [releases page](https://github.com/carmonac/pnp_tool_multi_platform/releases).
They carry everything the program needs; there is nothing else to install.

### macOS

Requires macOS 14 or later.

1. Download `PnpTool-<version>-macos-arm64.dmg` for a Mac with Apple Silicon (M1 and later),
   or `PnpTool-<version>-macos-x64.dmg` for a Mac with an Intel processor.
2. Open it and drag **PNP Tool** to **Applications**.

After the first start, `.pnp`, `.pnps` and `.pnpa` files open with a double click. Screen
capture and the colour eyedropper ask for permission to record the screen the first time.

### Linux

For x64 (Intel and AMD) and arm64, as an AppImage or a `.tar.gz`. Use the files named
`x86_64` or `x64` on an ordinary PC, and `aarch64` or `arm64` on ARM machines.

**AppImage**, a single file that runs from wherever it is:

```bash
chmod +x PnpTool-*.AppImage
```

```bash
./PnpTool-*.AppImage
```

It needs FUSE, which most desktops have; without it, add `--appimage-extract-and-run`.

**`.tar.gz`**, to have PNP Tool in the applications menu and open `.pnp` files with it:

```bash
tar -xzf PnpTool-*-linux-x64.tar.gz
```

```bash
PnpTool-*-linux-x64/install.sh
```

The program stays in the folder it was unpacked to, so unpack it where it is to live.
`uninstall.sh` removes the menu entry. No administrator rights are needed.

From the system the program needs fontconfig, X11 and ICU, which desktop installs have. On a
minimal system:

| System | Packages |
|---|---|
| Debian, Ubuntu | `libfontconfig1 libx11-6 libice6 libsm6 libicu74` (the ICU package is named after its version) |
| Fedora | `fontconfig libX11 libICE libSM libicu` |

Screen capture uses one of `import`, `scrot`, `maim`, `grim`, `gnome-screenshot` or
`spectacle`, and the calculator button one of `gnome-calculator`, `kcalc`, `mate-calc`,
`galculator`, `qalculate-gtk` or `xcalc`.

### Your files

Settings, recent files, the log and your own styles are kept in
`~/Library/Application Support/PnpTool` on macOS and `~/.config/PnpTool` on Linux. Projects
made with PNP Tool for Windows open here, and the other way round. A project stores the
paths of its images, so one brought from another machine needs its images at those paths.

### Updates

When it starts, the program asks GitHub which versions of this project are published and, if
there is a newer one, offers to open its page. That is all: the request carries nothing about
you or your machine, and nothing is downloaded or installed. It can be turned off in
**Help ▸ PNP Tool** (the about window), with *Update notification*:

| Setting | Told about |
|---|---|
| None | Nothing; the program does not look |
| Major | A change in the first number of the version |
| Minor (default) | A change in the first or second number |
| Test | Any newer version, also those published for testing |

The **Check for Updates** button under it looks at once and tells of any newer version, whatever the
setting. The settings and their meaning are those of the Windows version.

## Technology

- .NET 11, C#
- [Avalonia](https://github.com/AvaloniaUI/Avalonia) for the user interface
- [iText](https://itextpdf.com/) for PDF generation
- [Magick.NET](https://github.com/dlemstra/Magick.NET) for image processing and
  colour management
- [PDFium](https://pdfium.googlesource.com/pdfium/), through PDFtoImage, to draw PDF pages

## Building

Requires the .NET 11 SDK (the exact version is pinned in `global.json`).

```bash
dotnet build
```

```bash
dotnet test
```

```bash
dotnet run --project src/PnpTool.App
```

The tests need no display and never use the network. Before a release, run them a second
time with the code optimised from the first call, which is how it ends up running in the
program after a while and has shown a fault the normal run did not:

```bash
DOTNET_TieredCompilation=0 dotnet test
```

### Packaging for macOS

```bash
scripts/package-macos.sh
```

builds `PNP Tool.app` and a disk image for Apple Silicon and for Intel under `dist/` (pass
`arm64` or `x64` for only one). The program carries its own .NET runtime. The packages are not
signed with a developer identity: they run on the Mac that built them, while a downloaded copy
is stopped by Gatekeeper.

To sign them with a Developer ID and have Apple notarise them, which a download needs, see
[docs/macos-signing.md](docs/macos-signing.md).

### Packaging for Linux

```bash
scripts/package-linux.sh
```

builds, for x64 and arm64, a `.tar.gz` and an AppImage under `dist/` (pass `x64` or `arm64` for
only one). It runs on Linux and on macOS. The AppImage needs `mksquashfs` (`squashfs-tools`, or
`brew install squashfs` on macOS) and downloads the AppImage runtime once; without them only
the `.tar.gz` is made.

The program carries its own .NET runtime, Skia, ImageMagick and PDFium; what it needs from
the system is listed under [Installing](#linux).

## Differences from the Windows version

- No telemetry, no news page and no connection to the servers of the original. Newer versions
  are looked for among the releases of this project, and the program only links to them: there
  is no updater.
- No presets, logo or colour profile of the printing company the Windows version is tied to.
- Settings, recent files, the log and your own styles are kept in a per-user folder instead of
  beside the program: `~/Library/Application Support/PnpTool` on macOS and `~/.config/PnpTool`
  on Linux.
- Images are converted between RGB and CMYK through their ICC colour profiles. The Windows
  version converts by plain arithmetic, so CMYK colours differ: here they follow the chosen
  profile, and black is printed with all four inks rather than black ink alone.
- The bundled Windows-only helper programs (nanDECK, PosteRazor, AutoCaptura)
  are not included. Posters and screen captures have tools of the program's own in place of
  PosteRazor and AutoCaptura; the calculator button starts the calculator of the system.
- On macOS the commands are also in the menu bar at the top of the screen, and the window
  opens where it was last closed, on the monitor it was on.
- There is no splash screen: the program starts straight into its window.
- Images can also be added from a folder with a button of its own, and from compressed files
  (zip, rar, 7z, cbz, cbr), chosen in the file dialog or dropped on the cards. The images of a
  compressed file are written to a folder beside it with its name, and files already there
  are left as they are. The Windows version takes folders only when they are dropped.
- Screen capture and the colour eyedropper need the system's permission to record the
  screen on macOS, and one of `import`, `scrot`, `maim`, `grim`, `gnome-screenshot` or
  `spectacle` on Linux.

### Not ported yet

- The window to set up the booklets. A project that has these settings keeps them when it
  is saved, but they cannot be changed here.
- The program settings window and the one shown on the first start. Language, units and
  resolution limits follow the settings file; the update notification is in the about window.
- Numbered tickets (`.pnpt`). The Windows version no longer makes them either: it answers
  such a file with "Funcionalidad Obsoleta", and so does this one.
- Paper miniatures. Nothing in the Windows version starts them either: no style or button
  sets that kind of project, and what they produce is a package for the printing company.
- The Tabletop Simulator sheet and the 7 pixel dice.

### Defects of the Windows version fixed here

Found while porting PNP Tool 20.11.1, and listed so that they can be fixed in the Windows
version too. Names in `code` are the classes and methods of the original where the defect is.
Those marked **(seen)** were also reproduced in the Windows program itself; the rest were
found by reading its code, and each fix is covered by a test in this repository.
[docs/porting-map.md](docs/porting-map.md) has more detail on every one.

#### Images

- **PNG resolution is read in the wrong unit (seen).** PNG stores dots per metre and Magick
  reports them per centimetre, but `ImagenesGeneral` takes the number as dots per inch. A
  300 dpi PNG counts as 118 dpi and comes out 2.54 times larger than the same JPEG: the image
  panel shows a 63.5 × 88.9 mm PNG as 161.44 × 226.02 mm.
- **Mirror bleed that only adds height spoils the picture.** With no width to add, the side
  strips are cropped with width 0, which in Magick keeps the whole image, so a left-right
  mirrored copy is pasted over the card.
- **RGB ↔ CMYK conversion does not use the ICC profiles.** `ConvertirRGBaCMYK_v2` and
  `ConvertirCMYKaRGB` change the colour space first and then ask Magick to transform between
  profiles; Magick skips the transform because the source profile no longer matches the
  image. The result is plain arithmetic (black becomes 0/0/0/100) and the converted CMYK
  image still carries its sRGB profile.
- **Plain RGB → CMYK of pure black divides by zero.**
- **Custom cards (`CartaPersonalizada`).** A missing picture is replaced by the blank image
  and then the missing file is opened all the same, which fails. A picture stored in grey
  turns a coloured background grey.
- **Back pattern (`CreaPatronTrasera`).** The last column and row, when cut off by the edge,
  are filled with a slightly stretched copy of the pattern instead of continuing the tiles.

#### Adding images (`FormSubirImagenesModelo`, `FormPrincipalModeloLocal`)

- **"N Front N Back" fails with more fronts than backs.** `GeneraRutaNN` repeats each back
  as many times as the smaller of the two numbers and each front in a cycle of that same
  length, so with 3 fronts and 2 backs it reads a third back that is not there
  (`IndexOutOfRangeException`), and it would leave fronts out. Every front now goes with
  every back whichever there are more of; with as many backs or more the result is the same
  as before.
- **An odd file out in "Pairs Front/Back" gets a broken back.** `GeneraRuta11` gives the last
  front an empty path as back and `Subir_imagenes_WPF` reads it as an image, so the card
  shows the image-not-found picture on its back. The card is now left without back.
- **The warning about more than 50 copies names only one file.**
  `RevisaNumeroCopiasImagenesASubir` assigns the message for each file instead of adding to
  it, so only the last file found is shown. All are listed now.
- **Answering the "many images" question does the opposite of what is asked.**
  `CompruebaAlarmaImagenes` asks whether to use "process folder" instead: after Yes the
  images are not added and nothing else happens, and after Cancel they are added like after
  No. Yes now opens that tool with the folder of the images in its list, and Cancel adds
  nothing.
- **The colour space warning is almost never shown.** `IncongruenciaEspacioColorCartaProyecto`
  looks only at the first card of those being added, and marks the once-a-session warning as
  given even when that card is fine. With the warning turned off in the settings, as it
  comes, only the first image added after starting the program can raise it. Every card
  added is looked at now, and the one warning is spent only when it is shown.

#### Stacked layouts (`PanelImagenes.GenerarMazoDesdePaneles_Apilar`, `_ApilarPorEje`)

- **Cards with copies make the stacked layout fail.** `GenerarMazoDesdePaneles_Apilar` sizes
  the deck by the number of panels (`TotalElementos`) but fills it with one card for each
  copy, so a panel with more copies than free places writes past the end of the deck
  (`IndexOutOfRangeException`). The deck is sized by the cards now.
- **Creating a stacked PDF resets the copies of every panel to 1.** Both methods set
  `PanelEntrada.CartaLocal.NumerodeCopias = 1` on the card of the panel itself and never put
  the number back, so the next PDF has one of each. They work on copies of the cards now.
- **"By rows" loses cards past the first block.** `GenerarMazoDesdePaneles_ApilarPorEje`
  moves on to the next column when a block is full but never to the next block, so with more
  cards than block size × rows × columns the column number goes past the sheet and the cards
  land on places already taken. Further cards start a new block now, as in the stacked kind.
- **Sheets with no cards at the end.** When the cards do not fill a block, the block is
  shortened to the sheets needed, but `GenerarMazoDesdePaneles_Apilar` had already worked out
  the size of the deck from the block as set, and the last block of a longer deck is never
  shortened: the PDF ends with sheets of empty places. Those sheets are left out; the order
  of the cards is unchanged.

#### Making the PDF

- **A template chosen in the main window gives blank sheets (seen).** Choosing the group
  "Plantillas avanzadas" and a template leaves the project as `TipoProyecto` `Carta` with a
  card size of 0, instead of `EstilosAvanzados`, so every image is drawn with no size. The
  same project with `TipoProyecto` set to `EstilosAvanzados` draws the template correctly.
- **The bundled gutterfold style cuts cards off (seen).** `Cartas_Gutterfold.pnps` stores its
  paper already wider than tall and also marked as landscape, so the swap made before the
  PDF turns it upright; its four columns do not fit and the first and last card of each row
  are cut by the sheet. Storing the paper upright in the style file fixes it.
- **The checks before the PDF use the paper before it is turned.**
  `CompruebaConfiguracion` checks the paper as stored and the swap for landscape comes
  afterwards, so a layout can pass the check and still not fit the sheet written.
- **The shared back turns 180° after every gutterfold PDF**, and the turns add up from one
  PDF to the next.
- **Repeated cards are stored once per copy.** The image data is passed again for every
  copy of a card, so the PDF grows with the number of copies.
- **The calibration strip is drawn twice**: also on the extra sheet written for the last
  shared back.
- **Lists of images (`GenerarListadoImagenes`).** The one-sheet list for a shared back is
  indexed wrongly when rows and columns differ, and the pocketfold and advanced-template
  lists read past the end of their arrays in edge cases.
- **Splitting by maximum size** starts with a blank one-page document when the first sheet
  alone exceeds the limit.
- **"Mix" cut lines** save the graphics state twice and restore it once.
- **Missing files end the PDF with an error**: a project without an ICC profile file, and
  a page watermark whose file is missing.

#### PDF tools

- **Rotation has no effect (`WindowModificarAlineacionPdfModelo`).** The alignment angle,
  and with it the back rotation of a project, writes a small shift after the page content,
  where it changes nothing.
- **Page lists drop pages.** In a list such as "1;4-5" the range discards what came before.
- **Merging fails with a PDF not made by PNP Tool** (`FormUnirPdfModeloIText`), because of
  the document properties it expects.
- **Extracting images (`FormExtraerModelo`).** The copy with a higher stated resolution is
  always discarded, by an inverted check.
- **Unique file names** loop for ever once 1000 names are taken.

- **Book cover: the sheet for covers is used upright.** `WindowPortadaLibro` always works
  with `PapelHorizontal` set, and both the check (`CompruebaConfiguracionCabeEnHoja`) and
  the writer (`FicheroPdfMaquetadoLibro.InicializarPDF`) swap width and height. The sheet
  offered by default is stored as 500 × 330, already on its side, so it becomes 330 wide
  by 500 tall: covers that fit the sheet are refused as too wide.
- **Extracting images from several PDFs brings only the last one to the panel.**
  `ListadoImagenesLocal` holds the images of the last file processed.

#### Card search (`Scryfall`)

- **A set is cut at 175 cards (seen in the answers of the service).** `DescargaSets` reads
  one page of the search; Scryfall answers 175 cards a page with `has_more` and
  `next_page`, which are ignored. The set M10, for example, has 249 printings.
- **The cards of a set are never imported.** `ProcesaCartasdelJson` lists every card with
  an empty file path, so `CartasProcesadas` is empty and "import to lay out" does nothing
  after downloading a set.
- **A two-sided card stops the download of a deck.** `AnadeScryfallCardAcartasConImagen`
  reads `card.ImageUris.Png`, but cards printed on both sides have their images in
  `card_faces` and no `image_uris`.
- **A line that is not a card becomes a search.** `ParseLine` returns the card "e" of the
  set "e" for a line it cannot read, such as "Sideboard".
- **Deck file names are not cleaned.** A name such as "Fire // Ice" or one with ":" cannot
  be written as a file; the names of a set are cleaned with `LimpiarNombreArchivo`, those
  of a deck are not.
- **Copies are lost on import.** The quantity of the deck list only reaches the file name
  ("_x4"); every card is added to the panel once.

#### Screen capture (`AutoCaptura`)

- **The interval is not the one typed.** `Intervalos` is read once, when the window is
  built, so the box has no effect; and the timer both fires every `Intervalos` seconds and
  counts `Intervalos` ticks before a capture, so pictures are taken every 4 seconds
  (2 × 2) whatever is typed.
- **Every preview leaves a file.** `Capturar(preview: true)` also saves the whole screen
  as `Capture_N.jpg` in the destination folder and advances the number, so each change in
  the size boxes writes a picture among the captures.
- **The start boxes change the size.** `TextBox_TamanoX_TextChanged` puts the number typed
  in "start X" into the width; the height and "start Y" typed are never read while the
  window is open, and an empty width or "start X" box throws.
- **Stopping restarts the numbering**, so the next run overwrites the captures of the one
  before.

#### Windows and controls

- **Decimal boxes read their value in the language of the program.** A typed comma becomes
  a point, but the `Valor` property reads the text in the current culture, so "1.5" gives 15
  in Spanish; it also throws on an empty box, as the integer box does.
- **The numeric box** overflows at the largest number when its button is pressed.
- **Cancelling the font window keeps the changes**, because it edits the font it was given.
- **The wheel with Ctrl does not zoom the panel**: it moves the slider by 1, which snaps
  back to the same tick.
- **Panel pictures** show only one of the changes of a face (a turn, or else one
  mirroring), while the PDF applies them all.
- **Changing the image of a back** copies the new image onto it as a front face.
- **Undoing a deletion can bring back a card without its image**, because the temporary
  files of a deleted card are removed at once.
- **The kind of project sticks.** It is set when the "deck", "tabletop" or "seven pixels"
  group is chosen and never taken back, so the project keeps that kind in another group.
- **The recovery copy is deleted as soon as it is recovered**, before the work is saved.
- **A file that is not a project, or not a template, ends the program** instead of giving
  a message.
- **Accepting the window of the fronts always makes the project "custom", and that of the
  backs never.** `Control_ConfiguracionProyecto.Boton_Configurar_Cara_Frontal_Click` copies
  the new settings over the old ones and compares them afterwards, when they are already
  the same; for fronts the comparison is also missing its negation. So a style keeps its
  name after its backs are changed and loses it when the fronts window is accepted
  untouched. The settings are compared before they are copied now.
- **Opening the header window changes the size of the registration marks.**
  `Control_MarcaCalibracion.CargaConfiguracion` fills the box of the size of the marks with
  the height of the colour bar (`TamanoImagenCalibracion.Y`) instead of
  `TamanoMarcaCalibracion`, and filling the box stores it: accepting the window untouched
  leaves the marks as tall as the colour bar. The box shows the size of the marks now.
- **Typing in the header window changes the project even when it is cancelled.**
  `EncabezadoPie.Clone` shares the size of the colour bar and the place of the marks with
  the settings it copies, and the boxes of the window write straight into them. For the
  same reason `EncabezadoPie.EsIgual`, which compares those two by reference, never sees
  them change, so a style keeps its name after they are changed. Copies have their own
  values now and are compared by them.
- **Changing only the watermark does not make the project "custom".**
  `Boton_Configurar_EncabezadoPie_Click` compares the header settings and not the
  watermark. Both are compared now.
- **The colour bar's size is labelled in millimetres and taken in points.** The tooltips of
  `Control_ImagenCalibracion` name the unit of lengths, but the numbers are read as points.
  The tooltips say points now.
- **Changing the other parameters never makes the project "custom".**
  `Boton_Configurar_OtrosParametros_Click` asks `Equals` of the settings of the project and
  those of the window, two different objects, so it never sees a change (and would call the
  project custom when they were equal: the negation is missing). They are compared by value
  now.
- **The compression percentage is checked against the wrong kind of compression.**
  `WindowSetupOtrosParametros.Boton_Ok_Click` tests `OtrosParametros.ComprimirArchivo`, which
  still holds what the project had when the window opened: choosing "percentage" with 0 or
  100 is accepted, and a project that had "percentage" cannot change to another kind while
  the hidden box holds a number out of range. The kind chosen in the list is checked now.
  The message was also the name of a text that does not exist
  (`"PorcentajeCompresionIncorrecto >0 y <100"`).
- **A stacked layout with piles of 0 sheets is accepted** and fails when the PDF is made.
  The size of the pile must be 1 or more now.
- **Crosses as cut lines show a size of 0 and print with 2.** `Control_LineasDeCorte.CambiaVisibilidad_Cruz`
  gives crosses without size a size of 2 in the settings but leaves the box showing 0. The
  box shows the 2 now.

#### Image tools and packing

- **Packing a project: "create zip" and "delete files" are swapped.** `WindowComprimir`
  calls `PasaParametros(Ruta, CrearZip, Borrar)`, but the method takes
  `(RutaEntrada, BorrarEntrada, CrearZipEntrada)`. Ticking only "create zip" copies the
  images, makes no zip and then deletes the copies and the project file, leaving an empty
  folder. The start button also stays disabled after the "data missing" message.
- **Packing: images with the same file name overwrite each other.** `MueveImagen` copies
  every image into one folder by its file name alone, so two images called the same in
  different folders become one.
- **Changing images: "replace" never replaces.** `EditaImagen` compares
  `"." + FormatoSalida` (".Jpg") with the extension of the file (".jpg"), which never
  match, so a "_new" file is always written, with its extension capitalised.
- **Changing images: the resize filter chosen in the window is ignored**; the one of the
  project is used.
- **Changing images changes the project.** With a project that has no card size,
  `EditaImagen` writes the size in pixels of the image into the project's card size.
- **Cutting images: the preview and the cut disagree when working in inches.**
  `ControlCanvas` reads the space between pieces in inches and `FormCortarImagenesModelo`
  in millimetres, both as pixels.

- **Folders into PDFs: the check of the images cannot be cancelled.** The Cancel button of
  the progress window sets `FormValidarImagenesModelo.Cancelar`, which `Validar` never
  reads: every image is checked and the PDF is made anyway. Cancelling now stops the check
  and makes no PDF.

#### Importing from PdfCartas (`FicheroPdfCartas`)

- **Rows, columns and orientation are only read for a custom paper size.** In
  `LeeEstiloDisposicionImpresionProyecto` the lines that read `ManHorizontal`, `ManFilas` and
  `ManColumnas` are inside the case of the custom size, so a manual layout on A4 or A3 loses
  them. They are now read for the three papers.
- **A file that is not a PdfCartas project is not caught.** `DetectaErrores` hands the file
  to `XmlSerializer` without catching what it throws, so its own message "Error al leer el
  fichero" can never be shown. It is shown now.
- **A card without image stops the import.** `LeeElMazo` tests `Url == string.Empty` and
  then reads the first letter of `Url`, which fails when the file has no `Url` at all.

#### Files of old versions (`FicherosXml`, `ConfiguracionesViejas`)

- **The back shared by all cards is lost when an old file is upgraded.**
  `ConfiguracionProyectoV17.ConvertirAV18` copies every setting to the new version except
  `CaraTraseraIgual`, so a project of file version 14 to 17 that used the same back for all
  cards opens without it. It is copied now.
- **Opening an old project leaves a stray copy without extension.** `Comprobar_AbrirXML_V2`
  builds the name of its backup with `Path.GetFileNameWithoutExtension(Ruta + "_back.pnp")`,
  the parenthesis one word too late, so `deck.pnp` is copied to `deck.pnp_back`; then
  `AbrirXML_V2` makes the intended `deck_back.pnp` as well. Only the second is made now.
- **The backup of an old style with the extension `.xml` gets a wrong name.**
  `AbrirXMLEstilo_Viejo` removes five characters for the extension, which is right for
  `.pnps` and one too many for `.xml`: `style.xml` is kept as `styl_backup.xml`.

#### Sports deck creator (`WindowCrearMazoPersonalizadoDeportes`)

- **Every card gets the same player name.** `AnadirTextosImagenesDeportivas` draws the
  text box of the window on each card, so all the cards carry the name of the player that
  is selected when the deck is made, instead of their own.
- **A hidden team name is still printed.** Unticking the team name hides its text box, but
  `FusionarImagenes` draws it on the cards all the same.
- **Saving asks for the file twice**: `Guardar` calls `ShowDialog` two times in a row.

#### Advanced templates editor (`Window_PlantillasAvanzadas`)

- **Import changes the template.** `Boton_Importar_Click` saves the file read with the
  paper and bleed of the window, and adds the cards on show to its own.
- **Add with an empty box** says that data is missing and then adds a card of size 0.
- **A card beyond the sheet** is measured against the upright sheet even when the paper is
  on its side (`ComprobarValidez`).

## Licence

This project is licensed under the GNU Affero General Public License v3.0, see
[LICENSE](LICENSE). This licence is required by the iText library it depends on.

PNP Tool is the work of its original author. This port is not affiliated with
any printing company or professional printing service.
