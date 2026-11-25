using PnpTool.Core.Infrastructure;

namespace PnpTool.Core.Projects;

/// <summary>An entry of the list of cut-line styles. Original: <c>ComboboxItemTiposLineasCorte</c>.</summary>
public sealed record CutLineTypeOption(string Text, CutLineType Value)
{
    public override string ToString() => Text;

    /// <summary>Every style. Original: <c>CargarTiposLineasCorte</c>.</summary>
    public static CutLineTypeOption[] All() =>
    [
        new(StringResources.Get("LineasDeCorte_SinLinea"), CutLineType.None),
        new(StringResources.Get("LineasDeCorte_Normal"), CutLineType.Normal),
        new(StringResources.Get("LineasDeCorte_Cruces"), CutLineType.Cross),
        new(StringResources.Get("LineasDeCorte_Completa"), CutLineType.Full),
        new("Mix", CutLineType.Mixed),
        new(StringResources.Get("LineasDeCorte_Punto"), CutLineType.Dot),
    ];

    /// <summary>The two styles advanced templates offer. Original: <c>CargarTiposLineasCorte_Simple</c>.</summary>
    public static CutLineTypeOption[] Simple() =>
    [
        new(StringResources.Get("LineasDeCorte_SinLinea"), CutLineType.None),
        new(StringResources.Get("LineasDeCorte_Cruces"), CutLineType.Cross),
    ];
}
