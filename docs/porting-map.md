# Porting map

How every type of the Windows PNP Tool 20.11.1 maps onto the cross-platform
solution. The decompiled sources live in `reference/decompiled/` (git-ignored);
regenerate them with `scripts/decompile.sh`.

## Source at a glance

| Original assembly | Role | C# lines | XAML files | Fate |
|---|---|---:|---:|---|
| `PNPTool` | WPF application: main ribbon window, dialogs, controls | 18,500 | 100 | UI rewritten in Avalonia (`PnpTool.App`, `PnpTool.Controls`) |
| `ClasesGlobalesPNPTool` | Domain, file formats, imaging, PDF, window models, strings | 21,300 | 0 | Ported and split into `PnpTool.Core`, `.Imaging`, `.Pdf` |
| `ControlFuente` | Font picker control | 1,600 | 17 | Rewritten in `PnpTool.Controls` |
| `Dsafa.WpfColorPicker` | Colour picker with eyedropper | 1,600 | 9 | Replaced by Avalonia's own `ColorView` (`Avalonia.Controls.ColorPicker`) inside `ColorPickerDialog`; eyedropper rewritten as `EyeDropperWindow` (task 31) |
| `LibreriaLogs` | Logger, archive extraction, downloader | 350 | 0 | Ported to `PnpTool.Core`, without the update URLs |
| `Actualizador` | Self-updater | 340 | 1 | Not ported (new update check in task 36) |
| `Autocaptura_WPF` | Screen capture helper (separate exe) | 780 | 3 | Reference for task 31 |

Types mapped below: 356 (165 PnpTool.Core, 110 PnpTool.App, 40 PnpTool.Controls, 17 PnpTool.Pdf, 14 PnpTool.Imaging, 10 not ported).

## Naming conventions

Identifiers are renamed to English while porting. **XML element and attribute
names in the `.pnp*` files stay exactly as they are** (Spanish), pinned with
`[XmlRoot]`, `[XmlElement]`, `[XmlArray]`, `[XmlArrayItem]` and `[XmlEnum]`
attributes, so files remain interchangeable with the Windows version. Files are
written as UTF-16 (`Encoding.Unicode`) as the original does.

| Original pattern | New pattern | Example |
|---|---|---|
| `Window_X`, `WindowX` | `XWindow` | `Window_Unir_PDF` → `MergePdfWindow` |
| `Control_X` (user control) | `XView` | `Control_LineasDeCorte` → `CutLinesView` |
| `PanelX_WPF` | `XPanel` | `PanelCarta_WPF` → `CardPanel` |
| `FormXModelo`, `WindowXModelo` | `XModel` | `FormExtraerModelo` → `ExtractModel` |
| `ComboboxItemX` | `XOption` | `ComboboxItemTiposPapel` → `PaperTypeOption` |
| `TiposX` (enum) | `XType` / `XKind` | `TiposLineaCorte` → `CutLineType` |
| `ConfiguracionX` | `XSettings` | `ConfiguracionLibro` → `BookSettings` |
| `…V14`, `…V0`, `…Vieja` | same name + version suffix | load-only classes for old file versions |

Glossary for members and locals:

| Spanish | English | Spanish | English |
|---|---|---|---|
| mazo | deck | carta | card |
| baraja | card set | cara | face |
| frontal / trasera | front / back | hoja, folio | sheet, page |
| papel | paper | maquetación | layout |
| líneas de corte | cut lines | marco exterior | outer frame |
| marcas de perforación | perforation marks | marca de agua | watermark |
| marca de calibración | calibration mark | separación | spacing |
| sangrado | bleed | plantilla avanzada | advanced template |
| estilo | style | fichero | file |
| ruta | path | carpeta | folder |
| listado | list | disposición | layout / arrangement |
| encabezado / pie | header / footer | lomo | spine |
| portada | cover | libro | book |
| tamaño | size | ancho / alto | width / height |
| unir / separar | merge / split | extraer | extract |
| cortar | crop / cut | comprimir | compress |
| subir (imágenes) | add (images) | dado | die |
| idioma | language | fuente | font |
| pila | stack | deshacer | undo |

## Windows-only APIs and their replacements

The "Windows-only API" column in the tables uses these tags.

| Tag | Where it occurs | Replacement |
|---|---|---|
| `WPF` | Non-UI assembly files that reference `System.Windows.*` (colours, `BitmapSource`, `FormattedText`, `Visibility`, `MessageBox`) | Own value types in Core; SkiaSharp for text; UI concerns moved to the App |
| `GDI` | `System.Drawing` bitmaps, `Graphics`, `Color`, `Rectangle`, fonts — 13 files in ClasesGlobales, 6 in PNPTool | Magick.NET / SkiaSharp; own `Color`/`Rect` structs |
| `Ribbon` | `System.Windows.Controls.Ribbon` (main window, 11 ribbon controls, recent-files gallery, and two Core classes) | Custom ribbon-style tab bar in Avalonia |
| `WinForms` | `System.Windows.Forms` dialogs and screen info | Avalonia `StorageProvider`, `Screens` |
| `Win32Dialog` | `Microsoft.Win32.OpenFileDialog` / `SaveFileDialog` | Avalonia `StorageProvider` |
| `ApiCodePack` | `WindowsAPICodePack` folder picker | Avalonia `StorageProvider.OpenFolderPickerAsync` |
| `PInvoke` | `user32.dll EnumDisplaySettings` in `MonitorHelper` | Avalonia `Screens` (bounds, scaling) |
| `ScreenHelper` | `WpfScreenHelper` | Avalonia `Screens` |
| `Interop` | `HwndSource`, `WindowInteropHelper` | Not needed |
| `Registry` | `ConfiguracionPrograma`: reads Explorer's `IconsOnly` to decide thumbnail behaviour | Removed; default to thumbnails on |
| `Process` | `rundll32 OpenAs_RunDLL`, `explorer.exe /select`, shell-execute of files and URLs, `calc.exe`, bundled tools | `IPlatformServices`: `open`/`xdg-open`, reveal in Finder/file manager |
| `WinRT` | `Windows.Networking.Connectivity` (online check) | `NetworkInterface.GetIsNetworkAvailable` / HTTP probe |
| `Sound` | `SoundPlayer` in `CodigoMorse` | Dropped unless the feature is wanted |
| `Adorner` | WPF adorners for drag preview and colour picker | Avalonia drag visuals / overlay layer |
| `DragDrop` | WPF drag-and-drop with `DataObject` | Avalonia `DragDrop` + `IDataObject` |

Other platform assumptions found in the code:

- **Ghostscript**: PDF pages are rasterised through Magick.NET, which needs Ghostscript installed (`FormExtraerModelo`, `ImagenesGeneral`). Replaced by PDFium (`PDFtoImage`).
- **Paths**: backslashes are hard-coded in paths and in saved files (`\imagenes\uno\…`). Normalise on load, write with the original separator for compatibility.
- **Install-relative data**: config, logs, temp and styles are read and written beside the executable. Moved to the per-user data folder; read-only assets ship inside the app.
- **`dynamic`** is used by `FicherosXml.GuardarXMLGenerico`; replaced with generics.
- **Static global state**: `ConfiguracionPrograma` and `FuncionesGenerales` are static and reached from everywhere. Kept static in the first port to stay close to the original, isolated behind Core so the UI does not add new dependencies on them.

## Features confirmed from the sources

Ribbon tabs of the main window:

| Tab | Contents |
|---|---|
| Quick access bar | preview, create PDF, open, save, undo |
| Archivo (File) | new, open, recent files, save, save as; pack project (zip project + images); create / preview PDF |
| Acciones (Actions) | add images, add blank image; swap fronts/backs, change front or back images, refresh; delete actions; create / preview |
| Configuración (Settings) | project style selector, save style; buttons for layout, front face, back face, header/footer, other parameters, books |
| Herramientas (Tools) | extract from PDF, process folder, merge PDFs, edit images, crop image, split fronts/backs, AutoCaptura, book covers, modify PDF (page size, alignment), advanced templates, import card PDF, MTG (Scryfall); custom decks and sports decks; external tools (calculator, nanDECK, PosteRazor, TemplateMaker links) |
| Ayuda (Help) | forum/Telegram, sleeve sizes, BGG card guide, YouTube; developer "view variables" |

Main area: image panel (card grid), back-image side panel, status bar with image/copy count, progress bar and zoom slider.

Features that were not in the original plan and are now covered by backlog tasks:

- **Old file versions**: styles and projects saved by versions 14–17 are migrated on load (`ConfiguracionesViejas`, `PlantillaAvanzadaV1`, `DisposicionImpresionV0`).
- **Ticket projects** (`.pnpt`): a separate layout writer and settings. Only the settings were ported: the writer is obsolete in the original (task 38).
- **Book layout** writer and **custom-deck** writer, separate from the main PDF writer.
- **Import from PdfCartas** tool (`FicheroPdfCartas`): it reads a project of that program, not a PDF.
- **Pack project** and **validate images**.
- **Tabletop Simulator** sheet export and **dice** generator are image generators, not web features.
- **UI languages**: 15 satellite resource sets (ca, de, en-GB, en-US, es, fa, fr, it, nl, pl, pt, ru, tr, vi, zh), 620 strings each; the language enum also lists Korean and Japanese, to be checked in task 8.

Not ported:

- Telemetry (`AnaliticaSoriano`, `DatosEstadisticos`), update URLs and the `Actualizador` program.
- Soriano branding (`Control_AgradecimientoSoriano`, `logosriano.png`, Soriano styles).
- **PaperMini export** (`Papermini`): builds an encrypted package and a low-resolution PDF for the Soriano print service. Left out (task 41, decided 2026-11-27): nothing in the Windows program sets the project type `Papermini`, no bundled style has it, and its output is a `.pnpc` package for that service. The outline effect `EfectoCrearContorno` and `PanelImagenes.GenerarMazoDesdePaneles_Papermini`, which exist only for it, are left out with it.
- `CodigoMorse` (plays Morse beeps): left out unless wanted.

## Decisions taken while porting

Recorded as tasks are completed; where these differ from the type map below, this section wins.

- **Core references `Magick.NET.Core`** (the managed-only part of Magick.NET, no native code), because
  file formats store Magick values: `FilterType` in the project settings and `ColorProfile` on card faces.
  Anything that opens or changes an image stays in `PnpTool.Imaging`.
- **Image inspection is a hook.** Models that need image properties (`Card.LoadFromFiles`,
  `Deck.LoadFromFiles`, reloading CMYK profiles after opening a project) call
  `ImageInspector.Current`, which the imaging library registers at start-up.
- **Paths in files.** Relative image paths are written with backslashes, as Windows does, and
  converted to the local separator when read (`PathHelper`). Absolute Windows paths (`C:\…`) are
  kept as they are and will not resolve on macOS or Linux.
- **File versions 14–17** are upgraded on load (task 40, below).
- **Renames that differ from the map:** `BarajaPersonalizada` → static `CustomCardSet`;
  the ICC helpers of `GestionDelColor`/`ImagenesGeneral` used by models → `IccProfiles`;
  `FuentePersonalizada`'s `System.Drawing` enums → `FontSizeUnit` and `FontStyleFlags`;
  `Tipos.Fuente.Font` → `FontSpec` holding plain values instead of WPF objects.

- **Bundled assets** live in `assets/` under English folder names (`config/estilos/defecto` →
  `assets/styles/default`, `MazosPersonalizados` → `custom-decks`, `Imagenes` → `images`,
  `Imagenes/barajas` → `images/suits`); file names are unchanged. `assets/README.md` lists what was
  left out and why. The default ticket moved from `estilos/soriano/` to `assets/tickets/`.
- **The Soriano styles are not bundled**, so the sports-deck creator, which loads
  `soriano/SorianoA4.pnps`, needs another style (task 25), and the "Soriano" settings group has
  nothing to list.
- **Letter case of file names.** Four groups of bundled files were renamed to the spelling the
  decks use, so they resolve on Linux. Files made on Windows can have the same mismatch, so code
  that opens a path read from a project should fall back to a case-insensitive match (task 9).
- **Elements of older file versions.** Bundled files still hold `ColorspaceProyecto`,
  `NombreProyecto`, `TamanoTotalImagenesEnDisco`, `PosicionPixelesX/Y` (tickets) and each length
  in four extra units. Version 20.11 does not read them and neither does the port;
  `BundledAssetsTests` names each one.

- **Program files and user files are separate.** Windows kept everything in `config/` beside the
  executable. `AppPaths` splits it: read-only `assets/` shipped with the program, and a per-user
  folder for settings, recent files, custom colours, the log, user styles and advanced templates
  (macOS `~/Library/Application Support/PnpTool`, Linux `$XDG_CONFIG_HOME/PnpTool` or
  `~/.config/PnpTool`, Windows `%APPDATA%\PnpTool`; `PNPTOOL_DATA_DIR` overrides it). The bundled
  user style and advanced template are copied there on the first run.
- **Settings.** `ConfiguracionPrograma` → static `AppSettings`; `ConfiguracionProgramaNoEstatica` →
  `StoredAppSettings`, copying values by name instead of by reflection. The file keeps its name
  (`default_appsettings.xml`), wrapper and element names. Not kept: `EnvioAnalitica`,
  `SeLanzaProgramaDesdeWindows`, `MostrarPantallaNuevoAbrir`, `V_Photoshop`. A stored temporary
  folder, last folder or ICC profile that does not exist is ignored. The language is held as its
  code (`AppSettings.LanguageCode`); switching culture comes with task 8.
- **Log.** `LibreriaLogs` → static `Log` in Core. Tags name the calling file and member (the
  original always named its own wrapper), and enter/exit lines follow the logging setting (the
  original wrote them only when forced). `LibreriaLogs` download and extraction code is not ported.
- **Start-up** (`Startup.Initialize`) sets folders, starts the log, prepares the user folder and
  loads settings. The blacklist download, update check and statistics of the original are gone, so
  starting the program makes no network connection.
- **Other renames:** `UltimosFicherosClase` → `RecentFiles` (list only; menu items are built by the
  UI); colour-file handling of `Selector_Color`/`ColorPickerDialog` → `CustomColors`; web addresses
  → `AppLinks`.

- **Translated texts.** The 620 texts of `ClasesGlobalesPNPTool` and its 15 translations are in
  `src/PnpTool.Core/Resources/Strings*.resx`, rebuilt by `scripts/import-strings.py`. English is
  the default, as in the original. Keys are unchanged, accents and spaces included, because the
  user interface looks texts up by control name. `StringResources.Get` replaces
  `DevuelveCadenaRecurso`, with the same "Need Translation …" text for a missing key.
- **Languages.** `IdiomaSoportado` → `SupportedLanguage` (flag as a file name, not an image),
  `IdiomasSoportados` → `SupportedLanguages`, `CambiaIdioma` → `AppLanguage.Change`. The program
  offers the same 14 languages. Catalan keeps the code `cat` in the settings file but uses the
  culture `ca`, under which its translation is filed. Persian has a translation but is not offered,
  as in the original. Texts tied to the printing company remain in the files and are not used.
- **Two names per enum value.** Four values were named differently in the original code and in
  files (`SinTrasera`/`Sin_trasera`, `TraseraIgual`, `TraseraDiferente`, `ImagenATamano`/`ImagenAtamano`).
  `ToFileName()` gives the file name; `ToOriginalName()` gives the code name, which is what the
  original got from `ToString()` and used in resource keys. They carry `[OriginalName]`.
- **Flag images** were renamed to lower case (`Cat_fhd.png` → `cat_fhd.png`, …) to match the names
  the code builds.

