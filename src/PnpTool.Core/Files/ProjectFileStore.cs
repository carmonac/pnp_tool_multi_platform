using System.Collections.Concurrent;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using PnpTool.Core.Cards;
using PnpTool.Core.CustomDecks;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Projects;
using PnpTool.Core.Templates;

namespace PnpTool.Core.Files;

/// <summary>
/// Reads and writes the program's XML files: projects (.pnp), styles (.pnps), advanced
/// templates (.pnpa), custom decks (.pnpm) and tickets (.pnpt). Original: <c>FicherosXml</c>.
/// </summary>
/// <remarks>
/// Every file is UTF-16 XML with a comment, a wrapper element, a <c>Version</c> element and
/// then one serialised object per part. Element names are the original Spanish ones.
/// </remarks>
public static class ProjectFileStore
{
    private const string VersionElement = "Version";

    /// <summary>Version written to every file, whatever its kind. Original: <c>VersionFicheroXMLEstilo</c>.</summary>
    public const int StyleFileVersion = 18;

    /// <summary>Oldest style/project version that can be upgraded.</summary>
    public const int OldestUpgradableVersion = 14;

    /// <summary>Original: <c>VersionFicheroXMLTicket</c>.</summary>
    public const int TicketFileVersion = 1;

    /// <summary>Original: <c>VersionFicheroXMLPlantillaAvanzada</c>.</summary>
    public const int AdvancedTemplateFileVersion = 2;

    private const string FileComment = "Fichero del programa PNP Tool";
    private const string ProjectWrapper = "PNP_Tool_Proyecto";
    private const string StyleWrapper = "PNP_Tool_Estilo";
    private const string DeckWrapper = "PNP_Tool_Mazo";
    private const string TemplateWrapper = "PNP_Tool_Plantilla_Avanzada";
    private const string TicketWrapper = "PNP_Tool_Ticket";
    private const string AppSettingsWrapper = "PNP_Tool_ConfiguracionPrograma";
    private const string AppSettingsComment = "Fichero del programa PNP Tool - APP Settings";

    private const string SettingsElement = "ConfiguracionProyectoVActual";
    private const string DeckElement = "Mazo";
    private const string TemplateElement = "PlantillaAvanzada";
    private const string CustomDeckElement = "MazoPersonalizado";
    private const string TicketElement = "ConfiguracionProyectoTicket";
    private const string AppSettingsElement = "ConfiguracionProgramaNoEstatica";

    /// <summary>
    /// One serialiser per type and element name. A serialiser built with an explicit root
    /// generates code each time it is created and that code is never unloaded, so they are kept.
    /// </summary>
    private static readonly ConcurrentDictionary<(Type Type, string Element), XmlSerializer> Serializers = new();

    /// <summary>Receives (show, percent, message) while a project loads. Original: <c>ReportProgressDelegate</c>.</summary>
    public static Action<bool, double, string>? ReportProgress { get; set; }

    // ---------------------------------------------------------------- reading

    /// <summary>
    /// The number in the file's <c>Version</c> element, 0 when there is none.
    /// Original: <c>AbrirXML_DevuelveNumeroVersion</c>.
    /// </summary>
    public static (AppError Error, int Version) ReadVersion(string path)
    {
        var error = new AppError();
        if (!File.Exists(path))
        {
            error.Kind = ErrorKind.Error;
            error.Message = path + " not found";
            return (error, 0);
        }

        var document = new XmlDocument();
        document.Load(path);
        XmlNode? node = document.GetElementsByTagName(VersionElement)[0];
        int version = node != null ? Convert.ToInt32(node.InnerText) : 0;
        return (error, version);
    }

    /// <summary>
    /// Deserialises the first element called <paramref name="elementName"/> in the file.
    /// Original: <c>AbrirXML_Generico</c> followed by <c>XmlSerializer.Deserialize</c>.
    /// </summary>
    private static (AppError Error, T? Value) ReadElement<T>(string path, string elementName)
        where T : class
    {
        var error = new AppError();
        var document = new XmlDocument();
        document.Load(path);
        XmlNode? node = document.GetElementsByTagName(elementName)[0];
        if (node == null)
        {
            error.Kind = ErrorKind.Error;
            return (error, null);
        }

        XmlSerializer serializer = Serializers.GetOrAdd(
            (typeof(T), elementName),
            key => new XmlSerializer(key.Type, new XmlRootAttribute(key.Element)));
        using var reader = new XmlNodeReader(node);
        return (error, serializer.Deserialize(reader) as T);
    }

