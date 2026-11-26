using Avalonia.Controls;
using Avalonia.Threading;
using PnpTool.App.ViewModels;
using PnpTool.App.Views;
using PnpTool.Core;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Paper;
using PnpTool.Core.Projects;
using PnpTool.Core.Templates;

namespace PnpTool.App.Tests;

[Collection(UserInterface.Name)]
public sealed class TemplateEditorTests(HeadlessFixture fixture)
{
    /// <summary>Runs a test and then takes away the templates it left in the folder.</summary>
    private void Run(Action<TemplateEditorViewModel, List<string>> test) =>
        fixture.Run(LanguageId.Spanish, () =>
        {
            string[] before = Directory.GetFiles(AppPaths.AdvancedStylesFolder);
            try
            {
                var editor = new TemplateEditorViewModel();
                var notices = new List<string>();
                editor.Notice += notices.Add;
                test(editor, notices);
            }
            finally
            {
                foreach (string file in Directory.GetFiles(AppPaths.AdvancedStylesFolder).Except(before))
                {
                    File.Delete(file);
                }
            }
        });

    private static void AddCard(TemplateEditorViewModel editor, string x, string y, string width, string height, int rotation = 0)
    {
        editor.LocationXText = x;
        editor.LocationYText = y;
        editor.SizeXText = width;
        editor.SizeYText = height;
        editor.Rotation = rotation;
        editor.Add();
    }

    private static AdvancedTemplate Read(string fileName)
    {
        var template = new AdvancedTemplate();
        Assert.False(ProjectFileStore.LoadAdvancedTemplate(AppSettings.AdvancedStylesFolder + fileName, ref template).HasError);
        return template;
    }

    [Fact]
    public void Lists_the_templates_and_shows_the_one_chosen()
    {
        Run((editor, notices) =>
        {
            Assert.True(editor.IsNew);
            Assert.Equal("Nuevo", editor.Templates[0].Text);
            Assert.Equal(2, editor.Templates.Count);        // the bundled template
            Assert.Empty(editor.Slots);

            editor.SelectedTemplate = editor.Templates[1];

            AdvancedTemplate bundled = Read("1.pnpa");
            Assert.False(editor.IsNew);
            Assert.Equal(bundled.Name, editor.Name);
            Assert.Equal(bundled.Cards.Length, editor.Slots.Count);
            Assert.Equal(bundled.Paper.Type, editor.Paper.Selected.Value);
            Assert.Equal(bundled.Paper.Landscape, editor.Paper.Landscape);
            Assert.Equal("#1", editor.Slots[0].Order);
            Assert.Contains("Ubicación (X:10 Y:8) - Tamaño (X:120 Y:70) º:90", editor.Slots[0].Text);
            Assert.All(editor.Slots, slot => Assert.True(editor.Fits(slot)));
            Assert.Empty(notices);

            editor.SelectedTemplate = editor.Templates[0];
            Assert.Empty(editor.Slots);
            Assert.Equal(string.Empty, editor.Name);
        });
    }

    [Fact]
    public void Cards_are_added_removed_and_moved()
    {
        Run((editor, notices) =>
        {
            int previews = 0;
            editor.PreviewChanged += (_, _) => previews++;

            editor.Add();                                   // nothing typed
            Assert.Empty(editor.Slots);
            Assert.Equal([StringResources.Get("faltan_datos")], notices);

            AddCard(editor, "10", "10", "60", "90");
            AddCard(editor, "80", "10", "90", "60", rotation: 90);
            AddCard(editor, "10", "110", "60", "90");
            Assert.Equal(3, editor.Slots.Count);
            Assert.Equal(90, editor.Slots[1].Card.Rotation);
            Assert.Equal(80.0, editor.Slots[1].Card.Location.X.Millimeter, 2);
            Assert.NotEqual("#FFFFFF", editor.Slots[0].Card.Color);     // a colour of its own
            Assert.Single(notices);
            Assert.True(previews >= 3);

            AddCard(editor, "180", "10", "60", "90");       // beyond the right edge of an A4
            Assert.Equal(4, editor.Slots.Count);
            Assert.StartsWith("X ", notices[^1]);
            Assert.False(editor.Fits(editor.Slots[3]));
            editor.Paper.Landscape = true;                  // on its side it fits across
            Assert.True(editor.Fits(editor.Slots[3]));
            editor.Paper.Landscape = false;

            editor.SelectedSlot = editor.Slots[3];
            editor.RemoveSelected();
            Assert.Equal(3, editor.Slots.Count);
            Assert.Same(editor.Slots[2], editor.SelectedSlot);

            TemplateSlotViewModel last = editor.Slots[2];
            editor.MoveUp();
            editor.MoveUp();
            editor.MoveUp();                                // already first
            Assert.Same(last, editor.Slots[0]);
            Assert.Equal(["#1", "#2", "#3"], editor.Slots.Select(slot => slot.Order));
            Assert.Same(last, editor.SelectedSlot);
            editor.MoveDown();
            Assert.Same(last, editor.Slots[1]);
        });
    }

