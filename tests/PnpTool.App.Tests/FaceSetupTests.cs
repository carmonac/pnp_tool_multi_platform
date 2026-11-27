using Avalonia.Controls;
using Avalonia.Threading;
using PnpTool.App.ViewModels;
using PnpTool.App.Views;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;

namespace PnpTool.App.Tests;

/// <summary>The window of the settings of the fronts and of the backs.</summary>
[Collection(UserInterface.Name)]
public sealed class FaceSetupTests(HeadlessFixture fixture)
{
    /// <summary>A project of 63 x 88 mm cards with 3 mm of bleed and faces as large as the card, free to change.</summary>
    private static ProjectSettings Cards(SettingsGroup group = SettingsGroup.Cards)
    {
        var settings = new ProjectSettings { LockCardSize = false };
        settings.Group.Group = group;
        settings.Grid.CardSize = new UnitPoint { X = Unit.FromMillimeter(63), Y = Unit.FromMillimeter(88) };
        settings.Grid.Spacing = new UnitPoint { X = Unit.Zero, Y = Unit.Zero };
        settings.Grid.Bleed = Unit.FromMillimeter(3);
        foreach (FaceSettings face in new[] { settings.Front, settings.Back })
        {
            face.Size = new UnitPoint(settings.Grid.CardSize);
            face.CutLine.LineType = CutLineType.Normal;
            face.CutLine.Thickness = Unit.FromPoint(0.5);
            face.CutLine.Gap = 4;
        }

        settings.PrintLayout.DoubleSided = true;
        settings.PrintLayout.BackImage = BackImageMode.DifferentBack;
        return settings;
    }

    private void Run(Action test) => fixture.Run(LanguageId.EnglishUk, test);

    [Fact]
    public void Settings_come_back_as_they_were_when_nothing_is_touched()
    {
        Run(() =>
        {
            ProjectSettings settings = Cards();
            settings.Front.Effect = ImageEffectType.RoundedCorners;
            settings.Front.RoundedCornerSize = 12;
            settings.Front.PaintBackground = true;
            settings.Front.BackgroundFillColor = "255_10_20_30";
            settings.Front.ShowOuterFrame = true;
            settings.Front.OuterFrameSize = Unit.FromMillimeter(2);
            settings.Front.OuterFrameColor = "255_1_2_3";
            settings.Front.Locked = true;
            settings.Back.Locked = false;
            settings.BackOffset = new UnitPoint { X = Unit.FromMillimeter(-1.5), Y = Unit.FromMillimeter(2) };
            settings.BackRotation = -0.25;

            var front = new FaceSetupViewModel(settings, FaceType.Front);
            Assert.Equal("Front", front.Title);
            Assert.Equal("63", front.WidthText);
            Assert.Equal("88", front.HeightText);
            Assert.Equal("12", front.EffectSizeText);
            Assert.True(front.EffectHasSize);
            Assert.Equal("0.5", front.CutLineThicknessText);
            Assert.True(front.Result().Face.IsEqualTo(settings.Front));

            var back = new FaceSetupViewModel(settings, FaceType.Back);
            Assert.Equal("Back", back.Title);
            Assert.Equal("-1.5", back.OffsetXText);
            Assert.Equal("-0.25", back.RotationText);
            FaceSetupResult result = back.Result();
            Assert.True(result.Face.IsEqualTo(settings.Back));
            Assert.Equal(-1.5, result.BackOffset.X.Millimeter, 6);
            Assert.Equal(2.0, result.BackOffset.Y.Millimeter, 6);
            Assert.Equal(-0.25, result.BackRotation);
        });
    }

