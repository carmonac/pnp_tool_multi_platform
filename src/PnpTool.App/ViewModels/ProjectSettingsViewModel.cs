using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Files;
using PnpTool.Core.Projects;
using PnpTool.Core.Templates;

namespace PnpTool.App.ViewModels;

/// <summary>
/// The settings of the project in hand and the two lists that choose a style for it: the
/// group and, inside the group, the style. Original: <c>Control_ConfiguracionProyecto</c>
/// and <c>Control_SelectorProyecto</c>.
/// </summary>
public sealed partial class ProjectSettingsViewModel : ViewModelBase
{
    private bool _loading;

    public ProjectSettingsViewModel()
    {
        Groups = StyleCatalog.Groups();
        _loading = true;
        SelectedGroup = Groups.First(group => group.Group == SettingsGroup.Custom);
        Settings.Group.Group = SettingsGroup.Custom;
        _loading = false;
    }

    /// <summary>The text beside the list of groups. Original: <c>Form_Setup_Label_Grupo</c>.</summary>
    public static string GroupLabel => Core.Infrastructure.StringResources.Get("Form_Setup_Label_Grupo");

    /// <summary>The text beside the list of styles. Original: <c>Form_Setup_Label_Subgrupo</c>.</summary>
    public static string StyleLabel => Core.Infrastructure.StringResources.Get("Form_Setup_Label_Subgrupo");

    /// <summary>Settings and advanced template of the project. Original: <c>Proyecto_Control_Configuracion</c>.</summary>
    public Project Project { get; } = new();

    public ProjectSettings Settings => Project.Settings;

    /// <summary>Original: the items of <c>Form_Setup_ComboBox_Grupo</c>.</summary>
    public IReadOnlyList<StyleGroupOption> Groups
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>The text beside the list of groups, in the language of the moment.</summary>
    public string GroupText => GroupLabel;

    /// <summary>The text beside the list of styles, in the language of the moment.</summary>
    public string StyleText => StyleLabel;

    /// <summary>
    /// Reads the groups and the styles again in the language the program has been changed
    /// to, keeping the ones chosen.
    /// </summary>
    public void Relocalize()
    {
        SettingsGroup? group = SelectedGroup?.Group;
        string style = SelectedStyle?.FileName ?? string.Empty;
        _loading = true;
        Groups = StyleCatalog.Groups();
        SelectedGroup = Groups.FirstOrDefault(option => option.Group == group);
        if (group != null)
        {
            Styles = StyleCatalog.Styles(group.Value);
            SelectedStyle = Styles.FirstOrDefault(option => option.FileName == style);
        }

        _loading = false;
        OnPropertyChanged(nameof(GroupText));
        OnPropertyChanged(nameof(StyleText));
    }

    [ObservableProperty]
    private StyleGroupOption? _selectedGroup;

    /// <summary>The styles of the group chosen. Original: the items of <c>Form_Setup_ComboBox_Subgrupo</c>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStyles))]
    private IReadOnlyList<StyleOption> _styles = [];

    [ObservableProperty]
    private StyleOption? _selectedStyle;

    /// <summary>Whether the group chosen has styles to choose from.</summary>
    public bool HasStyles => Styles.Count != 0;

    /// <summary>The file of the style chosen, or empty. Original: <c>RutaFicheroXML</c>.</summary>
    public string StyleFile { get; private set; } = string.Empty;

    /// <summary>Raised when the settings change. Original: <c>NotificaCambioConfiguracion</c>.</summary>
    public event EventHandler? SettingsChanged;

    /// <summary>
    /// Raised when the cards change between showing one face and showing two. Its arguments
    /// are whether the project had the same back for every card until now, and the settings
    /// as they were before. Original: <c>NotificaCambioTipoPanel</c>.
    /// </summary>
    public event Action<bool, ProjectSettings>? PanelTypeChanged;

    /// <summary>Raised with the message when a style could not be read.</summary>
    public event Action<string>? ProblemFound;

    // What the buttons of the settings tab can open. Original: the EsVisible_Boton methods.

    public bool CanEditLayout => Settings.Group.Group != SettingsGroup.Dice7Pixel;

    public bool CanEditOther => Settings.Group.Group != SettingsGroup.Dice7Pixel;

    public bool CanEditFront => Settings.Group.Group != SettingsGroup.Dice7Pixel && Settings.Group.ProjectType != ProjectType.Papermini;

    public bool CanEditBack =>
        Settings.PrintLayout.BackImage is not (BackImageMode.None or BackImageMode.NoBack)
        && Settings.Group.Group is not (SettingsGroup.Dice or SettingsGroup.Dice7Pixel or SettingsGroup.Books)
        && Settings.Group.ProjectType != ProjectType.Papermini;

