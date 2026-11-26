using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.Core;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Core.Templates;

namespace PnpTool.App.ViewModels;

/// <summary>A place for a card in the template being edited. Original: <c>ObjetoLista</c>.</summary>
public sealed partial class TemplateSlotViewModel : ObservableObject
{
    public TemplateSlotViewModel(AdvancedTemplateCard card, Color color, string text)
    {
        Card = card;
        Color = color;
        Fill = new SolidColorBrush(color);
        Text = text;
    }

    public AdvancedTemplateCard Card { get; }

    public Color Color { get; }

    public IBrush Fill { get; }

    /// <summary>Where it is, its size and its turn, as the list shows them.</summary>
    public string Text { get; }

    /// <summary>Its place in the list, as "#1".</summary>
    [ObservableProperty]
    private string _order = string.Empty;
}

/// <summary>
/// The editor of advanced templates (.pnpa): sheets where every card has its own place, size
/// and turn. Original: <c>Window_PlantillasAvanzadas</c>.
/// </summary>
public sealed partial class TemplateEditorViewModel : ViewModelBase
{
    private const string White = "#FFFFFF";
    private readonly string _location = StringResources.Get("Ubicacion");
    private readonly string _size = StringResources.Get("Tamano");
    private bool _loading;

    public TemplateEditorViewModel()
    {
        Paper.Changed += (_, _) => PreviewChanged?.Invoke(this, EventArgs.Empty);
        Slots.CollectionChanged += (_, _) => PreviewChanged?.Invoke(this, EventArgs.Empty);
        _templates = ListTemplates();
        _selectedTemplate = Templates[0];
    }

