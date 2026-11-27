using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.Controls;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;

namespace PnpTool.App.ViewModels;

/// <summary>
/// The settings of books: where the images go on the pages, the side of the spine, the edge
/// the printer turns the sheets over, and the marks for the holes of the binding.
/// Original: <c>WindowSetupLibros</c> with <c>Control_MarcasPerforacion</c>.
/// </summary>
/// <remarks>Lengths are typed and shown in the unit the program is set to, with a point as decimal separator.</remarks>
public sealed partial class BookSetupViewModel : ViewModelBase
{
    private readonly PerforationMarks _marks = new();
    private readonly string _marginAtStart;
    private readonly string _sizeAtStart;

    /// <param name="book">The book settings of the project; they are not changed.</param>
    public BookSetupViewModel(BookSettings book)
    {
        _marks.CopyFrom(book.PerforationMarks);

        PositionOptions = BookImagePositionOption.All();
        _selectedPosition = PositionOptions.First(option => option.Value == book.ImagePlacement);
        SpineOptions = BookSpineOption.All();
        _selectedSpine = SpineOptions.First(option => option.Value == book.Spine);
        EdgeOptions = PrintFlipEdgeOption.All();
        _selectedEdge = EdgeOptions.First(option => option.Value == book.FlipEdge);

        MarkOptions = PerforationMarkOption.All();
        _selectedMarks = MarkOptions.First(option => option.Value == _marks.Type);
        _marginText = _marginAtStart = _marks.Margin.ToDisplayString();
        _countText = _marks.Count.ToString();
        _sizeText = _sizeAtStart = _marks.Size.ToDisplayString();
    }

    public IReadOnlyList<BookImagePositionOption> PositionOptions { get; }

    [ObservableProperty]
    private BookImagePositionOption _selectedPosition;

    public IReadOnlyList<BookSpineOption> SpineOptions { get; }

    [ObservableProperty]
    private BookSpineOption _selectedSpine;

    public IReadOnlyList<PrintFlipEdgeOption> EdgeOptions { get; }

    [ObservableProperty]
    private PrintFlipEdgeOption _selectedEdge;

    // ---- Marks for the holes. Original: Control_MarcasPerforacion ----

    public IReadOnlyList<PerforationMarkOption> MarkOptions { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarksDrawn))]
    private PerforationMarkOption _selectedMarks;

    /// <summary>How far from the edge of the sheet.</summary>
    [ObservableProperty]
    private string _marginText;

    /// <summary>How many marks.</summary>
    [ObservableProperty]
    private string _countText;

    [ObservableProperty]
    private string _sizeText;

    public bool MarksDrawn => SelectedMarks.Value != PerforationMarkType.None;

    /// <summary>The settings as they stand in the window. Original: <c>Boton_Ok_Click</c>.</summary>
    public BookSettings Result() => new()
    {
        ImagePlacement = SelectedPosition.Value,
        Spine = SelectedSpine.Value,
        FlipEdge = SelectedEdge.Value,
        PerforationMarks = new PerforationMarks
        {
            Type = SelectedMarks.Value,

            // A length that was not retyped keeps the unit the file gave it, so that nothing
            // counts as changed.
            Margin = MarginText == _marginAtStart ? _marks.Margin : Parse(MarginText),
            Count = IntegerTextBox.Parse(CountText),
            Size = SizeText == _sizeAtStart ? _marks.Size : Parse(SizeText),
        },
    };

    private static Unit Parse(string? text) => string.IsNullOrEmpty(text) ? Unit.Zero : Unit.FromDisplayString(text);
}
