using Avalonia.Controls;
using Avalonia.Threading;
using PnpTool.App.ViewModels;
using PnpTool.App.Views;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Paper;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using Grid = PnpTool.Core.Projects.Grid;

namespace PnpTool.App.Tests;

[Collection(UserInterface.Name)]
public sealed class LayoutWindowTests(HeadlessFixture fixture)
{
    private static ProjectSettings Style(string file, SettingsGroup group = SettingsGroup.Cards)
    {
        var settings = new ProjectSettings();
        Assert.False(ProjectFileStore.LoadStyle(StyleCatalog.PathOf(group, file), ref settings).Error.HasError);
        return settings;
    }

    private static UnitPoint Millimeters(double x, double y) => new() { X = Unit.FromMillimeter(x), Y = Unit.FromMillimeter(y) };

    /// <summary>Poker cards on an upright A4, one side, rows and columns worked out.</summary>
    private static LayoutViewModel Poker(DeckStatistics? statistics = null)
    {
        ProjectSettings settings = Style("Cartas_Standar.pnps");
        settings.Grid.CardSize = Millimeters(63.5, 88.0);
        settings.Grid.Spacing = Millimeters(0.0, 0.0);
        settings.PrintLayout.Paper.Type = PaperType.A4;
        settings.PrintLayout.Paper.Size = Millimeters(210.0, 297.0);
        settings.PrintLayout.Paper.Landscape = false;
        settings.PrintLayout.DoubleSided = false;
        settings.PrintLayout.BackImage = BackImageMode.NoBack;
        settings.PrintLayout.CalculateAutomatically = true;
        settings.DoublePanel = false;
        return new LayoutViewModel(settings, statistics);
    }

    [Fact]
    public void Shows_the_layout_of_the_project_and_gives_it_back_untouched()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ProjectSettings settings = Style("Cartas_Standar.pnps");
            var model = new LayoutViewModel(settings);

            Assert.Equal(settings.PrintLayout.Paper.Type, model.SelectedPaper.Value);
            Assert.Equal(settings.Grid.CardSize.X.ToDisplayString(), model.CardWidthText);
            Assert.Equal(0, model.SelectedSide);
            Assert.Equal([BackImageMode.NoBack], model.BackOptions.Select(option => option.Value));
            Assert.True(model.IsValid);
            Assert.Null(model.QuestionBeforeAccepting());

