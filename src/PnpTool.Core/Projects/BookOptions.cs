using PnpTool.Core.Infrastructure;

namespace PnpTool.Core.Projects;

/// <summary>Where the images go on the pages of a book, as a list shows it. Original: <c>ComboboxItemPosicionesImagenesLibro</c>.</summary>
public sealed record BookImagePositionOption(string Text, BookImagePlacement Value)
{
    public override string ToString() => Text;

    /// <summary>Original: <c>CargarPosicionesImagenesLibro</c>.</summary>
    public static BookImagePositionOption[] All() =>
    [
        new(StringResources.Get("ComboBox_PosicionesImagenesLibro_Centradas"), BookImagePlacement.Centered),
        new(StringResources.Get("ComboBox_PosicionesImagenesLibro_AlCentro"), BookImagePlacement.TowardsSpine),
    ];
}

/// <summary>The side of the spine as a list shows it. Original: <c>ComboboxItemTiposLomoLibro</c>.</summary>
public sealed record BookSpineOption(string Text, BookSpineType Value)
{
    public override string ToString() => Text;

    /// <summary>Original: <c>CargarTiposLomoLibro</c>.</summary>
    public static BookSpineOption[] All() =>
    [
        new(StringResources.Get("Form_Setup_libro_Combobox_Lomo_Izquierda"), BookSpineType.Left),
        new(StringResources.Get("Form_Setup_libro_Combobox_Lomo_Derecha"), BookSpineType.Right),
    ];
}

/// <summary>The edge the printer turns the sheet over, as a list shows it. Original: <c>ComboboxItemTiposImprimirPorBorde</c>.</summary>
public sealed record PrintFlipEdgeOption(string Text, PrintFlipEdge Value)
{
    public override string ToString() => Text;

    /// <summary>Original: <c>CargarTiposImprimirPorBorde</c>.</summary>
    public static PrintFlipEdgeOption[] All() =>
    [
        new(StringResources.Get("ComboboxItemTiposImprimirPorBorde_Largo"), PrintFlipEdge.Long),
        new(StringResources.Get("ComboboxItemTiposImprimirPorBorde_Corto"), PrintFlipEdge.Short),
    ];
}

/// <summary>A kind of marks for the holes of the binding, as a list shows it. Original: <c>ComboboxItemMarcasPerforacion</c>.</summary>
public sealed record PerforationMarkOption(string Text, PerforationMarkType Value)
{
    public override string ToString() => Text;

    /// <summary>Original: <c>CargarMarcasPerforacion</c>.</summary>
    public static PerforationMarkOption[] All() =>
        [.. Enum.GetValues<PerforationMarkType>().Select(type => new PerforationMarkOption(StringResources.ForEnum("ComboBox_MarcasPerforacion_", type), type))];
}
