using System.Xml.Serialization;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PaperSheet = PnpTool.Core.Paper.Paper;

namespace PnpTool.Core.Templates;

/// <summary>One card slot of an advanced template. Original: <c>PlantillaAvanzadaCarta</c>.</summary>
[XmlType("PlantillaAvanzada_Carta")]
public class AdvancedTemplateCard
{
    [XmlElement("Ubicacion")]
    public UnitPoint Location { get; set; } = new();

    [XmlElement("Tamano")]
    public UnitPoint Size { get; set; } = new();

    [XmlElement("Rotacion")]
    public int Rotation { get; set; }

    /// <summary>Colour of the slot in the editor, as "#RRGGBB" or "#AARRGGBB".</summary>
    public string Color { get; set; } = "#FFFFFF";

    public AdvancedTemplateCard()
    {
    }

    public AdvancedTemplateCard(AdvancedTemplateCard source, string color)
    {
        Location = new UnitPoint(source.Location);
        Size = new UnitPoint(source.Size);
        Rotation = source.Rotation;
        Color = color;
    }
}

/// <summary>
/// A free layout where every card slot has its own position, size and rotation (.pnpa).
/// Original: <c>PlantillaAvanzada</c>.
/// </summary>
[XmlRoot("PlantillaAvanzada")]
[XmlType("PlantillaAvanzada")]
public class AdvancedTemplate
{
    /// <summary>Original: the public field <c>ListadoCartas</c>.</summary>
    [XmlArray("ListadoCartas")]
    [XmlArrayItem("PlantillaAvanzada_Carta")]
    public AdvancedTemplateCard[] Cards = [];

    [XmlElement("Nombre")]
    public string Name { get; set; } = string.Empty;

    [XmlElement("Papel")]
    public PaperSheet Paper { get; set; } = new();

    /// <summary>File name inside the advanced-styles folder, for example "1.pnpa".</summary>
    [XmlElement("Ruta")]
    public string FileName { get; set; } = string.Empty;

    [XmlElement("Sangrado")]
    public Unit Bleed { get; set; } = Unit.FromPoint(0);

    /// <summary>
    /// Loads a template file into the project and adjusts the project settings to it.
    /// Original: the static <c>AbrirPlantillaAvanzada(string, ref Proyecto)</c>.
    /// </summary>
    public static AppError LoadInto(string file, Project project)
    {
        AdvancedTemplate template = project.AdvancedTemplate;
        AppError error = ProjectFileStore.LoadAdvancedTemplate(file, ref template);
        ProjectSettings settings = project.Settings;
        settings.PrintLayout.Paper.CopyFrom(template.Paper);
        settings.Group.Group = SettingsGroup.AdvancedStyles;
        settings.Group.ProjectType = ProjectType.AdvancedStyles;
        settings.PrintLayout.Columns = template.Cards.Length;
        settings.PrintLayout.Rows = 1;
        settings.Grid.Bleed = template.Bleed;
        project.AdvancedTemplate = template.Clone();
        return error;
    }

    /// <summary>Original: <c>AnadirCarta</c>.</summary>
    public void AddCard(AdvancedTemplateCard card)
    {
        int length = Cards.Length;
        Array.Resize(ref Cards, length + 1);
        Cards[length] = card;
    }

    /// <summary>Deep copy through XML, as the original does. Original: <c>Clonar</c>.</summary>
    public AdvancedTemplate Clone()
    {
        var serializer = new XmlSerializer(typeof(AdvancedTemplate));
        using var stream = new MemoryStream();
        serializer.Serialize(stream, this);
        stream.Position = 0L;
        return serializer.Deserialize(stream) as AdvancedTemplate ?? new AdvancedTemplate();
    }

    /// <summary>
    /// Saves into the advanced-styles folder, picking the first free "N.pnpa" name for a new
    /// template. Original: <c>GuardaPlantilla</c>.
    /// </summary>
    public void Save()
    {
        if (FileName == string.Empty)
        {
            int index = 0;
            do
            {
                index++;
            }
            while (File.Exists(AppSettings.AdvancedStylesFolder + index + ".pnpa"));
            FileName = index + ".pnpa";
        }

        ProjectFileStore.SaveAdvancedTemplate(this);
    }
}