- **Images (`PnpTool.Imaging`).** The loading, density, transform and conversion parts of the
  2,084-line `ImagenesGeneral` are split into `ImageFormats`, `ImageDensity`, `MagickImageInspector`
  (registered as Core's `ImageInspector`), `ImageTransforms`, `MirrorEffect`, `ImageConversions`,
  `StockImages` and `TempImageFiles`. Effects, the contour, masks and the back pattern follow in
  task 10, colour conversion in task 11, the book-cover mirror with the book-cover tool.
- **No GDI.** Everything that passed through `System.Drawing` or WPF bitmaps is gone
  (`CortayRotaImage`, `EscalaImagen`, `CreaMapadeBits`, `ToImageFormat`, `FromBitmapSource`,
  `ConvertirMagickImageAImagen`, …). For the screen, `DisplayImages` gives raw BGRA pixels any UI
  library can wrap, and makes the panel thumbnails the original took from the Windows shell
  (32, 96, 256 and 1024 pixels).
- **Fixes to the original in this area**, each covered by a test:
  - *PNG resolution.* PNG stores dots per metre, which Magick reports per centimetre; the original
    read the number as dots per inch, so a 300 dpi PNG counted as 118 dpi and 2.54 times larger
    than the same JPEG. `ImageDensity` converts the unit.
  - *Mirror bleed that only adds height.* A crop of width 0 keeps the whole image in Magick, so the
    original pasted a left-right mirrored copy over the picture when no width was added. The side
    strips are skipped when their width is 0.
  - *Temporary file names* are built from the whole source path; they are shortened with a hash
    when they would exceed what macOS and Linux allow for one file name.
  - *Letter case.* `PathHelper.FindExisting` finds a file whose stored path differs from the disk
    only in case; used when a project's image paths are resolved.
- **Unused code not ported:** `ConvertirPngAJpg_v2`, and the forced garbage collection after reading
  a colour profile.

- **Effects and text (`PnpTool.Imaging`).** `AplicaEfecto` and its helpers → `ImageEffects`; the
  three GDI mask builders are one Magick function (`CreateMask`) with the same result, including
  the hard edge GDI left by keeping anti-aliased pixels opaque. `CreaImagenColorSolido` →
  `SolidColorImages`; `CreaPatronTrasera` → `BackPattern`; `TextToBitmap` → `TextRenderer`, drawn
  with SkiaSharp (the version Avalonia uses) instead of WPF; the image methods of
  `CartaPersonalizada` → `CustomCardImages`. Stored colours ("A_R_G_B") are `ArgbColor` in Core.
- **Fonts.** `FontCatalog` looks for a family among the fonts in `assets/fonts` first and then
  among installed ones. `FontHelper.GetFont`/`SetFont` and `FontExtension` are WPF glue and belong
  to the font picker (task 17); the serialised form was ported as `FontSpec` in task 5.
- **Outline effect not ported.** `EfectoCrearContorno` (about 400 lines of GDI pixel code) exists
  only for the paper-miniature feature, which is not ported (see "PaperMini export" above);
  `ImageEffects.Apply` returns an error for it.
- **Differences from the original in this area:**
  - *Back pattern:* the original filled the cut-off last column and row with a slightly stretched
    copy of the pattern; here the tiles continue unchanged.
  - *Custom cards:* a missing picture is really replaced by the blank image (the original then
    opened the missing file and failed), and a picture stored in grey no longer turns a coloured
    background grey.
  - *Plain RGB→CMYK of pure black* gives 0/0/0/100 instead of dividing by zero.

- **Colour management (`ColorManagement` in `PnpTool.Imaging`).** `ConvertirRGBaCMYK_v2`,
  `ConvertirCMYKaRGB(_V2)`, `CambiarPerfilColor`, `DisparidadPerfilesColor` and the per-image
  profile decisions of the PDF writer (`CargarImagenNuevaAPDFDisparidadPerfiles…`) are ported.
  `ComprobarPerfilCMYK_Soriano` is not.
- **The original's RGB↔CMYK conversion does not use the ICC profiles.** It changes the colour space
  first (plain arithmetic: red → 0/100/100/0, black → 0/0/0/100) and then asks Magick to transform
  between profiles; Magick skips that because the source profile no longer matches the image's
  colour space. The image keeps its *source* profile, so a converted CMYK image carries an sRGB
  profile. Tests pin this. **Decided: the port converts through the profiles**
  (`ColorManagement.Mode = Icc`, the default, target profile embedded), so CMYK output differs from
  Windows: colours are profile-accurate and black is four-ink instead of black ink only.
  `Original` reproduces the Windows result and is kept for comparison. The mirror effect is the one place where the original does convert through
  profiles, because there the image is still in its own colour space.
- **Changing a face's profile only retags it**, as in the original: the profile is replaced
  without converting the pixels.

- **PDF engine (`PnpTool.Pdf.Layout`).** `ListadoImagenes` → `PdfImage`; `GenerarListadoImagenes`
  → `PdfImageList` (order of the cells for each kind of project); `FicheroPdfMaquetadoNoDependienteLibreria`
  → `PageLayout` (positions, cell stepping, image preparation, footer texts);
  `FicheroPdfMaquetadoItext` → `PdfLayoutWriter` (iText: one file per sheet, then joined; shared
  backs as a reusable form; gutterfold; PDF/X-3 metadata and output intent). `PdfGenerator` is the
  headless entry point and holds the pre-flight warnings, which the original kept in the UI.
- **Left to the caller.** After writing, the original opened the PDF or its folder and, at random,
  a web page of the printing company. The writer returns a `PdfOutput` instead; nothing is opened.
- **Deferred from the writer:** Spanish-deck and
  paper-miniature post-processing (task 38; the second is not ported); the "Dice 7 pixels" list (task 28).
- **Differences from the original in the PDF engine**, each documented where it is:
  - Footer font is Liberation Serif instead of Times New Roman.
  - A project without an ICC profile file gets no output intent instead of failing.
  - Splitting by maximum size no longer starts with a blank one-page document when the first
    sheet alone exceeds the limit.
  - The one-sheet list for a shared back fills every cell (the original indexed it wrongly when
    rows and columns differed); pocketfold and advanced-template lists no longer read past the
    end of their arrays in edge cases.
  - The zero printer margin and the special warnings for the printing company's styles are gone.

- **PDF decorations.** `LineasCorteNormal` and `LineasCorteCruz` → `CutLines` (positions only);
  the drawing methods of `FicheroPdfMaquetadoItext` → `PdfLayoutWriter.Decorations.cs`: footer text,
  calibration marks and strip, solid and mirror outer frames, the five cut-line styles, perforation
  marks, file ID and page watermark. `ComboboxItemTiposLineasCorte` → `CutLineTypeOption`.
- **Differences in the decorations:**
  - *Mirror outer frame.* The original built the picture of the sheet with the tabletop generator
    (`FormTabletopModelo`, not ported yet); here the cards of the sheet are joined directly at
    300 dpi and then given the mirrored frame. Same placement and size.
  - *"Mix" cut lines* saved the graphics state twice and restored it once; both are restored.
  - The printing company's logo beside the file ID is not drawn.
  - A page watermark whose file is missing is skipped instead of failing.

- **PDF tools (`PnpTool.Pdf.Tools`).** `FormExtraerModelo` + `ImageRenderListener` +
  `ListaImagenesExtraccion` → `PdfImageExtractor`; `FormUnirPdfModeloIText` → `PdfMerger`;
  `WindowModificarTamanoHojaPdfModelo` and `WindowModificarAlineacionPdfModelo` → `PdfPageTools`;
  page rendering → `PdfRasterizer` on PDFium, so Ghostscript is no longer needed.
  `FormSepararFrontalesTraserasModelo` (it sorts image files, not PDFs) → `FrontBackSplitter` in
  Core, with `FileNames` for unique names and document IDs.
- **Differences in the PDF tools:**
  - *Rotation really rotates.* In the original the alignment angle, and with it the project's
    back rotation, wrote a small shift after the page content, where it has no effect. Here the
    content is turned about the centre of the sheet. Each back sheet is turned once.
  - *Page lists* such as "1;4-5" add up; in the original a range discarded what came before it.
  - *Merging a PDF not made by PNP Tool* no longer fails on its missing properties.
  - *Extracted images* are written with the bytes the PDF holds; the original's extra copy with a
    higher stated resolution was discarded by an inverted check and is not made.
  - *Unique names* no longer loop for ever after 1000 attempts.

- **Comparison with Windows output.** `WindowsReferenceTests` holds geometry measured from a PDF
  made by PNP Tool for Windows (nine cards of 65.5 × 90.9 mm with 1 mm bleed, 2 mm apart, shared
  back, "normal" cut lines, calibration marks and strip). Card boxes, the 24 cut lines, the marks
  and the strip match to within 0.02 points. The reference is from 2023, made by an older version
  that wrote PDFs with PDFsharp, and was re-saved by macOS, so its metadata is gone; it is not in
  the repository because it holds a published game's artwork. Known differences from it:
  - The shared-back sheet is numbered "BACK"; the reference says "1B". The 20.11 code says "BACK".
  - The page number sits 6.3 points higher than in the reference (same horizontal position). Text
    placement depends on the PDF library and on font settings the reference no longer carries.
  - The file ID and logo at the foot of the reference belong to the printing company's styles.
- **Comparison with version 20.11.1.** `Windows2011ComparisonTests` opens five projects saved by
  PNP Tool 20.11.1 for Windows, makes their PDFs and compares them with the PDFs Windows made
  from the same projects: the bundled styles "Estandar 63,5x88,9", "Gutterfold 63.5x88.9",
  "A4 25x25 8x11" (tokens) and "Libreto en A4", and a double-sided project with a back per card.
  Sheet sizes, the place and size of every image, every line and the text are the same to a
  hundredth of a point, and the sheets drawn at 72 dots per inch have no differing pixels. The
  projects, PDFs and test images are in `samples/referencia-windows` (not in the repository; the
  tests are skipped without it, or take the folder from `PNPTOOL_WINDOWS_REFERENCE`).
  Not covered: a shared back, since the standard style has none, and backs that differ from card
  to card, since the references use one back image for all.
  - *Found by the comparison:* the original turns the paper just before making the PDF, in the
    main window: when the paper is marked as landscape its width and height are swapped. This is
    now `PdfGenerator.ForOutput`, which the code that makes a PDF must call first (task 22).
  - *A defect of the original, corrected in the style file:* the gutterfold style of 20.11.1
    stores its paper already wider than tall and marked as landscape, so the swap turns it upright
    and its four columns do not fit: the first and last card of each row are cut by the sheet (the
    Windows reference shows exactly that). The bundled `Cartas_Gutterfold.pnps` now stores the
    paper upright like every other style, so new gutterfold projects come out landscape and whole.
    The swap itself is as in the original, so a gutterfold project already saved by Windows comes
    out as it does there.
  - *Also seen on Windows:* the image panel shows PNG images of 63.5 × 88.9 mm at 300 dots per
    inch as 161.44 × 226.02 mm, the unit mistake corrected here in task 9.

- **Two changes made after the comparison:**
  - *Repeated cards are stored once.* The 20.11 code passes the image data again for every copy,
    which stores a copy per card; the port draws the same image object. The 2023 reference also
    stored each image once.
  - *The calibration strip appears on the first sheet only.* The 20.11 code would also draw it on
    the extra sheet written for the last shared back.

- **Main window (task 16).** `Window_Principal_v2` becomes `MainWindow` with
  `MainWindowViewModel`. Avalonia has no ribbon, so the command bar is a `TabControl` filled from
  data: `RibbonTabViewModel` → `RibbonGroupViewModel` → `RibbonButtonViewModel`. A button is
  described by its original control name, which is also its resource key (`<name>_Small` for the
  label, `<name>` for the tooltip title, `<name>_ToolTipDescription` for the tooltip text), and
  an icon. The five tabs, their groups and the quick-access bar follow the original; the 43 icons
  are the original bitmaps of `PNPTool.Properties.Resources`, renamed in English under
  `src/PnpTool.App/Assets/Icons`.
  - *Buttons that do nothing yet* write "not available yet in this version" in the status line.
    They are wired in tasks 18 to 28. The help buttons open their web pages already.
  - *Left out:* the printing company's buttons and logo, nanDECK, PosteRazor (replaced in
    task 30), AutoCaptura (replaced in task 31) and the
    updater (see the top of this document), and the project selector of the settings tab, which
    arrives with task 21.
  - *Platform services.* `IPlatformServices` (`Platform.Services` in Core) opens URLs, files and
    folders with `open` on macOS and `xdg-open` on Linux, replacing `explorer.exe` and
    `Process.Start` with shell execution. Sound and screens are not in it yet.
  - *Window placement.* Size and the maximised state are restored from `AppSettings` and saved on
    closing; a saved size smaller than the minimum is ignored. The place and the monitor are
    remembered too since task 42 (see its section below).
  - *Startup errors* are shown in `MessageWindow` instead of the console.
  - *Not done in task 16:* splash screen, loading service, native macOS menu, keyboard shortcuts,
    and the screen-scale font resources (`EscalaPantalla`, `BaseFontSize`).
  - *Tests.* `tests/PnpTool.App.Tests` runs the window with `Avalonia.Headless` and Skia. With the
    environment variable `PNPTOOL_SCREENSHOTS` set to a folder, the tests write screenshots there.

- **Shared controls (task 17).** In `PnpTool.Controls`: `DecimalTextBox`, `IntegerTextBox`,
  `NumericBox`, `ZoomSlider`, `UnitSelector`, `OkCancelButtons`, `FilterComboBox`,
  `LanguageComboBox`, `ColorButton`, `ColorPickerDialog`, `FontPicker` and `FontDialog`, with
  `ColorConversions` and `FontConversions` between the program's `ArgbColor`/`FontSpec` and
  Avalonia's types. The duplicates in `ControlFuente` are merged into these.
  - *Decimal numbers always use a point.* As in the original, a typed comma becomes a point in
    every language, and the program reads the text with a point. The original's `Valor` property
    read the text in the current culture instead ("1.5" gave 15 in Spanish) and threw on an empty
    box; here it reads with a point and gives 0 for text that is not a number. The integer box
    likewise gives 0 instead of throwing.
  - *Numeric box.* Same limits as typed in the original; a `ValueChanged` event is added, and the
    buttons no longer overflow at the largest number.
  - *Colour picker.* Avalonia's `ColorView` replaces the Dsafa picker (the plan named
    PixiEditor's; the first-party control follows Avalonia's version and needs no third party).
    Transparency is not offered, because the program reads every stored colour as opaque. The 19
    recent colours and their file work as before. `Selector_Color` (11 colours) is not ported:
    nothing in the original uses it.
  - *Font picker.* Families are the installed ones as Avalonia lists them; the fonts bundled in
    `assets/fonts` are not offered yet (task 25, which uses the picker, has to register them).
    Variants are named from weight, slant and width. The picker works on a copy: in the original,
    cancelling the dialog left the changes in the font it was given. `FontPickerPopup` and the
    HSL/RGB/HEX helpers of `ControlFuente` are not ported (unused, or part of the old picker).
  - *Text-box events.* Avalonia raises `TextChanged` after the change, not during it, so controls
    that must react at once watch the `Text` property.
  - *Not in this task:* `PathPicker`, `ImageResolutionComboBox`, `AddImagesComboBox` and the other
    controls mapped to `PnpTool.Controls` come with the windows that use them. The main window
    still has its plain slider; `ZoomSlider` replaces it in task 18.

- **Image panel (task 18).** `PanelImagenes` becomes `ImagePanelViewModel` (the cards, the
  selection, the counts) and the view `ImagePanel`; `PanelCarta_WPF` becomes `CardPanelViewModel`
  and `CardPanel`; `PanelCartaZoom_WPF` becomes `CardZoomPanel`. `FaceImages` makes the pictures.
  - *Only the rows in view have controls.* The original put every card in a wrap panel. Here the
    cards are cut into rows that fit the width and the rows are virtualised. Measured with 500
    cards in the headless tests on the development machine: 15 MB of managed memory instead of
    200 MB, 13 MB of pictures, and about 30 ms to bring a new row into view.
  - *Pictures* are made from the files on background threads, a few at a time, at the size of the
    "panel resolution" setting (96 pixels by default). They are all kept, as in the original, so
    the two largest settings cost memory in proportion to the number of cards. The original asked
    the Windows shell for thumbnails and showed only one of a face's changes (a turn, or else one
    mirroring); here the picture gets them all, like the PDF.
  - *Selection* follows the original: a click selects one card or unselects a selected one, the
    command key (Ctrl, Cmd on macOS) adds or takes away, Shift selects up to the last card
    clicked, a click on a picture selects that face, and a click outside the cards unselects the
    cards. The original's quirks are kept: with Shift the card clicked toggles like with any
    click, and the card the range starts from is not touched.
  - *Counts* are worked out from the cards when needed instead of being kept by adding and
    subtracting on every event.
  - *Zoom.* The wheel with the command key now changes the zoom. In the original it moved the
    slider by 1, which snapped back to the same tick, so it did nothing.
  - *Buttons now working:* add image (a plain file dialog; the window for fronts and backs comes
    with task 22), add blank, add empty, swap, refresh, delete, unselect, and select all, which
    selects every card (the original first asks for a range and for cards or faces in a window
    that is not ported yet).
  - *Not in this task:* drag and drop, context menus and rotation (task 19), undo (task 20), the
    blank colour chosen in the ribbon (task 21), and stacked layouts when making the deck (task 22).

- **Image panel interactions (task 19).** Moving, cloning, deleting, copies, turning and
  mirroring faces, changing their images and the handling of dropped files are in
  `ImagePanelViewModel`; the three context menus, the dragging and the drop are in the view
  `ImagePanel`. New windows: `MoveToWindow` (`Window_moverA`), `SelectCardsWindow`
  (`Window_SeleccionarPanelesCaras`) and `ChangeImagesWindow` (`Window_Anadir_Muchas_Imagenes_v2`).
  - *Menus* have the items of `Control_MenuContextual_PanelPrincipal`, `_PanelCarta` and
    `_PanelImagen`, built when asked for instead of one menu object per card. An action chosen on
    a selected face applies to every selected face, as in the original.
  - *Dragging a card* starts after the pointer has moved six pixels with the button down, so that
    a click still selects; the original started on the press. The copy of the card that followed
    the pointer (`PanelCarta_WPF_Arrastrar`, `DragAdorner`) is not shown. Only the card dragged
    moves, as in the original, even when several are selected.
  - *Dropping files:* images, and the images inside dropped folders, are added before the card
    they fall on, or at the end. A single `.pnp` file raises `ProjectFileDropped`; opening it
    comes with task 22.
  - *"Open with"* opens the image with the program the system associates with it (Windows showed
    its "open with" window); *"show in explorer"* selects the file in Finder, and on Linux opens
    its folder (`IPlatformServices.RevealFile`).
  - *Changing the image of a back* keeps it a back; the original copied the new image as a front
    face onto it. Faces changed in a block keep their own selection state.
  - *Background colour of a face:* the menu item appears when `BackgroundColorOffered` says so;
    the project settings that decide it come with task 21.
  - *Texts:* "Seleccionadas" and "Rango" in the change-images window are fixed Spanish texts in
    the original too.
  - *Not covered by automatic tests:* the gestures themselves (opening a menu with the right
    button, dragging a card, dropping from the file manager). Their logic is tested through the
    methods they call.
  - *Still to come:* undo for all of this (task 20).

