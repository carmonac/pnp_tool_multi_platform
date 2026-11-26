using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PnpTool.App.ViewModels;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Core.Templates;

namespace PnpTool.App.Views;

/// <summary>
/// The layout of a project made with an advanced template: only sides and backs can be
/// chosen, beside a sketch of the template. It closes with a <see cref="LayoutResult"/> when
/// accepted and with nothing when cancelled. Original: <c>WindowSetupPlantillasAvanzadas</c>.
/// </summary>
public partial class TemplateSetupWindow : Window
{
    public TemplateSetupWindow()
        : this(new LayoutViewModel(new ProjectSettings()), new AdvancedTemplate())
    {
    }

    public TemplateSetupWindow(LayoutViewModel model, AdvancedTemplate template)
    {
        InitializeComponent();
        DataContext = model;
        Model = model;
        Title = StringResources.Get("Form_Setup_TabPage_Disposicion");
        BleedLabel.Text = StringResources.Get("Form_Setup_LabelSangrado");
        UnitPoint paper = LayoutCalculator.Oriented(template.Paper);
        Preview.Show(
            paper.X.Millimeter,
            paper.Y.Millimeter,
            [.. template.Cards.Select(card => new TemplatePreviewCard(
                new Rect(card.Location.X.Millimeter, card.Location.Y.Millimeter, card.Size.X.Millimeter, card.Size.Y.Millimeter),
                Color.TryParse(card.Color, out Color color) ? color : Colors.Red))]);
    }

    public LayoutViewModel Model { get; }

    /// <summary>Accepts the sides chosen, as the accept button does.</summary>
    public void Accept() => Close(Model.Result());

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Close(null);
        }
    }

    private void OnAccepted(object? sender, EventArgs e) => Accept();

    private void OnCancelled(object? sender, EventArgs e) => Close(null);
}
