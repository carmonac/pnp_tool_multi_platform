using ImageMagick;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;

namespace PnpTool.Pdf.Layout;

/// <summary>
/// One cell of the PDF: the image (or colour, or nothing) to place there and how to turn it.
/// The cells of a document form a flat list in the order they are laid on the sheets.
/// Original: <c>ListadoImagenes</c>.
/// </summary>
public sealed class PdfImage
{
    /// <summary>Original: <c>TipoImagen</c>.</summary>
    public ImageKind Kind { get; set; }

    /// <summary>File of the image, or the colour text for a blank cell. Original: <c>Ruta</c>.</summary>
    public string Path { get; set; }

    /// <summary>Original: <c>ListadoImagenes_PropiedadesRotacion</c>.</summary>
    public RotationProperties Rotation { get; set; }

    /// <summary>Original: <c>Es_CMYK</c>.</summary>
    public bool IsCmyk { get; set; }

    /// <summary>
    /// The cell shows the same image, turned the same way, as the cell before it, so the PDF
    /// can reuse the image instead of storing it again. Original: <c>Es_Misma_Que_Anterior</c>.
    /// </summary>
    public bool SameAsPrevious { get; set; }

    /// <summary>Original: <c>PerfilColor</c>.</summary>
    public IColorProfile? ColorProfile { get; set; }

    /// <summary>Colour behind the image, as "A_R_G_B", or empty. Original: <c>ColorFondo</c>.</summary>
    public string BackgroundColor { get; set; }

    /// <summary>
    /// A cell from its parts. An image whose file does not exist becomes a blank cell, keeping
    /// the path as given.
    /// </summary>
    public PdfImage(ImageKind kind, string path, int angle, bool flipHorizontal, bool flipVertical, bool isCmyk)
    {
        Kind = !File.Exists(path) && kind != ImageKind.Empty ? ImageKind.Blank : kind;
        Path = path;
        Rotation = new RotationProperties { Angle = angle, FlipHorizontal = flipHorizontal, FlipVertical = flipVertical };
        IsCmyk = isCmyk;
        ColorProfile = null;
        SameAsPrevious = false;
        BackgroundColor = string.Empty;
    }

    /// <summary>A cell for a card face. An image whose file does not exist becomes a white cell.</summary>
    public PdfImage(CardFace face)
    {
        Kind = face.ImageKind;
        Path = face.Path;
        if (face.ImageKind == ImageKind.Normal && !File.Exists(face.Path))
        {
            Kind = ImageKind.Blank;
            Path = AppSettings.ColorWhite;
        }

        Rotation = new RotationProperties();
        Rotation.CopyFrom(face.Rotation);
        IsCmyk = face.IsCmyk;
        ColorProfile = face.ColorProfile;
        SameAsPrevious = false;
        BackgroundColor = face.BackgroundColor;
    }

    /// <summary>An empty cell.</summary>
    public static PdfImage Empty() => new(ImageKind.Empty, string.Empty, 0, flipHorizontal: false, flipVertical: false, isCmyk: false);

    /// <summary>Same file turned the same way. Original: <c>EsIgual</c>.</summary>
    public bool IsSameAs(PdfImage? other) =>
        other != null && Path == other.Path && Rotation.IsEqualTo(other.Rotation);
}