    [Fact]
    public void Size_keeps_its_proportion_with_the_padlock_and_cannot_pass_the_card()
    {
        Run(() =>
        {
            var model = new FaceSetupViewModel(Cards(), FaceType.Front);
            Assert.True(model.SizeOffered);
            Assert.True(model.SizeEnabled);
            Assert.False(model.RatioLocked);

            model.WidthText = "31.5";
            Assert.Equal("88", model.HeightText);
            model.RatioLocked = true;
            model.HeightText = "44";
            Assert.Equal(15.75, Unit.FromDisplayString(model.WidthText).Millimeter, 2);
            model.SwapSize();
            Assert.Equal("44", model.WidthText);
            Assert.Null(model.Problem());
            FaceSettings face = model.Result().Face;
            Assert.Equal(44.0, face.Size.X.Millimeter, 2);
            Assert.False(face.Locked);      // the file keeps "padlock open"

            model.RatioLocked = false;
            model.WidthText = "70";
            Assert.Equal("Front Width Scale size is not correct", model.Problem());
            model.HeightText = "90";
            Assert.Equal(2, model.Problem()!.Split(Environment.NewLine).Length);

            var back = new FaceSetupViewModel(Cards(), FaceType.Back) { HeightText = "88.5" };
            Assert.StartsWith("Back", back.Problem());
        });
    }

    [Fact]
    public void Size_tied_to_the_card_follows_the_effect()
    {
        Run(() =>
        {
            ProjectSettings settings = Cards();
            settings.LockCardSize = true;
            settings.Front.Size = new UnitPoint { X = Unit.FromMillimeter(20), Y = Unit.FromMillimeter(20) };

            var model = new FaceSetupViewModel(settings, FaceType.Front);
            Assert.False(model.SizeEnabled);
            Assert.Equal("63", model.WidthText);

            model.SelectedEffect = model.EffectOptions.First(option => option.Value == ImageEffectType.Mirror);
            Assert.Equal("57", model.WidthText);
            Assert.Equal("82", model.HeightText);
            Assert.Equal(57.0, model.Result().Face.Size.X.Millimeter, 2);

            model.SelectedEffect = model.EffectOptions.First(option => option.Value == ImageEffectType.Blur);
            Assert.Equal("88", model.HeightText);
            Assert.False(model.EffectHasSize);
        });
    }

    [Fact]
    public void Effects_offered_depend_on_the_kind_of_project()
    {
        Run(() =>
        {
            var cards = new FaceSetupViewModel(Cards(), FaceType.Front);
            Assert.Contains(cards.EffectOptions, option => option.Value == ImageEffectType.Mirror);
            Assert.DoesNotContain(cards.EffectOptions, option => option.Value == ImageEffectType.CreateOutline);
            Assert.Equal(ImageEffectType.None, cards.SelectedEffect.Value);

            var tickets = new FaceSetupViewModel(Cards(SettingsGroup.Tickets), FaceType.Front);
            Assert.DoesNotContain(tickets.EffectOptions, option => option.Value == ImageEffectType.Mirror);

            var soriano = new FaceSetupViewModel(Cards(SettingsGroup.Soriano), FaceType.Front);
            Assert.Equal(ImageEffectType.Mirror, Assert.Single(soriano.EffectOptions).Value);

            cards.SelectedEffect = cards.EffectOptions.First(option => option.Value == ImageEffectType.HexagonToken);
            cards.EffectSizeText = "30";
            FaceSettings face = cards.Result().Face;
            Assert.Equal(ImageEffectType.HexagonToken, face.Effect);
            Assert.Equal(30, face.RoundedCornerSize);
        });
    }

    [Fact]
    public void Background_colour_is_chosen_here_only_when_it_is_one_for_all()
    {
        Run(() =>
        {
            var model = new FaceSetupViewModel(Cards(), FaceType.Front);
            Assert.True(model.BackgroundOffered);
            Assert.False(model.BackgroundColorOffered);

            model.PaintBackground = true;
            Assert.True(model.BackgroundColorOffered);
            model.BackgroundColor = "255_200_100_50";
            model.SelectedBackground = model.BackgroundOptions.First(option => option.Value == CardBackgroundType.SingleColor);
            Assert.False(model.BackgroundColorOffered);

            FaceSettings face = model.Result().Face;
            Assert.True(face.PaintBackground);
            Assert.Equal(CardBackgroundType.SingleColor, face.BackgroundEffect);
            Assert.Equal("255_200_100_50", face.BackgroundFillColor);
        });
    }

