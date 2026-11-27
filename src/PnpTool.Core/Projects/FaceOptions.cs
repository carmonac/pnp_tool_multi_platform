using PnpTool.Core.Infrastructure;

namespace PnpTool.Core.Projects;

/// <summary>A kind of background behind the cards as a list shows it. Original: <c>ComboboxItemTiposFondoCarta</c>.</summary>
public sealed record CardBackgroundOption(string Text, CardBackgroundType Value)
{
    public override string ToString() => Text;
}

/// <summary>A kind of outer frame as a list shows it. Original: <c>ComboboxItemEfectoMarcoExterior</c>.</summary>
public sealed record OuterFrameEffectOption(string Text, OuterFrameEffect Value)
{
    public override string ToString() => Text;
}

/// <summary>A place for the page number as a list shows it. Original: <c>ComboboxItemPosicionNumeroHoja</c>.</summary>
public sealed record PageNumberPositionOption(string Text, PageNumberPosition Value)
{
    public override string ToString() => Text;

    /// <summary>Original: <c>CargarPosicionesHoja</c>.</summary>
    public static PageNumberPositionOption[] All() =>
    [
        new(StringResources.Get("PosicionNumeroPaginaProyecto_Interior"), PageNumberPosition.Left),
        new(StringResources.Get("PosicionNumeroPaginaProyecto_Centrado"), PageNumberPosition.Center),
        new(StringResources.Get("PosicionNumeroPaginaProyecto_Exterior"), PageNumberPosition.Right),
    ];
}

/// <summary>A kind of watermark as a list shows it. Original: <c>ComboboxItemTiposMarcaAgua</c>.</summary>
public sealed record WatermarkTypeOption(string Text, WatermarkType Value)
{
    public override string ToString() => Text;

    /// <summary>Original: <c>CargarTiposMarcaAgua</c>.</summary>
    public static WatermarkTypeOption[] All() =>
    [
        new(StringResources.Get("MarcaDeAgua_Sinmarca"), WatermarkType.None),
        new(StringResources.Get("MarcaDeAgua_EnCadaImagen"), WatermarkType.OnImage),
        new(StringResources.Get("MarcaDeAgua_EnCadaPagina"), WatermarkType.OnPage),
    ];
}
