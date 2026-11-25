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
| `Dsafa.WpfColorPicker` | Colour picker with eyedropper | 1,600 | 9 | Replaced by `PixiEditor.ColorPicker.AvaloniaUI`; eyedropper in task 31 |
| `LibreriaLogs` | Logger, archive extraction, downloader | 350 | 0 | Ported to `PnpTool.Core`, without the update URLs |
| `Actualizador` | Self-updater | 340 | 1 | Not ported (new update check in task 36) |
| `Autocaptura_WPF` | Screen capture helper (separate exe) | 780 | 3 | Reference for task 31 |

Types mapped below: 356 (165 PnpTool.Core, 112 PnpTool.App, 40 PnpTool.Controls, 20 PnpTool.Pdf, 13 PnpTool.Imaging, 6 not ported).

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
- **Ticket projects** (`.pnpt`): a separate layout writer and settings.
- **Book layout** writer and **custom-deck** writer, separate from the main PDF writer.
- **Import card PDF** tool (`FicheroPdfCartas`).
- **Pack project** and **validate images**.
- **Tabletop Simulator** sheet export and **dice** generator are image generators, not web features.
- **UI languages**: 15 satellite resource sets (ca, de, en-GB, en-US, es, fa, fr, it, nl, pl, pt, ru, tr, vi, zh), 620 strings each; the language enum also lists Korean and Japanese, to be checked in task 8.

Not ported:

- Telemetry (`AnaliticaSoriano`, `DatosEstadisticos`), update URLs and the `Actualizador` program.
- Soriano branding (`Control_AgradecimientoSoriano`, `logosriano.png`, Soriano styles).
- **PaperMini export** (`Papermini`): builds an encrypted package and a low-resolution PDF for the Soriano print service. Left out pending confirmation from the author.
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
- **File versions 14–17** are refused with a message for now; the upgrade code is backlog task 40.
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
  only for the paper-miniature feature, which is on hold; `ImageEffects.Apply` returns an error for
  it. Backlog task 41.
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
- **Deferred from the writer:** cut lines, marks, footer, frames, watermark on the page and file ID
  (task 13; the writer calls them through partial methods); rotation of back sheets
  (`PdfLayoutWriter.RotatePages`, supplied by the page-alignment tool in task 14); Spanish-deck and
  paper-miniature post-processing (tasks 38 and 41); the "Dice 7 pixels" list (task 28).
