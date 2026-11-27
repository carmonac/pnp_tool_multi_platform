using Avalonia.Controls;
using Avalonia.Threading;
using PnpTool.App.ViewModels;
using PnpTool.App.Views;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;

namespace PnpTool.App.Tests;

/// <summary>The window of the settings of books.</summary>
[Collection(UserInterface.Name)]
public sealed class BookSetupTests(HeadlessFixture fixture)
{
    private void Run(Action test) => fixture.Run(LanguageId.EnglishUk, test);

    private static BookSettings Sample() => new()
    {
        ImagePlacement = BookImagePlacement.TowardsSpine,
        Spine = BookSpineType.Right,
        FlipEdge = PrintFlipEdge.Short,
        PerforationMarks = new PerforationMarks
        {
            Type = PerforationMarkType.Outside,
            Margin = Unit.FromPoint(28.3464567),     // 10 mm, as a file may have it
            Count = 4,
            Size = Unit.FromMillimeter(5),
        },
    };

    [Fact]
    public void Settings_come_back_as_they_were_when_nothing_is_touched()
    {
        Run(() =>
        {
            BookSettings book = Sample();

            var model = new BookSetupViewModel(book);

            Assert.Equal(BookImagePlacement.TowardsSpine, model.SelectedPosition.Value);
            Assert.Equal(BookSpineType.Right, model.SelectedSpine.Value);
            Assert.Equal(PrintFlipEdge.Short, model.SelectedEdge.Value);
            Assert.True(model.MarksDrawn);
            Assert.Equal("10", model.MarginText);
            Assert.Equal("4", model.CountText);
            Assert.Equal("5", model.SizeText);
            Assert.True(model.Result().IsEqualTo(book));
        });
    }

    [Fact]
    public void What_is_chosen_is_what_comes_back()
    {
        Run(() =>
        {
            var model = new BookSetupViewModel(new BookSettings());
            Assert.False(model.MarksDrawn);
            Assert.Equal(2, model.PositionOptions.Count);
            Assert.Equal(2, model.SpineOptions.Count);
            Assert.Equal(2, model.EdgeOptions.Count);
            Assert.Equal(3, model.MarkOptions.Count);
            Assert.All(
                model.PositionOptions.Select(option => option.Text)
                    .Concat(model.SpineOptions.Select(option => option.Text))
                    .Concat(model.EdgeOptions.Select(option => option.Text))
                    .Concat(model.MarkOptions.Select(option => option.Text)),
                text => Assert.DoesNotContain(StringResources.MissingPrefix, text));

            model.SelectedPosition = model.PositionOptions[1];
            model.SelectedSpine = model.SpineOptions.First(option => option.Value == BookSpineType.Right);
            model.SelectedEdge = model.EdgeOptions[1];
            model.SelectedMarks = model.MarkOptions.First(option => option.Value == PerforationMarkType.Inside);
            Assert.True(model.MarksDrawn);
            model.MarginText = "12.5";
            model.CountText = "2";
            model.SizeText = "6";

            BookSettings result = model.Result();
            Assert.Equal(BookImagePlacement.TowardsSpine, result.ImagePlacement);
            Assert.Equal(BookSpineType.Right, result.Spine);
            Assert.Equal(PrintFlipEdge.Short, result.FlipEdge);
            Assert.Equal(PerforationMarkType.Inside, result.PerforationMarks.Type);
            Assert.Equal(12.5, result.PerforationMarks.Margin.Millimeter, 6);
            Assert.Equal(2, result.PerforationMarks.Count);
            Assert.Equal(6.0, result.PerforationMarks.Size.Millimeter, 6);
        });
    }

    [Fact]
    public void Project_becomes_custom_only_when_something_changes()
    {
        Run(() =>
        {
            var model = new MainWindowViewModel();
            ProjectSettingsViewModel project = model.ProjectSettings;
            project.SelectedGroup = project.Groups.First(option => option.Group == SettingsGroup.Books);
            project.SelectedStyle = project.Styles[0];
            Assert.True(project.CanEditBooks);
            Func<BookSetupViewModel, BookSettings?> answer = _ => null;
            model.AskBookSetup = book => Task.FromResult(answer(book));

            HeadlessFixture.Wait(model.EditBookAsync());

            // Accepted as it was: the Windows program made the project custom here.
            answer = book => book.Result();
            HeadlessFixture.Wait(model.EditBookAsync());
            Assert.Equal(SettingsGroup.Books, project.Settings.Group.Group);

            // Changed: the Windows program left the style here.
            BookSpineType spine = project.Settings.Book.Spine;
            answer = book =>
            {
                book.SelectedSpine = book.SpineOptions.First(option => option.Value != spine);
                return book.Result();
            };
            HeadlessFixture.Wait(model.EditBookAsync());
            Assert.NotEqual(spine, project.Settings.Book.Spine);
            Assert.Equal(SettingsGroup.Custom, project.Settings.Group.Group);
            Assert.True(model.HasChanged);
        });
    }

    [Fact]
    public void Window_shows_the_marks_when_there_are_and_accepts_or_cancels()
    {
        Run(() =>
        {
            var owner = new Window();
            owner.Show();
            var window = new BookSetupWindow(new BookSetupViewModel(new BookSettings()));
            Task<BookSettings?> result = window.ShowDialog<BookSettings?>(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Book", window.Title);
            Assert.Equal("Spine", window.FindControl<TextBlock>("SpineLabel")!.Text);
            Assert.False(window.FindControl<StackPanel>("MarksPanel")!.IsVisible);

            window.Model.SelectedMarks = window.Model.MarkOptions[1];
            window.Model.CountText = "3";
            Dispatcher.UIThread.RunJobs();
            Assert.True(window.FindControl<StackPanel>("MarksPanel")!.IsVisible);
            Screenshots.Capture(window, "ventana-libros");
            window.Accept();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(3, result.Result!.PerforationMarks.Count);

            window = new BookSetupWindow(new BookSetupViewModel(new BookSettings()));
            result = window.ShowDialog<BookSettings?>(owner);
            window.FindControl<PnpTool.Controls.OkCancelButtons>("Buttons")!.CancelButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Null(result.Result);
            owner.Close();
        });
    }
}
