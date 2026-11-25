using Avalonia;
using Avalonia.Controls;
using PnpTool.App.Services;
using PnpTool.App.ViewModels;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;

namespace PnpTool.App.Views;

/// <summary>The main window. Original: <c>Window_Principal_v2</c>.</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        RestorePlacement();
        Closing += (_, _) => SavePlacement();

        // Original: Control_Slider_Local.Inicializa(25, 1, 75, 300, 100).
        ZoomSlider.ValueChangedCallback = value =>
        {
            if (DataContext is MainWindowViewModel model)
            {
                model.Zoom = value;
            }
        };
        ZoomSlider.Initialize(tick: 25, smallChange: 1, minimum: 75, maximum: 300, value: 100);
        CardsPanel.ZoomStepRequested += step => ZoomSlider.Step(step);
        CardsPanel.ProblemFound += message => _ = new MessageWindow(message).ShowDialog(this);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel model)
        {
            model.PickImages = () => ImageFiles.PickAsync(this, multiple: true);
            model.ShowMessage = message => new MessageWindow(message).ShowDialog(this);
            model.AskSelection = cards => new SelectCardsWindow(cards).ShowDialog<CardSelection?>(this);
            model.AskImageChange = (cards, face, selected) => new ChangeImagesWindow(cards, face, selected).ShowDialog<ImageChange?>(this);
        }
    }

    /// <summary>
    /// Opens maximised, or at the size it had when it was last closed, as the settings say.
    /// A saved size larger than the screen it opens on is ignored. Original: the start of
    /// <c>Window_OnLoad</c> with <c>IniciarVentanaMaximizada</c> and <c>TamanoVentanaAbierta</c>.
    /// </summary>
    private void RestorePlacement()
    {
        PointI saved = AppSettings.WindowSize;
        if (saved.X >= MinWidth && saved.Y >= MinHeight)
        {
            PixelRect? area = Screens.Primary?.WorkingArea;
            double scale = Screens.Primary?.Scaling ?? 1.0;
            if (area == null || (saved.X <= area.Value.Width / scale && saved.Y <= area.Value.Height / scale))
            {
                Width = saved.X;
                Height = saved.Y;
            }
        }

        if (AppSettings.StartMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    /// <summary>Remembers the size of the window, unless it is maximised or minimised, and saves the settings.</summary>
    private void SavePlacement()
    {
        if (WindowState == WindowState.Normal)
        {
            AppSettings.WindowSize = new PointI((int)Width, (int)Height);
        }

        AppSettings.StartMaximized = WindowState == WindowState.Maximized;
        try
        {
            AppSettings.Save();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Write("Settings not saved: " + exception.Message, force: true);
        }
    }
}