- **Differences from the original in the PDF engine**, each documented where it is:
  - Footer font is Liberation Serif instead of Times New Roman.
  - A project without an ICC profile file gets no output intent instead of failing.
  - Splitting by maximum size no longer starts with a blank one-page document when the first
    sheet alone exceeds the limit.
  - The one-sheet list for a shared back fills every cell (the original indexed it wrongly when
    rows and columns differed); pocketfold and advanced-template lists no longer read past the
    end of their arrays in edge cases.
  - The zero printer margin and the special warnings for the printing company's styles are gone.

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
| `ConfiguracionesViejas/ConfiguracionOtrosParametros.cs` (30) | `ConfiguracionOtrosParametros` | `OtherSettingsLegacy` | PnpTool.Core |  | old file version, load-only |
| `ConfiguracionesViejas/ConfiguracionOtrosParametrosV2.cs` (51) | `ConfiguracionOtrosParametrosV2` | `OtherSettingsV2` | PnpTool.Core |  | old file version, load-only |
| `ConfiguracionesViejas/ConfiguracionProyectoV14.cs` (117) | `ConfiguracionProyectoV14` | `ProjectSettingsV14` | PnpTool.Core |  | old file version, load-only |
|  | `OtrosParametros` | `OtherParametersV14` | PnpTool.Core |  | old file version, load-only |
| `ConfiguracionesViejas/ConfiguracionProyectoV15.cs` (125) | `ConfiguracionProyectoV15` | `ProjectSettingsV15` | PnpTool.Core |  | old file version, load-only |
| `ConfiguracionesViejas/ConfiguracionProyectoV16.cs` (126) | `ConfiguracionProyectoV16` | `ProjectSettingsV16` | PnpTool.Core |  | old file version, load-only |
| `ConfiguracionesViejas/ConfiguracionProyectoV17.cs` (171) | `ConfiguracionProyectoV17` | `ProjectSettingsV17` | PnpTool.Core |  | old file version, load-only |
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
| `EspacioProyecto/DisposicionImpresionV0.cs` (41) | `DisposicionImpresionV0` | `PrintLayoutV0` | PnpTool.Core |  | old file version, load-only |
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
| `Maquetacion/FicheroPdfMaquetado_Ticket.cs` (335) | `FicheroPdfMaquetado_Ticket` | `TicketPdfWriter` | PnpTool.Pdf |  |  |
| `Maquetacion/FicheroPdfMaquetadoBarajaPersonalizada.cs` (287) | `FicheroPdfMaquetadoBarajaPersonalizada` | `CustomDeckPdfWriter` | PnpTool.Pdf |  |  |
| `Maquetacion/FicheroPdfMaquetadoItext.cs` (1708) | `FicheroPdfMaquetadoItext` | `PdfLayoutWriter` | PnpTool.Pdf |  | main PDF generator (1,700 lines, iText) |
| `Maquetacion/FicheroPdfMaquetadoLibro.cs` (177) | `FicheroPdfMaquetadoLibro` | `BookPdfWriter` | PnpTool.Pdf |  |  |
| `Maquetacion/FicheroPdfMaquetadoNoDependienteLibreria.cs` (832) | `FicheroPdfMaquetadoNoDependienteLibreria` | `PdfLayoutBase` | PnpTool.Pdf | WPF | library-independent position maths |
|  | `QueArrayPosiciones` *(enum)* | `PositionArrayKind` | PnpTool.Pdf | WPF |  |
| `MarcasDePerforacion.cs` (59) | `MarcasDePerforacion` | `PerforationMarks` | PnpTool.Core |  |  |
|  | `TiposMarcasperforacion` *(enum)* | `PerforationMarkType` | PnpTool.Core |  |  |
| `Mazo.cs` (660) | `Mazo` | `Deck` | PnpTool.Core | WPF | the project's cards; drop its WPF usings |
| `Models/DonarModelo.cs` (50) | `DonarModelo` | `DonationModel` | PnpTool.Core |  |  |
| `Models/FormCortarImagenesModelo.cs` (140) | `FormCortarImagenesModelo` | `CropImagesModel` | PnpTool.Imaging | GDI, WPF |  |
| `Models/FormCrearTicketModelo.cs` (141) | `FormCrearTicketModelo` | `TicketModel` | PnpTool.Pdf | GDI | uses System.Drawing Rectangle/Color |
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
| `Models/FormSubirImagenesModelo.cs` (131) | `FormSubirImagenesModelo` | `AddImagesModel` | PnpTool.Core |  |  |
| `Models/FormTabletopModelo.cs` (638) | `FormTabletopModelo` | `TabletopModel` | PnpTool.Imaging | GDI | builds Tabletop Simulator sheet images |
| `Models/FormUnirPdfModeloIText.cs` (238) | `FormUnirPdfModeloIText` | `MergePdfModel` | PnpTool.Pdf |  |  |
| `Models/FormValidarImagenesModelo.cs` (73) | `FormValidarImagenesModelo` | `ValidateImagesModel` | PnpTool.Core |  |  |
| `Models/ModeloComprimir.cs` (116) | `ModeloComprimir` | `CompressModel` | PnpTool.Imaging | WPF |  |
| `Models/WindowCrearMazoPersonalizadoDeportesModelo.cs` (72) | `WindowCrearMazoPersonalizadoDeportesModelo` | `SportsDeckCreatorModel` | PnpTool.Core | WPF |  |
| `Models/WindowCrearMazoPersonalizadoModelo.cs` (67) | `WindowCrearMazoPersonalizadoModelo` | `CustomDeckCreatorModel` | PnpTool.Core |  |  |
| `Models/WindowModificarAlineacionPdfModelo.cs` (138) | `WindowModificarAlineacionPdfModelo` | `PdfAlignmentModel` | PnpTool.Pdf |  |  |
| `Models/WindowModificarTamanoHojaPdfModelo.cs` (36) | `WindowModificarTamanoHojaPdfModelo` | `PdfPageSizeModel` | PnpTool.Pdf |  |  |
| `Models/WindowSetupPnpToolModelo.cs` (160) | `WindowSetupPnpToolModelo` | `AppSetupModel` | PnpTool.Core | WPF |  |
| `NumericComparer.cs` (15) | `NumericComparer` | `NumericComparer` | PnpTool.Core |  |  |
| `Papermini.cs` (68) | `Papermini` | - | not ported |  | PaperMini export (encrypted package for the Soriano print service); confirm with the author before dropping for good |
| `PilaOperaciones.cs` (348) | `PilaOperaciones` | `OperationStack` | PnpTool.Core | WPF | undo/redo stack |
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
| `Tipos/FicheroPdfCartas.cs` (244) | `FicheroPdfCartas` | `CardPdfImporter` | PnpTool.Pdf |  | "import card PDF" tool |
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
| `Controls/Selector_Color.xaml.cs` (103) | `Selector_Color` | `ColorSelector` | PnpTool.Controls |  | wraps PixiEditor.ColorPicker -> PixiEditor.ColorPicker.AvaloniaUI |
| `Controls/Setup/Barra_Izquierda.xaml.cs` (63) | `Barra_Izquierda` | `SetupSideBar` | PnpTool.App |  |  |
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
| `Models/LoadingService.cs` (48) | `LoadingService` | `LoadingService` | PnpTool.App |  |  |
| `Models/MazoWinForm.cs` (157) | `MazoWinForm` | `DeckUiBridge` | PnpTool.App |  |  |
| `Models/MonitorHelper.cs` (121) | `MonitorHelper` | `ScreenHelper` | PnpTool.App | WinForms, PInvoke, Interop | user32 EnumDisplaySettings P/Invoke -> Avalonia Screens |
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
| `Ventanas_WPF/Window_News.xaml.cs` (24) | `Window_News` | `NewsWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Window_PreguntarCambioCara.xaml.cs` (80) | `Window_PreguntarCambioCara` | `AskFaceChangeWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Window_Principal_v2.xaml.cs` (1165) | `Window_Principal_v2` | `MainWindow` | PnpTool.App | Ribbon | RibbonWindow, 1,165 lines |
| `Ventanas_WPF/Window_SeleccionarPanelesCaras.xaml.cs` (104) | `Window_SeleccionarPanelesCaras` | `SelectFacePanelsWindow` | PnpTool.App |  |  |
| `Ventanas_WPF/Window_SplashScreen.xaml.cs` (103) | `Window_SplashScreen` | `SplashWindow` | PnpTool.App | WinForms |  |
| `Ventanas_WPF/Window_SubirImagenes.xaml.cs` (144) | `Window_SubirImagenes` | `AddImagesWindow` | PnpTool.App |  |  |
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
| `Fuente/FontPickerControl.xaml.cs` (331) | `FontPickerControl` | `FontPickerControl` | PnpTool.Controls |  |  |
| `Fuente/FontPickerPopup.xaml.cs` (198) | `FontPickerPopup` | `FontPickerPopup` | PnpTool.Controls |  |  |
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
