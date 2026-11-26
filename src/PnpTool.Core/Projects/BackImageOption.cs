using PnpTool.Core.Infrastructure;

namespace PnpTool.Core.Projects;

/// <summary>A way of giving the cards their backs, as the layout window lists it. Original: <c>ComboboxItemImagenTrasera</c>.</summary>
public sealed record BackImageOption(string Text, BackImageMode Value)
{
    public override string ToString() => Text;

    /// <summary>
    /// The ways offered: for double-sided printing, the same back for all, a back per card,
    /// one back only and a pattern (paper miniatures: only the first two); for gutterfold,
    /// the same back or a back per card; for one side, no back. Advanced templates are
    /// offered the four ways of double-sided printing, or none.
    /// Original: <c>CargarTiposImagenesTraseras</c> and <c>CargarTiposImagenesTraseras_Simple</c>.
    /// </summary>
    public static BackImageOption[] For(bool doubleSided, bool gutterfold, ProjectType projectType, SettingsGroup group)
    {
        BackImageMode[] modes;
        if (group == SettingsGroup.AdvancedStyles)
        {
            modes = doubleSided
                ? [BackImageMode.SameBack, BackImageMode.DifferentBack, BackImageMode.OnlyOne, BackImageMode.Pattern]
                : [BackImageMode.NoBack];
        }
        else if (doubleSided)
        {
            modes = projectType == ProjectType.Papermini
                ? [BackImageMode.SameBack, BackImageMode.DifferentBack]
                : [BackImageMode.SameBack, BackImageMode.DifferentBack, BackImageMode.OnlyOne, BackImageMode.Pattern];
        }
        else if (gutterfold)
        {
            modes = [BackImageMode.SameBack, BackImageMode.DifferentBack];
        }
        else
        {
            modes = [BackImageMode.NoBack];
        }

        return [.. modes.Select(mode => new BackImageOption(StringResources.ForEnum("ComboBox_ImagenTrasera_", mode), mode))];
    }
}
