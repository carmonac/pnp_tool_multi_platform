using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App.ViewModels;

/// <summary>
/// Takes a picture of an area of the screen every few seconds and saves them to a folder,
/// to copy the cards of a game shown on screen one after another. Original: the separate
/// program AutoCaptura (<c>Autocaptura_WPF.MainWindow</c>), which the Windows version starts
/// from its tools tab.
/// </summary>
/// <remarks>
/// Differences from AutoCaptura, which has no check box for it: the pictures taken can be
/// added to the project when the window is closed. Its defects are not carried over: see
/// the porting map.
/// </remarks>
public sealed partial class ScreenCaptureViewModel : ViewModelBase
{
    private readonly bool _spanish = CultureInfo.CurrentCulture.TwoLetterISOLanguageName is "es" or "ca";
    private readonly IScreenCapture? _capture;
    private readonly List<string> _taken = [];
    private IReadOnlyList<ScreenArea> _screens = [];
    private int _seconds;
    private int _number;
    private DateTime _started;

    /// <param name="capture">Takes the pictures; the one of the program when null.</param>
    public ScreenCaptureViewModel(IScreenCapture? capture = null)
    {
        _capture = capture;
        _folder = AppSettings.LastFolder ?? string.Empty;
        ImportLabel = StringResources.Get("Form_Extraer_CheckBox_importar");
    }

    public string IntervalLabel => Text("Intervalo (s)", "Interval (s)");

    public string ScreenLabel => Text("Pantalla", "Screen");

    public string WidthLabel => Text("Ancho de la captura", "Capture width");

    public string HeightLabel => Text("Alto de la captura", "Capture height");

    public string OffsetXLabel => Text("Inicio X", "Start X");

    public string OffsetYLabel => Text("Inicio Y", "Start Y");

    public string FolderLabel => Text("Carpeta de destino", "Destination folder");

    public string ImportLabel { get; }

    /// <summary>Seconds between two pictures. Original: <c>NumericUpDown_Intervalo</c>, 2 at first.</summary>
    [ObservableProperty]
    private string _intervalText = "2";

    /// <summary>"#0", "#1"…, one per screen. Original: <c>ComboBox_Pantalla</c>.</summary>
    [ObservableProperty]
    private IReadOnlyList<string> _screenNames = [];

    [ObservableProperty]
    private int _selectedScreen = -1;

    [ObservableProperty]
    private string _widthText = string.Empty;

    [ObservableProperty]
    private string _heightText = string.Empty;

    [ObservableProperty]
    private string _offsetXText = "0";

    [ObservableProperty]
    private string _offsetYText = "0";

    [ObservableProperty]
    private string _folder;

    /// <summary>Whether the pictures taken are added to the project when the window closes.</summary>
    [ObservableProperty]
    private bool _import;

    [ObservableProperty]
    private bool _isRunning;

    /// <summary>
    /// True in the second half of the wait: a picture is about to be taken, so what is on
    /// screen should not be changed. Original: the light <c>PictureBox_Semaforo</c>, red then.
    /// </summary>
    [ObservableProperty]
    private bool _aboutToCapture;

    [ObservableProperty]
    private string _status = string.Empty;

    /// <summary>Raised to tell the user something.</summary>
    public event Action<string>? Notice;

    /// <summary>Raised with the file of a new picture of the area chosen, to show it.</summary>
    public event Action<string>? PreviewTaken;

    /// <summary>The pictures saved since the window was opened.</summary>
    public IReadOnlyList<string> Taken => _taken;

    /// <summary>The pictures to add to the project: those taken, when asked to.</summary>
    public IReadOnlyList<string> ToImport => Import ? [.. _taken.Where(File.Exists)] : [];

    /// <summary>Gives the screens of the computer; the first is chosen. Original: <c>Screen.AllScreens</c>.</summary>
    public void SetScreens(IReadOnlyList<ScreenArea> screens)
    {
        _screens = screens;
        ScreenNames = [.. screens.Select((_, index) => "#" + index)];
        SelectedScreen = screens.Count == 0 ? -1 : 0;
    }

