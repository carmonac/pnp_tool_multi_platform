using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PnpTool.App.ViewModels;

namespace PnpTool.App.Views;

/// <summary>
/// The credits, the terms and the links to support the program. Original: the left bar and
/// the web browser of <c>WindowSetupPnpTool</c>; the pages are drawn here with ordinary
/// controls instead of HTML.
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
        : this(new AboutViewModel())
    {
    }

    public AboutWindow(AboutViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Model = model;
        model.PropertyChanged += OnModelChanged;
        Closed += (_, _) => model.PropertyChanged -= OnModelChanged;
        ShowPage();
    }

    public AboutViewModel Model { get; }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AboutViewModel.Selected))
        {
            ShowPage();
        }
    }

    private void ShowPage()
    {
        AboutPage page = Model.Selected;
        PageContent.Children.Clear();
        PageContent.Children.Add(new TextBlock
        {
            Text = page.Title,
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 8),
        });

        foreach (AboutSection section in page.Sections)
        {
            if (section.Heading != null)
            {
                PageContent.Children.Add(Heading(section));
            }

            foreach (string line in section.Lines)
            {
                PageContent.Children.Add(Line(line));
            }
        }

        if (page.Closing != null)
        {
            PageContent.Children.Add(new TextBlock
            {
                Text = page.Closing,
                FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(0, 12, 0, 0),
            });
        }
    }

    private static Control Heading(AboutSection section)
    {
        var text = new TextBlock
        {
            Text = section.Heading,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 10, 0, 0) };
        if (section.FlagPath != null)
        {
            row.Children.Add(new Image { Source = new Bitmap(section.FlagPath), Height = 18, VerticalAlignment = VerticalAlignment.Center });
        }

        row.Children.Add(text);
        return row;
    }

    /// <summary>A line of text; one that carries a web address opens it when pressed.</summary>
    private Control Line(string line)
    {
        var text = new TextBlock { Text = line, TextWrapping = TextWrapping.Wrap };
        string? link = AboutViewModel.LinkIn(line);
        if (link == null)
        {
            return text;
        }

        text.Classes.Add("link");
        text.TextDecorations = TextDecorations.Underline;
        text.Cursor = new Cursor(StandardCursorType.Hand);
        text.PointerPressed += (_, _) => Model.OpenLink(link);
        if (!link.StartsWith("https://github.com/", StringComparison.Ordinal))
        {
            return text;
        }

        text.VerticalAlignment = VerticalAlignment.Center;
        var mark = new Avalonia.Controls.Shapes.Path
        {
            Name = "GitHubMark",
            Data = Geometry.Parse(GitHubMark),
            Width = 20,
            Height = 20,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
        };
        mark.Bind(Avalonia.Controls.Shapes.Shape.FillProperty, text.GetObservable(TextBlock.ForegroundProperty));
        // A grid, so that the address wraps in a narrow window instead of being cut.
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8, Margin = new Thickness(0, 12, 0, 0) };
        Grid.SetColumn(text, 1);
        row.Children.Add(mark);
        row.Children.Add(text);
        return row;
    }

    /// <summary>Outline of the GitHub mark, drawn beside the link to the source code.</summary>
    private const string GitHubMark =
        "M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94"
        + "-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07"
        + "-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27"
        + ".68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95"
        + ".29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z";

    private async void OnCheckUpdates(object? sender, RoutedEventArgs e) => await Model.CheckForUpdatesAsync();

    private void OnOpenUpdate(object? sender, RoutedEventArgs e) => Model.OpenUpdate();

    private void OnDonate(object? sender, RoutedEventArgs e) => Model.Donate();

    private void OnTelegram(object? sender, RoutedEventArgs e) => Model.OpenTelegram();

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