    [Fact]
    public void Frame_gets_a_size_when_turned_on_and_a_mirror_frame_has_no_colour()
    {
        Run(() =>
        {
            ProjectSettings settings = Cards();
            settings.Front.OuterFrameColor = "255_9_9_9";
            var model = new FaceSetupViewModel(settings, FaceType.Front);
            Assert.False(model.FrameColorOffered);

            model.ShowFrame = true;
            Assert.Equal("3", model.FrameSizeText);
            Assert.True(model.FrameColorOffered);

            model.SelectedFrame = model.FrameOptions.First(option => option.Value == OuterFrameEffect.Mirror);
            Assert.False(model.FrameColorOffered);
            FaceSettings mirror = model.Result().Face;
            Assert.Equal(OuterFrameEffect.Mirror, mirror.OuterFrameEffect);
            Assert.Equal(string.Empty, mirror.OuterFrameColor);
            Assert.Equal(3.0, mirror.OuterFrameSize.Millimeter, 2);

            model.SelectedFrame = model.FrameOptions.First(option => option.Value == OuterFrameEffect.SolidColor);
            Assert.Equal("255_9_9_9", model.Result().Face.OuterFrameColor);
        });
    }

    [Fact]
    public void Mirror_frame_is_refused_with_space_between_the_cards()
    {
        Run(() =>
        {
            ProjectSettings settings = Cards();
            settings.Grid.Spacing = new UnitPoint { X = Unit.FromMillimeter(2), Y = Unit.Zero };
            var model = new FaceSetupViewModel(settings, FaceType.Front) { ShowFrame = true };
            var said = new List<string>();
            model.Notice += said.Add;

            model.SelectedFrame = model.FrameOptions.First(option => option.Value == OuterFrameEffect.Mirror);

            Assert.StartsWith("The mirror effect for FRAME", Assert.Single(said));
            Assert.False(model.ShowFrame);
            Assert.Equal(OuterFrameEffect.SolidColor, model.SelectedFrame.Value);
            Assert.Equal(OuterFrameEffect.SolidColor, model.Result().Face.OuterFrameEffect);
        });
    }

    [Fact]
    public void Cut_lines_show_what_each_kind_needs()
    {
        Run(() =>
        {
            ProjectSettings settings = Cards();
            settings.Front.CutLine.Gap = 0;
            var model = new FaceSetupViewModel(settings, FaceType.Front);
            Assert.Equal(6, model.CutLineOptions.Count);
            Assert.True(model.CutLineDrawn);
            Assert.True(model.CutLineGapOffered);
            Assert.False(model.CutLinesToEdgeOffered);
            Assert.Equal("Separate (pt)", model.CutLineGapLabel);

            model.SelectedCutLine = model.CutLineOptions.First(option => option.Value == CutLineType.Cross);
            Assert.True(model.CutLinesToEdgeOffered);
            Assert.Equal("Size (mm)", model.CutLineGapLabel);
            Assert.Equal("2", model.CutLineGapText);     // crosses of no size get one

            model.SelectedCutLine = model.CutLineOptions.First(option => option.Value == CutLineType.Dot);
            Assert.True(model.CutLineDrawn);
            Assert.False(model.CutLineGapOffered);

            model.SelectedCutLine = model.CutLineOptions.First(option => option.Value == CutLineType.None);
            Assert.False(model.CutLineDrawn);

            model.SelectedCutLine = model.CutLineOptions.First(option => option.Value == CutLineType.Cross);
            model.CutLinesToEdge = true;
            model.CutLineThicknessText = "1.25";
            model.CutLineGapText = "5";
            model.CutLineColor = "255_255_0_0";
            CutLineSettings line = model.Result().Face.CutLine;
            Assert.Equal(CutLineType.Cross, line.LineType);
            Assert.True(line.CrossLinesToEdge);
            Assert.Equal(1.25, line.Thickness.Point, 6);
            Assert.Equal(5, line.Gap);
            Assert.Equal("255_255_0_0", line.Color);
        });
    }

