using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.Core.Paper;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;

namespace PnpTool.App.ViewModels;

/// <summary>
/// The choice of a paper: one of the known sizes or a typed one, upright or on its side.
/// Original: <c>Control_TamanosPapel</c>.
/// </summary>
public sealed partial class PaperChoiceViewModel : ViewModelBase
{
    private bool _loading;

    public PaperChoiceViewModel()
        : this(PaperType.A4)
    {
    }

    /// <param name="initial">The paper chosen at first; the sheet for book covers is offered when it is the one.</param>
    public PaperChoiceViewModel(PaperType initial)
    {
        Options = PaperTypeOption.ForSheets(covers: initial == PaperType.SorianoCovers);
        _selected = Options.First(option => option.Value == initial);
        UnitPoint size = Paper.GetSize(initial);
        _widthText = size.X.ToDisplayString();
        _heightText = size.Y.ToDisplayString();
    }

    public IReadOnlyList<PaperTypeOption> Options { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustom))]
    private PaperTypeOption _selected;

    [ObservableProperty]
    private string _widthText;

    [ObservableProperty]
    private string _heightText;

    /// <summary>Whether the sheet is used on its side.</summary>
    [ObservableProperty]
    private bool _landscape;

    /// <summary>Only a custom size can be typed.</summary>
    public bool IsCustom => Selected.Value == PaperType.Custom;

    /// <summary>Raised when the paper chosen changes. Original: <c>NotificaCambiosPapel</c>.</summary>
    public event EventHandler? Changed;

    /// <summary>The paper chosen, with its size as stored: not turned when it is on its side.</summary>
    public Paper Paper => new()
    {
        Type = Selected.Value,
        Size = new UnitPoint { X = Parse(WidthText), Y = Parse(HeightText) },
        Landscape = Landscape,
    };

    /// <summary>The size of the sheet as it is printed.</summary>
    public UnitPoint OrientedSize => LayoutCalculator.Oriented(Paper);

    /// <summary>Shows a paper. Original: <c>CargaConfiguracion</c>.</summary>
    public void Load(Paper paper)
    {
        _loading = true;
        Selected = Options.FirstOrDefault(option => option.Value == paper.Type) ?? Options[0];
        WidthText = paper.Size.X.ToDisplayString();
        HeightText = paper.Size.Y.ToDisplayString();
        Landscape = paper.Landscape;
        _loading = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    partial void OnSelectedChanged(PaperTypeOption value)
    {
        if (_loading)
        {
            return;
        }

        if (value.Value != PaperType.Custom)
        {
            UnitPoint size = Paper.GetSize(value.Value);
            _loading = true;
            WidthText = size.X.ToDisplayString();
            HeightText = size.Y.ToDisplayString();
            _loading = false;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    partial void OnWidthTextChanged(string value) => Announce();

    partial void OnHeightTextChanged(string value) => Announce();

    partial void OnLandscapeChanged(bool value) => Announce();

    private void Announce()
    {
        if (!_loading)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private static Unit Parse(string? text) => string.IsNullOrEmpty(text) ? Unit.Zero : Unit.FromDisplayString(text);
}