    /// <summary>
    /// Loads a style, or the settings part of a project. The second value is true when the
    /// file was upgraded from an older version and the project should be saved again.
    /// Original: <c>AbrirXMLEstilo</c>.
    /// </summary>
    public static (AppError Error, bool SaveAfterUpgrade) LoadStyle(string path, ref ProjectSettings settings)
    {
        (AppError error, int version) = ReadVersion(path);
        if (error.HasError)
        {
            return (error, false);
        }

        if (version < OldestUpgradableVersion)
        {
            error.Kind = ErrorKind.Error;
            error.Message = "PNP Files with version under 14 are not longer supported. Please use -> Version 18.12 to update to version 12 Version 20.6 to update to version 14";
            return (error, false);
        }

        bool upgraded = version < StyleFileVersion;
        if (upgraded)
        {
            (error, settings) = LoadOldStyle(path, version);
        }
        else
        {
            (error, ProjectSettings? loaded) = ReadElement<ProjectSettings>(path, SettingsElement);
            settings = loaded ?? new ProjectSettings();
        }

        string? folder = Path.GetDirectoryName(path);
        bool relative = settings.Other.ImagePathMode == ImagePathMode.Relative;
        if (!string.IsNullOrEmpty(settings.SharedBack.OriginalPath))
        {
            if (relative)
            {
                settings.SharedBack.OriginalPath = PathHelper.ResolveRelative(folder, settings.SharedBack.OriginalPath);
            }

            settings.SharedBack.Path = settings.SharedBack.OriginalPath;
        }

        OtherSettings other = settings.Other;
        if (!string.IsNullOrEmpty(other.ColorProfilePath))
        {
            if (relative)
            {
                other.ColorProfilePath = PathHelper.ResolveRelative(folder, other.ColorProfilePath);
                if (!File.Exists(other.ColorProfilePath))
                {
                    other.ColorProfilePath = IccProfiles.PathOrDefault(null, other.ColorSpace);
                }
            }
        }
        else
        {
            other.ColorProfilePath = IccProfiles.PathOrDefault(null, other.ColorSpace);
        }

        other.ColorProfile = IccProfiles.TryLoad(other.ColorProfilePath);
        return (error, upgraded);
    }

    /// <summary>
    /// Reads the settings of a file of version 14 to 17 and brings them to today's. A style
    /// file is saved again in today's version, its old self kept beside it as
    /// <c>name_backup.pnps</c>; a project is saved by <see cref="LoadProject"/>.
    /// Original: <c>AbrirXMLEstilo_Viejo</c>.
    /// </summary>
    private static (AppError Error, ProjectSettings Settings) LoadOldStyle(string path, int version)
    {
        // Up to version 16 the element carried the version in its name.
        string element = version >= 17 ? SettingsElement : "ConfiguracionProyectoV" + version;
        (AppError error, LegacyProjectSettings? old) = ReadElement<LegacyProjectSettings>(path, element);
        ProjectSettings settings = (old ?? new LegacyProjectSettings()).Upgrade(version);

        string extension = Path.GetExtension(path);
        if (extension is ".pnps" or ".xml")
        {
            string backup = Path.Combine(
                Path.GetDirectoryName(path) ?? string.Empty,
                Path.GetFileNameWithoutExtension(path) + "_backup" + extension);
            try
            {
                File.Copy(path, backup, overwrite: true);
                SaveStyle(path, settings);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A style that cannot be written (a read-only folder) is still usable as read.
                Log.Write("Style not saved after upgrade: " + exception.Message);
            }
        }

        return (error, settings);
    }

    /// <summary>
    /// True when the file was written by a program older than 20.3, or its first line does not
    /// say. The groups of styles were renumbered then. Original: <c>CompruebaCambioV20117</c>.
    /// </summary>
    private static bool WrittenBeforeGroupsChanged(string path)
    {
        // The first line is the comment "<!--Fichero del programa PNP Tool 20.11.1 ...-->".
        const int VersionStart = 34;
        try
        {
            string[] parts = File.ReadLines(path).First()[VersionStart..].Split('-')[0].Split('.');
            return parts.Length < 3
                || !int.TryParse(parts[0], out int major)
                || !int.TryParse(parts[1], out int minor)
                || !int.TryParse(parts[2], out int patch)
                || (major * 10000) + (minor * 100) + patch < 200300;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException)
        {
            return true;
        }
    }

