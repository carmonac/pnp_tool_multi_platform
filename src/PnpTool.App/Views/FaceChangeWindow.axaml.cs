using Avalonia.Controls;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App.Views;

/// <summary>
/// Asks what to do with the images when the cards change between showing one face and
/// showing two: take them as fronts, as backs, as fronts and backs in turn, or (towards two
/// faces only) repeat each front as its back. It closes with the choice, with
/// <see cref="FaceChangeChoice.Cancel"/> when cancelled, or with nothing when closed some
/// other way. Original: <c>Window_PreguntarCambioCara</c>.
/// </summary>
public partial class FaceChangeWindow : Window
{
    public FaceChangeWindow()
        : this(true)
    {
    }

    /// <param name="toDouble">Whether the cards are going to show two faces.</param>
    public FaceChangeWindow(bool toDouble)
    {
        InitializeComponent();
        Title = StringResources.Get("Form_PreguntarCambioCara");
        Heading.Text = StringResources.Get("Form_PreguntarCambioCara_GroupBox");
        FrontsOption.Content = StringResources.Get("Form_PreguntarCambioCara_RadioButton_Frontales");
        BacksOption.Content = StringResources.Get("Form_PreguntarCambioCara_RadioButton_Trasera");
        BothOption.Content = StringResources.Get("Form_PreguntarCambioCara_RadioButton_Ambas");
        MatchOption.Content = StringResources.Get("Form_PreguntarCambioCara_RadioButton_FrontalesIgualTrasera");
        MatchOption.IsVisible = toDouble;
    }

    /// <summary>The option chosen in the window.</summary>
    public FaceChangeChoice Choice =>
        BacksOption.IsChecked == true ? FaceChangeChoice.Backs
        : BothOption.IsChecked == true ? FaceChangeChoice.Group
        : MatchOption.IsChecked == true ? FaceChangeChoice.Match
        : FaceChangeChoice.Fronts;

    /// <summary>Accepts the option chosen, as the accept button does.</summary>
    public void Accept() => Close((FaceChangeChoice?)Choice);

    private void OnAccepted(object? sender, EventArgs e) => Accept();

    private void OnCancelled(object? sender, EventArgs e) => Close((FaceChangeChoice?)FaceChangeChoice.Cancel);
}
