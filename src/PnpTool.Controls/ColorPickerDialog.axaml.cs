using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PnpTool.Core;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;

namespace PnpTool.Controls;

/// <summary>
/// The window to choose a colour: a colour picker and, under it, the last colours chosen.
/// It closes with the chosen colour, or with nothing when cancelled.
/// Original: <c>Dsafa.WpfColorPicker.ColorPickerDialog</c>.
/// </summary>
/// <remarks>
/// The picker itself is Avalonia's <c>ColorView</c> in place of the original's own. The
/// program reads every stored colour as opaque, so transparency is not offered. The button
/// with the dropper takes a picture of the screen the window is on and lets a colour be
/// picked from it (<see cref="EyeDropperWindow"/>).
/// </remarks>
public partial class ColorPickerDialog : Window
{
    /// <summary>How many recent colours are shown and kept. Original: <c>NumeroElementosColor</c>.</summary>
    public const int PaletteSize = 19;

    private static readonly Uri NoColorIcon = new("avares://PnpTool.Controls/Assets/no-color.png");

    private readonly List<ArgbColor> _palette;

    public ColorPickerDialog()
        : this(new ArgbColor(255, 0, 0))
    {
    }

    public ColorPickerDialog(ArgbColor color)
    {
        InitializeComponent();
        Title = StringResources.Get("Click_Para_Color");
        _palette = [.. LoadPalette()];
        foreach (ArgbColor recent in _palette)
        {
            Palette.Children.Add(PaletteButton(recent));
        }

        // The row ends with a white button showing a "no colour" picture.
        Button none = PaletteButton(ArgbColor.White);
        none.Padding = new Thickness(0.0);
        none.Content = new Image { Source = new Bitmap(AssetLoader.Open(NoColorIcon)), Stretch = Stretch.Fill };
        Palette.Children.Add(none);
        Picker.Color = color.ToAvalonia();
        FindScreen = ScreenOfWindow;
        PickFromPicture = ShowEyeDropper;
    }

    /// <summary>The colour shown by the picker.</summary>
    public ArgbColor Color
    {
        get => new(Picker.Color.R, Picker.Color.G, Picker.Color.B);
        set => Picker.Color = value.ToAvalonia();
    }

    /// <summary>The colours offered when none has been chosen yet. Original: <c>DefaultPalette</c>.</summary>
    public static IReadOnlyList<ArgbColor> DefaultPalette { get; } =
    [
        .. new[]
        {
            Colors.Black, Colors.Gray, Colors.White, Colors.Red, Colors.Orange, Colors.Yellow, Colors.Green,
            Colors.Blue, Colors.Indigo, Colors.Magenta, Colors.Purple, Colors.DarkRed, Colors.DarkOrange,
            Colors.DarkGreen, Colors.DarkBlue, Colors.DarkMagenta, Colors.MediumPurple, Colors.LightGreen,
            Colors.LightBlue,
        }.Select(color => color.ToArgb()),
    ];

    /// <summary>
    /// The recent colours of the user's colour file, or the default ones when there are none.
    /// Original: <c>CargaUltimosColores</c>.
    /// </summary>
    public static IReadOnlyList<ArgbColor> LoadPalette()
    {
        var stored = new CustomColors(PaletteSize);
        stored.Load();
        return stored.Colors.Count == 0 ? DefaultPalette : [.. stored.Colors.Select(ArgbColor.Parse)];
    }

    /// <summary>
    /// Writes the chosen colour followed by the colours that were shown, dropping the last
    /// one when the list is full. Original: <c>GuardaColoresFichero</c>.
    /// </summary>
    public static void SavePalette(ArgbColor chosen, IEnumerable<ArgbColor> shown)
    {
        var stored = new CustomColors(PaletteSize);
        foreach (ArgbColor color in shown.Reverse())
        {
            stored.Push(color.ToString());
        }

        stored.Push(chosen.ToString());
        stored.Save();
    }

    /// <summary>Accepts the colour shown, as the accept button does.</summary>
    public void Accept()
    {
        ArgbColor chosen = Color;
        SavePalette(chosen, _palette);
        Close((ArgbColor?)chosen);
    }

    /// <summary>Original: <c>CreaNuevoBoton</c>.</summary>
    private Button PaletteButton(ArgbColor color)
    {
        var button = new Button
        {
            Width = 28.0,
            Height = 28.0,
            Margin = new Thickness(2.0),
            BorderThickness = new Thickness(1.0),
            BorderBrush = Brushes.Gray,
            Background = color.ToBrush(),
        };
        button.Click += (_, _) => Color = color;
        return button;
    }

    /// <summary>
    /// The screen the colour is picked from; the test of the window replaces it. Null when
    /// the system does not say where the window is.
    /// </summary>
    public Func<ScreenArea?> FindScreen { get; set; }

    /// <summary>Shows the picture of the screen and gives the colour picked; the test of the window replaces it.</summary>
    public Func<string, ScreenArea, Task<ArgbColor?>> PickFromPicture { get; set; }

    private ScreenArea? ScreenOfWindow()
    {
        Screen? screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        return screen == null
            ? null
            : new ScreenArea(screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height, screen.Scaling);
    }

    private Task<ArgbColor?> ShowEyeDropper(string picture, ScreenArea screen) =>
        new EyeDropperWindow(picture, new PixelPoint(screen.X, screen.Y)).ShowDialog<ArgbColor?>(this);

    /// <summary>
    /// Takes a picture of the screen and lets a colour be picked from it. Gives whether a
    /// colour was picked. Original: <c>EyeDropper.ButtonOnClick</c>.
    /// </summary>
    public async Task<bool> PickFromScreenAsync()
    {
        EyeDropperMessage.Text = string.Empty;
        ScreenArea? screen = FindScreen();
        if (screen == null)
        {
            EyeDropperMessage.Text = "Screen not found";
            return false;
        }

        string picture = Path.Combine(Path.GetTempPath(), "pnptool-dropper-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            AppError error = await Task.Run(() => ScreenCapture.Current.Capture(screen.Value, picture));
            if (error.HasError)
            {
                EyeDropperMessage.Text = error.Message;
                return false;
            }

            ArgbColor? picked = await PickFromPicture(picture, screen.Value);
            if (picked == null)
            {
                return false;
            }

            Color = picked.Value;
            return true;
        }
        finally
        {
            try
            {
                File.Delete(picture);
            }
            catch (IOException)
            {
            }
        }
    }

    private async void OnEyeDropper(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await PickFromScreenAsync();

    private void OnAccepted(object? sender, EventArgs e) => Accept();

    private void OnCancelled(object? sender, EventArgs e) => Close((ArgbColor?)null);
}
