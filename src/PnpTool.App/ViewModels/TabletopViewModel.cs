using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.Controls;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Pdf.Tabletop;

namespace PnpTool.App.ViewModels;

/// <summary>
/// What is asked before the cards are written as sheets for Tabletop Simulator: columns and
/// rows of the table, whether to keep the limits of Tabletop Simulator, and the folder and
/// the name of the files. Original: <c>Window_Tabletop</c>.
/// </summary>
public sealed partial class TabletopViewModel : ViewModelBase
{
    private readonly TabletopSheets _sheets;
    private readonly UnitPoint _cardSize;

    /// <param name="sheets">What will write the sheets; its table is set from here.</param>
    /// <param name="cardSize">The size of the cards of the project, to show.</param>
    public TabletopViewModel(TabletopSheets sheets, UnitPoint cardSize)
    {
        _sheets = sheets;
        _cardSize = cardSize;
        _columnsText = sheets.Columns.ToString();
        _rowsText = sheets.Rows.ToString();
        _applyRestrictions = sheets.ApplyRestrictions;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private string _columnsText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private string _rowsText;

    /// <summary>2 to 10 columns, 2 to 7 rows and sheets of 8980 × 6286 pixels at most.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private bool _applyRestrictions;

    /// <summary>Where the files go.</summary>
    [ObservableProperty]
    private string _folder = string.Empty;

    /// <summary>The beginning of the names of the files.</summary>
    [ObservableProperty]
    private string _fileName = string.Empty;

    /// <summary>The columns the sheets will have: what is typed, within the limits when they apply. Original: <c>ValidaRestriccion_X</c>.</summary>
    public int Columns => TabletopSheets.RestrictColumns(IntegerTextBox.Parse(ColumnsText), ApplyRestrictions);

    /// <summary>Original: <c>ValidaRestriccion_Y</c>.</summary>
    public int Rows => TabletopSheets.RestrictRows(IntegerTextBox.Parse(RowsText), ApplyRestrictions);

    /// <summary>
    /// The size of the card, the table that will be used, and the size of each sheet in
    /// pixels with how many sheets there will be. Original: <c>CambiarEtiqueta</c>, which
    /// showed two numbers that were not the size of anything.
    /// </summary>
    public string Summary
    {
        get
        {
            Apply();
            PointI sheet = _sheets.SheetSize();
            return _cardSize.X.ToDisplayString() + " x " + _cardSize.Y.ToDisplayString() + " " + LayoutViewModel.UnitName
                + "   " + Columns + " x " + Rows
                + "   " + StringResources.Get("Imagen") + ": " + sheet.X + " x " + sheet.Y + " px"
                + "   (" + _sheets.SheetCount + ")";
        }
    }

    /// <summary>What stops the sheets from being written, or null: no folder, no name, or no table.</summary>
    public string? Problem() =>
        string.IsNullOrWhiteSpace(Folder) || !Directory.Exists(Folder) || string.IsNullOrWhiteSpace(FileName) || Columns < 1 || Rows < 1
            ? StringResources.Get("faltan_datos")
            : null;

    /// <summary>Sets the table of the sheets and gives the folder and beginning of the names of the files.</summary>
    public string Result()
    {
        Apply();
        return Path.Combine(Folder, FileName.Trim());
    }

    private void Apply()
    {
        _sheets.ApplyRestrictions = ApplyRestrictions;
        _sheets.Columns = Columns;
        _sheets.Rows = Rows;
    }
}