    /// <summary>Original: <c>Comprobar_AbrirXML_V2</c>.</summary>
    private static AppError CheckProjectVersion(string path)
    {
        (AppError error, int version) = ReadVersion(path);
        if (error.HasError)
        {
            return error;
        }

        if (version <= 12)
        {
            error.Message = "PNP Files with version under 12 are not longer supported. Please use Version from 18.12 to update to version 12";
            error.Kind = ErrorKind.Error;
        }
        else if (version > StyleFileVersion)
        {
            error.AppendResource("FicheroXML_MasModerno");
            error.Kind = ErrorKind.Error;
        }

        return error;
    }

    /// <summary>Loads a project file (.pnp). Original: <c>AbrirXML_V2</c>.</summary>
    public static (AppError Error, Project Project) LoadProject(string path)
    {
        AppError error = CheckProjectVersion(path);
        var project = new Project();
        if (error.HasError)
        {
            return (error, project);
        }

        ReportProgress?.Invoke(true, 0.0, string.Empty);
        ReportProgress?.Invoke(true, 10.0, "open style");
        ProjectSettings settings = project.Settings;
        (error, bool upgraded) = LoadStyle(path, ref settings);
        project.Settings = settings;
        if (error.HasError)
        {
            ReportProgress?.Invoke(false, 0.0, string.Empty);
            return (error, project);
        }

        if (project.Settings.Group.Group == SettingsGroup.AdvancedStyles)
        {
            AdvancedTemplate template = project.AdvancedTemplate;
            LoadAdvancedTemplate(path, ref template);
            project.AdvancedTemplate = template;
        }

        ReportProgress?.Invoke(true, 30.0, "open deck");
        Deck deck = project.Deck;
        LoadDeck(path, ref deck, project.Settings.Other.ImagePathMode);
        project.Deck = deck;
        if (upgraded)
        {
            SaveUpgradedProject(path, project);
        }

        ReportProgress?.Invoke(true, 80.0, "last check");
        ReportProgress?.Invoke(false, 0.0, string.Empty);
        return (error, project);
    }

    /// <summary>
    /// Saves a project read from an older file version in today's, keeping the old file beside
    /// it as <c>name_back.pnp</c>. Original: the end of <c>AbrirXML_V2</c>.
    /// </summary>
    private static void SaveUpgradedProject(string path, Project project)
    {
        if (WrittenBeforeGroupsChanged(path))
        {
            project.Settings.Group.Group = SettingsGroup.Custom;
            project.Settings.Group.SubgroupIndex = 0;
        }

        string backup = Path.Combine(
            Path.GetDirectoryName(path) ?? string.Empty,
            Path.GetFileNameWithoutExtension(path) + "_back.pnp");
        try
        {
            File.Move(path, backup, overwrite: true);
            SaveProject(path, project);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A project that cannot be written (a read-only folder) still opens.
            Log.Write("Project not saved after upgrade: " + exception.Message);
        }
    }

    /// <summary>Loads the deck part of a project. Original: <c>AbrirXMLMazo</c>.</summary>
    public static AppError LoadDeck(string path, ref Deck deck, ImagePathMode pathMode)
    {
        Log.Enter(nameof(LoadDeck));
        (AppError error, Deck? loaded) = ReadElement<Deck>(path, DeckElement);
        if (error.HasError)
        {
            error.Kind = ErrorKind.Error;
            error.Message = "unknow error";
            Log.Exit(error.Message);
            return error;
        }

        deck = loaded ?? new Deck();
        if (pathMode == ImagePathMode.Relative)
        {
            deck.ResolveRelativePaths(Path.GetDirectoryName(path) ?? string.Empty);
        }

        ReportProgress?.Invoke(true, 45.0, "redo path");

        // Colour profiles are not stored in the file; CMYK images get theirs read again.
        if (ImageInspector.IsRegistered)
        {
            foreach (Card card in deck.Cards)
            {
                ReloadCmykProfile(card.Front);
                ReloadCmykProfile(card.Back);
            }
        }

        Log.Exit(nameof(LoadDeck));
        return error;
    }

    private static void ReloadCmykProfile(CardFace face)
    {
        if (face.ImageKind == ImageKind.Normal && face.IsCmyk)
        {
            face.ColorProfile = ImageInspector.Current.GetImageProperties(face.OriginalPath).ColorProfile;
        }
    }