    [Fact]
    public void Advanced_templates_offer_less()
    {
        Run(() =>
        {
            ProjectSettings settings = Cards(SettingsGroup.AdvancedStyles);
            settings.Front.CutLine.LineType = CutLineType.Cross;
            settings.Front.CutLine.Thickness = Unit.Zero;
            settings.Front.OuterFrameEffect = OuterFrameEffect.Mirror;

            var model = new FaceSetupViewModel(settings, FaceType.Front);

            Assert.False(model.SizeOffered);
            Assert.False(model.BackgroundOffered);
            Assert.True(model.FrameOffered);
            Assert.Equal(OuterFrameEffect.SolidColor, Assert.Single(model.FrameOptions).Value);
            Assert.Equal(2, model.CutLineOptions.Count);
            Assert.False(model.CutLinesToEdgeOffered);
            Assert.Equal("0.4", model.CutLineThicknessText);
            FaceSettings face = model.Result().Face;
            Assert.Equal(OuterFrameEffect.SolidColor, face.OuterFrameEffect);
            Assert.Equal(0.4, face.CutLine.Thickness.Point, 6);
        });
    }

    [Fact]
    public void Backs_are_moved_and_turned_and_have_no_cut_lines_of_their_own_in_gutterfold()
    {
        Run(() =>
        {
            ProjectSettings settings = Cards();
            var back = new FaceSetupViewModel(settings, FaceType.Back);
            Assert.True(back.BackPlaceOffered);
            Assert.True(back.CutLinesOffered);
            Assert.False(new FaceSetupViewModel(settings, FaceType.Front).BackPlaceOffered);

            back.OffsetXText = "-";
            back.OffsetYText = "0.75";
            back.RotationText = "-";
            FaceSetupResult result = back.Result();
            Assert.Equal(0.0, result.BackOffset.X.Millimeter);
            Assert.Equal(0.75, result.BackOffset.Y.Millimeter, 6);
            Assert.Equal(0.0, result.BackRotation);
            back.RotationText = "1.5";
            Assert.Equal(1.5, back.Result().BackRotation);

            settings.PrintLayout.DoubleSided = false;
            Assert.False(new FaceSetupViewModel(settings, FaceType.Back).CutLinesOffered);
            Assert.True(new FaceSetupViewModel(settings, FaceType.Front).CutLinesOffered);

            var tabletop = new FaceSetupViewModel(Cards(SettingsGroup.Tabletop), FaceType.Back);
            Assert.False(tabletop.BackPlaceOffered);
            Assert.False(tabletop.BackgroundOffered);
            Assert.False(tabletop.FrameOffered);
            Assert.False(tabletop.CutLinesOffered);
            Assert.True(tabletop.EffectOffered);
        });
    }