            LayoutResult result = model.Result();
            Assert.True(result.Grid.IsEqualTo(settings.Grid));
            Assert.Equal(settings.PrintLayout.BackImage, result.Layout.BackImage);
            Assert.Equal(settings.PrintLayout.Paper.Type, result.Layout.Paper.Type);
            Assert.Equal(settings.DoublePanel, result.DoublePanel);
            Assert.Equal(settings.LockCardSize, result.LockCardSize);
        });
    }

    [Fact]
    public void Rows_and_columns_follow_paper_and_card_when_worked_out()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            LayoutViewModel model = Poker();
            int previews = 0;
            model.PreviewChanged += (_, _) => previews++;
            Assert.Equal(("3", "3"), (model.ColumnsText, model.RowsText));

            model.Landscape = true;
            Assert.Equal(("4", "2"), (model.ColumnsText, model.RowsText));
            Assert.True(previews > 0);

            model.CardWidthText = "100";
            Assert.Equal((2, 2), (model.Columns, model.Rows));

            model.SelectedPaper = model.PaperOptions.First(option => option.Value == PaperType.A3);
            Assert.False(model.IsCustomPaper);
            Assert.Equal("297", model.PaperWidthText);
            Assert.Equal((4, 3), (model.Columns, model.Rows));      // 420 × 297 on its side

            model.SelectedPaper = model.PaperOptions.First(option => option.Value == PaperType.Custom);
            Assert.True(model.IsCustomPaper);
            model.PaperWidthText = "150";
            model.PaperHeightText = "210";
            Assert.Equal((2, 1), (model.Columns, model.Rows));      // 210 × 150 on its side
            Assert.Equal(PaperType.Custom, model.Result().Layout.Paper.Type);
            Assert.Equal(150.0, model.Result().Layout.Paper.Size.X.Millimeter, 2);
        });
    }

    [Fact]
    public void Typed_rows_and_columns_that_do_not_fit_are_marked_and_asked_about()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            LayoutViewModel model = Poker();
            model.AutoCalculate = false;

            model.ColumnsText = "4";
            Assert.False(model.ColumnsFit);
            Assert.True(model.RowsFit);
            Assert.False(model.IsValid);
            Assert.StartsWith("256", model.TotalWidthText);       // 4 × 63.5 + 2 of margin
            Assert.NotNull(model.QuestionBeforeAccepting());
            Assert.Equal(4, model.Result().Layout.Columns);

            model.ColumnsText = "2";
            Assert.True(model.IsValid);
            Assert.Null(model.QuestionBeforeAccepting());
            Assert.False(model.Result().Layout.CalculateAutomatically);

            model.AutoCalculate = true;
            Assert.Equal("3", model.ColumnsText);
        });
    }

    [Fact]
    public void Sides_decide_which_backs_are_offered_and_whether_cards_show_two_faces()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            LayoutViewModel model = Poker();

            model.SelectedSide = 1;     // both sides
            Assert.Equal(4, model.BackOptions.Count);
            Assert.Equal(BackImageMode.SameBack, model.SelectedBack!.Value);
            Assert.False(model.DoublePanel);
            Assert.True(model.Result().Layout.DoubleSided);

            model.SelectedBack = model.BackOptions.First(option => option.Value == BackImageMode.DifferentBack);
            Assert.True(model.DoublePanel);

            model.SelectedSide = 2;     // gutterfold keeps a back per card
            Assert.True(model.IsGutterfold);
            Assert.Equal(2, model.BackOptions.Count);
            Assert.Equal(BackImageMode.DifferentBack, model.SelectedBack!.Value);
            Assert.Equal("2", model.RowsText);                       // an even number of rows
            model.SelectedFold = 1;
            LayoutResult folded = model.Result();
            Assert.False(folded.Layout.DoubleSided);
            Assert.False(folded.Layout.FoldBottom);
            Assert.True(folded.DoublePanel);

            model.SelectedSide = 0;
            Assert.Equal(BackImageMode.NoBack, model.SelectedBack!.Value);
            Assert.False(model.DoublePanel);
            Assert.Equal("3", model.RowsText);
        });
    }

    [Fact]
    public void A_gutterfold_refuses_an_odd_number_of_rows()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            LayoutViewModel model = Poker();
            model.SelectedSide = 2;
            model.AutoCalculate = false;
            var notices = new List<string>();
            model.Notice += notices.Add;

            model.RowsText = "3";

            Assert.Single(notices);
            Assert.Equal("2", model.RowsText);
            Assert.Equal(2, model.Result().Layout.Rows);
        });
    }

    [Fact]
    public void Card_size_keeps_its_proportion_swaps_and_takes_the_sizes_of_the_deck()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var deck = new Deck { Cards = [new Card(), new Card()] };
            deck.Cards[0].Front.Size = Millimeters(40.0, 60.0);
            deck.Cards[1].Front.Size = Millimeters(50.0, 0.0);
            LayoutViewModel model = Poker(new DeckStatistics(deck, BackImageMode.NoBack));

            model.CardWidthText = "50";
            model.CardHeightText = "100";
            model.RatioLocked = true;
            model.CardWidthText = "60";
            Assert.Equal("120", model.CardHeightText);
            model.CardHeightText = "80";
            Assert.Equal("40", model.CardWidthText);
            model.RatioLocked = false;

            model.SwapCardSize();
            Assert.Equal(("80", "40"), (model.CardWidthText, model.CardHeightText));
            Assert.Equal(80.0, model.Result().Grid.CardSize.X.Millimeter, 2);

            model.UseSize(model.Statistics.Maximum);
            Assert.Equal(("50", "60"), (model.CardWidthText, model.CardHeightText));
            model.UseSize(new UnitPoint());                         // no size: 1 a side
            Assert.Equal(("1", "1"), (model.CardWidthText, model.CardHeightText));

            bool locked = model.LockCardSize;
            model.ToggleLockCardSize();
            Assert.Equal(!locked, model.Result().LockCardSize);

            model.SpacingXText = "2";
            model.SpacingYText = "3";
            model.BleedText = "1.5";
            Grid grid = model.Result().Grid;
            Assert.Equal(2.0, grid.Spacing.X.Millimeter, 2);
            Assert.Equal(3.0, grid.Spacing.Y.Millimeter, 2);
            Assert.Equal(1.5, grid.Bleed.Millimeter, 2);
        });
    }

    [Fact]
    public void Groups_with_a_fixed_layout_lock_their_controls()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ProjectSettings settings = Style("Cartas_Standar.pnps");
            settings.Group.Group = SettingsGroup.Tabletop;
            settings.LockCardSize = true;
            var tabletop = new LayoutViewModel(settings);
            Assert.False(tabletop.PaperAndTableOffered);
            Assert.True(tabletop.BacksOffered);
            tabletop.ToggleLockCardSize();
            Assert.True(tabletop.LockCardSize);

            settings.Group.Group = SettingsGroup.Dice;
            Assert.False(new LayoutViewModel(settings).BacksOffered);
            settings.Group.Group = SettingsGroup.Books;
            Assert.False(new LayoutViewModel(settings).BacksOffered);
        });
    }

    [Fact]
    public void Window_accepts_and_cancels()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var owner = new Window();
            owner.Show();
            var window = new LayoutWindow(Poker());
            Task<LayoutResult?> result = window.ShowDialog<LayoutResult?>(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("3", window.FindControl<TextBox>("ColumnsBox")!.Text);
            Assert.False(window.FindControl<ComboBox>("FoldBox")!.IsVisible);
            window.Model.SelectedSide = 2;
            window.Model.SelectedBack = window.Model.BackOptions[1];
            Dispatcher.UIThread.RunJobs();
            Assert.True(window.FindControl<ComboBox>("FoldBox")!.IsVisible);
            Assert.Equal(2, window.FindControl<SheetPreview>("Preview")!.Rows);
            Screenshots.Capture(window, "ventana-disposicion");
            HeadlessFixture.Wait(window.AcceptAsync());
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(BackImageMode.DifferentBack, result.Result!.Layout.BackImage);
            Assert.True(result.Result.DoublePanel);

            // A layout that does not fit asks first: no keeps the window open, yes accepts.
            window = new LayoutWindow(Poker());
            result = window.ShowDialog<LayoutResult?>(owner);
            window.Model.AutoCalculate = false;
            window.Model.RowsText = "5";
            var answers = new Queue<bool?>([false, true]);
            var questions = new List<string>();
            window.Ask = question =>
            {
                questions.Add(question);
                return Task.FromResult(answers.Dequeue());
            };
            HeadlessFixture.Wait(window.AcceptAsync());
            Dispatcher.UIThread.RunJobs();
            Assert.False(result.IsCompleted);
            Screenshots.Capture(window, "ventana-disposicion-no-cabe");
            HeadlessFixture.Wait(window.AcceptAsync());
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, questions.Count);
            Assert.Equal(5, result.Result!.Layout.Rows);

            window = new LayoutWindow(Poker());
            result = window.ShowDialog<LayoutResult?>(owner);
            window.FindControl<PnpTool.Controls.OkCancelButtons>("Buttons")!.CancelButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Null(result.Result);
            owner.Close();
        });
    }

    [Fact]
    public void Layout_button_changes_the_project()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var model = new MainWindowViewModel();
            ProjectSettingsViewModel settings = model.ProjectSettings;
            settings.SelectedGroup = settings.Groups.First(option => option.Group == SettingsGroup.Cards);
            Assert.False(model.SharedBack.IsVisible);
            LayoutViewModel? shown = null;
            model.AskLayout = layout =>
            {
                shown = layout;
                layout.SelectedSide = 1;
                layout.CardWidthText = "50";
                return Task.FromResult<LayoutResult?>(layout.Result());
            };

            HeadlessFixture.Wait(model.EditLayoutAsync());
            Dispatcher.UIThread.RunJobs();

            Assert.NotNull(shown);
            Assert.True(settings.Settings.PrintLayout.DoubleSided);
            Assert.Equal(BackImageMode.SameBack, settings.Settings.PrintLayout.BackImage);
            Assert.Equal(50.0, settings.Settings.Grid.CardSize.X.Millimeter, 2);
            Assert.Equal(SettingsGroup.Custom, settings.Settings.Group.Group);
            Assert.True(model.SharedBack.IsVisible);

            // Cancelling leaves the project as it was.
            model.AskLayout = _ => Task.FromResult<LayoutResult?>(null);
            HeadlessFixture.Wait(model.EditLayoutAsync());
            Assert.Equal(50.0, settings.Settings.Grid.CardSize.X.Millimeter, 2);
        });
    }
}
