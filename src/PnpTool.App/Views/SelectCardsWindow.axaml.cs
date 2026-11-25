using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App.Views;

/// <summary>What to select in the image panel.</summary>
/// <param name="From">First position, from 0.</param>
/// <param name="To">Position after the last.</param>
/// <param name="Cards">Whether to select the cards.</param>
/// <param name="Faces">Which faces to select; <see cref="FaceType.None"/> for none.</param>
public sealed record CardSelection(int From, int To, bool Cards, FaceType Faces);

/// <summary>
/// Asks which cards to select: a range of positions, and whether to select the cards, their
/// images (fronts, backs or both) or both things. It closes with the answer, or with nothing
/// when closed without accepting. Original: <c>Window_SeleccionarPanelesCaras</c>.
/// </summary>
public partial class SelectCardsWindow : Window
{
    private sealed record FaceOption(string Text, FaceType Face)
    {
        public override string ToString() => Text;
    }

    public SelectCardsWindow()
        : this(1)
    {
    }

    /// <param name="cardCount">How many cards the panel has.</param>
    public SelectCardsWindow(int cardCount)
    {
        InitializeComponent();
        Title = StringResources.Get("Form_SeleccionarPanelesCaras");
        FromLabel.Text = StringResources.Get("Form_SeleccionarPanelesCaras_Label_Desde");
        ToLabel.Text = StringResources.Get("Form_SeleccionarPanelesCaras_Label_Hasta");
        ImagesCheck.Content = StringResources.Get("Form_SeleccionarPanelesCaras_CheckBox_Imagenes");
        CardsCheck.Content = StringResources.Get("Form_SeleccionarPanelesCaras_CheckBox_Paneles");
        ToolTip.SetTip(AcceptButton, StringResources.Get("Form_SeleccionarPanelesCaras_Boton_Aceptar"));
        FromBox.Text = "1";
        ToBox.Text = cardCount.ToString();

        // Original: ComboboxItemTiposCaras.CargarTiposCara.
        FaceList.ItemsSource = new[]
        {
            new FaceOption(StringResources.Get("Ambas"), FaceType.Both),
            new FaceOption(StringResources.Get("Form_PreguntarCambioCara_RadioButton_Frontales"), FaceType.Front),
            new FaceOption(StringResources.Get("Form_PreguntarCambioCara_RadioButton_Trasera"), FaceType.Back),
        };
        FaceList.SelectedIndex = 0;
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close((CardSelection?)null);
            }
        };
    }

    /// <summary>
    /// Accepts what the window shows. The two ends of the range may come in either order.
    /// Original: <c>Boton_Aceptar_Click</c>.
    /// </summary>
    public void Accept()
    {
        int from = Math.Min(FromBox.Value, ToBox.Value);
        int to = Math.Max(FromBox.Value, ToBox.Value);
        FaceType faces = ImagesCheck.IsChecked == true ? ((FaceOption)FaceList.SelectedItem!).Face : FaceType.None;
        Close(new CardSelection(from - 1, to, CardsCheck.IsChecked == true, faces));
    }

    private void OnAccept(object? sender, RoutedEventArgs e) => Accept();
}