- **Undo and redo (task 20).** `PilaOperaciones` becomes `OperationStack` in Core, and the
  `_Undo` methods of the panel and main window become snapshots in `ImagePanelViewModel`.
  - *How it works.* The original kept ten commands, each with the data to reverse it, and one
    method per kind of command to undo it. Here every operation on the cards is wrapped in
    `Record`, which notes the cards before and after (which cards, in what order, and what each
    holds) and pushes a step that restores one or the other. Restoring keeps the cards that did
    not change, with their pictures.
  - *Redo is new.* The original could only undo. The limit of ten steps is kept.
  - *What is remembered:* what the original remembered (adding, deleting and moving cards, copies
    set from the menu, changing and turning images) and also cloning, swapping two faces,
    changing images in a block and the background colour. As in the original, copies typed in a
    card's own box and selections are not steps.
  - *Keys:* the system's own shortcuts, Ctrl+Z and Ctrl+Y, or Cmd+Z and Shift+Cmd+Z on macOS.
    Delete removes the selected cards, as in the original; so does the backspace key, which is
    the "delete" key of Mac keyboards. While a text box has the keyboard the keys are its own.
  - *Temporary files* of a deleted card are no longer removed at once, since undo can bring the
    card back; they are removed when the panel is emptied for another project. The original
    removed them at once, so undoing a deletion could bring back a card without its image.
  - *Still to come:* the change between single and double panels (`CambioPaneles`) becomes a
    step with task 21, which brings that change.

- **Settings tab, shared back and blank colour (task 21).** `Control_ConfiguracionProyecto` and
  `Control_SelectorProyecto` become `ProjectSettingsViewModel`, shown at the start of the
  settings tab as two lists; the lists of groups and styles are `StyleCatalog` in Core;
  `Control_ImagenTrasera` becomes `SharedBackViewModel` and `SharedBackPanel`; `ImagenBlanca`
  becomes a narrow colour button beside "add blank image"; `Window_PreguntarCambioCara` becomes
  `FaceChangeWindow`.
  - *Scope.* On the Windows ribbon the settings tab holds only the two lists and seven buttons;
    every other setting is inside the windows those buttons open. Those windows and the controls
    in them (paper, card size, spacing, cut lines, marks, watermark and the rest) are task 23.
    Here the buttons are enabled and disabled by the original's rules and still say that their
    window is not available.
  - *One face or two.* A style or layout that changes whether cards show their back rebuilds the
    cards with `Deck.ConvertToDoublePanel` or `ConvertToSinglePanel`, asking what the images are
    when the original asks. It is one undo step that also puts the settings back.
  - *Left out:* the printing company's group. The "tabletop" and "seven pixels dice" groups are
    listed, but what they make comes with task 28.
  - *A correction.* The original set the kind of project when the "deck", "tabletop" or "seven
    pixels" group was chosen and never took it back, so the project kept that kind after another
    group was chosen. Here it goes back to a card project unless the new style says otherwise.
  - *Kept from the original:* changing only the sides or the kind of back in the layout does not
    turn the project into a custom one; rows, columns, paper, card size, spacing or bleed do.
  - *Not ported:* the button that folds the shared-back panel away, and its larger view on hover.

- **Project files and making the PDF (task 22).** New, open, save, recent files, the copy for
  recovery and the creation of the PDF are in `MainWindowViewModel.Project.cs`; the checks before
  the PDF are `SettingsValidator` in Core (`FormSetupModelo.CompruebaConfiguracion`); new windows
  are `ConfirmWindow` (the yes/no/cancel message boxes) and `ProgressWindow`
  (`Window_BarraProgreso`).
  - *The window asks, the model decides.* File dialogs, questions and the progress window are
    functions the main window gives to the model, so the whole flow runs in tests without windows.
  - *Recent files* are a list at the start of the file tab; picking one opens it. The original
    showed them as a gallery inside the ribbon's menu.
  - *Recovery copy.* As in the original, every change saves a copy of the project in the
    program's temporary folder, offered at the next start if the program did not close properly.
    After recovering it the copy is kept until the work is saved or discarded; the original
    deleted it at once.
  - *Checks before the PDF* run on the settings as the PDF will use them, after the paper has
    been turned (`PdfGenerator.ForOutput`). The original checked the paper as stored and turned it
    afterwards, so a layout could pass the check and still not fit the sheet written.
  - *Cancelling* stops after the sheet in hand (`PdfGenerator.GenerateCancellable`), as the
    original's flag did.
  - *After the PDF* it is opened with the system's viewer, or its folder is shown when several
    files were written.
  - *A file that is not a project* gives a message instead of ending the program.
  - *Not ported here:* the turn of 180 degrees the original gave the shared back after every
    gutterfold PDF, which added up from one PDF to the next; tabletop, "seven pixels" dice and
    paper-miniature output (task 28); packing a project (task 39).
  - *Moved to another task:* the window to choose fronts and backs separately when adding images
    to cards with two faces, adding from folders and archives, the warnings about copies and
    colour space when adding, and the stacked layouts when making the deck.

### Layout window (task 23)

- `WindowSetupImpresion` and its controls (`Control_TamanosPapel`, `Control_DobleCara`,
  `Control_TamanoCarta`, `Control_SeparacionCarta`, `Control_Tabla`, `Control_FolioPreview`)
  are one window, `LayoutWindow`, over one view model, `LayoutViewModel`. The window gives
  back a `LayoutResult` that goes to `ProjectSettingsViewModel.ApplyLayout`.
- The arithmetic lives in Core so it is tested without a window: `LayoutCalculator`
  (`CalculaFilasColumnas`, `EstableceTamanoTotal`), `BackImageOption`
  (`CargarTiposImagenesTraseras`) and `DeckStatistics` (`DatosEstadisticos`). Quirks kept:
  dice fit by 3 × 5 cells but their total size counts 3 × 4; cards without size give
  1000 rows and columns; the median of an even count is the upper middle value.
- The rules and margins of the printing company's group are left out, as everywhere.
- The sketch of the sheet (`SheetPreview`) is drawn in code: a sheet with the proportion of
  an A4, upright or on its side, and the table of cards in green, or red when it does not
  fit. The original showed a bitmap of a sheet with a grid.
- Automatic rows and columns is a padlock button, as in the original; the original has no
  text for it in any language.
- A layout that does not fit asks whether to go on, with the original's message.

### Advanced templates editor (task 24)

- `Window_PlantillasAvanzadas` is `TemplateEditorWindow` over `TemplateEditorViewModel`; the
  paper part (`Control_TamanosPapel`) is `PaperChoiceViewModel`. The cards of the template
  are kept as objects; the original kept the text of the list and read the numbers back
  from it when saving.
- `WindowSetupPlantillasAvanzadas` is `TemplateSetupWindow`, over the `LayoutViewModel` of the
  layout window without gutterfold. Its result reaches the project through
  `ProjectSettingsViewModel.ApplySides`: sides and backs only, as in the original.
- The sketch (`TemplatePreview`) is to scale for any paper, marks the card chosen in the
  list and outlines in red a card that goes beyond the sheet. The original drew on a canvas
  of 210 by 297 whatever the paper and stretched the cards to it.
- Fixed from the original:
  - *Import* brings the file in as it is. The original saved it with the paper and bleed of
    the window and with the cards on show added to its own.
  - *Add* with a box left empty tells that data is missing and adds nothing; the original
    told it and then added a card of size 0.
  - *A card beyond the sheet* is measured against the sheet as printed; the original
    measured against the upright sheet even when it was on its side.
  - A file that is not a template gives a message instead of ending the program.
- Changed: *Export* asks where to save a copy of the template; the original opened the
  file with whatever program the system had for `.pnpa`. Typing a place beyond the sheet
  no longer opens a message at every keystroke: the message comes when the card is added
  or the template saved.
- After saving or deleting in the editor, the list of styles of the main window is listed
  again when it shows the advanced templates.
- Compared with Windows 20.11.1 (`Windows2011ComparisonTests`, cases 6 and 7), with the
  bundled template "Tarot 70x120 (6)" and twelve cards:
  - A project of the kind of a template (`TipoProyecto` `EstilosAvanzados`) gives the same
    PDF in both programs: same places and sizes, no pixel of difference.
  - *Windows writes blank sheets* when the template is chosen from the lists of the main
    window: it leaves the project as a "card" one (`TipoProyecto` `Carta`) with cards of
    size 0, so every image is drawn with no size. Opening that project here gives the same
    blank sheets, but choosing a template here keeps the kind of a template
    (`AdvancedTemplate.LoadInto`), so the cards are drawn in their places.

### Custom decks and sports decks (task 25)

- `WindowCrearMazoPersonalizado` is `CustomDeckWindow` over `CustomDeckViewModel`;
  `WindowCrearMazoPersonalizadoDeportes` is `SportsDeckWindow` over `SportsDeckViewModel`;
  `Window_EditarTextBox` is `TextInputWindow`; the bar of buttons
  `RibbonWindowMazoPersonalizado` is part of each window. The two models
  (`WindowCrearMazoPersonalizadoModelo`, `...DeportesModelo`) are `CustomDeckProjects` in
  `PnpTool.Imaging`, which makes the images and the project without any window.
- *Making the deck.* As in the original, a folder is asked for, the image of every card is
  written there, the PDF is made and the project is left in the folder as
  `MazoPersonalizado.pnp`. The window of the creator closes while this is done and comes
  back as it was, so that the questions and the progress window belong to the main window.
  `MainWindowViewModel.MakePdfAsync` now makes the PDF of any project, not only of the one
  in the panel.
- *Sports decks without the printing company's style.* The original laid them out with
  `soriano/SorianoA4.pnps`, which is not shipped. `CustomDeckProjects.SportsSettings` gives
  the same card: faces of 63 by 88 mm with the mirror effect and 1 mm of bleed on cells of
  65 by 90, 2 mm apart, with cut lines. The company's marks, page numbers, file identifier,
  CMYK output and split by size are left out.
- Fixed from the original:
  - *Every sports card carries the name of its own player.* The original drew the text box
    of the window on all the cards, so all of them got the name of the player on show.
  - *A hidden team name is not written.* The original hid the text box but still drew it.
  - *Saving a sports deck asks for the file once*; the original opened the dialog twice.
  - A file that is not a deck gives a message instead of ending the program.
- The texts of a sports card are placed as a share of the card (0 to 1), as 20.11 saves
  them; places saved by older versions in points of the screen are brought onto the card.
- "Nombre Equipo:" and the name of the saved project are fixed texts in the original too.
  The card names of the bundled decks follow the language: Spanish, or English otherwise.
- *Not ported:* `Window_VerMazo`, a developer's window (a tree of the deck, of the images
  and of the undo stack) that no button of the released program opens; the progress bar of
  the two windows, replaced by the one of the main window.
- *Still to do:* the fonts of `assets/fonts` are not offered by the font window, which
  lists the fonts of the system.

### Image tools, packing and folders (task 26)

- `Window_CortarImagenes` with `ControlCanvas` and `FormCortarImagenesModelo` are
  `CutImagesWindow`, `CutPreview`, `CutImagesViewModel` and `ImageCutter` (Imaging).
  `WindowEditarimagenes` with `FormEditarImagenesModelo` are `EditImagesWindow`,
  `EditImagesViewModel` and `ImageBatchEditor` (Imaging). `WindowComprimir` with
  `ModeloComprimir` are `PackProjectWindow`, `PackProjectViewModel` and `ProjectPacker`
  (Core). `Window_ProcesarCarpeta` with `ProcesarCarpetaModelo` are `ProcessFoldersWindow`,
  `ProcessFoldersViewModel` and `MainWindowViewModel.ProcessFoldersAsync`.
  `ComboboxItemEfectosImagenes` is `ImageEffectOption` (Core). `Window_moverA` and
  `Window_SeleccionarPanelesCaras`, also named in the task, came with task 19.
- *Cutting.* The arithmetic of the pieces is the original's (`ImageCutter.Pieces`). All
  measures are pixels: the original took the space between pieces from a box in
  millimetres or inches and used the number as pixels. The image is shown shrunk to fit
  the window, with every piece outlined; the original showed it at full size under a zoom
  slider and drew the lines between pieces. Pieces are written in the format of the
  extension chosen (jpg, png, bmp, tif, webp, avif).
- *Changing images.* The effects offered are the original's (all but the mirror and the
  outline). The project is read for the effects that need the size of a card and is not
  changed.
- *Packing.* The project of the panel is packed as it stands, saved or not. The folder
  made is shown when done.
- *Folders into PDFs.* The layout is confirmed first, as in the original. The images of the
  folders are read in the order of the list, a folder after another, and the PDF is made
  with `MakePdfAsync`, which now takes the file to write. The cards of the panel are left
  alone. With "1:1 PDF" the PDFs are not opened one by one: their folder is shown at the
  end. A book with pages much wider than the rest offers to cut them, opening the cut tool
  set to two columns. Tabletop projects are not processed yet (task 28).
- Fixed from the original:
  - *Packing: "zip" and "delete" were swapped.* The window passed them to the model in each
    other's place, so asking only for a zip deleted the copies and made no zip. Deleting
    now only happens together with a zip, and leaves the zip.
  - *Packing: images with the same name.* Two images called the same in different folders
    overwrote each other in the pack; the second copy now gets a number.
  - *Changing images: replacing never happened.* The extension was compared as ".Jpg"
    against ".jpg"; it now replaces when the format is the image's own.
  - *Changing images: the filter chosen was ignored*; the project's was used.
  - *Changing images: the project was changed.* A project without card size got the size
    in pixels of the image, as points.
  - *Cutting: the preview and the cut disagreed in inches*, since one read the space in
    inches and the other in millimetres.
- The step that checks every image of the folders before making the PDF
  (`Form_ValidarImagenes`) came with task 39.

### PDF tools and book cover (task 27)

- Windows over the engine of task 14: `WindowExtraer` is `ExtractPdfWindow` with
  `ExtractPdfViewModel`; `Window_Unir_PDF` is `MergePdfWindow` with `MergePdfViewModel`;
  `WindowSepararFrontalesTraseras` is `SplitFrontsBacksWindow` with
  `SplitFrontsBacksViewModel`; `Window_ModificarTamanoHojaPDF` and
  `Window_ModificarAlineacionPDF` are the two tabs of `ModifyPdfWindow` with
  `ModifyPdfViewModel` (the ribbon offered them as two entries of one button).
- *Book cover.* `WindowPortadaLibro` is `BookCoverWindow` with `BookCoverViewModel`;
  `FormPortadaLibroModelo` is the static `BookCover` beside `BookCoverSettings` (Core);
  `FicheroPdfMaquetadoLibro` is `BookCoverWriter` (Pdf); `EfectoEspejoImage_Libro`, drawn
  with GDI in the original, is `BookCoverMirror` (Imaging), with the same arithmetic;
  `ComboboxItemVolumenPapel` is `PaperStockOption`, with the kinds of paper named by what
  they are instead of after the printing company.
- *Extracting.* The images of every PDF processed are given back, found by comparing the
  folder before and after. Deleting small images is done here, after extracting. The box
  "show errors" of the original did nothing and is not shown.
- *Joining.* "Interleave" and "open when done" are check boxes with their text; the
  original used two picture buttons. The file moved up or down stays selected.
- Fixed from the original:
  - *The sheet for book covers stood upright.* The tool always marks the sheet as landscape
    and swaps width and height; the sheet for covers is stored as 500 by 330, already on
    its side, so it became 330 wide by 500 tall and covers that fit it were refused.
    `BookCover.OrientedPaper` puts the long side across.
  - *Only the images of the last PDF reached the panel* when several were extracted at once.
- *Kept, with a doubt:* the width of the spine is pages × thickness of a page + 0.015 and
  goes into the box as it is. The thicknesses of the list (0.0107 for 80 g paper) look like
  centimetres a sheet, while the box is read in the unit of the program, so the value
  offered may be too small; it can be typed over.
- *Not ported:* the check of the permissions of a PDF before aligning it
  (`CompruebaPermisosPDF`) and the "file in use, retry" question (`DialogoReintentar`); a
  file that cannot be read or written gives its error message.

### Card search in Scryfall (task 28)

- `Scryfall` is `ScryfallClient` in Core (`PnpTool.Core.Online`), over `HttpClient`, with the
  server replaceable for tests; `Window_MTG` is `CardSearchWindow` with
  `CardSearchViewModel`. `HttpClientDownloadWithProgress` of `LibreriaLogs` is not ported:
  answers are read in memory and images written straight to their files.
- The task was split: Tabletop (task 49) and "seven pixels" dice (task 50) are local
  generators with tasks of their own; `Window_SubirImagenes` is the window to choose fronts
  and backs when adding images (task 43); `Window_DescargaFichero` belonged to the updater.
- *What is sent.* Requests go to `api.scryfall.com` with the name of the program and its
  version as client name, as Scryfall asks. Nothing about the system is sent; the original
  added the version of Windows. Requests are 100 ms apart, as Scryfall asks.
- *Without network* the request fails and its message is shown; the original asked Windows
  whether there was a connection before starting.