    /// <summary>Loads a custom deck (.pnpm). Original: <c>AbrirXML_MazoPersonalizado</c>.</summary>
    public static AppError LoadCustomDeck(string path, ref CustomDeck deck)
    {
        (AppError error, CustomDeck? loaded) = ReadElement<CustomDeck>(path, CustomDeckElement);
        if (!error.HasError)
        {
            deck = loaded ?? new CustomDeck();
        }

        return error;
    }

    /// <summary>
    /// Loads a sports deck stored under the element <paramref name="elementName"/>.
    /// Original: <c>AbrirXML_MazoPersonalizadoDeporte</c>.
    /// </summary>
    public static (AppError Error, SportsDeck Deck) LoadSportsDeck(string path, string elementName)
    {
        (AppError error, SportsDeck? loaded) = ReadElement<SportsDeck>(path, elementName);
        return (error, loaded ?? new SportsDeck());
    }

    /// <summary>
    /// Loads an advanced template (.pnpa, or the template part of a project). Version 1 files
    /// are upgraded and saved back to the advanced-styles folder. Original: <c>AbrirPlantillaAvanzada</c>.
    /// </summary>
    public static AppError LoadAdvancedTemplate(string path, ref AdvancedTemplate template)
    {
        (AppError error, int version) = ReadVersion(path);
        if (error.HasError)
        {
            return error;
        }

        if (version < AdvancedTemplateFileVersion)
        {
            (error, AdvancedTemplateV1? old) = ReadElement<AdvancedTemplateV1>(path, TemplateElement);
            if (error.HasError)
            {
                return error;
            }

            template = old?.Upgrade() ?? new AdvancedTemplate();
            template.Save();
            return error;
        }

        (error, AdvancedTemplate? loaded) = ReadElement<AdvancedTemplate>(path, TemplateElement);
        if (!error.HasError)
        {
            template = loaded ?? new AdvancedTemplate();
        }

        return error;
    }

    /// <summary>
    /// Loads the program settings into <see cref="AppSettings"/>. A missing file is reported as
    /// an error and leaves the settings as they are. Original: <c>AbrirXMLAPPSettings_V2</c>;
    /// switching the interface language afterwards is left to the caller.
    /// </summary>
    public static AppError LoadAppSettings(string path)
    {
        (AppError error, _) = ReadVersion(path);
        if (error.HasError)
        {
            return error;
        }

        (error, StoredAppSettings? stored) = ReadElement<StoredAppSettings>(path, AppSettingsElement);
        if (!error.HasError)
        {
            (stored ?? new StoredAppSettings()).CopyToAppSettings();
        }

        return error;
    }

    /// <summary>Loads a ticket project (.pnpt). Original: <c>AbrirXMLTicket</c>.</summary>
    /// <remarks>
    /// The original also checked the version number, but its result was discarded, and saved
    /// tickets carry the style version (18) rather than the ticket version. The check is omitted.
    /// </remarks>
    public static AppError LoadTicket(string path, ref TicketProjectSettings settings)
    {
        (AppError error, _) = ReadVersion(path);
        if (error.HasError)
        {
            return error;
        }

        (error, TicketProjectSettings? loaded) = ReadElement<TicketProjectSettings>(path, TicketElement);
        if (!error.HasError)
        {
            settings = loaded ?? new TicketProjectSettings();
        }

        return error;
    }

    // ---------------------------------------------------------------- writing

    /// <summary>
    /// Writes a file: comment, wrapper element, version, then each non-null part.
    /// Original: <c>GuardarXMLGenerico</c>.
    /// </summary>
    private static void Write(string path, string title, string wrapperElement, params object?[] parts)
    {
        using var stream = new FileStream(path, FileMode.Create);
        using var writer = new XmlTextWriter(stream, Encoding.Unicode) { Formatting = Formatting.Indented };
        writer.WriteComment(title + " " + AppSettings.ProgramVersion);
        writer.WriteStartElement(wrapperElement);
        writer.WriteStartElement(VersionElement);
        writer.WriteString(StyleFileVersion.ToString());
        writer.WriteEndElement();
        foreach (object? part in parts)
        {
            if (part != null)
            {
                new XmlSerializer(part.GetType()).Serialize(writer, part);
            }
        }

        writer.WriteEndElement();
    }

