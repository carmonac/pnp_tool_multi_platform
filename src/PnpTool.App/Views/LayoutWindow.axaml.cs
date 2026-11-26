using Avalonia.Controls;
using Avalonia.Interactivity;
using PnpTool.App.ViewModels;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Projects;

namespace PnpTool.App.Views;

/// <summary>
/// The layout of the sheets: paper, sides and backs, card size, spacing, bleed, and rows and
/// columns, with a sketch of the sheet. It closes with a <see cref="LayoutResult"/> when
/// accepted and with nothing when cancelled. Original: <c>WindowSetupImpresion</c>.
/// </summary>
public partial class LayoutWindow : Window
{
    public LayoutWindow()
        : this(new LayoutViewModel(new ProjectSettings()))
    {
    }

    public LayoutWindow(LayoutViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Model = model;
        Title = StringResources.Get("Form_Setup_TabPage_Disposicion");
        PaperGroup.Header = StringResources.Get("Form_Setup_GroupBox_Papel");
        SidesGroup.Header = StringResources.Get("Form_Setup_RadioButton_ImpresionDobleCara");
        CardGroup.Header = StringResources.Get("Cartas");
        TableGroup.Header = StringResources.Get("Form_Setup_GroupBox_Tabla");
        SpacingXLabel.Text = StringResources.Get("Form_Setup_LabelSeparacionX");
        SpacingYLabel.Text = StringResources.Get("Form_Setup_LabelSeparacionY");
        BleedLabel.Text = StringResources.Get("Form_Setup_LabelSangrado");
        MaximumButton.Content = StringResources.Get("Form_Setup_BotonDatosEstadisticos_Max");
        MinimumButton.Content = StringResources.Get("Form_Setup_BotonDatosEstadisticos_Min");
        MeanButton.Content = StringResources.Get("Form_Setup_BotonDatosEstadisticos_Med");
        MedianButton.Content = StringResources.Get("Form_Setup_BotonDatosEstadisticos_Md");
        ToolTip.SetTip(PortraitButton, StringResources.Get("Form_Setup_Label_PapelVertical"));
        ToolTip.SetTip(LandscapeButton, StringResources.Get("Form_Setup_Label_PapelHorizontal"));
        ToolTip.SetTip(RatioButton, StringResources.Get("Bloquea_Ratio"));
        ToolTip.SetTip(LockSizeButton, StringResources.Get("Boton_BloquearTamano"));
        ToolTip.SetTip(ColumnsBox, StringResources.Get("Columnas"));
        ToolTip.SetTip(RowsBox, StringResources.Get("Filas"));
        foreach (Control box in new Control[] { PaperWidthBox, CardWidthBox })
        {
            ToolTip.SetTip(box, StringResources.Get("Ancho") + " (" + LayoutViewModel.UnitName + ")");
        }

        foreach (Control box in new Control[] { PaperHeightBox, CardHeightBox })
        {
            ToolTip.SetTip(box, StringResources.Get("Alto") + " (" + LayoutViewModel.UnitName + ")");
        }

        model.Notice += message => _ = new MessageWindow(message).ShowDialog(this);
    }

    public LayoutViewModel Model { get; }

    /// <summary>
    /// Asks to go on with a layout that does not fit the sheet; the test of the window
    /// replaces it. Gives true to accept.
    /// </summary>
    public Func<string, Task<bool?>>? Ask { get; set; }

    /// <summary>Accepts the layout, as the accept button does. Original: <c>Boton_Ok_Click</c>.</summary>
    public async Task AcceptAsync()
    {
        string? question = Model.QuestionBeforeAccepting();
        if (question != null)
        {
            bool? answer = Ask != null
                ? await Ask(question)
                : await new ConfirmWindow(question, withCancel: false).ShowDialog<bool?>(this);
            if (answer != true)
            {
                return;
            }
        }

        Close(Model.Result());
    }

    private async void OnAccepted(object? sender, EventArgs e) => await AcceptAsync();

    private void OnCancelled(object? sender, EventArgs e) => Close(null);

    private void OnPortrait(object? sender, RoutedEventArgs e) => Model.Landscape = false;

    private void OnLandscape(object? sender, RoutedEventArgs e) => Model.Landscape = true;

    private void OnSwap(object? sender, RoutedEventArgs e) => Model.SwapCardSize();

    private void OnLockSize(object? sender, RoutedEventArgs e) => Model.ToggleLockCardSize();

    private void OnMaximum(object? sender, RoutedEventArgs e) => Model.UseSize(Model.Statistics.Maximum);

    private void OnMinimum(object? sender, RoutedEventArgs e) => Model.UseSize(Model.Statistics.Minimum);

    private void OnMean(object? sender, RoutedEventArgs e) => Model.UseSize(Model.Statistics.Mean);

    private void OnMedian(object? sender, RoutedEventArgs e) => Model.UseSize(Model.Statistics.Median);
}
