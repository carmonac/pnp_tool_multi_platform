using PnpTool.Core.Infrastructure;

namespace PnpTool.Core.Projects;

/// <summary>How the images are written in project files, as a list shows it. Original: <c>ComboboxItemRutaImagenes</c>.</summary>
public sealed record ImagePathOption(string Text, ImagePathMode Value)
{
    public override string ToString() => Text;

    /// <summary>Original: <c>CargarTiposRutasImagenes</c>.</summary>
    public static ImagePathOption[] All() =>
        [.. Enum.GetValues<ImagePathMode>().Select(mode => new ImagePathOption(StringResources.ForEnum("ComboBox_TiposRutasImagenes_", mode), mode))];
}

/// <summary>A way of compressing the PDF as a list shows it. Original: <c>ComboboxItemComprimir</c>.</summary>
public sealed record CompressionOption(string Text, CompressionType Value)
{
    public override string ToString() => Text;

    /// <summary>Original: <c>CargarTiposCompresion</c>.</summary>
    public static CompressionOption[] All() =>
        [.. Enum.GetValues<CompressionType>().Select(type => new CompressionOption(StringResources.ForEnum("ComboBox_Comprimir_", type), type))];
}

/// <summary>A colour space as a list shows it. Original: <c>ComboboxItemColorSpacePdf</c>.</summary>
public sealed record PdfColorSpaceOption(string Text, ProjectColorSpace Value)
{
    public override string ToString() => Text;

    /// <summary>Original: <c>CargarTiposColorSpace</c>.</summary>
    public static PdfColorSpaceOption[] All() =>
        [.. Enum.GetValues<ProjectColorSpace>().Select(space => new PdfColorSpaceOption(space.ToString(), space))];
}

/// <summary>What files the PDF comes out as, as a list shows it. Original: <c>ComboboxItemFicheroPdfSalida</c>.</summary>
public sealed record PdfOutputFileOption(string Text, PdfOutputMode Value)
{
    public override string ToString() => Text;

    /// <summary>Original: <c>CargarTiposFinalizacionPDF</c>.</summary>
    public static PdfOutputFileOption[] All() =>
        [.. Enum.GetValues<PdfOutputMode>().Select(mode => new PdfOutputFileOption(StringResources.ForEnum("ComboBox_FicheroPDFSalida_", mode), mode))];
}

/// <summary>An order of the cards on the sheets as a list shows it. Original: <c>ComboboxItemTiposMaquetacionApilada</c>.</summary>
public sealed record StackedLayoutOption(string Text, LayoutType Value)
{
    public override string ToString() => Text;

    /// <summary>
    /// The standard order and the stacked one, as in the original, which had no entry for
    /// the order by rows; that one is listed, under its own name, only for a project that
    /// already has it, so that opening the window does not lose it.
    /// Original: <c>CargarTiposTiposMaquetacion</c>.
    /// </summary>
    public static StackedLayoutOption[] For(LayoutType current)
    {
        StackedLayoutOption[] usual =
        [
            new(StringResources.Get("Form_Setup_Otros_ComboBox_MaquetacionApilada_Standar"), LayoutType.Standard),
            new(StringResources.Get("Form_Setup_Otros_ComboBox_MaquetacionApilada_Apilada"), LayoutType.Stacked),
        ];
        return current == LayoutType.ByRows ? [.. usual, new StackedLayoutOption("PorFilas", LayoutType.ByRows)] : usual;
    }
}
