using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PnpTool.App.ViewModels;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App.Views;

/// <summary>
/// The tool that takes pictures of the screen at intervals. Original: the window of the
/// separate program AutoCaptura. Closes with the pictures to add to the project.
/// </summary>
public partial class ScreenCaptureWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1.0) };

    public ScreenCaptureWindow()
        : this(new ScreenCaptureViewModel())
    {
    }

    public ScreenCaptureWindow(ScreenCaptureViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Model = model;
        Title = StringResources.Get("ShellMenuBarItem_Herramientas_AutoCaptura");
        model.Notice += message => _ = new MessageWindow(message).ShowDialog(this);
        model.PreviewTaken += ShowPreview;
        model.PropertyChanged += OnModelChanged;
        _timer.Tick += (_, _) => model.Tick();
        Opened += (_, _) =>
        {
            if (model.ScreenNames.Count == 0)
            {
                model.SetScreens([.. Screens.All.Select(screen =>
                    new ScreenArea(screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height, screen.Scaling))]);
            }

            model.Preview();
        };
        Closing += (_, _) =>
        {
            _timer.Stop();
            model.Stop();
        };
        Closed += (_, _) =>
        {
            model.PreviewTaken -= ShowPreview;
            model.PropertyChanged -= OnModelChanged;
        };
    }

    public ScreenCaptureViewModel Model { get; }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ScreenCaptureViewModel.IsRunning):
                _timer.IsEnabled = Model.IsRunning;
                break;
            case nameof(ScreenCaptureViewModel.WidthText) or nameof(ScreenCaptureViewModel.HeightText)
                or nameof(ScreenCaptureViewModel.OffsetXText) or nameof(ScreenCaptureViewModel.OffsetYText):
                if (IsVisible && !Model.IsRunning)
                {
                    Model.Preview();
                }

                break;
        }
    }

    private void ShowPreview(string file)
    {
        try
        {
            using FileStream stream = File.OpenRead(file);
            PreviewPicture.Source = Bitmap.DecodeToWidth(stream, 900);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            PreviewPicture.Source = null;
        }
    }

    private void OnStart(object? sender, RoutedEventArgs e) => Model.Start();

    private void OnStop(object? sender, RoutedEventArgs e) => Model.Stop();

    private async void OnPickFolder(object? sender, RoutedEventArgs e)
    {
        string? folder = await DeckWindows.PickFolderAsync(this);
        if (folder != null)
        {
            Model.Folder = folder;
        }
    }
}
