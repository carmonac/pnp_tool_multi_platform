using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;

namespace PnpTool.Controls;

/// <summary>
/// A button painted in a colour; pressing it opens the colour dialog to choose another.
/// Original: <c>BotonSelectorColor</c> (the copy in <c>ControlFuente</c> is the same control).
/// </summary>
public class ColorButton : Button
{
    private static readonly Uri Icon = new("avares://PnpTool.Controls/Assets/pick-color.png");

    public ColorButton()
    {
        ToolTip.SetTip(this, StringResources.Get("Click_Para_Color"));
        Padding = new Thickness(2.0);
        Content = new Image { Source = new Bitmap(AssetLoader.Open(Icon)) };
        Load(ArgbColor.White);
    }

    /// <summary>Raised with the stored form of the colour after the user chose one. Original: <c>HayCambioColor</c>.</summary>
    public event Action<string>? ColorChanged;

    /// <summary>Original: <c>ColorSalida</c>.</summary>
    public ArgbColor Color { get; private set; }

    /// <summary>The colour as the project files store it, "A_R_G_B". Original: <c>String_ColorSalida</c>.</summary>
    public string ColorText => Color.ToString();

    /// <summary>Shows a colour without raising <see cref="ColorChanged"/>. Original: <c>CargaValores</c>.</summary>
    public void Load(ArgbColor color)
    {
        Color = color;
        Background = color.ToBrush();
    }

    /// <summary>Shows a stored colour. Text that is not a colour shows white.</summary>
    public void Load(string? colorText) => Load(ArgbColor.Parse(colorText));

    protected override Type StyleKeyOverride => typeof(Button);

    /// <summary>Original: <c>SeleccionarColor</c>.</summary>
    protected override async void OnClick()
    {
        base.OnClick();
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        ArgbColor? chosen = await new ColorPickerDialog(Color).ShowDialog<ArgbColor?>(owner);
        if (chosen != null)
        {
            Accept(chosen.Value);
        }
    }

    /// <summary>Takes a colour as chosen by the user.</summary>
    public void Accept(ArgbColor color)
    {
        Load(color);
        ColorChanged?.Invoke(ColorText);
    }
}
