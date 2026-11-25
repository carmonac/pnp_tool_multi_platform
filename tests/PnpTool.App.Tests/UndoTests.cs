using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using ImageMagick;
using PnpTool.App.ViewModels;
using PnpTool.App.ViewModels.Panel;
using PnpTool.App.Views;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Primitives;

namespace PnpTool.App.Tests;

[Collection(UserInterface.Name)]
public sealed class UndoTests(HeadlessFixture fixture)
{
    private static string Picture(string folder, string name, uint width = 120, uint height = 168)
    {
        string path = Path.Combine(folder, name);
        using var image = new MagickImage(new MagickColor("#3060C0"), width, height);
        image.Density = new Density(300);
        image.Write(path);
        return path;
    }

    private ImagePanelViewModel PanelWith(int cards, bool isDouble = false)
    {
        string folder = fixture.NewFolder();
        var panel = new ImagePanelViewModel { IsDouble = isDouble };
        string[] files = [.. Enumerable.Range(1, cards * (isDouble ? 2 : 1)).Select(i => Picture(folder, "card" + i + ".png"))];
        HeadlessFixture.Wait(panel.AddImagesAsync(files));
        return panel;
    }

    /// <summary>Everything undo is meant to bring back, as text.</summary>
    private static string State(ImagePanelViewModel panel) => string.Join(
        " | ",
        panel.Cards.Select(card =>
            card.Index + ":" + Path.GetFileName(card.Card.Front.Path) + "/" + Path.GetFileName(card.Card.Back.Path)
            + " x" + card.Copies + " r" + card.Card.Front.Rotation.Angle + (card.Card.Front.Rotation.FlipHorizontal ? "h" : string.Empty)
            + " " + card.Card.Front.ImageKind + " " + card.Card.Front.BackgroundColor + " " + card.Title));

    /// <summary>Does something, then checks that undo brings the panel back and redo does it again.</summary>
    private static void UndoesAndRedoes(ImagePanelViewModel panel, Action action)
    {
        string before = State(panel);
        int steps = panel.History.UndoNames.Count;

        action();

        string after = State(panel);
        Assert.NotEqual(before, after);
        Assert.Equal(Math.Min(steps + 1, OperationStack.Capacity), panel.History.UndoNames.Count);
        Assert.True(panel.History.Undo());
        Assert.Equal(before, State(panel));
        Assert.True(panel.History.Redo());
        Assert.Equal(after, State(panel));
    }

    [Fact]
    public void Adding_cards_is_undone_as_one_step()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(3);
            Assert.Equal(["add cards"], panel.History.UndoNames);
            string folder = fixture.NewFolder();

            UndoesAndRedoes(panel, () => HeadlessFixture.Wait(panel.AddImagesAsync([Picture(folder, "n1.png"), Picture(folder, "n2.png")], 1)));
            UndoesAndRedoes(panel, () => panel.AddBlankCard());
            UndoesAndRedoes(panel, () => panel.AddEmptyCard(0));
            UndoesAndRedoes(panel, () => HeadlessFixture.Wait(panel.DropFilesAsync([Picture(folder, "dropped.png")], onCard: 2)));

            Assert.Equal(8, panel.PanelCount);
            Assert.Equal("Paneles: 8 / Cartas: 8", panel.CountsText);
            while (panel.History.Undo())
            {
            }

