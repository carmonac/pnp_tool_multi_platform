using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;
using PnpTool.Pdf.Tools;

namespace PnpTool.App.ViewModels;

/// <summary>
/// The tool that joins PDFs one after another, or page by page: fronts from one and backs
/// from the other. Original: <c>Window_Unir_PDF</c> with <c>FormUnirPdfModeloIText</c>.
/// </summary>
public sealed partial class MergePdfViewModel : ViewModelBase
{
    public ObservableCollection<string> Files { get; } = [];

    [ObservableProperty]
    private int _selectedIndex = -1;

    /// <summary>Whether the pages of two PDFs are taken in turn.</summary>
    [ObservableProperty]
    private bool _interleave;

    /// <summary>Whether the PDF made is opened instead of telling that it is done.</summary>
    [ObservableProperty]
    private bool _openWhenDone;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private double _progressMaximum = 1.0;

    /// <summary>Raised to tell the user something.</summary>
    public event Action<string>? Notice;

    public void Add(IEnumerable<string> paths)
    {
        foreach (string path in paths.Where(path => string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase)))
        {
            Files.Add(path);
        }
    }

    public void RemoveSelected()
    {
        int index = SelectedIndex;
        if (index >= 0 && index < Files.Count)
        {
            Files.RemoveAt(index);
            SelectedIndex = Files.Count == 0 ? -1 : Math.Min(index, Files.Count - 1);
        }
    }

    /// <summary>Original: <c>Boton_FlechaArriba_Click</c>, which left the file moved unselected.</summary>
    public void MoveUp() => Move(-1);

    public void MoveDown() => Move(1);

    private void Move(int step)
    {
        int index = SelectedIndex;
        int target = index + step;
        if (index >= 0 && target >= 0 && target < Files.Count)
        {
            Files.Move(index, target);
            SelectedIndex = target;
        }
    }

    /// <summary>
    /// Joins the files into the destination. Gives whether it was done.
    /// Original: <c>BotonFusionar_Click</c>.
    /// </summary>
    public async Task<bool> MergeAsync(string? destination)
    {
        if (string.IsNullOrEmpty(destination))
        {
            Notice?.Invoke(StringResources.Get("faltan_datos"));
            return false;
        }

        if (!destination.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            destination += ".pdf";
        }

        var merger = new PdfMerger { Sources = [.. Files], Destination = destination, Interleave = Interleave };
        try
        {
            AppError error = merger.Validate();
            if (error.HasError)
            {
                Notice?.Invoke(error.Message);
                return false;
            }

            merger.Started += maximum => ProgressMaximum = Math.Max(1, maximum);
            merger.ProgressChanged += done => Progress = done;
            await Task.Run(merger.Merge);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or iText.Kernel.Exceptions.PdfException or iText.IO.Exceptions.IOException)
        {
            Notice?.Invoke(exception.Message);
            return false;
        }
        finally
        {
            Progress = 0.0;
        }

        if (OpenWhenDone)
        {
            Platform.Services.OpenFile(destination);
        }
        else
        {
            Notice?.Invoke(StringResources.Get("proceso_completado"));
        }

        return true;
    }
}
