using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using PnpTool.App.Services;
using PnpTool.App.ViewModels;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;

namespace PnpTool.App.Views;

/// <summary>
/// Where the back image shared by every card is chosen and shown: a click asks for a file, a
/// file can be dropped on it, and its menu turns and mirrors the image.
/// Original: <c>Control_ImagenTrasera</c>.
/// </summary>
public partial class SharedBackPanel : UserControl
{
    public SharedBackPanel()
    {
        InitializeComponent();
        TitleLabel.Text = StringResources.Get("Form_Principal_Label_ImagenTrasera");
        PictureArea.AddHandler(DragDrop.DropEvent, OnDropped);
        PictureArea.AddHandler(ContextRequestedEvent, OnContextRequested);
    }

    private SharedBackViewModel? Back => DataContext as SharedBackViewModel;

    /// <summary>
    /// The menu of the image: the turns and mirrorings and the two ways of opening the file.
    /// Original: <c>CargaMenuContextualImagen</c>, the image menu of the cards without its
    /// first two items and without the colour.
    /// </summary>
    public ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();
        if (Back is not { HasImage: true } back)
        {
            return menu;
        }

        menu.Items.Add(Item("ContextMenuStrip_BotonRotar_Izquierda", () => back.Rotate(new RotationProperties(-90, false, false))));
        menu.Items.Add(Item("ContextMenuStrip_BotonRotar_Derecha", () => back.Rotate(new RotationProperties(90, false, false))));
        menu.Items.Add(Item("ContextMenuStrip_BotonRotar_Vertical", () => back.Rotate(new RotationProperties(0, false, true))));
        menu.Items.Add(Item("ContextMenuStrip_BotonRotar_Horizontal", () => back.Rotate(new RotationProperties(0, true, false))));
        menu.Items.Add(Item("MenuContextualPanelImagen_AbrirCon", () => Platform.Services.OpenFile(back.Face.OriginalPath)));
        menu.Items.Add(Item("MenuContextualPanelImagen_Explorador", () => Platform.Services.RevealFile(back.Face.OriginalPath)));
        return menu;

        static MenuItem Item(string key, Action action)
        {
            var item = new MenuItem { Header = StringResources.Get(key), Tag = key };
            item.Click += (_, _) => action();
            return item;
        }
    }

    /// <summary>Original: <c>PictureBox_Trasera_Click</c>.</summary>
    private async void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Back == null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        IReadOnlyList<string> files = await ImageFiles.PickAsync(top, multiple: false);
        if (files.Count == 1)
        {
            Back.SetImage(files[0]);
        }
    }

    /// <summary>Original: <c>PictureBox_Trasera_Drop</c>.</summary>
    private void OnDropped(object? sender, DragEventArgs e)
    {
        string? file = (e.DataTransfer.TryGetFiles() ?? []).Select(item => item.TryGetLocalPath()).OfType<string>().FirstOrDefault();
        if (file != null)
        {
            Back?.SetImage(file);
        }

        e.Handled = true;
    }

    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        ContextMenu menu = BuildMenu();
        if (menu.Items.Count != 0)
        {
            menu.Open(this);
            e.Handled = true;
        }
    }
}
