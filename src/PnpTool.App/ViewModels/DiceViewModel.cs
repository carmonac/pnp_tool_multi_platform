using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.Core.Infrastructure;
using PnpTool.Pdf.Tabletop;

namespace PnpTool.App.ViewModels;

/// <summary>
/// What is asked before the images are written as templates of "seven pixels" dice: the
/// folder and the name of the files. Original: <c>Window_Dice7Pixels</c>.
/// </summary>
public sealed partial class DiceViewModel : ViewModelBase
{
    /// <param name="dice">What will write the templates.</param>
    public DiceViewModel(SevenPixelDice dice)
    {
        Summary = dice.Die + "   " + StringResources.Get("Imagen") + ": " + dice.ImageCount + " / " + SevenPixelDice.FacesPerDie(dice.Die)
            + "   (" + dice.DiceCount + ")";
    }

    /// <summary>The kind of die, how many images there are for how many a die takes, and how many templates will be written.</summary>
    public string Summary { get; }

    /// <summary>Where the files go.</summary>
    [ObservableProperty]
    private string _folder = string.Empty;

    /// <summary>The beginning of the names of the files. Original: <c>NombreFichero</c>, "Output" until another is typed.</summary>
    [ObservableProperty]
    private string _fileName = "Output";

    /// <summary>What stops the templates from being written, or null: no folder or no name.</summary>
    public string? Problem() =>
        string.IsNullOrWhiteSpace(Folder) || !Directory.Exists(Folder) || string.IsNullOrWhiteSpace(FileName)
            ? StringResources.Get("faltan_datos")
            : null;

    /// <summary>The folder and beginning of the names of the files.</summary>
    public string Result() => Path.Combine(Folder, FileName.Trim());
}