- *Copies.* Cards brought to the panel get the copies their deck list asked for; the
  original added every card once and left the number only in the file name.
- Fixed from the original:
  - *A set was cut at 175 cards.* Only the first page of the answer was read; the
    following pages are now read too.
  - *The cards of a set never reached the panel*: the list of results held no file for
    them, so "import" did nothing after downloading a set.
  - *Cards printed on both sides* stopped the download of a deck, which looked for an
    image the card does not have; each face is now downloaded.
  - *A line that is not a card* ("Sideboard") became a search for the card "e" of the set
    "e"; it is now skipped.
  - *File names* of a deck are cleaned of the characters a file cannot have, as those of a
    set were: "Fire // Ice" could not be written.
- Checked against the real service on 2026-11-26 (three requests): the shape of the
  answers, the 404 for a card not found and the pages of 175 cards. The tests use a fake
  server and do not touch the network.

### Help, credits and terms (task 29)

- *Where it is.* The Windows program shows its credits and terms inside the program settings
  (`WindowSetupPnpTool`): a left bar (`Barra_Izquierda`) and a web browser control that loads
  the HTML pages of `config/webs`. Here they have a window of their own, `AboutWindow` with
  `AboutViewModel`, opened from a new button at the end of the Help tab. The button has no
  text among the translations, so it is labelled "PNP Tool". The Help tab also gains the
  official Telegram channel (`DonarModelo.AbrirTelegram`); the donations button
  (`Boton_Donar_Click`) is inside the window, as in the original bar.
- *Pages.* `equipo.html`, `Traductores.html`, `Mecenas.html` and `Terminos.html` became data
  in `PnpTool.Core.Credits` and are drawn with ordinary controls: there is no web browser
  control. Names are those of 20.11.1. The pages were in Spanish only; headings and terms are
  shown in Spanish when the program is in Spanish or Catalan and in English otherwise.
- *Terms.* The line saying that data is sent to a server at startup is replaced by one saying
  that this version sends nothing, since the telemetry was removed; a line names the licence
  (AGPL-3.0). The note on the Adobe ICC profiles stays, with its link.
- *Credit.* A first page says that PNP Tool is the work of Jupklass, that this is a port of
  20.11.1 to macOS and Linux, and that donations go to the original author, with a link
  to the source code of the port beside the GitHub mark (drawn as a shape, no image file).
- *Left out.* The news (`Window_News`, `URLNews`): the page is read from the web site of a
  printing company and was shown after an update. The changelog (`Web_menu.html`): it is the
  history of the Windows versions, not of this program. The printing-company thanks control.
- *Still to come.* The link to the terms beside the check box of the first-run window
  belongs to the program settings (task 48).

### Poster tool (task 30)

- *No code to port.* The Windows program starts `tools\\PosteRazor\\PosteRazor.exe`
  (`Control_HerramientasExternas.PosterazorToolStripMenuItem_Click`), a separate program that
  is not shipped here. The poster tool is new code that does the same job: `PosterLayout`
  (PnpTool.Core) for the arithmetic, `PosterWriter` (PnpTool.Pdf) for the PDF, and
  `PosterWindow` with `PosterViewModel` and `PosterPreview` (PnpTool.App).
- *How a poster is split.* Every sheet prints its whole printable area (the paper less the
  margin). A sheet starts one printable area less the overlap after the one before, so the
  strip of the overlap is printed on both sheets: one is trimmed at its margin and glued over
  the other. *n* sheets cover *n* × printable − (*n* − 1) × overlap. When that is more than
  the poster, the poster is aligned inside it (left, centre or right; top, centre or bottom).
- *Size.* As sheets across (need not be whole: 2.5 is two sheets and half of a third), as
  the width of the poster, or as a percentage of the size the image has at its own
  resolution. The height follows the proportions of the image.
- *PDF.* One page per sheet, row by row from the top. The picture is stored once and drawn
  on every page, moved and cut to the printable area. With trim marks, two short lines are
  drawn in the margin at each corner of the printable area.
- *Differences from PosteRazor.* One margin for the four sides instead of one each; one
  overlap for both directions, without the choice of the side it is on (both sheets print
  the strip); the size is given across, not also down; images only, as PosteRazor.
- *Button.* On the tools tab, after the card search, labelled "Poster": the texts of the
  original button name PosteRazor. Lengths use the unit of the program. The texts that the
  original has no translation for (overlap, alignment, size modes, the summary) are in
  Spanish when the program is in Spanish or Catalan and in English otherwise.
- *Limit.* More than 1000 sheets is refused as a mistake in the sizes.

### Screen capture and eyedropper (task 31)

- *Taking a picture of the screen.* `Graphics.CopyFromScreen` exists on Windows only.
  `IScreenCapture` (PnpTool.Core) is what the program asks for; `DesktopScreenCapture`
  (PnpTool.Imaging), registered at start-up, runs the capture program of the system and
  converts its picture to the format asked for:
  - *macOS:* `screencapture -x -t png -R x,y,w,h`. The area is given in pixels of the screen
    and divided by the scale of the screen, because `screencapture` measures in the units
    windows are placed in. macOS only lets a program see the windows of others once the user
    has allowed screen recording: `CGPreflightScreenCaptureAccess` is asked first and, when
    it says no, `CGRequestScreenCaptureAccess` makes the system offer the permission and the
    capture is refused with a message that says where to allow it.
  - *Linux, X11:* the first found of `import` (ImageMagick), `scrot`, `maim`,
    `gnome-screenshot`, `spectacle`.
  - *Linux, Wayland* (`XDG_SESSION_TYPE`): `grim`, `gnome-screenshot`, `spectacle`. A program
    cannot read the screen itself there; without one of them the message says which to
    install. The desktop portal is not used.
  - A program that only captures the whole desktop has the area cut out of its picture.
- *AutoCaptura.* The separate program `Autocaptura_WPF.exe`, started from the tools tab, is
  now a window of the program: `ScreenCaptureWindow` with `ScreenCaptureViewModel`, opened by
  the same button. Interval, screen, size and start of the area, folder, the two buttons, the
  light (green, red in the second half of the wait) and the preview are as in the original.
  Its texts, in Spanish only in the original, are in Spanish or English.
  - *Added:* a check box to add the pictures taken to the project when the window closes.
  - *Numbering:* `Capture_N.jpg` continues after a stop and skips numbers already in the
    folder, so nothing is overwritten.
  - Its defects are listed in the README.
- *Eyedropper.* `ColorPickerDialog` has a button with a dropper: it takes a picture of the
  screen the window is on and shows it over that whole screen (`EyeDropperWindow`), with a
  panel beside the pointer showing the points around it enlarged and the colour under it. A
  click picks the colour; Escape or another button cancels. The original
  (`Dsafa.WpfColorPicker.EyeDropperWindow`) retakes the picture when the pointer moves to
  another screen; here the colour is picked on the screen of the colour window.
- *Not checked on real systems.* The commands were tried by hand only on macOS
  (`screencapture`); the Linux ones are covered by tests with the programs replaced.

### External tools (task 32)

- *Calculator.* `EjecutaPrograma("calc.exe")` became `IPlatformServices.OpenCalculator`:
  `open -a Calculator` on macOS; on Linux the first found of `gnome-calculator`, `kcalc`,
  `mate-calc`, `galculator`, `qalculate-gtk`, `xcalc`; `calc.exe` on Windows. When none is
  found the status line says so. The button is the first of the last group of the tools
  tab, where the original has its external tools.
- *nanDECK.* Not offered: it is a Windows program and is not shipped. Its button
  (`ShellMenuBarItem_Herramientas_Nandeck`) is left out on every system.
- *PosteRazor and AutoCaptura* were replaced by tools of the program's own in tasks 30
  and 31, so no button starts a Windows executable.
- *Other calls to Windows.* `explorer.exe` remains only in the Windows branch of
  `DesktopPlatformServices.RevealFile`; macOS uses `open -R` and Linux opens the folder.
  Nothing in the port reads the registry or calls `rundll32`.

### macOS package (task 33)

Nothing of this is in the Windows program, which is a folder with `PNPTool.exe`, its `config`
folder beside it and an updater.

| Piece | Where | Notes |
|---|---|---|
| Bundle and disk image | `scripts/package-macos.sh` | Self-contained publish for `osx-arm64` and `osx-x64` into `dist/macos-<arch>/PNP Tool.app`, then `dist/PnpTool-<version>-macos-<arch>.dmg` with a shortcut to Applications. Two packages, not a universal one: it halves the download |
| Layout | | The publish output goes to `Contents/MacOS`; `assets/` to `Contents/Resources/assets`, the second place `AppPaths.FindAssetsFolder` looks in |
| `Info.plist` | `packaging/macos/Info.plist` | Template; the script fills in the version (from `Directory.Build.props`) and the oldest macOS, read from the published executable. Declares `.pnp`, `.pnps` and `.pnpa`, the three kinds `MainWindowViewModel.OpenAsync` takes |
| Icon | `scripts/make-icon.py`, `packaging/icon-1024.png`, `packaging/macos/PnpTool.icns` | The original only has a 32 pixel icon (`pnptool.ico`); the same ruler, knife and cut line are drawn again at full size. The window keeps the original picture |
| Entitlements | `packaging/macos/PnpTool.entitlements` | For the hardened runtime of a notarised build (task 34); the script does not use them |
| Signature | | Ad hoc only (`codesign --sign -`), which Apple Silicon needs to run anything. A downloaded copy is stopped by Gatekeeper until task 34 |
| Files opened from Finder | `App.OnFrameworkInitializationCompleted`, `MainWindow.OpenFromSystem` | macOS does not put a double-clicked file on the command line (`ConfiguracionPrograma.Argumentos` in the original): it sends it to the program, running or just started. Avalonia gives it as `IActivatableLifetime.Activated` with `FileActivatedEventArgs`. Before the window shows it becomes `StartFile`; later it goes to `OpenAsync`, which asks about unsaved changes as the Open button does |

### macOS signing and notarisation (task 34)

Nothing of the original. `scripts/sign-and-notarize.sh --identity … --profile … [arm64|x64|all]`
works on what `scripts/package-macos.sh` left in `dist/`: it signs every file of
`Contents/MacOS` and then the bundle with the hardened runtime, has Apple notarise the program
and the disk image, staples both and checks them. `--sign-only` stops before Apple. The guide
is `docs/macos-signing.md`. `scripts/macos-dmg.sh` makes the disk image for both scripts.

### Linux packages (task 35)

Nothing of the original. `scripts/package-linux.sh [x64|arm64|all]` runs on Linux and on macOS.

| Piece | Where | Notes |
|---|---|---|
| `.tar.gz` | `dist/PnpTool-<version>-linux-<arch>.tar.gz` | The self-contained publish with `assets/` beside the program, the first place `AppPaths.FindAssetsFolder` looks in. `install.sh` adds the menu entry, the icon and the file types for the user, with no administrator rights; `uninstall.sh` removes them |
| AppImage | `dist/PnpTool-<version>-<machine>.AppImage` | The same program under `usr/lib/pnptool` of an AppDir, packed with `mksquashfs` behind the runtime of the AppImage project (fetched once into `dist/cache`). Made by hand rather than with `appimagetool`, which only runs on Linux |
| Menu entry and file types | `packaging/linux/pnptool.desktop`, `pnptool.xml` | `.pnp`, `.pnps` and `.pnpa`, as on macOS. A file opened from the file manager comes on the command line (`%f`), which `App` already takes as `StartFile` |
| Left out of the package | `libcoreclrtraceptprovider.so` | Only for tracing .NET with LTTng; it asks for `liblttng-ust.so.0`, which few systems have |
| Skia for Linux | `Directory.Packages.props` | `SkiaSharp.NativeAssets.Linux` is named at the version of `SkiaSharp`. Avalonia brings an older one (3.119), and with it the program stopped at startup on Linux: "The version of the native libSkiaSharp library (119.0) is incompatible". macOS was not affected: `SkiaSharp` brings that library itself |

System libraries the program needs and does not carry: fontconfig, X11 (`libX11`, `libICE`,
`libSM`) and ICU. Every desktop install has them; a bare container does not.

### Update check (task 36)

| Original | Port | Notes |
|---|---|---|
| `FuncionesGenerales.Hay_Actualizacion_V2`, `DescargaFicheroVesionActualizacion` | `Core/Online/UpdateChecker` | The original reads a version file from the server of a printing company, and the same request reports the use of the program. Here one request goes to the public list of releases of this project on GitHub, with nothing about the user in it |
| `Hay_Actualizacion_Mayor`, `_Menor`, `_Tester` | `UpdateChecker.IsNewer` | Kept: "major" looks at the first number, "minor" at the first two, "test" at all three. Added: versions GitHub marks as pre-release only count with "test"; drafts, tags that are not version numbers and pages outside the project are ignored |
| `Actualizaciones.DescargaActualizacion`, `SePuedeDescargar` | `MainWindowViewModel.CheckForUpdatesAsync` | After startup, as the setting says. Asks with the original text `Actualizacion_Disponible` and the two versions; "yes" opens the page of the release in the browser |
| `Descarga`, `DescargaActualizacionConActualizador`, `Actualizador.exe`, `EstaNet10` | — | Not ported: nothing is downloaded, unpacked or installed, and the program carries its own .NET |
| `Control_Actualizaciones` (in the program settings) | `AboutViewModel`, `AboutWindow` | The list of settings and the search button, with the original texts. In the about window until the program settings window is ported (task 48). The search button looks whatever the setting and tells of any newer version (original: `forzar`, which also took test versions) |
| `ConfiguracionPrograma.AvisarActualizaciones` | `AppSettings.UpdateNotifications` | Already saved in the settings file under its original name; default "minor", as the original |

Only the real program looks at startup (`App` sets `MainWindow.AfterStart`); view models make
their `UpdateChecker` through `NewUpdateChecker`, which tests replace, so no test reaches the
network. A failed look is written to the log and otherwise silent.

### Smoke test pass (task 37)

`docs/smoke-test.md` has the checklist and the results of the first run. One change came out
of it: `App.CloseDialogOnKey`, a class handler that closes every window but the main one on
Escape (and Command+W on macOS). The original leaves that to each dialog; here no window had it.

### Custom decks, tickets and text on the sheet (task 38)

| Original | Port | Notes |
|---|---|---|
| `FicheroPdfMaquetadoBarajaPersonalizada` | `PdfLayoutWriter.CustomDeck.cs` (`DrawCustomDeck`) | Run from `FinishSingle` for the `BarajaEspanola` project type, as `TerminarCrearPDFSolo1` does: the written PDF is copied page by page and every front gets the suit symbol and the number in two corners, and Spanish cards their frame. Style 1 of the list is the Spanish deck (40 cards), any other the poker deck (52). Every second page is drawn on: these styles have one back for all |
| `BarajaPersonalizada.DevuelveRutaImagenPalo` | `CustomCardSet.SuitImagePaths` | Now gives the files as they are on disk (`assets/images/suits/oros.png`). The original asks for `Oros.png`, which Windows finds whatever the case and Linux does not; the port also looked in the wrong folder |
| `FicheroPdfMaquetadoLibro` | `BookCoverWriter` (task 27) | Already ported: it is the writer of book covers, not of booklets, which the main writer makes |
| `FicheroPdfMaquetado_Ticket`, `FormCrearTicketModelo` | — | Not ported. Nothing in the Windows program starts them: no button makes tickets, and a `.pnpt` file given to it is answered with "Funcionalidad Obsoleta" (`Window_Principal_v2.AbrirProyecto`). They also depend on the colour profile of the printing company and on a font of Windows (`ariali.ttf`). `MainWindowViewModel.OpenAsync` gives the same answer; `.pnpt` files still load and save through `ProjectFileStore` |
| `Paragraph.SetFixedPosition` + `Document.Add` (footers, file ID, deck numbers) | `PdfTextLine.Draw` | See below |

**Text is drawn without iText's layout.** Under .NET 11 on ARM64, `LineRenderer.Layout` of iText
throws a `NullReferenceException` on every call once the runtime has optimised it, which
happens after some thirty calls. A project with page numbers, a signature or the file ID
made its first sheets and then failed, and so did every PDF after it until the program was
restarted. With optimisation off (`DOTNET_JITMinOpts=1`) the same code works; with it on from
the start (`DOTNET_TieredCompilation=0`) it fails at once. The tests never saw it because they
write few paragraphs each. `PdfTextLine` writes the line with the text operators of the page
instead, where the layout would put it: the baseline is the descender plus half of what the
leading (1.35) adds to the height of the line, both from `TextRenderer.CalculateAscenderDescender`;
a paragraph turned half a turn turns about its left end, 4 points (its margin) below it.
`PdfTextLineTests` holds the places measured on iText's own layout, and the comparison with
the PDFs made on Windows covers the footers.

To catch this kind of fault, run the tests a second time with everything optimised:
`DOTNET_TieredCompilation=0 dotnet test`.

### PdfCartas import and the check of images (task 39)

