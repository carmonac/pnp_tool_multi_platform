using Avalonia.Controls;
using PnpTool.App.ViewModels;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Projects;

namespace PnpTool.App.Views;

/// <summary>
/// The settings of books. It closes with the <see cref="BookSettings"/> when accepted and
/// with nothing when cancelled. Original: <c>WindowSetupLibros</c>.
/// </summary>
public partial class BookSetupWindow : Window
{
    public BookSetupWindow()
        : this(new BookSetupViewModel(new BookSettings()))
    {
    }

    public BookSetupWindow(BookSetupViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Model = model;
        Title = StringResources.Get("Boton_Configurar_Libros");
        PositionLabel.Text = StringResources.Get("Form_Setup_libro_Label_PosicionImagenes");
        SpineLabel.Text = StringResources.Get("Form_Setup_libro_Label_Lomo");
        EdgeLabel.Text = StringResources.Get("Form_Setup_RadioButton_ImpresionDobleCara");
        MarksLabel.Text = StringResources.Get("Form_Setup_libro_GroupBox_MarcasPerforacion");
        MarginLabel.Text = StringResources.Get("Form_Setup_libro_Label_MarcasPerforacionMargen");
        CountLabel.Text = StringResources.Get("Form_Setup_libro_Label_MarcasPerforacionNumero");
        SizeLabel.Text = StringResources.Get("Form_Setup_libro_Label_MarcasPerforacionTamano");
        string unit = "(" + LayoutViewModel.UnitName + ")";
        ToolTip.SetTip(MarginBox, unit);
        ToolTip.SetTip(SizeBox, unit);
    }

    public BookSetupViewModel Model { get; }

    /// <summary>Accepts the settings, as the accept button does.</summary>
    public void Accept() => Close(Model.Result());

    private void OnAccepted(object? sender, EventArgs e) => Accept();

    private void OnCancelled(object? sender, EventArgs e) => Close(null);
}