    [Fact]
    public void Saves_a_new_template_changes_it_clones_it_and_deletes_it()
    {
        Run((editor, notices) =>
        {
            int changes = 0;
            editor.TemplatesChanged += (_, _) => changes++;
            AddCard(editor, "10", "10", "60", "90");
            AddCard(editor, "80", "10", "60", "90");

            editor.Save();                                  // a new template needs a name
            Assert.Equal([StringResources.Get("faltan_datos")], notices);
            Assert.Equal(2, editor.Templates.Count);

            editor.Name = "Dos cartas";
            editor.BleedText = "2";
            editor.Paper.Selected = editor.Paper.Options.First(option => option.Value == PaperType.A5);
            editor.Paper.Landscape = true;
            editor.Save();

            Assert.Equal(1, changes);
            Assert.Equal(3, editor.Templates.Count);
            Assert.Equal("Dos cartas", editor.SelectedTemplate.Text);
            string fileName = editor.SelectedTemplate.FileName;
            Assert.Equal("2.pnpa", fileName);
            AdvancedTemplate saved = Read(fileName);
            Assert.Equal("Dos cartas", saved.Name);
            Assert.Equal(2, saved.Cards.Length);
            Assert.Equal(2.0, saved.Bleed.Millimeter, 2);
            Assert.Equal(PaperType.A5, saved.Paper.Type);
            Assert.True(saved.Paper.Landscape);
            Assert.Equal(80.0, saved.Cards[1].Location.X.Millimeter, 2);
            Assert.Equal(editor.Slots[0].Card.Color, saved.Cards[0].Color);  // colours survive the file
            Assert.Equal(2, editor.Slots.Count);

            // Saving again writes over the same file.
            AddCard(editor, "150", "10", "50", "50");
            editor.Name = "Tres cartas";
            editor.Save();
            Assert.Equal(fileName, editor.SelectedTemplate.FileName);
            Assert.Equal(3, Read(fileName).Cards.Length);
            Assert.Equal("Tres cartas", editor.SelectedTemplate.Text);

            editor.Clone();
            Assert.Equal(4, editor.Templates.Count);
            Assert.Equal("Tres cartas_Clone", editor.SelectedTemplate.Text);
            Assert.Equal("3.pnpa", editor.SelectedTemplate.FileName);
            Assert.Equal(3, Read("3.pnpa").Cards.Length);

            editor.Delete();
            Assert.True(editor.IsNew);
            Assert.Equal(3, editor.Templates.Count);
            Assert.False(File.Exists(AppSettings.AdvancedStylesFolder + "3.pnpa"));
            Assert.Empty(editor.Slots);
            editor.Delete();                                // nothing chosen: nothing happens
            Assert.Equal(3, editor.Templates.Count);
            Assert.Single(notices);
        });
    }

    [Fact]
    public void A_template_without_cards_is_not_saved()
    {
        Run((editor, notices) =>
        {
            editor.Name = "Vacía";
            editor.Save();
            Assert.Equal([StringResources.Get("faltan_datos")], notices);
            Assert.Equal(2, editor.Templates.Count);
        });
    }

    [Fact]
    public void Imports_a_template_as_it_is_and_exports_a_copy()
    {
        Run((editor, notices) =>
        {
            string folder = fixture.NewFolder();
            string copy = Path.Combine(folder, "copia.pnpa");
            Assert.Null(editor.ExportName);
            editor.Export(copy);
            Assert.False(File.Exists(copy));

            editor.SelectedTemplate = editor.Templates[1];
            Assert.Equal(editor.SelectedTemplate.Text, editor.ExportName);
            editor.Export(copy);
            Assert.True(File.Exists(copy));

            // Whatever the window shows, the file comes in untouched.
            AdvancedTemplate bundled = Read("1.pnpa");
            editor.SelectedTemplate = editor.Templates[0];
            AddCard(editor, "1", "1", "5", "5");
            editor.BleedText = "9";
            editor.Import(copy);

            Assert.Equal(3, editor.Templates.Count);
            Assert.Equal("2.pnpa", editor.SelectedTemplate.FileName);
            AdvancedTemplate imported = Read("2.pnpa");
            Assert.Equal(bundled.Name, imported.Name);
            Assert.Equal(bundled.Cards.Length, imported.Cards.Length);
            Assert.Equal(bundled.Bleed.Millimeter, imported.Bleed.Millimeter, 3);
            Assert.Equal(bundled.Paper.Type, imported.Paper.Type);
            Assert.Equal(bundled.Cards.Length, editor.Slots.Count);
            Assert.Empty(notices);

            string broken = Path.Combine(folder, "rota.pnpa");
            File.WriteAllText(broken, "this is not a template");
            editor.Import(broken);
            Assert.Single(notices);
            Assert.Equal(3, editor.Templates.Count);
        });
    }