| Original | Port | Notes |
|---|---|---|
| `FicheroPdfCartas` (`LeeFichero`) | `PdfCartasImporter.Import` (Core, `Files`) | Not a PDF tool: it turns a project of PdfCartas, another Windows program (a ".Bsk" XML file, version 4 only), into a `.pnp` beside it. It is in Core, not in `PnpTool.Pdf`, because it touches no PDF |
| `FicheroPdfCartas.Bsk`, `Imagen` | `PdfCartasImporter.Bsk`, `BskImage` | English names, with the element names of the file as attributes |
| `ShellMenuBarItem_Herramientas_ImportarPDFCartas_Click` | `MainWindowViewModel.ImportPdfCartasAsync` | Same steps: the warning, the file, the result, the offer to open the new project. The four texts are Spanish in every language, as in the original |
| `FormValidarImagenesModelo.Validar` | `ImageValidation.Usable` (Imaging) | Run by `ProcessFoldersAsync` under the progress window before the deck is made, as `ProcesarCarpeta_GeneraMazo` does |
| `WindowComprimir`, `ModeloComprimir` | `PackProjectWindow`, `ProjectPacker` (task 26) | Already ported |

- *Importing.* The new project starts from the style `Cartas_Standar.pnps`; layout, card size,
  spacing and the cut lines of the fronts are then replaced by what the file says, and image
  paths are kept absolute. An image whose path starts with "\" is beside the file; the
  backslashes of the others become the separator of the system, so a project made on Windows
  with paths like `C:\…` opens with its images missing. A card without image becomes a white
  card (the original gave it a blank face with no colour). Copies are the file's, and the
  count of the deck is made again from them. The picker offers `*.bsk` files and, unlike the
  original's, all files too.
- *Checking images.* An image that cannot be read is left out; one whose resolution is
  outside the limits of the settings (100 to 1000 dpi), or that states none, is replaced by
  a copy in the temporary folder that states 300 dpi. The copy is written again in the
  format of the image, so a JPEG is compressed a second time: that is what the original does
  (`CambiarDPI`), and it only happens here, not when images are added to the panel.
- Fixed from the original: rows, columns and orientation of a manual layout were only read
  for a custom paper; a file that is not XML, or an image without `Url`, threw instead of
  giving an error; Cancel did nothing during the check of the images.

### Files of versions 14 to 17 (task 40)

| Original | Port | Notes |
|---|---|---|
| `ConfiguracionProyectoV14` to `V17`, `ConfiguracionOtrosParametros`, `ConfiguracionOtrosParametrosV2` | `LegacyProjectSettings` (Core, `Projects`) | One class instead of six. The four versions differ in a handful of elements and the XML reader leaves alone what a file lacks, so a class with the elements of all four reads any of them; `Upgrade(version)` applies the steps that version still needs, the same ones as `CopiardeV14`, `CopiardeV15`, `CopiardeV16` and `ConvertirAV18` |
| `DisposicionImpresionV0`, `DisposicionImpresion.CopiarV0` | `LegacyPrintLayout.Upgrade` | Paper size, format and orientation move into `Papel` |
| `FicherosXml.AbrirXMLEstilo_Viejo` | `ProjectFileStore.LoadOldStyle` | A `.pnps` is saved again in version 18, the old one kept as `name_backup.pnps` |
| end of `FicherosXml.AbrirXML_V2` | `ProjectFileStore.SaveUpgradedProject` | The old project is kept as `name_back.pnp` |
| `FicherosXml.CompruebaCambioV20117` | `ProjectFileStore.WrittenBeforeGroupsChanged` | Reads the program version in the comment of the first line; before 20.3, or unreadable, the group becomes Custom and the subgroup 0 |

What each step does, as in the original:

- **14 → 15**: `MarcasPerforacion` and `ImagenEnLibro` move into the book settings.
- **15 → 16**: only those two are taken from the book settings and the spine is set on the
  left, whatever the file said; `CheckBox_Trasera_100` becomes the back image kind `SoloUna`.
- **16 → 17**: of the other parameters only the first six existed; compression percentage,
  colour space, colour profile, stacked layout and resize filter take their defaults, the back
  rotation is 0 and the card size is locked.
- **17 → 18**: the print layout gets its `Papel`.

Differences from the original:

- **The shared back is kept** (`ConvertirAV18` did not copy `CaraTraseraIgual`). See the README.
- **One backup per project**, `name_back.pnp`; the original also left `name.pnp_back`. See the README.
- **The backup of a style is named from its file name**, so `.xml` styles get `name_backup.xml`.
- **A folder that cannot be written** does not stop the file from opening: the upgrade is
  used in memory and the failure goes to the log. The original let the exception out.

Not checked against real files: none written by those old versions was available, so the
tests rewrite files of version 18 into the old shapes, following the classes above
(`OldFileVersionTests`).

### App shell leftovers: window place, macOS menu bar, splash (task 42)

| Original | Port | Notes |
|---|---|---|
| `Window_Principal_v2.EstableceTamanoVentana` | `MainWindow.RestorePlacement` / `SavePlacement`, `WindowPlacement` (App, `Services`) | The original remembers the size and whether the window was maximised, and lets Windows place it. Here the place is remembered too (`AppSettings.WindowPosition`, element `PosicionVentana` of the settings file, which the Windows program does not have and ignores) |
| — | `MenuBar.Build` (App, `Services`) | The menu bar of macOS: one menu for each tab of the command bar, its buttons as items, a line between groups; the group that makes the PDF, which is in three tabs, only in the first. Cmd+N, Cmd+O, Cmd+S and Shift+Cmd+S for new, open, save and save as. Set on the main window, and only on macOS |
| — | `App.Initialize`, `App.AboutMenuText`, `AboutWindow.ShowOnce` | The application menu: "About PNP Tool" ("Acerca de PNP Tool" in Spanish and Catalan, as the credits) opens the credits; Avalonia adds the items to hide and quit. Without it Avalonia shows its own "About Avalonia". The credits open once however they are asked for |
| `Window_SplashScreen` | — | Not ported. Behind its picture it loaded the settings, asked the server of the original for the list of blocked versions and for updates, sent the "program opened" statistic and showed the news: none of that exists here, and what is left of the start-up takes no noticeable time. Settings are read in `Startup.Initialize` and newer versions are looked for after the window shows (task 36) |
| `MonitorHelper` | — | Not ported: it only wrote the monitors and their resolutions to the log from the splash screen. Avalonia's `Screens` gives what the window placement needs |
| `ConfiguracionPrograma.EscalaPantalla`, `BaseFontSize`, `TamanoImagenPorResolucionPantalla`, `RibbonGroupHeight` | — | Not ported. The original enlarges letters, icons and the ribbon in proportion to the width of the screen over 1920, because WPF sizes are fixed. Here sizes are in device-independent units and the system's own scaling is used |
| `LoadingService` | `MainWindowViewModel.RunWithProgress`, `ProgressWindow` | Already ported with the tasks that needed it |

How a saved place is judged (`WindowPlacement.ScreenFor`): the strip at the top of the window,
30 units high, must be whole inside the working area of some screen, with at least 120 units
of its width in sight, so that the window can be taken hold of. If no screen shows that much
(the monitor is gone, the resolution changed), the window opens in the middle of the main
screen. A saved size is used only if it fits the screen the window opens on. A maximised
window saves the corner of its monitor, so it is maximised on the same monitor next time.

Sound and screens in `IPlatformServices`: nothing in the port needs them, so none were added.

Checked in the running program on macOS: the menus and their items, About from the
application menu (asked twice, one window), a saved place used, and a place on no screen
replaced by the middle of the screen.

### Adding images: fronts and backs, folders, compressed files, warnings; stacked decks (task 43)

| Original | Port | Notes |
|---|---|---|
| `Window_SubirImagenes` | `AddImagesWindow` (App) | Only the question: the four ways and "never ask again". The original window also opened the file dialogs from its accept button; here `MainWindowViewModel.AddImagesAsync` does, so it can be tested without windows |
| `FormSubirImagenesModelo` (`DevolverMultiSelect`, `GeneraRutasFinales`) | `FrontBackFiles.MultiSelect`, `Pair` (Core, `Cards`) | Pairs in one go (two by two); many fronts and one back, or the reverse (the one repeated); every front with every back. An odd file out in pairs is a card without back |
| `FuncionesGenerales.OrdenaAlfabeticamente` | `FrontBackFiles.SortByName` | |
| `FormPrincipalModeloLocal.SeleccionarImagenes`, first half of `Subir_imagenes_WPF` | `MainWindowViewModel.AddImagesAsync(position)`, `AddFilesAsync` | The menus of the cards go through the same method (`ImagePanelViewModel.RequestImages`), as `AnadirImagenNormal(Posicion)` did |
| second half of `Subir_imagenes_WPF` | `ImagePanelViewModel.AddPairsAsync` | Reads the images away from the user-interface thread and adds the cards as one step for undo |
| `CompruebaAlarmaImagenes` | in `MainWindowViewModel.AddFilesAsync` | Above `AppSettings.PerformanceWarningImageCount` images (300), with cards of one face and files chosen in a dialog or a folder, as in the original; not for dropped files. Yes opens the folders tool with the folder of the images, Cancel adds nothing |
| `FormPrincipalModelo.RevisaNumeroCopiasImagenesASubir` | `FrontBackFiles.WithTooManyCopies` | Shown through `ImagePanelViewModel.Notify` before the images are read |
| `FormPrincipalModeloLocal.IncongruenciaEspacioColorCartaProyecto` (+ `…CaraRGB`, `…CaraCMYK`, `…_MX`) | `Card.ColorSpaceWarning` (Core) | Every card added is looked at; faces that are not images, or could not be read, do not count. `Mazo.IncongruenciaEspacioColorCartaProyecto` (`Deck.CheckColorSpace`) is another check, used when the PDF is made |
| `PanelImagenes.GenerarMazoDesdePaneles(TiposProyectos, MaquetacionApilada, …)`, `_Apilar`, `_ApilarPorEje`, `_IniciarMazo`, `AvanzarPosicion…`, `RellenaMazoNulos` | `DeckStacking.Arrange`, `Place` (Core, `Cards`) | Used by `MainWindowViewModel.CreatePdfAsync` when the layout of the project is not the standard one. The paper miniature branch is not ported (task 41) |
| — | `ImageArchives` (Core, `Files`), `ImagePanelViewModel.Expand` | Compressed files: not in the original. SharpCompress, which the original only used in its updater |
| — | `MainWindowViewModel.AddFolderAsync`, button "Add images from folder" | The original took folders only when dropped on the cards (`ProcesaRutasDragDrop`, ported in task 19). The label is the one of the button of the cut-images tool, which every language has |

*Stacked order.* With `c` columns, `r` rows and blocks of `n` sheets, card number `i` (from 0,
every copy counted) goes to block `i / (n·c·r)`; with `k` the rest of that division:

- stacked: sheet `k mod n`, column `(k / n) mod c`, row `k / (n·c)`: one card on each sheet
  of the block at the same place, then the next place along the row;
- by rows: row `k mod r`, sheet `(k / r) mod n`, column `k / (r·n)`: down a column of the
  first sheet, down the same column of the next sheet, and so on, then the next column.

Its place in the deck is `block·n·c·r + sheet·c·r + row·c + column`. When the cards need
fewer than `n` sheets, `n` is that number of sheets, as in the original. A test compares
these places with the ones the loops of the Windows program give, for the decks where those
loops work (whole blocks for the stacked kind, one block for the other). Past that the
original fails or loses cards; see the README. The layout can only be chosen once the
other-parameters window is ported (task 46); until then it comes from projects and styles
saved on Windows.

*Compressed files.* The entries with the extension of an image are written under
`<folder of the file>/<name of the file>/`, keeping their folders; entries whose path would
leave that folder are skipped, and so are hidden files (the `._name` copies macOS adds). A
file already there is not written again. A temporary folder was not used because the cards
of a saved project point at the files. A compressed file that cannot be read (damaged, with
a password, on a disk that cannot be written) is reported and the rest is added. Tested with
zip files made by the tests; rar and 7z go through the same code of SharpCompress but no
file of those kinds is in the tests.

*"Never ask again"* sets `AppSettings.DefaultAddImagesChoice` (`OpcionDefectoAnadirImagenes`),
saved with the settings when the program closes. The list to change it back is in the
program-settings window (`ComboBox_SubirImagenes`), which is task 48; until then it can only
be changed in the settings file.

## Type map

Numbers in brackets are the line counts of the decompiled file. Partial classes
of the original (for example the `Window_Principal_v2_Eventos_*` files) are
merged into one file by the decompiler; they will be split again by concern
when ported.

### ClasesGlobalesPNPTool