    /// <summary>Original: <c>ComboBox_Pantalla_SelectionChanged</c>: the area becomes the whole screen.</summary>
    partial void OnSelectedScreenChanged(int value)
    {
        if (value >= 0 && value < _screens.Count)
        {
            OffsetXText = "0";
            OffsetYText = "0";
            WidthText = _screens[value].Width.ToString(CultureInfo.InvariantCulture);
            HeightText = _screens[value].Height.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// The area to capture: the one typed, kept inside the screen chosen. Null without a
    /// screen. Original: the limits of <c>TextBox_TamanoX_TextChanged</c>.
    /// </summary>
    public ScreenArea? Area
    {
        get
        {
            if (SelectedScreen < 0 || SelectedScreen >= _screens.Count)
            {
                return null;
            }

            ScreenArea screen = _screens[SelectedScreen];
            int x = Math.Clamp(Whole(OffsetXText), 0, Math.Max(0, screen.Width - 1));
            int y = Math.Clamp(Whole(OffsetYText), 0, Math.Max(0, screen.Height - 1));
            int width = Math.Clamp(Whole(WidthText), 0, screen.Width - x);
            int height = Math.Clamp(Whole(HeightText), 0, screen.Height - y);
            return new ScreenArea(screen.X + x, screen.Y + y, width, height, screen.Scaling);
        }
    }

    /// <summary>Seconds between pictures, one at least.</summary>
    public int Interval => Math.Max(1, Whole(IntervalText));

    /// <summary>What stops the pictures from being taken, or null. Original: the checks of <c>Boton_Comenzar_Click</c>.</summary>
    public string? Problem()
    {
        if (string.IsNullOrWhiteSpace(Folder))
        {
            return StringResources.Get("Form_Dice7Pixels_Error_CarpetaDestino");
        }

        ScreenArea? area = Area;
        return area == null || area.Value.Width <= 0 || area.Value.Height <= 0
            ? Text("La zona a capturar no tiene tamaño", "The area to capture has no size")
            : null;
    }

    /// <summary>Starts taking pictures. Gives whether it started. Original: <c>Boton_Comenzar_Click</c>.</summary>
    public bool Start()
    {
        string? problem = Problem();
        if (problem != null)
        {
            Notice?.Invoke(problem);
            return false;
        }

        try
        {
            Directory.CreateDirectory(Folder);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Notice?.Invoke(exception.Message);
            return false;
        }

        _seconds = 0;
        _started = DateTime.Now;
        AboutToCapture = false;
        IsRunning = true;
        return true;
    }

    /// <summary>
    /// A second has gone by. Halfway through the wait the light turns red; at its end a
    /// picture is taken and the wait starts again. Original: <c>Timer1_Tick</c>.
    /// </summary>
    public void Tick()
    {
        if (!IsRunning)
        {
            return;
        }

        _seconds++;
        int interval = Interval;
        if (_seconds >= interval)
        {
            _seconds = 0;
            AboutToCapture = false;
            if (!CaptureNow())
            {
                Stop();
            }
        }
        else if (_seconds >= (interval + 1) / 2)
        {
            AboutToCapture = true;
        }
    }

    /// <summary>Stops and says how long it ran and how many pictures there are. Original: <c>Boton_Parar_Click</c>.</summary>
    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;
        AboutToCapture = false;
        TimeSpan elapsed = DateTime.Now - _started;
        Status = Text("Tiempo: ", "Time elapsed: ") + elapsed.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture) + " -- " + _taken.Count;
    }

    /// <summary>
    /// Saves a picture of the area as the next <c>Capture_N.jpg</c> that does not exist in
    /// the folder. Gives whether it was saved. Original: <c>Capturar(false)</c>.
    /// </summary>
    public bool CaptureNow()
    {
        ScreenArea? area = Area;
        if (area == null)
        {
            return false;
        }

        string file;
        do
        {
            file = Path.Combine(Folder, "Capture_" + _number.ToString(CultureInfo.InvariantCulture) + ".jpg");
            _number++;
        }
        while (File.Exists(file));

        AppError error = (_capture ?? ScreenCapture.Current).Capture(area.Value, file);
        if (error.HasError)
        {
            _number--;
            Status = error.Message;
            Notice?.Invoke(error.Message);
            return false;
        }

        _taken.Add(file);
        Status = "Capture: " + (_number - 1).ToString(CultureInfo.InvariantCulture) + " T" + DateTime.Now.ToString(CultureInfo.CurrentCulture);
        return true;
    }

    /// <summary>
    /// Takes a picture of the area to show it, without saving it among the captures. A
    /// failure is written in the status line. Original: <c>ActualizaPreview</c>.
    /// </summary>
    public bool Preview()
    {
        ScreenArea? area = Area;
        if (area == null || area.Value.Width <= 0 || area.Value.Height <= 0)
        {
            return false;
        }

        string file = Path.Combine(Path.GetTempPath(), "pnptool-capture-preview.jpg");
        AppError error = (_capture ?? ScreenCapture.Current).Capture(area.Value, file);
        if (error.HasError)
        {
            Status = error.Message;
            return false;
        }

        PreviewTaken?.Invoke(file);
        return true;
    }

    private string Text(string spanish, string english) => _spanish ? spanish : english;

    private static int Whole(string? text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
}
