using Avalonia.Controls;
using Avalonia.Interactivity;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;

namespace PnpTool.Controls;

/// <summary>
/// The choice between the metric and the imperial system.
/// Original: <c>Control_Selector_UnidadesMedida</c>.
/// </summary>
public partial class UnitSelector : UserControl
{
    private UnitType _selectedUnit;

    public UnitSelector()
    {
        InitializeComponent();
        Translate();
        SelectedUnit = UnitType.Millimeter;
    }

    /// <summary>
    /// <see cref="UnitType.Millimeter"/> or <see cref="UnitType.Inch"/>; any other unit set
    /// here shows the imperial system. Original: <c>UnidadSeleccionada</c> and <c>EstableceValor</c>.
    /// </summary>
    public UnitType SelectedUnit
    {
        get => _selectedUnit;
        set
        {
            _selectedUnit = value;
            MetricButton.IsChecked = value == UnitType.Millimeter;
            ImperialButton.IsChecked = value != UnitType.Millimeter;
        }
    }

    /// <summary>Puts the texts in the current language. Original: <c>Traduce</c>.</summary>
    public void Translate()
    {
        MetricButton.Content = StringResources.Get("Form_Setup_PnpTool_RadioButton_Smetrico");
        ImperialButton.Content = StringResources.Get("Form_Setup_PnpTool_RadioButton_Simperial");
    }

    private void OnMetric(object? sender, RoutedEventArgs e) => _selectedUnit = UnitType.Millimeter;

    private void OnImperial(object? sender, RoutedEventArgs e) => _selectedUnit = UnitType.Inch;
}