| Original file | Original type | New name | Target | Windows-only API | Notes |
|---|---|---|---|---|---|
| `BloqueImagenes/PropiedadesRotacion.cs` (94) | `PropiedadesRotacion` | `RotationProperties` | PnpTool.Core |  |  |
| `BloqueImagenes/TextToBitmap.cs` (49) | `TextToBitmap` | `TextRenderer` | PnpTool.Imaging | WPF | WPF FormattedText/RenderTargetBitmap -> SkiaSharp |
| `CodigoMorse.cs` (103) | `CodigoMorse` | `MorseBeeper` | not ported | Sound | easter egg using Console.Beep/System.Media; low priority, port only if wanted |
| `ComboboxItemFicheroPdfSalida.cs` (44) | `ComboboxItemFicheroPdfSalida` | `PdfOutputFileOption` | PnpTool.Core |  | combo box entry: value + localised label |
| `ComboboxItemGrupoConfiguracion.cs` (47) | `ComboboxItemGrupoConfiguracion` | `SettingsGroupOption` | PnpTool.Core |  | combo box entry: value + localised label |
| `ComboboxItemImagenTrasera.cs` (113) | `ComboboxItemImagenTrasera` | `BackImageOption` | PnpTool.Core |  | combo box entry: value + localised label |
| `ComboboxItemMarcasPerforacion.cs` (43) | `ComboboxItemMarcasPerforacion` | `PerforationMarkOption` | PnpTool.Core |  | combo box entry: value + localised label |
| `ComboboxItemPosicionesImagenesLibro.cs` (38) | `ComboboxItemPosicionesImagenesLibro` | `BookImagePositionOption` | PnpTool.Core |  | combo box entry: value + localised label |
| `ComboboxItemPosicionNumeroHoja.cs` (39) | `ComboboxItemPosicionNumeroHoja` | `PageNumberPositionOption` | PnpTool.Core |  | combo box entry: value + localised label |
| `ComboboxItems/ComboboxItemActualizacion.cs` (44) | `ComboboxItemActualizacion` | `UpdateChannelOption` | PnpTool.Core |  | combo box entry: value + localised label |
| `ComboboxItems/ComboboxItemColorSpacePdf.cs` (42) | `ComboboxItemColorSpacePdf` | `PdfColorSpaceOption` | PnpTool.Core |  | combo box entry: value + localised label |
| `ComboboxItems/ComboboxItemComprimir.cs` (44) | `ComboboxItemComprimir` | `CompressionOption` | PnpTool.Core |  | combo box entry: value + localised label |
| `ComboboxItems/ComboboxItemEfectoMarcoExterior.cs` (44) | `ComboboxItemEfectoMarcoExterior` | `OuterFrameEffectOption` | PnpTool.Core |  | combo box entry: value + localised label |
| `ComboboxItems/ComboboxItemEfectosImagemagick.cs` (42) | `ComboboxItemEfectosImagemagick` | `MagickEffectOption` | PnpTool.Core |  | combo box entry: value + localised label |
| `ComboboxItems/ComboboxItemEfectosImagenes.cs` (61) | `ComboboxItemEfectosImagenes` | `ImageEffectOption` | PnpTool.Core |  | combo box entry: value + localised label |
| `ComboboxItems/ComboboxItemIdioma.cs` (49) | `ComboboxItemIdioma` | `LanguageOption` | PnpTool.Core | GDI | combo box entry: value + localised label |
| `ComboboxItems/ComboboxItemImagen.cs` (15) | `ComboboxItemImagen` | `ImageOption` | PnpTool.Core | WPF | combo box entry: value + localised label |
| `ComboboxItems/ComboboxItemRutaImagenes.cs` (38) | `ComboboxItemRutaImagenes` | `ImagePathOption` | PnpTool.Core |  | combo box entry: value + localised label |
| `ComboboxItems/ComboboxItemStringString.cs` (25) | `ComboboxItemStringString` | `StringPairOption` | PnpTool.Core |  | combo box entry: value + localised label |
| `ComboboxItems/ComboboxItemSubGrupoConfiguracion.cs` (226) | `ComboboxItemSubGrupoConfiguracion` | `SettingsSubgroupOption` | PnpTool.Core | WPF | combo box entry: value + localised label |
| `ComboboxItems/ComboboxItemTiposCaras.cs` (39) | `ComboboxItemTiposCaras` | `FaceTypeOption` | PnpTool.Core |  | combo box entry: value + localised label |
| `ComboboxItems/ComboboxItemTiposFondoCarta.cs` (44) | `ComboboxItemTiposFondoCarta` | `CardBackgroundOption` | PnpTool.Core |  | combo box entry: value + localised label |
| `ComboboxItems/ComboboxItemTiposImprimirPorBorde.cs` (38) | `ComboboxItemTiposImprimirPorBorde` | `PrintFlipEdgeOption` | PnpTool.Core |  | combo box entry: value + localised label |
| `ComboboxItems/ComboboxItemTiposLomoLibro.cs` (38) | `ComboboxItemTiposLomoLibro` | `BookSpineOption` | PnpTool.Core |  | combo box entry: value + localised label |
| `ComboboxItems/ComboboxItemTiposMaquetacionApilada.cs` (38) | `ComboboxItemTiposMaquetacionApilada` | `StackedLayoutOption` | PnpTool.Core |  | combo box entry: value + localised label |
| `ComboboxItems/ComboboxItemTiposMarcaAgua.cs` (39) | `ComboboxItemTiposMarcaAgua` | `WatermarkTypeOption` | PnpTool.Core |  | combo box entry: value + localised label |
| `ComboboxItems/ComboboxItemTiposResolucionImagen.cs` (41) | `ComboboxItemTiposResolucionImagen` | `ImageResolutionOption` | PnpTool.Core |  | combo box entry: value + localised label |
| `ComboboxItems/ComboboxItemVolumenPapel.cs` (54) | `ComboboxItemVolumenPapel` | `PaperWeightOption` | PnpTool.Core |  | combo box entry: value + localised label |
| `ComboboxItemSubirImagenes.cs` (41) | `ComboboxItemSubirImagenes` | `AddImagesOption` | PnpTool.Core |  | combo box entry: value + localised label |
| `ConfiguracionesActuales/ConfiguracionPortadaLibro.cs` (60) | `ConfiguracionPortadaLibro` | `BookCoverSettings` | PnpTool.Core |  |  |
|  | `TiposLomoLibro` *(enum)* | `BookSpineType` | PnpTool.Core |  |  |
| `ConfiguracionesActuales/ConfiguracionPrograma.cs` (301) | `ConfiguracionPrograma` | `AppSettings` | PnpTool.Core | Registry, Win32Dialog, Ribbon, WPF | static global state; Registry and Ribbon references move out; paths re-based on per-OS data folder |
| `ConfiguracionesActuales/ConfiguracionProgramaNoEstatica.cs` (164) | `ConfiguracionProgramaNoEstatica` | `AppSettingsData` | PnpTool.Core |  | serialised part of the app settings |
| `ConfiguracionesActuales/ConfiguracionProyectoTicket.cs` (108) | `ConfiguracionProyectoTicket` | `TicketProjectSettings` | PnpTool.Core | GDI | .pnpt |
|  | `Coordenadas` | `TicketCoordinates` | PnpTool.Core | GDI |  |
| `ConfiguracionesViejas/ConfiguracionOtrosParametros.cs` (30) | `ConfiguracionOtrosParametros` | `OtherSettings` (read by `LegacyProjectSettings`) | PnpTool.Core |  | task 40: no class of its own, its six values are a subset of today's |
| `ConfiguracionesViejas/ConfiguracionOtrosParametrosV2.cs` (51) | `ConfiguracionOtrosParametrosV2` | `LegacyProjectSettings.Upgrade` | PnpTool.Core |  | task 40: only a step between two copies |
| `ConfiguracionesViejas/ConfiguracionProyectoV14.cs` (117) | `ConfiguracionProyectoV14` | `LegacyProjectSettings` | PnpTool.Core |  | task 40: one class reads versions 14 to 17 |
|  | `OtrosParametros` | `OtherParametersV14` | PnpTool.Core |  | old file version, load-only |
| `ConfiguracionesViejas/ConfiguracionProyectoV15.cs` (125) | `ConfiguracionProyectoV15` | `LegacyProjectSettings` | PnpTool.Core |  | task 40 |
| `ConfiguracionesViejas/ConfiguracionProyectoV16.cs` (126) | `ConfiguracionProyectoV16` | `LegacyProjectSettings` | PnpTool.Core |  | task 40 |
| `ConfiguracionesViejas/ConfiguracionProyectoV17.cs` (171) | `ConfiguracionProyectoV17` | `LegacyProjectSettings` | PnpTool.Core |  | task 40 |
| `EspacioCarta/CaraDeCarta.cs` (143) | `CaraDeCarta` | `CardFace` | PnpTool.Core |  |  |
|  | `EnumTipoCara` *(enum)* | `FaceType` | PnpTool.Core |  |  |
| `EspacioCarta/Carta.cs` (74) | `Carta` | `Card` | PnpTool.Core |  |  |
| `EspacioPapel/ComboboxItemTiposPapel.cs` (62) | `ComboboxItemTiposPapel` | `PaperTypeOption` | PnpTool.Core |  | combo box entry: value + localised label |
| `EspacioPapel/Papel.cs` (200) | `Papel` | `Paper` | PnpTool.Core | GDI |  |
|  | `TiposPapel` *(enum)* | `PaperType` | PnpTool.Core | GDI |  |
|  | `TiposVolumenPapel` *(enum)* | `PaperWeightType` | PnpTool.Core | GDI |  |
| `EspacioProyecto/ConfiguracionCara.cs` (126) | `ConfiguracionCara` | `FaceSettings` | PnpTool.Core |  |  |
| `EspacioProyecto/ConfiguracionLibro.cs` (66) | `ConfiguracionLibro` | `BookSettings` | PnpTool.Core |  |  |
|  | `ImagenesEnLibro` *(enum)* | `BookImagePlacement` | PnpTool.Core |  |  |
|  | `ImprimirPorBorde` *(enum)* | `PrintFlipEdge` | PnpTool.Core |  |  |
| `EspacioProyecto/ConfiguracionOtrosParametrosVActual.cs` (169) | `ConfiguracionOtrosParametrosVActual` | `OtherSettings` | PnpTool.Core |  |  |
|  | `TiposRutasImagenes` *(enum)* | `ImagePathMode` | PnpTool.Core |  |  |
|  | `TipoSalidaPdf` *(enum)* | `PdfOutputMode` | PnpTool.Core |  |  |
|  | `TiposCompresion` *(enum)* | `CompressionType` | PnpTool.Core |  |  |
|  | `ColorspaceProyecto` *(enum)* | `ProjectColorSpace` | PnpTool.Core |  |  |
| `EspacioProyecto/ConfiguracionProyectoVActual.cs` (101) | `ConfiguracionProyectoVActual` | `ProjectSettings` | PnpTool.Core |  | current style/project configuration (.pnps) |
| `EspacioProyecto/Cuadricula.cs` (54) | `Cuadricula` | `Grid` | PnpTool.Core |  |  |
| `EspacioProyecto/DisposicionImpresion.cs` (105) | `DisposicionImpresion` | `PrintLayout` | PnpTool.Core |  |  |
| `EspacioProyecto/DisposicionImpresionV0.cs` (41) | `DisposicionImpresionV0` | `LegacyPrintLayout` | PnpTool.Core |  | task 40 |
| `EspacioProyecto/EncabezadoPie.cs` (106) | `EncabezadoPie` | `HeaderFooter` | PnpTool.Core |  |  |
|  | `PosicionNumeroPagina` *(enum)* | `PageNumberPosition` | PnpTool.Core |  |  |
| `EspacioProyecto/GrupoProyecto.cs` (34) | `GrupoProyecto` | `ProjectGroup` | PnpTool.Core |  |  |
| `EspacioProyecto/MarcaDeAgua.cs` (30) | `MarcaDeAgua` | `Watermark` | PnpTool.Core |  |  |
|  | `TiposMarcaAgua` *(enum)* | `WatermarkType` | PnpTool.Core |  |  |
| `EspacioProyecto/ParametrosDeEntrada.cs` (43) | `ParametrosDeEntrada` | `InputParameters` | PnpTool.Core |  |  |
| `EspacioProyecto/Proyecto.cs` (31) | `Proyecto` | `Project` | PnpTool.Core |  | root of a .pnp file |
| `FicherosXml.cs` (621) | `FicherosXml` | `ProjectFileStore` | PnpTool.Core |  | XML load/save for all .pnp* formats, incl. old-version migration |
| `FuncionesGenerales.cs` (1127) | `FuncionesGenerales` | `AppFunctions` | PnpTool.Core | Process, WinRT | split: file helpers stay in Core; Process/explorer/WinRT calls move behind IPlatformServices |
| `GenerarListadoImagenes/GenerarListadoImagenes.cs` (596) | `GenerarListadoImagenes` | `ImageListBuilder` | PnpTool.Core |  | expands deck + settings into the per-page image list |
| `GestionDelColor.cs` (136) | `GestionDelColor` | `ColorManagement` | PnpTool.Imaging | GDI, WPF | ICC profiles; System.Drawing.Color -> own colour struct |
| `Imagenes/ImagenesGeneral.cs` (2084) | `ImagenesGeneral` | `ImageOps` | PnpTool.Imaging | GDI, WPF | 2,000-line image pipeline; GDI paths rewritten on Magick.NET/SkiaSharp |
|  | `TiposEfectos` *(enum)* | `ImageEffectType` | PnpTool.Imaging | GDI, WPF |  |
|  | `TiposImagenes` *(enum)* | `ImageKind` | PnpTool.Imaging | GDI, WPF |  |
|  | `ImagenesEnResources` *(enum)* | `EmbeddedImage` | PnpTool.Imaging | GDI, WPF |  |
| `Imagenes/ImageRenderListener.cs` (85) | `ImageRenderListener` | `ImageRenderListener` | PnpTool.Pdf |  | iText extraction listener |
| `Kernel/Pdf/Canvas/Parser/Listener/IEventListener2.cs` (8) | `IEventListener2` *(interface)* | `IEventListener2` | PnpTool.Pdf |  |  |
| `LineasCorte/ComboboxItemTiposLineasCorte.cs` (49) | `ComboboxItemTiposLineasCorte` | `CutLineTypeOption` | PnpTool.Core |  | combo box entry: value + localised label |
| `LineasCorte/LineasCorteCruz.cs` (131) | `LineasCorteCruz` | `CrossCutLines` | PnpTool.Core |  |  |
| `LineasCorte/LineasCorteNormal.cs` (167) | `LineasCorteNormal` | `StandardCutLines` | PnpTool.Core |  |  |
| `LineasCorte/LineasDeCorte.cs` (82) | `LineasDeCorte` | `CutLines` | PnpTool.Core |  |  |
|  | `TiposLineaCorte` *(enum)* | `CutLineType` | PnpTool.Core |  |  |
|  | `ConfiguracionLineasDeCorte` | `CutLineSettings` | PnpTool.Core |  |  |
| `ListadoImagenes.cs` (92) | `ListadoImagenes` | `ImageListEntry` | PnpTool.Core |  | one placed image in the layout list |
| `ListaImagenesExtraccion.cs` (17) | `ListaImagenesExtraccion` | `ExtractedImageList` | PnpTool.Pdf |  |  |
| `Maquetacion/ExecutionHandler.cs` (3) | `ExecutionHandler` *(delegate)* | `ExecutionHandler` | PnpTool.Core |  |  |
| `Maquetacion/FicheroPdfMaquetado_Ticket.cs` (335) | `FicheroPdfMaquetado_Ticket` | — | not ported |  | obsolete in the original, see task 38 |
| `Maquetacion/FicheroPdfMaquetadoBarajaPersonalizada.cs` (287) | `FicheroPdfMaquetadoBarajaPersonalizada` | `CustomDeckPdfWriter` | PnpTool.Pdf |  |  |
| `Maquetacion/FicheroPdfMaquetadoItext.cs` (1708) | `FicheroPdfMaquetadoItext` | `PdfLayoutWriter` | PnpTool.Pdf |  | main PDF generator (1,700 lines, iText) |
| `Maquetacion/FicheroPdfMaquetadoLibro.cs` (177) | `FicheroPdfMaquetadoLibro` | `BookPdfWriter` | PnpTool.Pdf |  |  |
| `Maquetacion/FicheroPdfMaquetadoNoDependienteLibreria.cs` (832) | `FicheroPdfMaquetadoNoDependienteLibreria` | `PdfLayoutBase` | PnpTool.Pdf | WPF | library-independent position maths |
|  | `QueArrayPosiciones` *(enum)* | `PositionArrayKind` | PnpTool.Pdf | WPF |  |
| `MarcasDePerforacion.cs` (59) | `MarcasDePerforacion` | `PerforationMarks` | PnpTool.Core |  |  |
|  | `TiposMarcasperforacion` *(enum)* | `PerforationMarkType` | PnpTool.Core |  |  |
| `Mazo.cs` (660) | `Mazo` | `Deck` | PnpTool.Core | WPF | the project's cards; drop its WPF usings |
| `Models/DonarModelo.cs` (50) | `DonarModelo` | `AppLinks`, `AboutViewModel` | PnpTool.Core, PnpTool.App |  | links only |
| `Models/FormCortarImagenesModelo.cs` (140) | `FormCortarImagenesModelo` | `CropImagesModel` | PnpTool.Imaging | GDI, WPF |  |
| `Models/FormCrearTicketModelo.cs` (141) | `FormCrearTicketModelo` | — | not ported | GDI | obsolete in the original, see task 38 |
| `Models/FormDice7PixelsModelo.cs` (509) | `FormDice7PixelsModelo` | `DiceModel` | PnpTool.Imaging | GDI | dice net generator |
|  | `TiposDadosD7Pixels` *(enum)* | `DiceType` | PnpTool.Imaging | GDI |  |
|  | `CaracteristicasDado` | `DiceSpec` | PnpTool.Imaging | GDI |  |
| `Models/FormEditarImagenesModelo.cs` (139) | `FormEditarImagenesModelo` | `EditImagesModel` | PnpTool.Imaging | GDI, WPF |  |
| `Models/FormExtraerModelo.cs` (233) | `FormExtraerModelo` | `ExtractModel` | PnpTool.Pdf |  |  |
| `Models/FormPortadaLibroModelo.cs` (121) | `FormPortadaLibroModelo` | `BookCoverModel` | PnpTool.Pdf |  |  |
|  | `RutasImagenes` *(enum)* | `BookCoverImageSlot` | PnpTool.Pdf |  |  |
| `Models/FormPrincipalModelo.cs` (122) | `FormPrincipalModelo` | `MainModel` | PnpTool.Core | WPF, DragDrop |  |
|  | `OpcionesMenuContextual` *(enum)* | `ContextMenuOption` | PnpTool.Core | WPF, DragDrop |  |
|  | `TiposRotacionMenuContextual` *(enum)* | `ContextMenuRotation` | PnpTool.Core | WPF, DragDrop |  |
| `Models/FormSepararFrontalesTraserasModelo.cs` (43) | `FormSepararFrontalesTraserasModelo` | `SplitFrontsBacksModel` | PnpTool.Pdf |  |  |
| `Models/FormSetupModelo.cs` (348) | `FormSetupModelo` | `SetupModel` | PnpTool.Core |  |  |
| `Models/FormSubirImagenesModelo.cs` (131) | `FormSubirImagenesModelo` | `FrontBackFiles` | PnpTool.Core |  | task 43 |
| `Models/FormTabletopModelo.cs` (638) | `FormTabletopModelo` | `TabletopModel` | PnpTool.Imaging | GDI | builds Tabletop Simulator sheet images |
| `Models/FormUnirPdfModeloIText.cs` (238) | `FormUnirPdfModeloIText` | `MergePdfModel` | PnpTool.Pdf |  |  |
| `Models/FormValidarImagenesModelo.cs` (73) | `FormValidarImagenesModelo` | `ImageValidation` | PnpTool.Imaging |  | task 39 |
| `Models/ModeloComprimir.cs` (116) | `ModeloComprimir` | `CompressModel` | PnpTool.Imaging | WPF |  |
| `Models/WindowCrearMazoPersonalizadoDeportesModelo.cs` (72) | `WindowCrearMazoPersonalizadoDeportesModelo` | `SportsDeckCreatorModel` | PnpTool.Core | WPF |  |
| `Models/WindowCrearMazoPersonalizadoModelo.cs` (67) | `WindowCrearMazoPersonalizadoModelo` | `CustomDeckCreatorModel` | PnpTool.Core |  |  |
| `Models/WindowModificarAlineacionPdfModelo.cs` (138) | `WindowModificarAlineacionPdfModelo` | `PdfAlignmentModel` | PnpTool.Pdf |  |  |
| `Models/WindowModificarTamanoHojaPdfModelo.cs` (36) | `WindowModificarTamanoHojaPdfModelo` | `PdfPageSizeModel` | PnpTool.Pdf |  |  |
| `Models/WindowSetupPnpToolModelo.cs` (160) | `WindowSetupPnpToolModelo` | `AppSetupModel` | PnpTool.Core | WPF |  |
| `NumericComparer.cs` (15) | `NumericComparer` | `NumericComparer` | PnpTool.Core |  |  |
| `Papermini.cs` (68) | `Papermini` | - | not ported |  | PaperMini export (encrypted package for the Soriano print service); left out, see task 41 |
| `PilaOperaciones.cs` (348) | `PilaOperaciones` | `OperationStack` | PnpTool.Core | WPF | undo/redo stack; the command arguments are replaced by snapshots |
|  | `ArgumentosCaraCarta` | `CardFaceArgs` | PnpTool.Core | WPF |  |
|  | `ArgumentosPanelCarta` | `CardPanelArgs` | PnpTool.Core | WPF |  |
|  | `ArgumentosRotacionPanelCarta` | `CardPanelRotationArgs` | PnpTool.Core | WPF |  |
|  | `ArgumentosCambiarNumeroCopias` | `ChangeCopiesArgs` | PnpTool.Core | WPF |  |
|  | `ArgumentosCambioPanel` | `PanelChangeArgs` | PnpTool.Core | WPF |  |
|  | `Comandos` | `UndoCommands` | PnpTool.Core | WPF |  |
| `Resources/Resources.cs` (42) | `Resources` | `AppResources` | PnpTool.App |  | 82 embedded images/strings |
| `Scryfall/Scryfall.cs` (604) | `Scryfall` | `ScryfallClient` | PnpTool.Core |  | MTG card lookup over HTTPS |
|  | `ScryfallDeckJson` | `ScryfallDeckJson` | PnpTool.Core |  |  |
|  | `JsonDeckSections` | `JsonDeckSections` | PnpTool.Core |  |  |
|  | `JsonDeckEntry` | `JsonDeckEntry` | PnpTool.Core |  |  |
|  | `CardDigest` | `CardDigest` | PnpTool.Core |  |  |
|  | `JsonImageUris` | `JsonImageUris` | PnpTool.Core |  |  |
|  | `ScryfallList` | `ScryfallList` | PnpTool.Core |  |  |
|  | `ScryfallCard` | `ScryfallCard` | PnpTool.Core |  |  |
|  | `CardFace` | `ScryfallCardFace` | PnpTool.Core |  | renamed to avoid clash with CaraDeCarta -> CardFace |
|  | `ImageUris` | `ImageUris` | PnpTool.Core |  |  |
| `StringLogicalComparer.cs` (233) | `StringLogicalComparer` | `StringLogicalComparer` | PnpTool.Core |  |  |
| `Tipos/AnaliticaSoriano.cs` (56) | `AnaliticaSoriano` | - | not ported |  | telemetry to the Soriano server |
|  | `TiposAcciones` *(enum)* | - | not ported |  | telemetry action ids |
| `Tipos/Config.cs` (19) | `Config` | `AppConfigFile` | PnpTool.Core |  |  |
| `Tipos/CoordenadasRaton.cs` (16) | `CoordenadasRaton` | `MouseCoordinates` | PnpTool.Core |  |  |
| `Tipos/DatosEstadisticos.cs` (141) | `DatosEstadisticos` | - | not ported |  | telemetry payload |
| `Tipos/DatosTexto.cs` (40) | `DatosTexto` | `TextData` | PnpTool.Core |  |  |
| `Tipos/Error.cs` (95) | `Error` | `AppError` | PnpTool.Core |  |  |
|  | `TiposError` *(enum)* | `ErrorKind` | PnpTool.Core |  |  |
| `Tipos/FicheroPdfCartas.cs` (244) | `FicheroPdfCartas` | `PdfCartasImporter` | PnpTool.Core |  | imports a project of PdfCartas (not a PDF), task 39 |
|  | `Imagen` | `ImportedImage` | PnpTool.Pdf |  |  |
|  | `Bsk` | `Bsk` | PnpTool.Pdf |  | internal record of the card PDF importer; name kept until its role is clear |
| `Tipos/Fuente/ColorFormat.cs` (9) | `ColorFormat` *(enum)* | `ColorFormat` | PnpTool.Core |  |  |
| `Tipos/Fuente/ColorTextFormat.cs` (9) | `ColorTextFormat` *(enum)* | `ColorTextFormat` | PnpTool.Core |  |  |
| `Tipos/Fuente/Font.cs` (129) | `Font` | `FontSpec` | PnpTool.Core | WPF | WPF FontFamily/Typeface inside; replace with plain family/style/weight fields |
| `Tipos/Fuente/FontExtension.cs` (34) | `FontExtension` | `FontSpecExtensions` | PnpTool.Core | WPF |  |
| `Tipos/Fuente/FontHelper.cs` (186) | `FontHelper` | `FontHelper` | PnpTool.Core | WPF | WPF Fonts.SystemFontFamilies -> SkiaSharp SKFontManager |
| `Tipos/Fuente/FontUpdateMananger.cs` (36) | `FontUpdateMananger` | `FontUpdateManager` | PnpTool.Core | WPF |  |
| `Tipos/Fuente/TextDecorationType.cs` (10) | `TextDecorationType` *(enum)* | `TextDecorationType` | PnpTool.Core |  |  |
| `Tipos/FuentePersonalizada.cs` (57) | `FuentePersonalizada` | `CustomFont` | PnpTool.Core | GDI | uses System.Drawing font types; re-base on Core font model |
| `Tipos/Idiomas/IdiomaSoportado.cs` (192) | `IdiomaSoportado` | `SupportedLanguage` | PnpTool.Core | GDI, WPF | holds a System.Drawing flag image; replace with asset path |
|  | `IdiomasSoportadosEnum` *(enum)* | `LanguageId` | PnpTool.Core | GDI, WPF |  |
| `Tipos/Idiomas/IdiomasSoportados.cs` (118) | `ListadosIdiomas` *(enum)* | `LanguageList` | PnpTool.Core |  |  |
| `Tipos/ImagenesSoportadas.cs` (63) | `ImagenesSoportadas` | `SupportedImages` | PnpTool.Core | GDI |  |
|  | `Extension` | `ImageExtension` | PnpTool.Core | GDI |  |
| `Tipos/MaquetacionApilada.cs` (39) | `MaquetacionApilada` | `StackedLayout` | PnpTool.Core |  |  |
| `Tipos/Personalizados/BarajaPersonalizada.cs` (99) | `BarajaPersonalizada` | `CustomCardSet` | PnpTool.Core |  |  |
|  | `Palo` *(enum)* | `Suit` | PnpTool.Core |  |  |
|  | `TipoBaraja` *(enum)* | `CardSetType` | PnpTool.Core |  |  |
| `Tipos/Personalizados/CartaPersonalizada.cs` (111) | `CartaPersonalizada` | `CustomCard` | PnpTool.Core | WPF |  |
| `Tipos/Personalizados/MazoDeportivo.cs` (20) | `MazoDeportivo` | `SportsDeck` | PnpTool.Core |  |  |
| `Tipos/Personalizados/MazoPersonalizado.cs` (32) | `MazoPersonalizado` | `CustomDeck` | PnpTool.Core |  | .pnpm |
| `Tipos/PlantillaAvanzada.cs` (80) | `PlantillaAvanzada` | `AdvancedTemplate` | PnpTool.Core |  | .pnpa |
| `Tipos/PlantillaAvanzadaCarta.cs` (32) | `PlantillaAvanzadaCarta` | `AdvancedTemplateCard` | PnpTool.Core |  |  |
| `Tipos/PlantillaAvanzadaV1.cs` (55) | `PlantillaAvanzadaV1` | `AdvancedTemplateV1` | PnpTool.Core |  | old file version, load-only |
| `Tipos/PuntosXY/LineaPuntoXYDoble.cs` (48) | `LineaPuntoXYDoble` | `LineD` | PnpTool.Core |  |  |
| `Tipos/PuntosXY/PuntoXUnit.cs` (41) | `PuntoXUnit` | `UnitPoint` | PnpTool.Core |  |  |
| `Tipos/PuntosXY/PuntoXY.cs` (39) | `PuntoXY` | `PointI` | PnpTool.Core |  |  |
| `Tipos/PuntosXY/PuntoXYDoble.cs` (43) | `PuntoXYDoble` | `PointD` | PnpTool.Core |  |  |
| `Tipos/PuntosXY/PuntoXYFloat.cs` (31) | `PuntoXYFloat` | `PointF` | PnpTool.Core |  |  |
| `Tipos/PuntosXY/PuntoXYUint.cs` (26) | `PuntoXYUint` | `PointU` | PnpTool.Core |  |  |
| `Tipos/TiposGenerales.cs` (118) | `TiposGenerales` | (container removed) | PnpTool.Core |  | nested enums become top-level |
|  | `TiposProyectos` *(enum)* | `ProjectType` | PnpTool.Core |  |  |
|  | `GruposConfiguracion` *(enum)* | `SettingsGroup` | PnpTool.Core |  |  |
|  | `ImagenesTraseras` *(enum)* | `BackImageMode` | PnpTool.Core |  |  |
|  | `TiposUnidades` *(enum)* | `UnitType` | PnpTool.Core |  |  |
|  | `TiposSalidasAnadirImagenes` *(enum)* | `AddImagesResult` | PnpTool.Core |  |  |
|  | `TiposResolucionImagen` *(enum)* | `ImageResolutionType` | PnpTool.Core |  |  |
|  | `TiposActualizaciones` *(enum)* | `UpdateChannel` | PnpTool.Core |  |  |
|  | `OpcionesSalidaCambioCara` *(enum)* | `FaceChangeChoice` | PnpTool.Core |  |  |
|  | `SalidasMenuArchivo` *(enum)* | `FileMenuResult` | PnpTool.Core |  |  |
|  | `TiposFondoCarta` *(enum)* | `CardBackgroundType` | PnpTool.Core |  |  |
|  | `EfectosMarcoExterior` *(enum)* | `OuterFrameEffect` | PnpTool.Core |  |  |
| `Tipos/TiposMaquetacion.cs` (8) | `TiposMaquetacion` *(enum)* | `LayoutType` | PnpTool.Core |  |  |
| `Tipos/UltimosFicherosClase.cs` (196) | `UltimosFicherosClase` | `RecentFiles` | PnpTool.Core | Ribbon, WPF | currently builds Ribbon gallery items; split data (Core) from UI (App) |
|  | `UltimoElemento` | `RecentFileEntry` | PnpTool.Core | Ribbon, WPF |  |
| `Tipos/XGraphicsUnit.cs` (10) | `XGraphicsUnit` *(enum)* | `GraphicsUnit` | PnpTool.Core |  |  |
| `Tipos/XUnit.cs` (501) | `XUnit` *(struct)* | `Unit` | PnpTool.Core |  | measurement value type (pt/mm/cm/inch); keep arithmetic identical |