    public bool CanEditHeaderFooter =>
        Settings.Group.Group is not (SettingsGroup.Soriano or SettingsGroup.Dice or SettingsGroup.Dice7Pixel or SettingsGroup.Tabletop);

    public bool CanEditBooks => Settings.Group.Group == SettingsGroup.Books;

    /// <summary>
    /// Whether the shared back image has a place in the main window: double-sided projects
    /// whose cards show one face, and gutterfold with the same back for every card.
    /// Original: <c>Mostrar_TraseraIgual</c>.
    /// </summary>
    public bool ShowsSharedBack
    {
        get
        {
            if (Settings.Group.ProjectType == ProjectType.Papermini)
            {
                return false;
            }

            PrintLayout print = Settings.PrintLayout;
            return print.DoubleSided ? !Settings.DoublePanel : print.BackImage is not (BackImageMode.NoBack or BackImageMode.DifferentBack);
        }
    }

    /// <summary>
    /// Whether a background colour is painted behind fronts and behind backs, so that the
    /// panel's menus offer one per card. Original: the start of <c>CambioConfiguracion</c>.
    /// </summary>
    public (bool Front, bool Back) PaintsBackground =>
        (Settings.Front.PaintBackground && Settings.Front.BackgroundEffect == CardBackgroundType.SingleColor,
         Settings.Back.PaintBackground && Settings.Back.BackgroundEffect == CardBackgroundType.SingleColor);

    /// <summary>
    /// Shows the settings of a project that was opened. Original: <c>CargaProyecto</c>.
    /// </summary>
    public void Load(ProjectSettings settings, AdvancedTemplate? template = null)
    {
        _loading = true;
        Settings.CopyFrom(settings);
        if (template != null)
        {
            Project.AdvancedTemplate = template.Clone();
        }

        SelectedGroup = Groups.FirstOrDefault(group => group.Group == Settings.Group.Group);
        Styles = StyleCatalog.Styles(Settings.Group.Group);
        int index = Settings.Group.SubgroupIndex;
        SelectedStyle = index >= 0 && index < Styles.Count ? Styles[index] : null;
        StyleFile = SelectedStyle == null ? string.Empty : StyleCatalog.PathOf(Settings.Group.Group, SelectedStyle.FileName);
        _loading = false;
        Changed();
    }

    /// <summary>
    /// Takes the layout the layout window gives back. Settings that changed turn the project
    /// into a custom one, and a change between one face and two is announced.
    /// Original: <c>Boton_Configurar_Disposicion_Click_Normal</c>.
    /// </summary>
    public void ApplyLayout(PrintLayout layout, bool doublePanel, Grid grid, bool lockCardSize)
    {
        var before = new ProjectSettings();
        before.CopyFrom(Settings);
        bool changed = !Settings.PrintLayout.IsEqualTo(layout)
            || Settings.DoublePanel != doublePanel
            || !Settings.Grid.IsEqualTo(grid)
            || Settings.LockCardSize != lockCardSize;
        Settings.PrintLayout.CopyFrom(layout);
        Settings.DoublePanel = doublePanel;
        Settings.Grid.CopyFrom(grid);
        Settings.LockCardSize = lockCardSize;
        MatchFaceSizesToCard();
        if (changed)
        {
            SwitchToCustom();
        }

        Changed();
        AnnouncePanelType(before);
    }

    /// <summary>
    /// Takes the sides and backs chosen for a project made with an advanced template, whose
    /// layout is the template's. Original: <c>Boton_Configurar_Disposicion_Click_PlantillaAvanzada</c>.
    /// </summary>
    public void ApplySides(bool doubleSided, BackImageMode backImage, bool doublePanel)
    {
        var before = new ProjectSettings();
        before.CopyFrom(Settings);
        Settings.PrintLayout.DoubleSided = doubleSided;
        Settings.PrintLayout.BackImage = backImage;
        Settings.DoublePanel = doublePanel;
        Changed();
        AnnouncePanelType(before);
    }

