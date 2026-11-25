using System.Xml.Serialization;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;

namespace PnpTool.Core.Projects;

/// <summary>Layout options of the front or the back faces. Original: <c>ConfiguracionCara</c>.</summary>
[XmlType("ConfiguracionCara")]
public class FaceSettings
{
    [XmlElement("Tamano")]
    public UnitPoint Size { get; set; } = new();

    [XmlElement("Efecto")]
    public ImageEffectType Effect { get; set; } = ImageEffectType.None;

    [XmlElement("EsquinasRedondeadasTamano")]
    public int RoundedCornerSize { get; set; }

    [XmlElement("PintarFondo")]
    public bool PaintBackground { get; set; }

    [XmlElement("Efecto_FondoCarta")]
    public CardBackgroundType BackgroundEffect { get; set; } = CardBackgroundType.SolidColor;

    [XmlElement("ColorRellenoFondo")]
    public string BackgroundFillColor { get; set; } = AppSettings.ColorWhite;

    [XmlElement("MostrarMarcoExterior")]
    public bool ShowOuterFrame { get; set; }

    [XmlElement("ColorMostrarMarcoExterior")]
    public string OuterFrameColor { get; set; } = AppSettings.ColorWhite;

    [XmlElement("Efecto_MarcoExterior")]
    public OuterFrameEffect OuterFrameEffect { get; set; } = OuterFrameEffect.SolidColor;

    [XmlElement("TamanoMarcoExterior")]
    public Unit OuterFrameSize { get; set; } = Unit.Zero;

    /// <summary>Keeps width and height in proportion when one is edited.</summary>
    [XmlElement("Candado")]
    public bool Locked { get; set; }

    [XmlElement("LineaDeCorte")]
    public CutLineSettings CutLine { get; set; } = new();

    /// <summary>Original: <c>Copiar</c>.</summary>
    public void CopyFrom(FaceSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Size = new UnitPoint(source.Size);
        Effect = source.Effect;
        RoundedCornerSize = source.RoundedCornerSize;
        PaintBackground = source.PaintBackground;
        BackgroundEffect = source.BackgroundEffect;
        BackgroundFillColor = source.BackgroundFillColor;
        ShowOuterFrame = source.ShowOuterFrame;
        OuterFrameColor = source.OuterFrameColor;
        OuterFrameSize = source.OuterFrameSize;
        OuterFrameEffect = source.OuterFrameEffect;
        CutLine.CopyFrom(source.CutLine);
        Locked = source.Locked;
    }

    /// <summary>Original: <c>EsIgual</c>.</summary>
    public bool IsEqualTo(FaceSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Size.X.EqualsInMillimeters(other.Size.X)
            && Size.Y.EqualsInMillimeters(other.Size.Y)
            && Effect == other.Effect
            && BackgroundEffect == other.BackgroundEffect
            && RoundedCornerSize == other.RoundedCornerSize
            && PaintBackground == other.PaintBackground
            && BackgroundFillColor == other.BackgroundFillColor
            && ShowOuterFrame == other.ShowOuterFrame
            && OuterFrameColor == other.OuterFrameColor
            && OuterFrameSize.Point == other.OuterFrameSize.Point
            && OuterFrameEffect == other.OuterFrameEffect
            && CutLine.IsEqualTo(other.CutLine)
            && Locked == other.Locked;
    }
}
