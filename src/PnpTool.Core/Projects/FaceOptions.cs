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