    /// <summary>
    /// Takes the settings of the fronts or of the backs the face window gives back, with
    /// where the backs are moved and turned. Settings that changed turn the project into a
    /// custom one. Original: the end of <c>Boton_Configurar_Cara_Frontal_Click</c>.
    /// </summary>
    /// <remarks>
    /// The original compared after copying, and the wrong way round for fronts: accepting
    /// the window of the fronts always made the project custom, and that of the backs never.
    /// </remarks>
    public void ApplyFace(Core.Cards.FaceType side, FaceSettings face, Core.Primitives.UnitPoint backOffset, double backRotation)
    {
        bool isBack = side == Core.Cards.FaceType.Back;
        FaceSettings current = isBack ? Settings.Back : Settings.Front;
        bool changed = !current.IsEqualTo(face);
        current.CopyFrom(face);
        if (isBack)
        {
            changed |= !Settings.BackOffset.X.EqualsInMillimeters(backOffset.X)
                || !Settings.BackOffset.Y.EqualsInMillimeters(backOffset.Y)
                || Settings.BackRotation != backRotation;
            Settings.BackOffset = new Core.Primitives.UnitPoint(backOffset);
            Settings.BackRotation = backRotation;
        }

        if (changed)
        {
            SwitchToCustom();
        }

        Changed();
    }

    /// <summary>
    /// Takes the header settings and the watermark the header window gives back. Settings
    /// that changed turn the project into a custom one.
    /// Original: the end of <c>Boton_Configurar_EncabezadoPie_Click</c>, which did not look at
    /// the watermark for that.
    /// </summary>
    public void ApplyHeaderFooter(HeaderFooter header, Watermark watermark)
    {
        bool changed = !Settings.HeaderFooter.IsEqualTo(header) || !Settings.Watermark.IsEqualTo(watermark);
        Settings.HeaderFooter = header.Clone();
        Settings.Watermark.CopyFrom(watermark);
        if (changed)
        {
            SwitchToCustom();
        }

        Changed();
    }

    /// <summary>
    /// Takes the other settings their window gives back. Settings that changed turn the
    /// project into a custom one. Original: the end of <c>Boton_Configurar_OtrosParametros_Click</c>,
    /// which asked <c>Equals</c> of two different objects and so never saw a change.
    /// </summary>
    public void ApplyOther(OtherSettings other)
    {
        bool changed = !Settings.Other.IsEqualTo(other);
        Settings.Other.CopyFrom(other);
        if (changed)
        {
            SwitchToCustom();
        }

        Changed();
    }

    /// <summary>
    /// Takes the book settings their window gives back. Settings that changed turn the
    /// project into a custom one. Original: the end of <c>Boton_Configurar_Libros_Click</c>,
    /// which did so when they had not changed.
    /// </summary>
    public void ApplyBook(BookSettings book)
    {
        bool changed = !Settings.Book.IsEqualTo(book);
        Settings.Book.CopyFrom(book);
        if (changed)
        {
            SwitchToCustom();
        }

        Changed();
    }

    /// <summary>
    /// Lists the styles of the group again, after templates were saved or deleted, keeping
    /// the one chosen when it is still there.
    /// </summary>
    public void RefreshStyles()
    {
        if (SelectedGroup?.Group != SettingsGroup.AdvancedStyles)
        {
            return;
        }

        string chosen = SelectedStyle?.FileName ?? string.Empty;
        _loading = true;
        Styles = StyleCatalog.Styles(SettingsGroup.AdvancedStyles);
        SelectedStyle = Styles.FirstOrDefault(option => option.FileName == chosen);
        _loading = false;
    }

    /// <summary>
    /// Marks the project as custom unless its group keeps its name whatever is changed.
    /// Original: <c>CompruebaCambioAPersonalizado</c> and <c>CambiaPersonalizado</c>.
    /// </summary>
    public void SwitchToCustom()
    {
        if (Settings.Group.Group is SettingsGroup.Soriano or SettingsGroup.Dice or SettingsGroup.Tabletop
            or SettingsGroup.AdvancedStyles or SettingsGroup.Dice7Pixel)
        {
            return;
        }

        _loading = true;
        SelectedGroup = Groups.First(group => group.Group == SettingsGroup.Custom);
        Styles = [];
        SelectedStyle = null;
        StyleFile = string.Empty;
        Settings.Group.Group = SettingsGroup.Custom;
        Settings.Group.SubgroupIndex = 0;
        _loading = false;
        Changed();
    }

    /// <summary>Writes the settings as a style file. Original: <c>GuadarXMLEstilo</c> from the save-style button.</summary>
    public void SaveStyle(string path) => ProjectFileStore.SaveStyle(path, Settings);

    /// <summary>
    /// With the card size locked, both faces take the size of the card, less the bleed on
    /// each side for a face with the mirror effect.
    /// Original: <c>Boton_Configurar_Disposicion_Click_CompruebaSangradoEspejo</c>.
    /// </summary>
    private void MatchFaceSizesToCard()
    {
        if (!Settings.LockCardSize)
        {
            return;
        }

        foreach (FaceSettings face in new[] { Settings.Front, Settings.Back })
        {
            face.Size = new Core.Primitives.UnitPoint(Settings.Grid.CardSize);
            if (face.Effect == ImageEffectType.Mirror)
            {
                double bleed = 2.0 * (double)Settings.Grid.Bleed;
                face.Size.X = (double)face.Size.X - bleed;
                face.Size.Y = (double)face.Size.Y - bleed;
            }
        }
    }