    [Fact]
    public void Window_shows_the_template_and_asks_before_deleting()
    {
        Run((editor, _) =>
        {
            var owner = new Window();
            owner.Show();
            AddCard(editor, "10", "10", "60", "90");
            AddCard(editor, "80", "10", "60", "90");
            AddCard(editor, "10", "110", "120", "70", rotation: 90);
            editor.Name = "Prueba";
            editor.Save();
            var window = new TemplateEditorWindow(editor);
            window.Show(owner);
            Dispatcher.UIThread.RunJobs();

            TemplatePreview preview = window.FindControl<TemplatePreview>("Preview")!;
            Assert.Equal(3, preview.Cards.Count);
            Assert.Equal(3, window.FindControl<ListBox>("SlotsBox")!.ItemCount);
            Assert.True(window.FindControl<Button>("DeleteButton")!.IsEnabled);
            editor.SelectedSlot = editor.Slots[1];
            Dispatcher.UIThread.RunJobs();
            Assert.True(preview.Cards[1].Selected);
            Screenshots.Capture(window, "ventana-plantillas-avanzadas");

            var answers = new Queue<bool?>([false, true]);
            window.Ask = _ => Task.FromResult(answers.Dequeue());
            HeadlessFixture.Wait(window.DeleteAsync());
            Assert.False(editor.IsNew);                     // answered no
            HeadlessFixture.Wait(window.DeleteAsync());
            Dispatcher.UIThread.RunJobs();
            Assert.True(editor.IsNew);
            Assert.Empty(preview.Cards);
            Assert.False(window.FindControl<Button>("DeleteButton")!.IsEnabled);
            window.Close();
            owner.Close();
        });
    }

    [Fact]
    public void Layout_button_of_a_template_project_only_chooses_sides_and_backs()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var model = new MainWindowViewModel();
            ProjectSettingsViewModel settings = model.ProjectSettings;
            settings.SelectedGroup = settings.Groups.First(option => option.Group == SettingsGroup.AdvancedStyles);
            Assert.Equal(SettingsGroup.AdvancedStyles, settings.Settings.Group.Group);
            double paperWidth = settings.Settings.PrintLayout.Paper.Size.X.Millimeter;
            bool askedLayout = false;
            model.AskLayout = _ =>
            {
                askedLayout = true;
                return Task.FromResult<LayoutResult?>(null);
            };
            AdvancedTemplate? shown = null;
            model.AskTemplateSides = (layout, template) =>
            {
                shown = template;
                Assert.Equal(2, layout.SideOptions.Count);  // no gutterfold
                layout.SelectedSide = 1;
                layout.SelectedBack = layout.BackOptions.First(option => option.Value == BackImageMode.DifferentBack);
                layout.CardWidthText = "5";                 // not taken: the layout is the template's
                return Task.FromResult<LayoutResult?>(layout.Result());
            };
            double cardWidth = settings.Settings.Grid.CardSize.X.Millimeter;

            HeadlessFixture.Wait(model.EditLayoutAsync());
            Dispatcher.UIThread.RunJobs();

            Assert.False(askedLayout);
            Assert.Equal(Read("1.pnpa").Cards.Length, shown!.Cards.Length);
            Assert.True(settings.Settings.PrintLayout.DoubleSided);
            Assert.Equal(BackImageMode.DifferentBack, settings.Settings.PrintLayout.BackImage);
            Assert.True(settings.Settings.DoublePanel);
            Assert.True(model.ImagePanel.IsDouble);
            Assert.Equal(SettingsGroup.AdvancedStyles, settings.Settings.Group.Group);
            Assert.Equal(cardWidth, settings.Settings.Grid.CardSize.X.Millimeter, 3);
            Assert.Equal(paperWidth, settings.Settings.PrintLayout.Paper.Size.X.Millimeter, 3);

            var owner = new Window();
            owner.Show();
            var window = new TemplateSetupWindow(new LayoutViewModel(settings.Settings), shown);
            Task<LayoutResult?> result = window.ShowDialog<LayoutResult?>(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(shown.Cards.Length, window.FindControl<TemplatePreview>("Preview")!.Cards.Count);
            Screenshots.Capture(window, "ventana-disposicion-plantilla");
            window.Accept();
            Dispatcher.UIThread.RunJobs();
            Assert.True(result.Result!.Layout.DoubleSided);
            owner.Close();
        });
    }

    [Fact]
    public void Templates_saved_in_the_editor_reach_the_list_of_styles()
    {
        Run((_, _) =>
        {
            var model = new MainWindowViewModel();
            ProjectSettingsViewModel settings = model.ProjectSettings;
            settings.SelectedGroup = settings.Groups.First(option => option.Group == SettingsGroup.AdvancedStyles);
            Assert.Single(settings.Styles);
            model.ShowTemplateEditor = editor =>
            {
                AddCard(editor, "10", "10", "60", "90");
                editor.Name = "Nueva";
                editor.Save();
                return Task.CompletedTask;
            };

            HeadlessFixture.Wait(model.EditTemplatesAsync());

            Assert.Equal(2, settings.Styles.Count);
            Assert.Equal("1.pnpa", settings.SelectedStyle!.FileName);      // the one chosen stays
            Assert.Contains(settings.Styles, option => option.Text == "Nueva");
        });
    }
}
