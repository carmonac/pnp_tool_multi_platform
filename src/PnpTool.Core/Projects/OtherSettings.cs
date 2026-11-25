using System.Xml.Serialization;
using ImageMagick;
using PnpTool.Core.Cards;

namespace PnpTool.Core.Projects;

/// <summary>Original: <c>ConfiguracionOtrosParametrosVActual.TiposRutasImagenes</c>.</summary>
[XmlType("TiposRutasImagenes")]
public enum ImagePathMode
{
    [XmlEnum("Relativas")] Relative,
    [XmlEnum("Absolutas")] Absolute,
}

/// <summary>Original: <c>ConfiguracionOtrosParametrosVActual.TipoSalidaPdf</c>.</summary>
[XmlType("TipoSalidaPdf")]
public enum PdfOutputMode
{
    /// <summary>Fronts and backs in separate files.</summary>
    [XmlEnum("Separados")] Separate,

    /// <summary>Split into files of a maximum size.</summary>
    [XmlEnum("TamanoMaximo")] MaximumSize,

    /// <summary>One file.</summary>
    [XmlEnum("Solo1")] SingleFile,
}

/// <summary>Original: <c>ConfiguracionOtrosParametrosVActual.TiposCompresion</c>.</summary>
[XmlType("TiposCompresion")]
public enum CompressionType
{
    [XmlEnum("SinComprimir")] None,
    [XmlEnum("Rapida")] Fast,
    [XmlEnum("ImagenAtamano")] ImageToSize,
    [XmlEnum("Porcentaje")] Percentage,
}

/// <summary>Original: <c>ConfiguracionOtrosParametrosVActual.ColorspaceProyecto</c>.</summary>
[XmlType("ColorspaceProyecto")]
public enum ProjectColorSpace
{
    RGB,
    CMYK,
}

/// <summary>Original: <c>MaquetacionApilada</c>.</summary>
[XmlType("MaquetacionApilada")]
public sealed class StackedLayout
{
    [XmlElement("TipoMaquetacion")]
    public LayoutType LayoutType { get; set; } = LayoutType.Standard;

    [XmlElement("TamanoBloque")]
    public int BlockSize { get; set; } = 1;

    public StackedLayout()
    {
    }

    public StackedLayout(int blockSize, LayoutType layoutType)
    {
        BlockSize = blockSize;
        LayoutType = layoutType;
    }

    /// <summary>Original: <c>Copiar</c>.</summary>
    public void CopyFrom(StackedLayout source)
    {
        BlockSize = source.BlockSize;
        LayoutType = source.LayoutType;
    }

    public bool IsEqualTo(StackedLayout other) => BlockSize == other.BlockSize && LayoutType == other.LayoutType;
}

/// <summary>
/// Output and colour options of a project. Original: <c>ConfiguracionOtrosParametrosVActual</c>.
/// </summary>
[XmlType("Configuracion_OtrosParametrosVActual")]
public sealed class OtherSettings
{
    [XmlElement("TipoRutaImagenes")]
    public ImagePathMode ImagePathMode { get; set; } = ImagePathMode.Relative;

    [XmlElement("PdfPorSeparados")]
    public PdfOutputMode PdfOutput { get; set; } = PdfOutputMode.SingleFile;

    /// <summary>Maximum file size in megabytes for <see cref="PdfOutputMode.MaximumSize"/>.</summary>
    [XmlElement("PdfPorSeparadosTamano")]
    public int PdfMaximumSize { get; set; }

    [XmlElement("GenerarIDFichero")]
    public bool GenerateFileId { get; set; }

    [XmlElement("IDFichero_Semilla")]
    public string FileIdSeed { get; set; } = string.Empty;

    [XmlElement("ComprimirArchivo")]
    public CompressionType Compression { get; set; } = CompressionType.None;

    [XmlElement("porcentajecompresion")]
    public double CompressionPercent { get; set; } = 100.0;

    [XmlElement("Colorspace")]
    public ProjectColorSpace ColorSpace { get; set; } = ProjectColorSpace.RGB;

    /// <summary>
    /// The loaded ICC profile, or null when <see cref="ColorProfilePath"/> does not point to a file.
    /// Original: <c>PerfilDeColor</c>, which was never null because the program always had a default.
    /// </summary>
    [XmlIgnore]
    public ColorProfile? ColorProfile { get; set; }

    /// <summary>Original: <c>sPefilDeColor</c>.</summary>
    [XmlElement("sPefilDeColor")]
    public string ColorProfilePath { get; set; }

    [XmlElement("MaquetarApilado")]
    public StackedLayout StackedLayout { get; set; } = new();

    /// <summary>Resampling filter for resizing images.</summary>
    [XmlElement("FiltroResizeMagick")]
    public FilterType ResizeFilter { get; set; } = FilterType.Lanczos;

    public OtherSettings()
    {
        ColorProfilePath = IccProfiles.PathOrDefault(null, ColorSpace);
        ColorProfile = IccProfiles.TryLoad(ColorProfilePath);
    }

    /// <summary>Original: <c>Copiar</c>.</summary>
    public void CopyFrom(OtherSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ImagePathMode = source.ImagePathMode;
        PdfOutput = source.PdfOutput;
        PdfMaximumSize = source.PdfMaximumSize;
        GenerateFileId = source.GenerateFileId;
        FileIdSeed = source.FileIdSeed;
        Compression = source.Compression;
        CompressionPercent = source.CompressionPercent;
        ColorSpace = source.ColorSpace;
        ColorProfile = IccProfiles.Copy(source.ColorProfile);
        ColorProfilePath = IccProfiles.PathOrDefault(source.ColorProfilePath, ColorSpace);
        StackedLayout.CopyFrom(source.StackedLayout);
        ResizeFilter = source.ResizeFilter;
    }

    /// <summary>Original: <c>Equals(ConfiguracionOtrosParametrosVActual)</c>.</summary>
    public bool IsEqualTo(OtherSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return ImagePathMode == other.ImagePathMode
            && PdfOutput == other.PdfOutput
            && PdfMaximumSize == other.PdfMaximumSize
            && GenerateFileId == other.GenerateFileId
            && FileIdSeed == other.FileIdSeed
            && Compression == other.Compression
            && CompressionPercent == other.CompressionPercent
            && ColorSpace == other.ColorSpace
            && ColorProfile?.Description == other.ColorProfile?.Description
            && ColorProfilePath == other.ColorProfilePath
            && StackedLayout.IsEqualTo(other.StackedLayout)
            && ResizeFilter == other.ResizeFilter;
    }
}
