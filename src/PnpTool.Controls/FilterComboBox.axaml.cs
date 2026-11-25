using Avalonia.Controls;
using ImageMagick;
using PnpTool.Core.Infrastructure;

namespace PnpTool.Controls;

/// <summary>
/// The list of filters images can be resized with. Original: <c>ComboBox_Filtro</c> and
/// <c>ComboboxItemEfectosImagemagick</c>.
/// </summary>
public partial class FilterComboBox : UserControl
{
    public FilterComboBox()
    {
        InitializeComponent();
        FilterLabel.Text = StringResources.Get("Form_Setup_Otros_Label_Filtro");
        FilterList.ItemsSource = Enum.GetValues<FilterType>().Distinct().ToArray();
        SelectedFilter = FilterType.Lanczos;
    }

    /// <summary>Original: <c>FiltroSeleccionado</c> and <c>CargaValor</c>.</summary>
    public FilterType SelectedFilter
    {
        get;
        set
        {
            field = value;
            FilterList.SelectedItem = value;
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (FilterList.SelectedItem is FilterType filter)
        {
            SelectedFilter = filter;
        }
    }
}
