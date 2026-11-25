using System.ComponentModel;
using Avalonia.Controls;
using PnpTool.App.ViewModels.Panel;

namespace PnpTool.App.Views;

/// <summary>
/// A card as the image panel shows it. The clicks are handled by <see cref="ImagePanel"/>.
/// Original: <c>PanelCarta_WPF</c>.
/// </summary>
public partial class CardPanel : UserControl
{
    private CardPanelViewModel? _card;

    public CardPanel()
    {
        InitializeComponent();
        CopiesBox.SetRange(maximum: 9999, minimum: 0, step: 1);
        CopiesBox.ValueChanged += (_, _) =>
        {
            if (_card != null)
            {
                _card.Copies = CopiesBox.Value;
            }
        };
    }

    public CardPanelViewModel? Card => _card;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_card != null)
        {
            _card.PropertyChanged -= OnCardChanged;
        }

        // Detached first, so that showing the copies of the new card is not taken as a change.
        _card = null;
        if (DataContext is CardPanelViewModel card)
        {
            CopiesBox.Value = card.Copies;
            _card = card;
            _card.PropertyChanged += OnCardChanged;
        }
    }

    private void OnCardChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CardPanelViewModel.Copies) && _card != null && CopiesBox.Value != _card.Copies)
        {
            CopiesBox.Value = _card.Copies;
        }
    }
}