    /// <summary>"New" and then the templates of the folder. Original: <c>ComboBox_Listado_Plantillas</c>.</summary>
    [ObservableProperty]
    private IReadOnlyList<StyleOption> _templates;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNew))]
    private StyleOption _selectedTemplate;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _bleedText = "0";

    public PaperChoiceViewModel Paper { get; } = new();

    public ObservableCollection<TemplateSlotViewModel> Slots { get; } = [];

    [ObservableProperty]
    private TemplateSlotViewModel? _selectedSlot;

    [ObservableProperty]
    private string _locationXText = string.Empty;

    [ObservableProperty]
    private string _locationYText = string.Empty;

    [ObservableProperty]
    private string _sizeXText = string.Empty;

    [ObservableProperty]
    private string _sizeYText = string.Empty;

    /// <summary>Turns offered for a card, in degrees.</summary>
    public IReadOnlyList<int> Rotations { get; } = [0, 90];

    [ObservableProperty]
    private int _rotation;

    /// <summary>Whether no template of the folder is chosen.</summary>
    public bool IsNew => SelectedTemplate.FileName.Length == 0;

    /// <summary>Raised to tell the user something.</summary>
    public event Action<string>? Notice;

    /// <summary>Raised when what the sketch of the sheet draws has changed.</summary>
    public event EventHandler? PreviewChanged;

    /// <summary>Raised when a template has been saved or deleted.</summary>
    public event EventHandler? TemplatesChanged;

    private static string FilePath(string fileName) => AppSettings.AdvancedStylesFolder + fileName;

    /// <summary>Reads a template file; a file that is not one gives an error instead of ending the program.</summary>
    private static AppError Read(string path, ref AdvancedTemplate template)
    {
        try
        {
            return ProjectFileStore.LoadAdvancedTemplate(path, ref template);
        }
        catch (Exception exception) when (exception is System.Xml.XmlException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return new AppError { Kind = ErrorKind.Error, Message = Path.GetFileName(path) + ": " + exception.Message };
        }
    }

    private static IReadOnlyList<StyleOption> ListTemplates() =>
        [new StyleOption(StringResources.Get("Nuevo").Trim(), string.Empty, ProjectType.Card), .. StyleCatalog.Styles(SettingsGroup.AdvancedStyles)];

    /// <summary>Lists the templates again and chooses one by its file. Original: <c>CargaComboBox</c>.</summary>
    private void Reload(string fileName)
    {
        IReadOnlyList<StyleOption> templates = ListTemplates();
        StyleOption chosen = templates.FirstOrDefault(option => option.FileName == fileName) ?? templates[0];
        _loading = true;
        Templates = templates;
        _loading = false;
        SelectedTemplate = chosen;
        Show(chosen);
    }

    partial void OnSelectedTemplateChanged(StyleOption value)
    {
        if (!_loading && value != null)
        {
            Show(value);
        }
    }

    /// <summary>Original: <c>ComboBox_Listado_Plantillas_SelectionChanged</c>.</summary>
    private void Show(StyleOption option)
    {
        Slots.Clear();
        Name = string.Empty;
        if (option.FileName.Length == 0)
        {
            return;
        }

        var template = new AdvancedTemplate();
        AppError error = Read(FilePath(option.FileName), ref template);
        if (error.HasError)
        {
            Notice?.Invoke(error.Message);
            return;
        }

        Name = template.Name;
        BleedText = template.Bleed.ToDisplayString();
        Paper.Load(template.Paper);
        foreach (AdvancedTemplateCard card in template.Cards)
        {
            Insert(card, tell: false);
        }
    }

    // ---- Cards of the template ----

    /// <summary>Original: <c>InsertaElementoEnLista_Valor</c>.</summary>
    private string Describe(AdvancedTemplateCard card) =>
        _location + " (X:" + card.Location.X.ToDisplayString() + " Y:" + card.Location.Y.ToDisplayString() + ") - "
        + _size + " (X:" + card.Size.X.ToDisplayString() + " Y:" + card.Size.Y.ToDisplayString() + ") º:" + card.Rotation;

    /// <summary>A card without colour of its own gets one at random. Original: <c>InsertaElementoEnLista_Brocha</c>.</summary>
    private static Color ColorOf(string text)
    {
        if (text == White || !Color.TryParse(text, out Color color))
        {
            byte[] parts = new byte[3];
            Random.Shared.NextBytes(parts);
            return Color.FromRgb(parts[0], parts[1], parts[2]);
        }

        return color;
    }

    private void Insert(AdvancedTemplateCard card, bool tell)
    {
        Color color = ColorOf(card.Color);
        card.Color = color.ToString().ToUpperInvariant();
        Slots.Add(new TemplateSlotViewModel(card, color, Describe(card)) { Order = "#" + (Slots.Count + 1) });
        string? problem = ProblemWith(card);
        if (tell && problem != null)
        {
            Notice?.Invoke(problem);
        }
    }

    /// <summary>
    /// What is wrong with a card that goes beyond the sheet, or null. The sheet is measured as
    /// it is printed. Original: <c>ComprobarValidez</c>, which measured it upright always.
    /// </summary>
    public string? ProblemWith(AdvancedTemplateCard card)
    {
        UnitPoint paper = Paper.OrientedSize;
        string text = _location + " + " + _size + " > " + StringResources.Get("Papel") + " " + _size;
        string? problem = null;
        if (card.Location.X.Millimeter + card.Size.X.Millimeter > paper.X.Millimeter + 0.001)
        {
            problem = "X " + text;
        }

        if (card.Location.Y.Millimeter + card.Size.Y.Millimeter > paper.Y.Millimeter + 0.001)
        {
            problem = "Y " + text;
        }

        return problem;
    }

    /// <summary>Whether every card is inside the sheet.</summary>
    public bool Fits(TemplateSlotViewModel slot) => ProblemWith(slot.Card) == null;

    /// <summary>
    /// Adds a card with the place, size and turn typed. Original: <c>BotonAnadir_Click</c>,
    /// which told that data was missing and added the card all the same.
    /// </summary>
    public void Add()
    {
        if (LocationXText.Length == 0 || LocationYText.Length == 0 || SizeXText.Length == 0 || SizeYText.Length == 0)
        {
            Notice?.Invoke(StringResources.Get("faltan_datos"));
            return;
        }

        Insert(
            new AdvancedTemplateCard
            {
                Location = new UnitPoint { X = Unit.FromDisplayString(LocationXText), Y = Unit.FromDisplayString(LocationYText) },
                Size = new UnitPoint { X = Unit.FromDisplayString(SizeXText), Y = Unit.FromDisplayString(SizeYText) },
                Rotation = Rotation,
            },
            tell: true);
    }

    /// <summary>Original: <c>Quitar</c>.</summary>
    public void RemoveSelected()
    {
        if (SelectedSlot == null)
        {
            return;
        }

        int index = Slots.IndexOf(SelectedSlot);
        Slots.RemoveAt(index);
        Renumber();
        SelectedSlot = Slots.Count == 0 ? null : Slots[Math.Min(index, Slots.Count - 1)];
    }

    /// <summary>Original: <c>Boton_FlechaArriba_Click</c>.</summary>
    public void MoveUp() => MoveSelected(-1);

    /// <summary>Original: <c>Boton_FlechaAbajo_Click</c>.</summary>
    public void MoveDown() => MoveSelected(1);

    private void MoveSelected(int step)
    {
        TemplateSlotViewModel? slot = SelectedSlot;
        int index = slot == null ? -1 : Slots.IndexOf(slot);
        int target = index + step;
        if (index < 0 || target < 0 || target >= Slots.Count)
        {
            return;
        }

        Slots.Move(index, target);
        Renumber();
        SelectedSlot = slot;
    }

    private void Renumber()
    {
        for (int index = 0; index < Slots.Count; index++)
        {
            Slots[index].Order = "#" + (index + 1);
        }
    }

    partial void OnSelectedSlotChanged(TemplateSlotViewModel? value) => PreviewChanged?.Invoke(this, EventArgs.Empty);

    // ---- Files ----

    /// <summary>
    /// Saves the template shown: over the one chosen, or as a new one, which needs a name.
    /// Original: <c>Boton_Guardar_Click</c>.
    /// </summary>
    public void Save()
    {
        if (IsNew && Name.Length == 0)
        {
            Notice?.Invoke(StringResources.Get("faltan_datos"));
            return;
        }

        Write(new AdvancedTemplate
        {
            Name = Name.Length != 0 ? Name : SelectedTemplate.Text,
            FileName = SelectedTemplate.FileName,
        });
    }

    /// <summary>Saves what is shown as another template. Original: <c>Boton_Clonar_Click</c>.</summary>
    public void Clone() => Write(new AdvancedTemplate { Name = Name + "_Clone" });

    /// <summary>Original: <c>Guardar</c>.</summary>
    private void Write(AdvancedTemplate template)
    {
        if (Slots.Count == 0)
        {
            Notice?.Invoke(StringResources.Get("faltan_datos"));
            return;
        }

        template.Bleed = BleedText.Length == 0 ? Unit.Zero : Unit.FromDisplayString(BleedText);
        template.Paper.CopyFrom(Paper.Paper);
        string? problem = null;
        foreach (TemplateSlotViewModel slot in Slots)
        {
            template.AddCard(new AdvancedTemplateCard(slot.Card, slot.Card.Color));
            problem ??= ProblemWith(slot.Card);
        }

        template.Save();
        if (problem != null)
        {
            Notice?.Invoke(problem);
        }

        Reload(template.FileName);
        TemplatesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Brings a template file into the folder, as it is, and shows it.
    /// Original: <c>Boton_Importar_Click</c>, which gave the file read the paper and bleed of
    /// the window and added to it the cards on show.
    /// </summary>
    public void Import(string path)
    {
        var template = new AdvancedTemplate();
        AppError error = Read(path, ref template);
        if (error.HasError || template.Cards.Length == 0)
        {
            Notice?.Invoke(error.HasError ? error.Message : StringResources.Get("faltan_datos"));
            return;
        }

        if (template.Name.Length == 0)
        {
            template.Name = Path.GetFileNameWithoutExtension(path);
        }

        template.FileName = string.Empty;
        template.Save();
        Reload(template.FileName);
        TemplatesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The name offered for the copy of the template chosen, or null when there is none to export.</summary>
    public string? ExportName => IsNew ? null : (SelectedTemplate.Text.Length != 0 ? SelectedTemplate.Text : Path.GetFileNameWithoutExtension(SelectedTemplate.FileName));

    /// <summary>
    /// Copies the file of the template chosen. Original: <c>Boton_Exportar_Click</c>, which
    /// opened the file with the program the system had for it.
    /// </summary>
    public void Export(string path)
    {
        if (IsNew)
        {
            return;
        }

        try
        {
            File.Copy(FilePath(SelectedTemplate.FileName), path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Notice?.Invoke(exception.Message);
        }
    }

    /// <summary>Deletes the template chosen; the window asks first. Original: <c>Boton_Borrar_Click</c>.</summary>
    public void Delete()
    {
        if (IsNew)
        {
            return;
        }

        try
        {
            File.Delete(FilePath(SelectedTemplate.FileName));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Notice?.Invoke(exception.Message);
            return;
        }

        Reload(string.Empty);
        TemplatesChanged?.Invoke(this, EventArgs.Empty);
    }
}