    [Fact]
    public void Project_becomes_custom_only_when_a_face_changes()
    {
        Run(() =>
        {
            var model = new MainWindowViewModel();
            ProjectSettingsViewModel project = model.ProjectSettings;
            project.SelectedGroup = project.Groups.First(option => option.Group == SettingsGroup.Cards);
            project.SelectedStyle = project.Styles.First(style => style.FileName == "Cartas_Euro.pnps");
            var asked = new List<FaceSetupViewModel>();
            Func<FaceSetupViewModel, FaceSetupResult?> answer = _ => null;
            model.AskFaceSetup = face =>
            {
                asked.Add(face);
                return Task.FromResult(answer(face));
            };

            // Cancelled.
            HeadlessFixture.Wait(model.EditFaceAsync(FaceType.Front));
            Assert.False(Assert.Single(asked).IsBack);
            Assert.Equal(SettingsGroup.Cards, project.Settings.Group.Group);

            // Accepted as it was: the Windows program made the project custom here.
            answer = face => face.Result();
            HeadlessFixture.Wait(model.EditFaceAsync(FaceType.Front));
            HeadlessFixture.Wait(model.EditFaceAsync(FaceType.Back));
            Assert.Equal(SettingsGroup.Cards, project.Settings.Group.Group);

            // The backs moved: the Windows program left the style here.
            answer = face =>
            {
                face.OffsetXText = "1";
                return face.Result();
            };
            HeadlessFixture.Wait(model.EditFaceAsync(FaceType.Back));
            Assert.Equal(1.0, project.Settings.BackOffset.X.Millimeter, 6);
            Assert.Equal(SettingsGroup.Custom, project.Settings.Group.Group);
            Assert.True(model.HasChanged);

            // A front changed.
            project.SelectedGroup = project.Groups.First(option => option.Group == SettingsGroup.Cards);
            project.SelectedStyle = project.Styles.First(style => style.FileName == "Cartas_Euro.pnps");
            answer = face =>
            {
                face.PaintBackground = !face.PaintBackground;
                return face.Result();
            };
            bool painted = project.Settings.Front.PaintBackground;
            HeadlessFixture.Wait(model.EditFaceAsync(FaceType.Front));
            Assert.Equal(!painted, project.Settings.Front.PaintBackground);
            Assert.Equal(SettingsGroup.Custom, project.Settings.Group.Group);
        });
    }

    [Fact]
    public void Window_shows_the_settings_and_accepts_or_says_what_is_wrong()
    {
        Run(() =>
        {
            var owner = new Window();
            owner.Show();
            ProjectSettings settings = Cards();
            settings.Back.ShowOuterFrame = true;
            settings.Back.OuterFrameSize = Unit.FromMillimeter(3);
            settings.Back.PaintBackground = true;
            settings.Back.CutLine.LineType = CutLineType.Cross;
            var window = new FaceSetupWindow(new FaceSetupViewModel(settings, FaceType.Back));
            var said = new List<string>();
            window.ShowMessage = message =>
            {
                said.Add(message);
                return Task.CompletedTask;
            };
            Task<FaceSetupResult?> result = window.ShowDialog<FaceSetupResult?>(owner);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Back", window.Title);
            Assert.Equal("63", window.FindControl<TextBox>("WidthBox")!.Text);
            Assert.True(window.FindControl<Avalonia.Controls.Grid>("BackPanel")!.IsVisible);
            Assert.True(window.FindControl<CheckBox>("ToEdgeCheck")!.IsVisible);
            Assert.Equal("Outer frame", window.FindControl<CheckBox>("FrameCheck")!.Content);
            Assert.False(window.FindControl<TextBox>("EffectSizeBox")!.IsVisible);
            Screenshots.Capture(window, "ventana-cara-trasera");

            window.Model.HeightText = "99";
            HeadlessFixture.Wait(window.AcceptAsync());
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Back height Scale size is not correct", Assert.Single(said));
            Assert.False(result.IsCompleted);

            window.Model.HeightText = "80";
            window.Model.RotationText = "180";
            HeadlessFixture.Wait(window.AcceptAsync());
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(80.0, result.Result!.Face.Size.Y.Millimeter, 2);
            Assert.Equal(180.0, result.Result.BackRotation);

            var front = new FaceSetupWindow(new FaceSetupViewModel(Cards(), FaceType.Front));
            Task<FaceSetupResult?> cancelled = front.ShowDialog<FaceSetupResult?>(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.False(front.FindControl<Avalonia.Controls.Grid>("BackPanel")!.IsVisible);
            Screenshots.Capture(front, "ventana-cara-frontal");
            front.FindControl<PnpTool.Controls.OkCancelButtons>("Buttons")!.CancelButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Null(cancelled.Result);
            owner.Close();
        });
    }
}
