using System.Xml.Serialization;
using PnpTool.Core.Paper;
using PnpTool.Core.Primitives;
using PaperSheet = PnpTool.Core.Paper.Paper;

namespace PnpTool.Core.Templates;

/// <summary>
/// Advanced template as written by file version 1; only read, then upgraded.
/// Original: <c>PlantillaAvanzadaV1</c>.
/// </summary>
[XmlRoot("PlantillaAvanzada")]
[XmlType("PlantillaAvanzada")]
public class AdvancedTemplateV1
{
    [XmlArray("ListadoCartas")]
    [XmlArrayItem("PlantillaAvanzada_Carta")]
    public AdvancedTemplateCard[] Cards { get; set; } = [];

    [XmlElement("Nombre")]
    public string Name { get; set; } = string.Empty;

    [XmlElement("Horizontal")]
    public bool Landscape { get; set; }

    [XmlElement("TamanoPapel")]
    public UnitPoint PaperSize { get; set; } = new();

    [XmlElement("IndiceComboPapel")]
    public PaperType PaperType { get; set; } = PaperType.A4;

    [XmlElement("TamanoIgualListadoCartas")]
    public bool SameSizeForAllCards { get; set; }

    [XmlElement("Ruta")]
    public string FileName { get; set; } = string.Empty;

    [XmlElement("Sangrado")]
    public Unit Bleed { get; set; } = Unit.FromPoint(0);

    /// <summary>Converts to the current shape. Original: <c>Actualizar</c>.</summary>
    public AdvancedTemplate Upgrade() => new()
    {
        Cards = [.. Cards],
        Name = Name,
        FileName = FileName,
        Bleed = Bleed,
        Paper = new PaperSheet
        {
            Landscape = Landscape,
            Type = PaperType,
            Size = PaperSize,
        },
    };
}