    /// <summary>Original: <c>Form_Setup_ComboBox_Grupo_SelectionChanged</c>.</summary>
    partial void OnSelectedGroupChanged(StyleGroupOption? value)
    {
        if (_loading || value == null)
        {
            return;
        }

        var before = new ProjectSettings();
        before.CopyFrom(Settings);
        ProjectGroup group = Settings.Group;
        group.PDFX = false;
        group.Group = value.Group;
        group.SubgroupIndex = 0;
        _loading = true;
        Styles = value.Group is SettingsGroup.Custom or SettingsGroup.Tabletop ? [] : StyleCatalog.Styles(value.Group);
        SelectedStyle = Styles.Count != 0 ? Styles[0] : null;
        _loading = false;
        StyleFile = string.Empty;
        if (SelectedStyle != null)
        {
            LoadStyle(SelectedStyle, 0);
        }

        ForceProjectType();
        Changed();
        AnnouncePanelType(before);
    }

    /// <summary>Original: <c>Form_Setup_ComboBox_Subgrupo_SelectionChanged</c>.</summary>
    partial void OnSelectedStyleChanged(StyleOption? value)
    {
        if (_loading || value == null)
        {
            return;
        }

        var before = new ProjectSettings();
        before.CopyFrom(Settings);
        LoadStyle(value, Math.Max(Styles.ToList().IndexOf(value), 0));
        ForceProjectType();
        Changed();
        AnnouncePanelType(before);
    }

    /// <summary>
    /// Reads the file of a style into the settings. Dice of the "seven pixels" kind have no
    /// file. Original: <c>CambioTipoProyecto</c>.
    /// </summary>
    private void LoadStyle(StyleOption style, int index)
    {
        SettingsGroup group = Settings.Group.Group;
        StyleFile = StyleCatalog.PathOf(group, style.FileName);
        if (StyleFile.Length != 0)
        {
            var read = new Project();
            AppError error;
            if (group == SettingsGroup.AdvancedStyles)
            {
                error = AdvancedTemplate.LoadInto(StyleFile, read);
            }
            else
            {
                ProjectSettings settings = read.Settings;
                error = ProjectFileStore.LoadStyle(StyleFile, ref settings).Error;
                read.Settings = settings;
            }

            if (error.HasError)
            {
                ProblemFound?.Invoke(error.Message);
            }
            else
            {
                Settings.CopyFrom(read.Settings);
                Project.AdvancedTemplate = read.AdvancedTemplate.Clone();
            }
        }

        // The lists are what the user chose, whatever group and position the file names.
        Settings.Group.Group = group;
        Settings.Group.SubgroupIndex = index;
    }

    /// <summary>
    /// The groups whose projects are of one kind whatever their style says. The original set
    /// the kind when such a group was chosen and never took it back, so a project stayed a
    /// "tabletop" or a "deck" one after leaving the group.
    /// </summary>
    private void ForceProjectType()
    {
        ProjectType? forced = Settings.Group.Group switch
        {
            SettingsGroup.SpanishDeck => ProjectType.SpanishDeck,
            SettingsGroup.Tabletop => ProjectType.Tabletop,
            SettingsGroup.Dice7Pixel => ProjectType.Dice7Pixel,
            _ => null,
        };
        if (forced != null)
        {
            Settings.Group.ProjectType = forced.Value;
        }
        else if (Settings.Group.ProjectType is ProjectType.SpanishDeck or ProjectType.Tabletop or ProjectType.Dice7Pixel)
        {
            // Left over from one of those groups, when the new group brought no style of its own.
            Settings.Group.ProjectType = ProjectType.Card;
        }
    }

    private void AnnouncePanelType(ProjectSettings before)
    {
        if (before.DoublePanel != Settings.DoublePanel)
        {
            PanelTypeChanged?.Invoke(before.PrintLayout.BackImage == BackImageMode.SameBack, before);
        }
    }

    private void Changed()
    {
        OnPropertyChanged(nameof(CanEditLayout));
        OnPropertyChanged(nameof(CanEditOther));
        OnPropertyChanged(nameof(CanEditFront));
        OnPropertyChanged(nameof(CanEditBack));
        OnPropertyChanged(nameof(CanEditHeaderFooter));
        OnPropertyChanged(nameof(CanEditBooks));
        OnPropertyChanged(nameof(ShowsSharedBack));
        if (!_loading)
        {
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
