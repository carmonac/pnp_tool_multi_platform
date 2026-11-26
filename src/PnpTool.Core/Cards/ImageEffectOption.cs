using PnpTool.Core.Infrastructure;

namespace PnpTool.Core.Cards;

/// <summary>An effect for images as a list shows it. Original: <c>ComboboxItemEfectosImagenes</c>.</summary>
public sealed record ImageEffectOption(string Text, ImageEffectType Value)
{
    public override string ToString() => Text;

    /// <summary>
    /// The effects offered, all but the outline, which no list shows; the mirror is left out
    /// where there is no card for it to fill. Original: <c>CargarEfectos</c> and the filter of
    /// <c>Control_Efecto.CargaComboBox</c>.
    /// </summary>
    public static ImageEffectOption[] All(bool withMirror) =>
    [
        .. Enum.GetValues<ImageEffectType>()
            .Where(effect => effect != ImageEffectType.CreateOutline && (withMirror || effect != ImageEffectType.Mirror))
            .Select(effect => new ImageEffectOption(StringResources.ForEnum("ComboBox_EfectosImagenes_", effect), effect)),
    ];
}
