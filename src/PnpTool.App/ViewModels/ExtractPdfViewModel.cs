using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;
using PnpTool.Pdf.Tools;

namespace PnpTool.App.ViewModels;

/// <summary>
/// The tool that takes the images out of PDFs, or turns their pages into images, and can
/// bring them to the panel as cards. Original: <c>WindowExtraer</c> with <c>FormExtraerModelo</c>.
/// </summary>
public sealed partial class ExtractPdfViewModel : ViewModelBase
{
    /// <summary>The folder made inside a folder that already has files, when the user prefers so.</summary>
    public const string Subfolder = "Extract";

    public ObservableCollection<string> Files { get; } = [];

    [ObservableProperty]
    private string? _selectedFile;

    /// <summary>Whether the images go to the panel as cards when done.</summary>
    [ObservableProperty]
    private bool _import = true;

    /// <summary>Whether every page becomes an image, instead of taking out the images it holds.</summary>
    [ObservableProperty]
    private bool _wholePages;

    [ObservableProperty]
    private bool _clearDestination;

    [ObservableProperty]
    private bool _invertColors;

    /// <summary>Whether images of less than <see cref="AppSettings.SmallFileSizeKb"/> are deleted.</summary>
    [ObservableProperty]
    private bool _deleteSmall;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isBusy;

    public static string SmallText => StringResources.Get("Form_Extraer_CheckBox_BorrarImagenesXTamano") + " (<" + AppSettings.SmallFileSizeKb + "Kb)";

    /// <summary>Raised to tell the user something.</summary>
    public event Action<string>? Notice;

    /// <summary>Adds the PDFs among the files given. Original: <c>BotonAnadir_Click</c> and <c>ListBox_pdf_Drop</c>.</summary>
    public void Add(IEnumerable<string> paths)
    {
        foreach (string path in paths.Where(path => string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase)))
        {
            Files.Add(path);
        }
    }

    /// <summary>Original: <c>Quitar</c>.</summary>
    public void RemoveSelected()
    {
        if (SelectedFile != null)
        {
            Files.Remove(SelectedFile);
        }
    }

    /// <summary>Whether a folder already has files, so that the user is asked about a folder inside it.</summary>
    public static bool HasFiles(string folder) => Directory.Exists(folder) && Directory.GetFiles(folder).Length != 0;

    /// <summary>
    /// Extracts into the folder. Gives the images written, of every PDF, or null when there
    /// was nothing to do or it failed. Original: <c>Boton_Extraer_Click</c> and <c>Trabajador_Fin</c>.
    /// </summary>
    public async Task<List<string>?> ExtractAsync(string folder)
    {
        if (Files.Count == 0)
        {
            Notice?.Invoke(StringResources.Get("faltan_datos"));
            return null;
        }

        var extractor = new PdfImageExtractor
        {
            Destination = folder,
            Files = [.. Files],
            ClearDestination = ClearDestination,
            WholePages = WholePages,
            InvertColors = InvertColors,
            ReportProgress = percent => Progress = percent,
        };
        bool deleteSmall = DeleteSmall;
        int dpi = AppSettings.TargetDpi;
        IsBusy = true;
        try
        {
            (AppError error, List<string> written) = await Task.Run(() =>
            {
                HashSet<string> before = Directory.Exists(folder) && !extractor.ClearDestination ? [.. Directory.GetFiles(folder)] : [];
                AppError result = extractor.Extract(dpi);
                List<string> made = Directory.Exists(folder)
                    ? [.. Directory.GetFiles(folder).Where(file => !before.Contains(file)).Order(new PnpTool.Core.Text.NumericComparer())]
                    : [];
                if (deleteSmall)
                {
                    foreach (string file in made.Where(file => new FileInfo(file).Length / 1024 < AppSettings.SmallFileSizeKb).ToList())
                    {
                        File.Delete(file);
                        made.Remove(file);
                    }
                }

                return (result, made);
            });
            Notice?.Invoke(error.HasError ? error.Message : StringResources.Get("proceso_completado"));
            return written;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or iText.Kernel.Exceptions.PdfException or iText.IO.Exceptions.IOException)
        {
            Notice?.Invoke(exception.Message);
            return null;
        }
        finally
        {
            IsBusy = false;
            Progress = 0.0;
        }
    }
}