### LibreriaLogs

| Original file | Original type | New name | Target | Windows-only API | Notes |
|---|---|---|---|---|---|
| `Descargar/ConfiguracionGlobalDescargas.cs` (14) | `ConfiguracionGlobalDescargas` | - | not ported |  | URLs of the original update server |
| `Extraccion.cs` (77) | `Extraccion` | `ArchiveExtractor` | PnpTool.Core |  | SharpCompress zip/rar extraction |
| `FuncionesGenerales.cs` (131) | `FuncionesGenerales` | `Log` | PnpTool.Core | Process | LogEnter/LogExit/LogWrite file logger |
| `HttpClientDownloadWithProgress.cs` (110) | `HttpClientDownloadWithProgress` | `HttpDownloader` | PnpTool.Core |  |  |
|  | `ProgressChangedHandler` *(delegate)* | `ProgressChangedHandler` | PnpTool.Core |  |  |

### PNPTool

| Original file | Original type | New name | Target | Windows-only API | Notes |
|---|---|---|---|---|---|
| `App.xaml.cs` (40) | `App` | `App` | PnpTool.App |  |  |
| `Controls/BotonSelectorColor.cs` (60) | `BotonSelectorColor` | `ColorButton` | PnpTool.Controls |  |  |
| `Controls/BuscarCarpeta.xaml.cs` (162) | `BuscarCarpeta` | `PathPicker` | PnpTool.Controls |  | folder/file chooser row; WinForms/APICodePack dialogs -> StorageProvider |
|  | `TiposdeBuscar` *(enum)* | `PathPickerTarget` | PnpTool.Controls |  |  |
|  | `TiposdeDialogo` *(enum)* | `PathPickerDialogKind` | PnpTool.Controls |  |  |
| `Controls/ComboBoxes/ComboBox_Filtro.xaml.cs` (64) | `ComboBox_Filtro` | `FilterComboBox` | PnpTool.Controls |  |  |
| `Controls/ComboBoxes/ComboBox_Idiomas.xaml.cs` (64) | `ComboBox_Idiomas` | `LanguageComboBox` | PnpTool.Controls | GDI |  |
| `Controls/ComboBoxes/ComboBox_ResolucionImagenes.xaml.cs` (76) | `ComboBox_ResolucionImagenes` | `ImageResolutionComboBox` | PnpTool.Controls |  |  |
| `Controls/ComboBoxes/ComboBox_SubirImagenes.xaml.cs` (70) | `ComboBox_SubirImagenes` | `AddImagesComboBox` | PnpTool.Controls |  |  |
| `Controls/ContextMenus/Control_MenuContextual_PanelCarta.xaml.cs` (25) | `Control_MenuContextual_PanelCarta` | `CardPanelContextMenu` | PnpTool.App |  |  |
| `Controls/ContextMenus/Control_MenuContextual_PanelImagen.xaml.cs` (35) | `Control_MenuContextual_PanelImagen` | `ImagePanelContextMenu` | PnpTool.App |  |  |
| `Controls/ContextMenus/Control_MenuContextual_PanelPrincipal.xaml.cs` (21) | `Control_MenuContextual_PanelPrincipal` | `MainPanelContextMenu` | PnpTool.App |  |  |
| `Controls/Control_Actualizaciones.xaml.cs` (93) | `Control_Actualizaciones` | `UpdatesView` | PnpTool.App |  |  |
| `Controls/Control_AgradecimientoSoriano.xaml.cs` (12) | `Control_AgradecimientoSoriano` | - | PnpTool.App |  | Soriano thanks banner: not ported |
| `Controls/Control_Botones_OkCancelar.xaml.cs` (20) | `Control_Botones_OkCancelar` | `OkCancelButtons` | PnpTool.Controls |  |  |
| `Controls/Control_CaracteristicasFicheroPDFSalida.xaml.cs` (89) | `Control_CaracteristicasFicheroPDFSalida` | `PdfOutputOptionsView` | PnpTool.App |  |  |
| `Controls/Control_ColorFondo.xaml.cs` (97) | `Control_ColorFondo` | `BackgroundColorView` | PnpTool.App |  |  |
| `Controls/Control_ConfiguracionCaraTrasera.xaml.cs` (74) | `Control_ConfiguracionCaraTrasera` | `BackFaceSettingsView` | PnpTool.App |  |  |
| `Controls/Control_DobleCara.xaml.cs` (227) | `Control_DobleCara` | `DoubleSidedView` | PnpTool.App |  |  |
| `Controls/Control_Efecto.xaml.cs` (122) | `Control_Efecto` | `EffectView` | PnpTool.App |  |  |
| `Controls/Control_FolioPreview.xaml.cs` (139) | `Control_FolioPreview` | `SheetPreview` | PnpTool.App |  |  |
| `Controls/Control_IDFichero.xaml.cs` (47) | `Control_IDFichero` | `FileIdView` | PnpTool.App |  |  |
| `Controls/Control_ImagenCalibracion.xaml.cs` (70) | `Control_ImagenCalibracion` | `CalibrationImageView` | PnpTool.App |  |  |
| `Controls/Control_ImagenTrasera.xaml.cs` (296) | `Control_ImagenTrasera` | `BackImageView` | PnpTool.App |  |  |
| `Controls/Control_LineasDeCorte.xaml.cs` (182) | `Control_LineasDeCorte` | `CutLinesView` | PnpTool.App |  |  |
| `Controls/Control_MarcaCalibracion.xaml.cs` (79) | `Control_MarcaCalibracion` | `CalibrationMarkView` | PnpTool.App |  |  |
| `Controls/Control_MarcaDeAgua.xaml.cs` (72) | `Control_MarcaDeAgua` | `WatermarkView` | PnpTool.App |  |  |
| `Controls/Control_MarcasPerforacion.xaml.cs` (87) | `Control_MarcasPerforacion` | `PerforationMarksView` | PnpTool.App |  |  |
| `Controls/Control_Numerico_v2.xaml.cs` (128) | `Control_Numerico_v2` | `NumericBox` | PnpTool.Controls |  |  |
| `Controls/Control_NumeroDeHoja.xaml.cs` (82) | `Control_NumeroDeHoja` | `PageNumberView` | PnpTool.App |  |  |
| `Controls/Control_PerfilesICCDefecto.xaml.cs` (27) | `Control_PerfilesICCDefecto` | `DefaultIccProfilesView` | PnpTool.App |  |  |
| `Controls/Control_Selector_UnidadesMedida.xaml.cs` (46) | `Control_Selector_UnidadesMedida` | `UnitSelector` | PnpTool.Controls |  |  |
| `Controls/Control_SelectorEspacioColor.xaml.cs` (125) | `Control_SelectorEspacioColor` | `ColorSpaceSelector` | PnpTool.App |  |  |
| `Controls/Control_SelectorProyecto.xaml.cs` (175) | `Control_SelectorProyecto` | `ProjectStyleSelector` | PnpTool.App |  |  |
| `Controls/Control_SeparacionCarta.xaml.cs` (63) | `Control_SeparacionCarta` | `CardSpacingView` | PnpTool.App |  |  |
| `Controls/Control_Slider.xaml.cs` (53) | `Control_Slider` | `ZoomSlider` | PnpTool.Controls |  |  |
| `Controls/Control_Tabla.xaml.cs` (225) | `Control_Tabla` | `LayoutTableView` | PnpTool.App |  |  |
| `Controls/Control_TamanoCarta.xaml.cs` (147) | `Control_TamanoCarta` | `CardSizeView` | PnpTool.App |  |  |
| `Controls/Control_TamanoPorcentajes.xaml.cs` (116) | `Control_TamanoPorcentajes` | `SizePercentView` | PnpTool.App |  |  |
| `Controls/Control_TamanosPapel.xaml.cs` (178) | `Control_TamanosPapel` | `PaperSizeView` | PnpTool.App | GDI |  |
| `Controls/ControlCanvas.cs` (255) | `ControlCanvas` | `SheetCanvas` | PnpTool.App | GDI |  |
| `Controls/ControlCanvasPlantillaAvanzada.cs` (86) | `ControlCanvasPlantillaAvanzada` | `AdvancedTemplateCanvas` | PnpTool.App |  |  |
| `Controls/ControlFirma.xaml.cs` (31) | `ControlFirma` | `SignatureView` | PnpTool.App |  |  |
| `Controls/ControlMarcoExterior.xaml.cs` (149) | `ControlMarcoExterior` | `OuterFrameView` | PnpTool.App |  |  |
| `Controls/DragAdorner.cs` (42) | `DragAdorner` | `DragPreview` | PnpTool.Controls | Adorner | WPF Adorner -> Avalonia drag visual |
| `Controls/ImagenBlanca.xaml.cs` (48) | `ImagenBlanca` | `BlankImageView` | PnpTool.App |  |  |
| `Controls/PanelCarta_WPF.xaml.cs` (705) | `PanelCarta_WPF` | `CardPanel` | PnpTool.App |  |  |
|  | `TiposAccionesPanelCara` *(enum)* | `CardPanelAction` | PnpTool.App |  |  |
| `Controls/PanelCarta_WPF_Arrastrar.xaml.cs` (40) | `PanelCarta_WPF_Arrastrar` | `CardDragPanel` | PnpTool.App |  |  |
| `Controls/PanelCartaZoom_WPF.xaml.cs` (113) | `PanelCartaZoom_WPF` | `CardZoomPanel` | PnpTool.App |  |  |
| `Controls/PanelImagenes.xaml.cs` (1588) | `PanelImagenes` | `ImagePanel` | PnpTool.App | Adorner, DragDrop | 1,600 lines: grid, selection, drag/drop, undo, context actions |
| `Controls/Ribbon/Control_AccionesEliminar.xaml.cs` (20) | `Control_AccionesEliminar` | `DeleteActionsView` | PnpTool.App | Ribbon |  |
| `Controls/Ribbon/Control_AccionesSobreImagenes.xaml.cs` (38) | `Control_AccionesSobreImagenes` | `ImageActionsView` | PnpTool.App | Ribbon |  |
| `Controls/Ribbon/Control_Ayuda.xaml.cs` (45) | `Control_Ayuda` | `HelpView` | PnpTool.App | Ribbon |  |
| `Controls/Ribbon/Control_Comandos.xaml.cs` (86) | `Control_Comandos` | `FileCommandsView` | PnpTool.App | Ribbon |  |
| `Controls/Ribbon/Control_ConfiguracionProyecto.xaml.cs` (517) | `Control_ConfiguracionProyecto` | `ProjectSettingsView` | PnpTool.App | Ribbon |  |
| `Controls/Ribbon/Control_CrearPrevisualizar.xaml.cs` (21) | `Control_CrearPrevisualizar` | `CreatePreviewView` | PnpTool.App | Ribbon |  |
| `Controls/Ribbon/Control_Herramientas.xaml.cs` (154) | `Control_Herramientas` | `ToolsView` | PnpTool.App | Ribbon |  |
| `Controls/Ribbon/Control_HerramientasExternas.xaml.cs` (51) | `Control_HerramientasExternas` | `ExternalToolsView` | PnpTool.App | Ribbon |  |
| `Controls/Ribbon/Control_Imagenes.xaml.cs` (29) | `Control_Imagenes` | `AddImagesView` | PnpTool.App | Ribbon |  |
| `Controls/Ribbon/Control_MazosPersonalizados.xaml.cs` (40) | `Control_MazosPersonalizados` | `CustomDecksView` | PnpTool.App | Ribbon |  |
| `Controls/Ribbon/Control_RibbonQuickAccessToolBar.xaml.cs` (28) | `Control_RibbonQuickAccessToolBar` | `QuickAccessBar` | PnpTool.App | Ribbon |  |
| `Controls/Ribbon_MazoPersonalizado/RibbonWindowMazoPersonalizado.xaml.cs` (32) | `RibbonWindowMazoPersonalizado` | `CustomDeckRibbon` | PnpTool.App | Ribbon |  |
| `Controls/Selector_Color.xaml.cs` (103) | `Selector_Color` | (not ported) | PnpTool.Controls |  | no window of the original uses it; the colour dialog is `ColorPickerDialog` |
| `Controls/Setup/Barra_Izquierda.xaml.cs` (63) | `Barra_Izquierda` | `AboutWindow` | PnpTool.App |  | its own window |
| `Controls/TextBoxDecimalWpf.cs` (63) | `TextBoxDecimalWpf` | `DecimalTextBox` | PnpTool.Controls |  |  |
| `Controls/TextBoxEnteroWpf.cs` (59) | `TextBoxEnteroWpf` | `IntegerTextBox` | PnpTool.Controls |  |  |
| `Models/Actualizaciones.cs` (184) | `Actualizaciones` | `UpdateChecker` | PnpTool.App |  | rewritten against this project's releases (task 36) |
| `Models/Dialogos.cs` (353) | `Dialogos` | `Dialogs` | PnpTool.App | Win32Dialog, ApiCodePack, Interop | message boxes and file dialogs |
|  | `TiposFicherosSalvar` *(enum)* | `SaveFileKind` | PnpTool.App | Win32Dialog, ApiCodePack, Interop |  |
| `Models/DoubleConverter.cs` (44) | `DoubleConverter` | `DoubleConverter` | PnpTool.Controls |  |  |
| `Models/ErrorWpf.cs` (87) | `ErrorWpf` | `ErrorPresenter` | PnpTool.App |  |  |
| `Models/FormPrincipalModeloLocal.cs` (600) | `FormPrincipalModeloLocal` | `MainWindowModel` | PnpTool.App |  |  |
|  | `ListadoAccesosDirectos` *(enum)* | `Shortcut` | PnpTool.App |  |  |
| `Models/FuncionesGeneralesPrograma.cs` (295) | `FuncionesGeneralesPrograma` | `AppHelpers` | PnpTool.App | ScreenHelper |  |
| `Models/GroupedItem.cs` (56) | `GroupedItem` | `GroupedItem` | PnpTool.App |  |  |
| `Models/ImagenesWpf.cs` (157) | `ImagenesWpf` | `BitmapInterop` | PnpTool.App | GDI, ApiCodePack | Magick/GDI <-> WPF bitmap conversion -> Magick <-> Avalonia Bitmap |
| `Models/InformacionSistema.cs` (75) | `InformacionSistema` | `SystemInfo` | PnpTool.App |  |  |
| `Models/LoadingService.cs` (48) | `LoadingService` | `ProgressWindow` | PnpTool.App |  | through `MainWindowViewModel.RunWithProgress` |
| `Models/MazoWinForm.cs` (157) | `MazoWinForm` | `DeckUiBridge` | PnpTool.App |  |  |
| `Models/MonitorHelper.cs` (121) | `MonitorHelper` | - | not ported | WinForms, PInvoke, Interop | only logged the monitors; window placement is `WindowPlacement` (task 42) |
|  | `DEVMODE` *(struct)* | (removed) | PnpTool.App | WinForms, PInvoke, Interop | Win32 struct |
| `Models/MultiplierConverter.cs` (32) | `MultiplierConverter` | `MultiplierConverter` | PnpTool.Controls |  |  |
| `Models/ProcesarCarpetaModelo.cs` (133) | `ProcesarCarpetaModelo` | `ProcessFolderModel` | PnpTool.App |  |  |
| `Models/ResolutionImageExtension.cs` (26) | `ResolutionImageExtension` | `ResolutionImageExtension` | PnpTool.Controls |  | markup extension picking icon size by screen resolution |
| `MyResourceDictionary.xaml.cs` (8) | `MyResourceDictionary` | (removed) | PnpTool.App |  | empty code-behind |
| `Properties/Resources.cs` (277) | `Resources` | `AppResources` | PnpTool.App | GDI | 82 embedded images/strings |
| `Properties/Settings.cs` (14) | `Settings` | (removed) | PnpTool.App |  | unused designer settings |
| `Ventanas_WPF/Configuracion/WindowConfigurarCara.xaml.cs` (272) | `WindowConfigurarCara` | `FaceSettingsWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Configuracion/WindowSetupEncabezado.xaml.cs` (94) | `WindowSetupEncabezado` | `HeaderFooterSetupWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Configuracion/WindowSetupImpresion.xaml.cs` (305) | `WindowSetupImpresion` | `PrintLayoutSetupWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Configuracion/WindowSetupLibros.xaml.cs` (123) | `WindowSetupLibros` | `BookSetupWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Configuracion/WindowSetupOtrosParametros.xaml.cs` (186) | `WindowSetupOtrosParametros` | `OtherSettingsWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Configuracion/WindowSetupPlantillasAvanzadas.xaml.cs` (106) | `WindowSetupPlantillasAvanzadas` | `AdvancedTemplateSetupWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Configuracion/WindowSetupPnpTool.xaml.cs` (208) | `WindowSetupPnpTool` | `AppSetupWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Configuracion/WindowSetupPnpToolReducida.xaml.cs` (76) | `WindowSetupPnpToolReducida` | `FirstRunSetupWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Herramientas/Window_CortarImagenes.xaml.cs` (503) | `Window_CortarImagenes` | `CropImagesWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Herramientas/Window_EditarTextBox.xaml.cs` (68) | `Window_EditarTextBox` | `EditTextWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Herramientas/Window_ModificarAlineacionPDF.xaml.cs` (109) | `Window_ModificarAlineacionPDF` | `PdfAlignmentWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Herramientas/Window_ModificarTamanoHojaPDF.xaml.cs` (87) | `Window_ModificarTamanoHojaPDF` | `PdfPageSizeWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Herramientas/Window_MTG.xaml.cs` (187) | `Window_MTG` | `MtgWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Herramientas/Window_PlantillasAvanzadas.xaml.cs` (467) | `Window_PlantillasAvanzadas` | `AdvancedTemplatesWindow` | PnpTool.App |  |  |
|  | `ObjetoLista` | `TemplateListItem` | PnpTool.App |  |  |
| `Ventanas_WPF/Herramientas/Window_ProcesarCarpeta.xaml.cs` (249) | `Window_ProcesarCarpeta` | `ProcessFolderWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Herramientas/Window_Unir_PDF.xaml.cs` (208) | `Window_Unir_PDF` | `MergePdfWindow` | PnpTool.App | GDI |  |
| `Ventanas_WPF/Herramientas/WindowComprimir.xaml.cs` (116) | `WindowComprimir` | `CompressWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Herramientas/WindowCrearMazoPersonalizado.xaml.cs` (279) | `WindowCrearMazoPersonalizado` | `CustomDeckCreatorWindow` | PnpTool.App | Win32Dialog, Ribbon |  |
| `Ventanas_WPF/Herramientas/WindowCrearMazoPersonalizadoDeportes.xaml.cs` (572) | `WindowCrearMazoPersonalizadoDeportes` | `SportsDeckCreatorWindow` | PnpTool.App | Win32Dialog, Ribbon |  |
| `Ventanas_WPF/Herramientas/WindowEditarimagenes.xaml.cs` (221) | `WindowEditarimagenes` | `EditImagesWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Herramientas/WindowExtraer.xaml.cs` (211) | `WindowExtraer` | `ExtractWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Herramientas/WindowPortadaLibro.xaml.cs` (284) | `WindowPortadaLibro` | `BookCoverWindow` | PnpTool.App | Ribbon |  |
| `Ventanas_WPF/Herramientas/WindowSepararFrontalesTraseras.xaml.cs` (64) | `WindowSepararFrontalesTraseras` | `SplitFrontsBacksWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/VentanaPrincipal/Ficheros.cs` (86) | `Ficheros` | `MainWindowFiles` | PnpTool.App | Ribbon |  |
| `Ventanas_WPF/VentanaPrincipal/UIState.cs` (25) | `UIState` | `UiState` | PnpTool.App |  |  |
| `Ventanas_WPF/Window_Anadir_Muchas_Imagenes_v2.xaml.cs` (229) | `Window_Anadir_Muchas_Imagenes_v2` | `AddManyImagesWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Window_Aviso.xaml.cs` (41) | `Window_Aviso` | `NoticeWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Window_BarraProgreso.xaml.cs` (202) | `Window_BarraProgreso` | `ProgressWindow` | PnpTool.App |  |  |
|  | `PantallasLlaman` *(enum)* | `ProgressCaller` | PnpTool.App |  |  |
| `Ventanas_WPF/Window_Cargando.xaml.cs` (20) | `Window_Cargando` | `LoadingWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Window_DescargaFichero.xaml.cs` (78) | `Window_DescargaFichero` | `DownloadFileWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Window_Dice7Pixels.xaml.cs` (91) | `Window_Dice7Pixels` | `DiceWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Window_moverA.xaml.cs` (71) | `Window_moverA` | `MoveToWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Window_News.xaml.cs` (24) | `Window_News` | (not ported) | - |  | loads a page from the printing company's site |
| `Ventanas_WPF/Window_PreguntarCambioCara.xaml.cs` (80) | `Window_PreguntarCambioCara` | `AskFaceChangeWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Window_Principal_v2.xaml.cs` (1165) | `Window_Principal_v2` | `MainWindow` | PnpTool.App | Ribbon | RibbonWindow, 1,165 lines |
| `Ventanas_WPF/Window_SeleccionarPanelesCaras.xaml.cs` (104) | `Window_SeleccionarPanelesCaras` | `SelectFacePanelsWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Window_SplashScreen.xaml.cs` (103) | `Window_SplashScreen` | - | not ported | WinForms | no splash screen (task 42) |
| `Ventanas_WPF/Window_SubirImagenes.xaml.cs` (144) | `Window_SubirImagenes` | `AddImagesWindow` | PnpTool.App |  | task 43; the file dialogs are opened by `MainWindowViewModel.AddImagesAsync` |
| `Ventanas_WPF/Window_Tabletop.xaml.cs` (175) | `Window_Tabletop` | `TabletopWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Window_VerMazo.xaml.cs` (61) | `Window_VerMazo` | `ViewDeckWindow` | PnpTool.App |  |  |