            Assert.Equal(0, panel.PanelCount);
            Assert.Equal("Paneles: 0 / Cartas: 0", panel.CountsText);
        });
    }

    [Fact]
    public void Deleting_moving_and_cloning_are_undone()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(5);
            panel.Cards[1].Copies = 3;   // typed in the card's own box: not a step
            Assert.Single(panel.History.UndoNames);

            UndoesAndRedoes(panel, () => panel.Move(0, 3));
            UndoesAndRedoes(panel, () => panel.Clone(2));
            UndoesAndRedoes(panel, () => panel.DeleteAt(1));
            panel.ClickCard(0, shift: false, command: false);
            panel.ClickCard(3, shift: false, command: true);
            UndoesAndRedoes(panel, () => Assert.Equal(2, panel.DeleteSelected()));

            // Cards that come back show their pictures again and are not selected.
            panel.History.Undo();
            HeadlessFixture.Wait(panel.ImagesLoaded);
            Assert.All(panel.Cards, card => Assert.NotNull(card.FrontImage));
            Assert.Equal(0, panel.SelectedPanelCount);
            Assert.Equal([0, 1, 2, 3, 4], panel.Cards.Select(card => card.Index));
        });
    }

    [Fact]
    public void Changes_to_faces_and_copies_are_undone()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(4, isDouble: true);
            string other = Picture(fixture.NewFolder(), "other.png", 200, 100);
            CardFace image = ImageInspector.Current.GetImageProperties(other);

            UndoesAndRedoes(panel, () => panel.RotateFace(0, FaceType.Front, new RotationProperties(90, false, false)));
            UndoesAndRedoes(panel, () => panel.RotateFace(1, FaceType.Front, new RotationProperties(0, true, false)));
            UndoesAndRedoes(panel, () => panel.ChangeFaceImage(1, FaceType.Front, other));
            UndoesAndRedoes(panel, () => panel.ChangeFaceToBlank(2, FaceType.Front));
            UndoesAndRedoes(panel, () => panel.ChangeImages(image, FaceType.Back, 0, 4));
            UndoesAndRedoes(panel, () => panel.SetFaceBackground(3, FaceType.Front, new ArgbColor(9, 9, 9)));
            UndoesAndRedoes(panel, () => panel.SetCopies(2, 7));
            panel.ClickFace(0, FaceType.Front);
            panel.ClickFace(3, FaceType.Front);
            UndoesAndRedoes(panel, () => Assert.True(panel.SwapFaces()));

            // The faces stay selected through undo, and the pictures follow the faces.
            panel.History.Undo();
            HeadlessFixture.Wait(panel.ImagesLoaded);
            Assert.True(panel.Cards[0].IsFrontSelected);
            Assert.True(panel.Cards[0].Card.Front.Selected);
            Assert.Equal(new Avalonia.PixelSize(96, 69), panel.Cards[0].FrontImage!.PixelSize);   // still turned
        });
    }

    [Fact]
    public void What_changes_nothing_is_not_a_step_and_a_new_project_forgets_the_steps()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(3);
            int steps = panel.History.UndoNames.Count;

            panel.Move(1, 1);
            panel.DeleteAt(9);
            Assert.Equal(0, panel.DeleteSelected());
            Assert.False(panel.SwapFaces());
            panel.ClickCard(0, shift: false, command: false);
            panel.ClickFace(1, FaceType.Front);
            panel.ChangeImages(new CardFace(), FaceType.Front, 2, 2);   // an empty range
            Assert.Equal(steps, panel.History.UndoNames.Count);

            panel.Clear();
            Assert.False(panel.History.CanUndo);
            Assert.False(panel.History.Undo());
            Assert.Equal(0, panel.PanelCount);
        });
    }

    [Fact]
    public void Temporary_copies_of_images_last_while_undo_can_bring_the_card_back()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string temp = AppSettings.TempFolder;
            AppSettings.TempFolder = fixture.NewFolder() + Path.DirectorySeparatorChar;
            try
            {
                ImagePanelViewModel panel = PanelWith(2);
                string original = panel.Cards[0].Card.Front.Path;
                string copy = Path.Combine(AppSettings.TempFolder, "copy.png");
                File.Copy(original, copy);
                panel.Cards[0].Card.Front.Path = copy;   // as after converting the image for the project

                panel.DeleteAt(0);
                Assert.True(File.Exists(copy));
                panel.History.Undo();
                Assert.Equal(copy, panel.Cards[0].Card.Front.Path);

                panel.DeleteAt(0);
                panel.Clear();
                Assert.False(File.Exists(copy));
                Assert.True(File.Exists(original));
            }
            finally
            {
                AppSettings.TempFolder = temp;
            }
        });
    }

    [Fact]
    public void Only_ten_steps_are_kept()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(2);
            for (int i = 0; i < 14; i++)
            {
                panel.AddBlankCard();
            }

            int undone = 0;
            while (panel.History.Undo())
            {
                undone++;
            }

            Assert.Equal(10, undone);
            Assert.Equal(6, panel.PanelCount);
        });
    }

    [Fact]
    public void Undo_button_and_keys_of_the_main_window()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            AppSettings.StartMaximized = false;
            AppSettings.WindowSize = new PointI(1280, 800);
            var model = new MainWindowViewModel();
            var window = new MainWindow { DataContext = model };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            System.Windows.Input.ICommand undo = model.QuickAccess.First(button => button.Key == "QATButton_Undo").Command;
            Assert.False(undo.CanExecute(null));

            model.ImagePanel.AddBlankCard();
            model.ImagePanel.AddBlankCard();
            model.ImagePanel.AddEmptyCard();
            Assert.True(undo.CanExecute(null));

            undo.Execute(null);
            Assert.Equal(2, model.ImagePanel.PanelCount);

            // The system's shortcuts: Ctrl+Z and Ctrl+Y here, Cmd+Z and Shift+Cmd+Z on macOS.
            var keys = Avalonia.Application.Current!.PlatformSettings!.HotkeyConfiguration;
            KeyGesture undoKey = keys.Undo[0];
            KeyGesture redoKey = keys.Redo[0];
            window.KeyPressQwerty(ToPhysical(undoKey.Key), ToRaw(undoKey.KeyModifiers));
            Assert.Equal(1, model.ImagePanel.PanelCount);
            window.KeyPressQwerty(ToPhysical(redoKey.Key), ToRaw(redoKey.KeyModifiers));
            Assert.Equal(2, model.ImagePanel.PanelCount);

            // Delete removes the selected cards, and that can be undone too.
            model.ImagePanel.ClickCard(0, shift: false, command: false);
            window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
            Assert.Equal(1, model.ImagePanel.PanelCount);
            model.Undo();
            Assert.Equal(2, model.ImagePanel.PanelCount);
            model.Redo();
            Assert.Equal(1, model.ImagePanel.PanelCount);
            window.Close();

            static PhysicalKey ToPhysical(Key key) => key switch
            {
                Key.Z => PhysicalKey.Z,
                Key.Y => PhysicalKey.Y,
                _ => throw new NotSupportedException(key.ToString()),
            };

            static RawInputModifiers ToRaw(KeyModifiers modifiers) =>
                (modifiers.HasFlag(KeyModifiers.Control) ? RawInputModifiers.Control : 0)
                | (modifiers.HasFlag(KeyModifiers.Meta) ? RawInputModifiers.Meta : 0)
                | (modifiers.HasFlag(KeyModifiers.Shift) ? RawInputModifiers.Shift : 0);
        });
    }
}
