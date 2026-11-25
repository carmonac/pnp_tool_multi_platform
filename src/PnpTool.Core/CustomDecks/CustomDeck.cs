using System.ComponentModel;
using System.Xml.Serialization;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;

namespace PnpTool.Core.CustomDecks;

/// <summary>A card of a custom deck. Original: <c>CartaPersonalizada</c>.</summary>
/// <remarks>The image-merging methods of the original live in the imaging library.</remarks>
[XmlType("CartaPersonalizada")]
public class CustomCard
{
    [XmlElement("NombreCartaEsp")]
    public string NameSpanish { get; set; } = string.Empty;

    [XmlElement("NombreCartaIng")]
    public string NameEnglish { get; set; } = string.Empty;

    [XmlElement("NumeroDeCopias")]
    public int Copies { get; set; } = 1;

    /// <summary>Image path, relative to the custom-decks folder for the bundled decks.</summary>
    [XmlElement("RutaImagen")]
    public string ImagePath { get; set; } = string.Empty;

    [XmlElement("RutaImagenFondo")]
    public string BackgroundImagePath { get; set; } = string.Empty;

    /// <summary>The generated image combining background and picture.</summary>
    [XmlElement("RutaImagenFusion")]
    public string MergedImagePath { get; set; } = string.Empty;

    /// <summary>Original: <c>Copiar</c>. As in the original, the merged image path is not copied.</summary>
    public void CopyFrom(CustomCard source)
    {
        NameSpanish = source.NameSpanish;
        NameEnglish = source.NameEnglish;
        Copies = source.Copies;
        ImagePath = source.ImagePath;
        BackgroundImagePath = source.BackgroundImagePath;
    }
}

/// <summary>A ready-made or user-made deck (.pnpm). Original: <c>MazoPersonalizado</c>.</summary>
[XmlRoot("MazoPersonalizado")]
[XmlType("MazoPersonalizado")]
public class CustomDeck
{
    /// <summary>Original: the public field <c>ListadoCartas</c>.</summary>
    [XmlArray("ListadoCartas")]
    [XmlArrayItem("CartaPersonalizada")]
    public CustomCard[] Cards = [];

    [XmlElement("NombreMazo")]
    public string? Name { get; set; }

    [XmlElement("RutaFicheroMazo")]
    public string DeckFilePath { get; set; } = string.Empty;

    /// <summary>Style file (.pnps) to apply when the deck is loaded.</summary>
    [XmlElement("Estilo")]
    public string Style { get; set; } = string.Empty;

    [XmlElement("RutaTrasera")]
    public string BackImagePath { get; set; } = string.Empty;

    /// <summary>Appends a copy of the card. Original: <c>Anadir</c>.</summary>
    public void Add(CustomCard card)
    {
        int index = Cards.Length;
        Array.Resize(ref Cards, index + 1);
        Cards[index] = new CustomCard();
        Cards[index].CopyFrom(card);
    }
}

/// <summary>A text placed on sports cards. Original: <c>DatosTexto</c>.</summary>
[XmlType("DatosTexto")]
public class TextData
{
    [XmlElement("Texto")]
    public string Text { get; set; } = string.Empty;

    public string Color { get; set; } = AppSettings.ColorBlack;

    /// <summary>Position as a fraction (0–1) of the card's width and height.</summary>
    [XmlElement("Posicion")]
    public PointD Position { get; set; } = new();

    [XmlIgnore]
    public FontSpec Font { get; set; } = new();

    /// <summary>The font in its file form; see <see cref="FontSpec.ToSerializedString"/>.</summary>
    [Browsable(false)]
    public string FontSerialize
    {
        get => Font.ToSerializedString();
        set => Font = FontSpec.FromSerializedString(value);
    }
}

/// <summary>A deck of sports cards (team and player texts over a template). Original: <c>MazoDeportivo</c>.</summary>
[XmlRoot("MazoDeportivo")]
[XmlType("MazoDeportivo")]
public class SportsDeck : CustomDeck
{
    [XmlElement("DatosEquipo")]
    public TextData TeamText { get; set; } = new();

    [XmlElement("DatosJugador")]
    public TextData PlayerText { get; set; } = new();

    [XmlElement("RutaImagenPlantilla")]
    public string TemplateImagePath { get; set; } = string.Empty;

    [XmlElement("VisibleDatosEquipo")]
    public bool TeamTextVisible { get; set; } = true;
}
