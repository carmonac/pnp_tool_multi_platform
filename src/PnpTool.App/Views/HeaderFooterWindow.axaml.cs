using Avalonia.Controls;
using Avalonia.Interactivity;
using PnpTool.App.Services;
using PnpTool.App.ViewModels;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Projects;

namespace PnpTool.App.Views;

/// <summary>
/// The colour bar, registration marks, signature, page number and watermark of the sheets.
/// It closes with a <see cref="HeaderFooterResult"/> when accepted and with nothing when
/// cancelled. Original: <c>WindowSetupEncabezado</c>.
/// </summary>
public partial class HeaderFooterWindow : Window
{
    public HeaderFooterWindow()
        : this(new HeaderFooterViewModel(new HeaderFooter(), new Watermark()))
    {
    }

    public HeaderFooterWindow(HeaderFooterViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Model = model;
        Title = StringResources.Get("Boton_Configurar_EncabezadoPie");
        ColorBarCheck.Content = StringResources.Get("Form_Setup_Encabezado_CheckImagenCalibracion");
        MarksCheck.Content = StringResources.Get("Form_Setup_Encabezado_CheckMarcaCalibracion");
        SignatureLabel.Text = StringResources.Get("Form_Setup_Encabezado_LabelFirma");
        PageNumberCheck.Content = StringResources.Get("Form_Setup_Encabezado_CheckMostrarNumeroHoja");
        FirstPageLabel.Text = StringResources.Get("Form_Setup_Encabezado_LabelNumeroHojaIniciar");
        WatermarkLabel.Text = StringResources.Get("Form_Setup_Encabezado_LabelMarcaAgua");

        // The original's tooltips gave the unit of lengths for the colour bar, which is in points.
        string unit = "(" + LayoutViewModel.UnitName + ")";
        foreach (Control control in new Control[] { ColorBarWidthIcon, ColorBarWidthBox })
        {
            ToolTip.SetTip(control, StringResources.Get("Ancho") + " (pt)");
        }

        foreach (Control control in new Control[] { ColorBarHeightIcon, ColorBarHeightBox })
        {
            ToolTip.SetTip(control, StringResources.Get("Alto") + " (pt)");
        }

        ToolTip.SetTip(MarkSizeIcon, "(pt)");
        ToolTip.SetTip(MarkSizeBox, "(pt)");
        ToolTip.SetTip(MarkXIcon, "X " + unit);
        ToolTip.SetTip(MarkXBox, "X " + unit);
        ToolTip.SetTip(MarkYIcon, "Y " + unit);
        ToolTip.SetTip(MarkYBox, "Y " + unit);

        FirstPageBox.SetRange(int.MaxValue, 0, 1);
        FirstPageBox.Value = model.FirstPageNumber;
        FirstPageBox.ValueChanged += (_, _) => model.FirstPageNumber = FirstPageBox.Value;
        model.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(HeaderFooterViewModel.FirstPageNumber) && FirstPageBox.Value != model.FirstPageNumber)
            {
                FirstPageBox.Value = model.FirstPageNumber;
            }
        };
    }

    public HeaderFooterViewModel Model { get; }

    /// <summary>Asks for the image of the watermark; the test of the window replaces it. Gives null when cancelled.</summary>
    public Func<Task<string?>>? PickImage { get; set; }

    /// <summary>Accepts the settings, as the accept button does.</summary>
    public void Accept() => Close(Model.Result());

    /// <summary>Chooses the image of the watermark, as its button does. Original: <c>BuscarCarpeta</c> for images.</summary>
    public async Task PickWatermarkAsync()
    {
        string? path = PickImage != null
            ? await PickImage()
            : (await ImageFiles.PickAsync(this, multiple: false)).FirstOrDefault();
        if (!string.IsNullOrEmpty(path))
        {
            Model.WatermarkPath = path;
        }
    }

    private void OnAccepted(object? sender, EventArgs e) => Accept();

    private void OnCancelled(object? sender, EventArgs e) => Close(null);

    private async void OnPickWatermark(object? sender, RoutedEventArgs e) => await PickWatermarkAsync();
}