    /// <summary>Saves the program settings. Original: <c>GuardarXMLAPPSettings</c>.</summary>
    public static void SaveAppSettings(string path) =>
        Write(path, AppSettingsComment, AppSettingsWrapper, new StoredAppSettings());

    /// <summary>Saves a style (.pnps). Original: <c>GuadarXMLEstilo</c>.</summary>
    public static void SaveStyle(string path, ProjectSettings settings) =>
        Write(path, FileComment, StyleWrapper, settings);

    /// <summary>Saves a custom or sports deck (.pnpm). Original: <c>GuadarXML_MazoPersonalizado</c>.</summary>
    public static void SaveCustomDeck(string path, CustomDeck deck) =>
        Write(path, FileComment, DeckWrapper, deck);

    /// <summary>
    /// Saves into the advanced-styles folder under the template's file name.
    /// Original: <c>GuardarXMLPlantillaAvanzada</c>.
    /// </summary>
    public static void SaveAdvancedTemplate(AdvancedTemplate template) =>
        Write(AppSettings.AdvancedStylesFolder + template.FileName, TemplateWrapper, TemplateWrapper, template);

    /// <summary>Saves a ticket project (.pnpt). Original: <c>GuardarXMLTicket</c>.</summary>
    public static void SaveTicket(string path, TicketProjectSettings settings) =>
        Write(path, TicketWrapper, TicketWrapper, settings);

    /// <summary>
    /// Saves a project (.pnp): settings, advanced template when the project uses one, and deck.
    /// With relative image paths, paths are written relative to the project file and restored
    /// in memory afterwards. The file is written under a temporary name and then moved into place.
    /// Original: <c>GuardarXML_V2</c>.
    /// </summary>
    public static void SaveProject(string path, Project project)
    {
        ProjectSettings settings = project.Settings;
        Deck deck = project.Deck;
        bool relative = settings.Other.ImagePathMode == ImagePathMode.Relative;
        bool sharedBack = settings.PrintLayout.BackImage == BackImageMode.SameBack;
        string sharedBackPath = string.Empty;
        string tempPath = path[..^1] + "~";
        var originalPaths = new string[deck.Cards.Length, 2];
        var paths = new string[deck.Cards.Length, 2];

        if (relative)
        {
            deck.MakePathsRelative(path, originalPaths, paths);
            if (sharedBack)
            {
                sharedBackPath = settings.SharedBack.OriginalPath;
                if (settings.SharedBack.ImageKind == ImageKind.Normal)
                {
                    settings.SharedBack.OriginalPath = PathHelper.ToFile(PathHelper.MakeRelative(path, settings.SharedBack.OriginalPath));
                }
            }
        }

        try
        {
            AdvancedTemplate? template = settings.Group.Group == SettingsGroup.AdvancedStyles ? project.AdvancedTemplate : null;
            Write(tempPath, FileComment, ProjectWrapper, settings, template, deck);
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (relative)
            {
                deck.RestoreFullPaths(originalPaths, paths);
                if (sharedBack)
                {
                    settings.SharedBack.OriginalPath = sharedBackPath;
                }
            }
        }
    }

    /// <summary>The project settings as an XML string. Original: <c>SerializeObjectToString</c>.</summary>
    public static string SerializeToString(Project project)
    {
        // The original serialised the whole Proyecto object, whose members are public fields.
        var serializer = new XmlSerializer(typeof(ProjectSettings));
        using var writer = new StringWriter();
        serializer.Serialize(writer, project.Settings);
        return writer.ToString();
    }
}

/// <summary>Names of the custom properties written into generated PDFs. Original: the <c>AtributoPDF_*</c> constants of <c>FicherosXml</c>.</summary>
public static class PdfMetadataKeys
{
    public const string PdfId = "IDPDF";
    public const string CardCount = "NumeroCartas";
    public const string SheetCount = "NumeroHojas";
    public const string SheetWidth = "TamanoHojaX";
    public const string SheetHeight = "TamanoHojaY";
    public const string CardWidth = "TamanoCartaX";
    public const string CardHeight = "TamanoCartaY";
    public const string SpacingX = "SeparacionX";
    public const string SpacingY = "SeparacionY";
    public const string Bleed = "Sangrado";
    public const string DoubleSided = "DobleCara";
    public const string Author = "PNP Tool";
    public const string GroupIndex = "IndiceGrupo";
    public const string ProgramVersion = "Version Programa";
    public const string Hash = "Hash";
}
