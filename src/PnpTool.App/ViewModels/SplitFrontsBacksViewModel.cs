using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App.ViewModels;

/// <summary>
/// The tool that sorts the images of a folder into "front" and "back" by a text in their
/// names. Original: <c>WindowSepararFrontalesTraseras</c> with <c>FormSepararFrontalesTraserasModelo</c>.
/// </summary>
public sealed partial class SplitFrontsBacksViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _folder = string.Empty;

    /// <summary>The text that the names of the fronts have. Original: <c>Criterio</c>.</summary>
    [ObservableProperty]
    private string _marker = string.Empty;

    /// <summary>Raised to tell the user something.</summary>
    public event Action<string>? Notice;

    /// <summary>Sorts the files. Gives how many went each way, or null when something is missing or fails.</summary>
    public (int Fronts, int Backs)? Split()
    {
        if (string.IsNullOrWhiteSpace(Folder) || Marker.Length == 0 || !Directory.Exists(Folder))
        {
            Notice?.Invoke(StringResources.Get("faltan_datos"));
            return null;
        }

        try
        {
            (int fronts, int backs) = FrontBackSplitter.Split(Folder, Marker);
            Notice?.Invoke(StringResources.Get("proceso_completado"));
            return (fronts, backs);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Notice?.Invoke(exception.Message);
            return null;
        }
    }
}
