# PNP Tool for macOS and Linux

A cross-platform port of **PNP Tool**, the print-and-play utility for laying out
cards, tokens and booklets on printable PDF sheets.

The original PNP Tool is a Windows-only application written by **Jupklass**.
This project brings it to macOS and Linux. It aims
to produce the same PDF output and to read and write the same project files
(`.pnp`, `.pnps`, `.pnpa`, `.pnpc`, `.pnpm`, `.pnpt`) as the Windows version.

> **Status:** early development. Nothing is usable yet.

## Technology

- .NET 11, C#
- [Avalonia](https://github.com/AvaloniaUI/Avalonia) for the user interface
- [iText](https://itextpdf.com/) for PDF generation
- [Magick.NET](https://github.com/dlemstra/Magick.NET) for image processing and
  colour management

## Building

Requires the .NET 11 SDK (the exact version is pinned in `global.json`).

```bash
dotnet build
dotnet test
dotnet run --project src/PnpTool.App
```

## Differences from the Windows version

- No telemetry and no connection to the original update servers.
- Settings, recent files, the log and your own styles are kept in a per-user folder instead of
  beside the program: `~/Library/Application Support/PnpTool` on macOS and `~/.config/PnpTool`
  on Linux.
- Images are converted between RGB and CMYK through their ICC colour profiles. The Windows
  version converts by plain arithmetic, so CMYK colours differ: here they follow the chosen
  profile, and black is printed with all four inks rather than black ink alone.
- The bundled Windows-only helper programs (nanDECK, PosteRazor, AutoCaptura)
  are not included. Posters and screen captures have tools of the program's own in place of
  PosteRazor and AutoCaptura.
- Screen capture and the colour eyedropper need the system's permission to record the
  screen on macOS, and one of `import`, `scrot`, `maim`, `grim`, `gnome-screenshot` or
  `spectacle` on Linux.

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
