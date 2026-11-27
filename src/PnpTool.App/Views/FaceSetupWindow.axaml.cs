using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PnpTool.App.ViewModels;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Projects;

namespace PnpTool.App.Views;

/// <summary>
/// The settings of the fronts or of the backs of the cards. It closes with a
/// <see cref="FaceSetupResult"/> when accepted and with nothing when cancelled.
/// Original: <c>WindowConfigurarCara</c>.
/// </summary>
public partial class FaceSetupWindow : Window
{
    public FaceSetupWindow()
        : this(new FaceSetupViewModel(new ProjectSettings(), FaceType.Front))
    {
    }

    public FaceSetupWindow(FaceSetupViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Model = model;
        Title = model.Title;
        FaceIcon.Source = new Bitmap(AssetLoader.Open(new Uri("avares://PnpTool/Assets/Icons/" + (model.IsBack ? "backs" : "fronts") + ".png")));
        EffectLabel.Text = StringResources.Get("Form_Setup_Caras_CaraFrontal_LabelEfectoFrontal");
        BackgroundCheck.Content = StringResources.Get("Form_Setup_Caras_CaraFrontal_CheckboxPintarFondo");
        FrameCheck.Content = StringResources.Get("Form_Setup_Caras_CaraFrontal_CheckboxMarcoExterior");
        FrameSizeLabel.Text = StringResources.Get("Form_Setup_libro_Label_MarcasPerforacionTamano");
        CutLineLabel.Text = StringResources.Get("Form_Setup_Caras_CaraFrontal_LabelLineaCorteFrontal");
        ToEdgeCheck.Content = StringResources.Get("Form_Setup_Caras_CaraFrontal_CheckboxHastaBorde");

        string unit = " (" + LayoutViewModel.UnitName + ")";
        ToolTip.SetTip(RatioButton, StringResources.Get("Bloquea_Ratio"));
        ToolTip.SetTip(WidthBox, StringResources.Get("Ancho") + unit);
        ToolTip.SetTip(HeightBox, StringResources.Get("Alto") + unit);
        ToolTip.SetTip(OffsetXLabel, unit.Trim());
        ToolTip.SetTip(OffsetYLabel, unit.Trim());
        ToolTip.SetTip(OffsetXBox, unit.Trim());
        ToolTip.SetTip(OffsetYBox, unit.Trim());
        ToolTip.SetTip(FrameSizeBox, unit.Trim());
        ToolTip.SetTip(ThicknessIcon, StringResources.Get("grosor") + " (pt)");
        ToolTip.SetTip(ThicknessBox, StringResources.Get("grosor") + " (pt)");

        BackgroundColorButton.Load(model.BackgroundColor);
        BackgroundColorButton.ColorChanged += color => model.BackgroundColor = color;
        FrameColorButton.Load(model.FrameColor);
        FrameColorButton.ColorChanged += color => model.FrameColor = color;
        CutLineColorButton.Load(model.CutLineColor);
        CutLineColorButton.ColorChanged += color => model.CutLineColor = color;
        model.Notice += message => _ = Say(message);
    }

    public FaceSetupViewModel Model { get; }

    /// <summary>Shows a message; the test of the window replaces it.</summary>
    public Func<string, Task>? ShowMessage { get; set; }

    private Task Say(string message) => ShowMessage != null ? ShowMessage(message) : new MessageWindow(message).ShowDialog(this);

    /// <summary>Accepts the settings, as the accept button does. Original: <c>Boton_Ok_Click</c>.</summary>
    public async Task AcceptAsync()
    {
        string? problem = Model.Problem();
        if (problem != null)
        {
            await Say(problem);
            return;
        }

        Close(Model.Result());
    }

    private async void OnAccepted(object? sender, EventArgs e) => await AcceptAsync();

    private void OnCancelled(object? sender, EventArgs e) => Close(null);

    private void OnSwap(object? sender, RoutedEventArgs e) => Model.SwapSize();
}