### ControlFuente

| Original file | Original type | New name | Target | Windows-only API | Notes |
|---|---|---|---|---|---|
| `Colores/DoubleExtension.cs` (18) | `DoubleExtension` | `DoubleExtensions` | PnpTool.Controls |  |  |
| `Colores/HEX.cs` (104) | `HEX` | `HexColor` | PnpTool.Controls |  |  |
| `Colores/HSL.cs` (76) | `HSL` | `HslColor` | PnpTool.Controls |  |  |
| `Colores/RGB.cs` (76) | `RGB` | `RgbColor` | PnpTool.Controls |  |  |
| `ColorToBrushConverter.cs` (27) | `ColorToBrushConverter` | `ColorToBrushConverter` | PnpTool.Controls |  |  |
| `ColorToTextConverter.cs` (41) | `ColorToTextConverter` | `ColorToTextConverter` | PnpTool.Controls |  |  |
| `Controls/BotonSelectorColor.cs` (47) | `BotonSelectorColor` | (merged into ColorButton) | PnpTool.Controls |  | duplicate |
| `FontTextConverter.cs` (28) | `FontTextConverter` | `FontTextConverter` | PnpTool.Controls |  |  |
| `Fuente/FontDialog.xaml.cs` (66) | `FontDialog` | `FontDialog` | PnpTool.Controls |  |  |
| `Fuente/FontExtension.cs` (35) | `FontExtension` | (merged into Core FontSpecExtensions) | PnpTool.Controls |  | duplicate of the ClasesGlobales class |
| `Fuente/FontHelper.cs` (99) | `FontHelper` | (merged into Core FontHelper) | PnpTool.Controls |  | duplicate |
| `Fuente/FontPickerControl.xaml.cs` (331) | `FontPickerControl` | `FontPicker` | PnpTool.Controls |  |  |
| `Fuente/FontPickerPopup.xaml.cs` (198) | `FontPickerPopup` | (not ported) | PnpTool.Controls |  | no window of the original uses it |
| `Fuente/FontPickerViewModel.cs` (45) | `FontPickerViewModel` | `FontPickerViewModel` | PnpTool.Controls |  |  |
| `Fuente/FontUpdateMananger.cs` (37) | `FontUpdateMananger` | (merged into Core FontUpdateManager) | PnpTool.Controls |  | duplicate |
| `Fuente/TextBoxDecimalWpf.cs` (53) | `TextBoxDecimalWpf` | (merged into DecimalTextBox) | PnpTool.Controls |  | duplicate |
| `HslPickerPosConverter.cs` (33) | `HslPickerPosConverter` | `HslPickerPositionConverter` | PnpTool.Controls |  |  |
| `HSLRangeConverter.cs` (41) | `HSLRangeConverter` | `HslRangeConverter` | PnpTool.Controls |  |  |
| `InvertLightColorConverter.cs` (32) | `InvertLightColorConverter` | `InvertLightColorConverter` | PnpTool.Controls |  |  |
| `PickerLanguageManager.cs` (170) | `PickerLanguageManager` | `PickerLanguageManager` | PnpTool.Controls |  | 14 XAML language dictionaries |
|  | `PickerSettings` | `PickerSettings` | PnpTool.Controls |  |  |

### Dsafa.WpfColorPicker, Actualizador, Autocaptura_WPF

Not mapped type by type. The colour picker is replaced by a third-party
Avalonia control, the updater is dropped, and AutoCaptura (one window, about
400 lines: timed screen-region capture into a folder using WinForms screen
APIs and GDI `CopyFromScreen`) is the behavioural reference for task 31.
