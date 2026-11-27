using Avalonia.Controls;
using PnpTool.App.ViewModels;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App.Views;

/// <summary>
/// Asks, when the cards show two faces, how the images to add will be chosen: pairs of front
/// and back in one go, many fronts with one back, one front with many backs, or every front
/// with every back. Ticking "never ask again" keeps the answer for the next times. It closes
/// with the answer, or with nothing when cancelled.
/// Original: <c>Window_SubirImagenes</c>, which also showed the file dialogs itself.
/// </summary>
public partial class AddImagesWindow : Window
{
    public AddImagesWindow()
    {
        InitializeComponent();
        Title = StringResources.Get("ShellMenuBarItem_Imagenes_AnadirImagen");
        PairsOption.Content = StringResources.Get("Form_SubirImagenes_RadioButton_11");
        ManyFrontsOption.Content = StringResources.Get("Form_SubirImagenes_RadioButton_N1");
        ManyBacksOption.Content = StringResources.Get("Form_SubirImagenes_RadioButton_1N");
        ManyBothOption.Content = StringResources.Get("Form_SubirImagenes_RadioButton_NN");
        RememberOption.Content = StringResources.Get("Form_SubirImagenes_Check_nopreguntar");
    }

    /// <summary>The way chosen in the window.</summary>
    public AddImagesResult Choice =>
        ManyFrontsOption.IsChecked == true ? AddImagesResult.ManyFrontsOneBack
        : ManyBacksOption.IsChecked == true ? AddImagesResult.OneFrontManyBacks
        : ManyBothOption.IsChecked == true ? AddImagesResult.ManyFrontsManyBacks
        : AddImagesResult.OneFrontOneBack;

    /// <summary>Accepts what is chosen, as the accept button does.</summary>
    public void Accept() => Close(new AddImagesAnswer(Choice, RememberOption.IsChecked == true));

    private void OnAccepted(object? sender, EventArgs e) => Accept();

    private void OnCancelled(object? sender, EventArgs e) => Close(null);
}
