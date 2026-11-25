using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using FilterType = ImageMagick.FilterType;
using PnpTool.Controls;
using PnpTool.Core;
using PnpTool.Core.CustomDecks;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Primitives;

namespace PnpTool.App.Tests;

[Collection(UserInterface.Name)]
public sealed class ControlsTests(HeadlessFixture fixture)
{
    /// <summary>Shows a control in a window, with the keyboard on it.</summary>
    private static Window Show(Control control)
    {
        var window = new Window { Width = 700, Height = 420, Content = control };
        window.Show();
        control.Focus();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Type(Window window, string text)
    {
        foreach (char letter in text)
        {
            window.KeyTextInput(letter.ToString());
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    // ---- Decimal and integer boxes ----

    [Theory]
    [InlineData(LanguageId.Spanish)]
    [InlineData(LanguageId.EnglishUk)]
    public void Decimal_box_shows_a_point_and_reads_the_same_number_in_every_language(LanguageId language)
    {
        fixture.Run(language, () =>
        {
            var box = new DecimalTextBox();
            Window window = Show(box);

            Type(window, "12,5");

            Assert.Equal("12.5", box.Text);
            Assert.Equal(12.5, box.Value);
            Assert.Equal(4, box.CaretIndex);
            window.Close();
        });
    }

    [Fact]
    public void Decimal_box_takes_only_digits_and_one_separator()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var box = new DecimalTextBox();
            Window window = Show(box);

            Type(window, "a1.x2.3,4-");

            Assert.Equal("1.234", box.Text);
            Assert.Equal(1.234, box.Value);
            window.Close();
        });
    }

    [Fact]
    public void Decimal_box_is_limited_to_six_characters()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var box = new DecimalTextBox();
            Window window = Show(box);

            Type(window, "123.456789");

            Assert.Equal("123.45", box.Text);
            window.Close();
        });
    }

    [Fact]
    public void Decimal_box_takes_a_minus_sign_only_when_allowed_and_only_first()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var box = new DecimalTextBox { AllowNegative = true };
            Window window = Show(box);

            Type(window, "-2-.5");

            Assert.Equal("-2.5", box.Text);
            Assert.Equal(-2.5, box.Value);
            window.Close();
        });
    }

    [Fact]
    public void Decimal_box_survives_text_that_is_not_a_number()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var box = new DecimalTextBox { Text = "3,75" };   // as pasted or set from code
            Assert.Equal("3.75", box.Text);
            Assert.Equal(3.75, box.Value);

            box.Text = string.Empty;
            Assert.Equal(0.0, box.Value);

            box.Text = "abc";
            Assert.Equal(0.0, box.Value);
        });
    }

    [Fact]
    public void Decimal_text_is_written_and_read_with_a_point()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            Assert.Equal("63.5", DecimalTextBox.Format(63.5));
            Assert.Equal(63.5, DecimalTextBox.Parse("63.5"));
            Assert.Equal(0.0, DecimalTextBox.Parse("63,5"));
            Assert.Equal(0.0, DecimalTextBox.Parse(null));
        });
    }

    [Fact]
    public void Integer_box_takes_three_digits()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var box = new IntegerTextBox();
            Window window = Show(box);

            Type(window, "1a2.-34");

            Assert.Equal("123", box.Text);
            Assert.Equal(123, box.Value);

            box.Text = string.Empty;
            Assert.Equal(0, box.Value);
            window.Close();
        });
    }

    [Fact]
    public void Integer_box_takes_a_minus_sign_when_allowed()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var box = new IntegerTextBox { AllowNegative = true };
            Window window = Show(box);

            Type(window, "-7");
            Assert.Equal(-7, box.Value);

            box.Text = "-";
            Assert.Equal(0, box.Value);
            window.Close();
        });
    }

    // ---- Numeric box ----

    [Fact]
    public void Numeric_box_keeps_the_number_in_its_range()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var box = new NumericBox(maximum: 50, minimum: 1, step: 1);
            int changes = 0;
            box.ValueChanged += (_, _) => changes++;
            Window window = Show(box);
            IntegerTextBox text = box.FindControl<IntegerTextBox>("ValueBox")!;

            Assert.Equal("1", text.Text);   // 0 is under the minimum
            Assert.Equal(1, box.Value);

            box.Value = 20;
            Assert.Equal(20, box.Value);
            Assert.Equal("20", text.Text);

            box.Value = 500;
            Assert.Equal(50, box.Value);
            Assert.Equal("50", text.Text);

            box.Value = -3;
            Assert.Equal(1, box.Value);
            Assert.Equal(3, changes);
            window.Close();
        });
    }

    [Fact]
    public void Numeric_box_limits_what_is_typed()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var box = new NumericBox(maximum: 50, minimum: 0, step: 1);
            Window window = Show(box);
            IntegerTextBox text = box.FindControl<IntegerTextBox>("ValueBox")!;
            text.Focus();
            text.SelectAll();

            Type(window, "4");
            Assert.Equal(4, box.Value);

            Type(window, "9");   // 49
            Assert.Equal(49, box.Value);

            Type(window, "9");   // 499 is over the maximum
            Assert.Equal(50, box.Value);
            Assert.Equal("50", text.Text);

            text.Text = string.Empty;
            Assert.Equal(0, box.Value);
            Assert.Equal("0", text.Text);
            window.Close();
        });
    }

    [Fact]
    public void Numeric_box_buttons_move_by_the_step_and_stop_at_the_limits()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var box = new NumericBox(maximum: 10, minimum: 2, step: 3);
            Window window = Show(box);
            RepeatButton up = box.FindControl<RepeatButton>("UpButton")!;
            RepeatButton down = box.FindControl<RepeatButton>("DownButton")!;

            Assert.Equal(2, box.Value);
            Click(up);
            Assert.Equal(5, box.Value);
            Click(up);
            Click(up);
            Assert.Equal(10, box.Value);
            Click(down);
            Assert.Equal(7, box.Value);

            box.SetRange(maximum: 100, minimum: 0, step: 10);
            Click(down);
            Assert.Equal(0, box.Value);
            window.Close();
        });
    }

    [Fact]
    public void Numeric_box_without_limits_does_not_overflow()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var box = new NumericBox();
            Window window = Show(box);

            Assert.Equal(0, box.Value);
            box.Value = int.MaxValue;
            Click(box.FindControl<RepeatButton>("UpButton")!);
            Assert.Equal(int.MaxValue, box.Value);
            window.Close();
        });
    }

    // ---- Slider ----

    [Fact]
    public void Slider_reports_its_changes_but_not_its_first_value()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var slider = new ZoomSlider();
            var reported = new List<double>();
            slider.ValueChangedCallback = reported.Add;

            slider.Initialize(tick: 1, smallChange: 1, minimum: 0, maximum: 10, value: 4);
            slider.Step(1);
            slider.Step(-0.5);
            slider.Value = 9;

            Assert.Equal([0.0, 5.0, 4.0, 9.0], reported);
            Assert.Equal(9.0, ToolTip.GetTip(slider));
        });
    }

    // ---- Units, buttons and lists ----

    [Fact]
    public void Unit_selector_offers_the_two_systems_in_the_language_of_the_program()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var selector = new UnitSelector();
            Window window = Show(selector);
            RadioButton metric = selector.FindControl<RadioButton>("MetricButton")!;
            RadioButton imperial = selector.FindControl<RadioButton>("ImperialButton")!;

            Assert.Equal(StringResources.Get("Form_Setup_PnpTool_RadioButton_Smetrico"), metric.Content);
            Assert.True(StringResources.Has("Form_Setup_PnpTool_RadioButton_Simperial"));
            Assert.Equal(UnitType.Millimeter, selector.SelectedUnit);
            Assert.True(metric.IsChecked);

            selector.SelectedUnit = UnitType.Inch;
            Assert.True(imperial.IsChecked);
            Assert.False(metric.IsChecked);

            metric.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(UnitType.Millimeter, selector.SelectedUnit);
            window.Close();
        });
    }

    [Fact]
    public void Ok_and_cancel_buttons_raise_their_events()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var buttons = new OkCancelButtons();
            var raised = new List<string>();
            buttons.Accepted += (_, _) => raised.Add("ok");
            buttons.Cancelled += (_, _) => raised.Add("cancel");
            Window window = Show(buttons);

            Click(buttons.CancelButton);
            Click(buttons.OkButton);

            Assert.Equal(["cancel", "ok"], raised);
            Assert.Equal(StringResources.Get("Form_SubirImagenes_BotonOk"), ToolTip.GetTip(buttons.OkButton));
            Assert.Equal(StringResources.Get("Form_Setup_Boton_Cancelar"), ToolTip.GetTip(buttons.CancelButton));
            Assert.True(buttons.OkButton.IsDefault);
            Assert.True(buttons.CancelButton.IsCancel);
            window.Close();
        });
    }

    [Fact]
    public void Filter_list_starts_on_lanczos_and_follows_the_selection()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var filters = new FilterComboBox();
            Window window = Show(filters);
            ComboBox list = filters.FindControl<ComboBox>("FilterList")!;

            Assert.Equal(FilterType.Lanczos, filters.SelectedFilter);
            Assert.Equal(FilterType.Lanczos, list.SelectedItem);
            Assert.Contains(FilterType.Mitchell, list.Items.Cast<FilterType>());

            list.SelectedItem = FilterType.Mitchell;
            Assert.Equal(FilterType.Mitchell, filters.SelectedFilter);

            filters.SelectedFilter = FilterType.Cubic;
            Assert.Equal(FilterType.Cubic, list.SelectedItem);
            window.Close();
        });
    }

    [Fact]
    public void Language_list_shows_the_languages_of_the_program_with_their_flags()
    {
        fixture.Run(LanguageId.Dutch, () =>
        {
            var languages = new LanguageComboBox();
            Window window = Show(languages);
            languages.Load();
            ComboBox list = languages.FindControl<ComboBox>("Languages")!;
            List<LanguageItem> items = [.. list.Items.Cast<LanguageItem>()];

            Assert.Equal(14, items.Count);
            Assert.All(items, item => Assert.True(item.Flag != null, item.Language.Name + " has no flag"));
            Assert.Equal("nl", languages.SelectedLanguage.Code);

            list.SelectedIndex = 0;
            Assert.Equal(items[0].Language, languages.SelectedLanguage);
            Screenshots.Capture(window, "control-idiomas");
            window.Close();
        });
    }

    [Fact]
    public void Language_list_for_card_search_starts_on_its_first_language_when_the_current_one_is_not_offered()
    {
        fixture.Run(LanguageId.Dutch, () =>
        {
            var languages = new LanguageComboBox { List = LanguageList.CardSearch };
            languages.Load();
            ComboBox list = languages.FindControl<ComboBox>("Languages")!;

            Assert.Equal(10, list.ItemCount);
            Assert.Equal(0, list.SelectedIndex);
            Assert.Equal(new SupportedLanguage(LanguageId.German).Code, languages.SelectedLanguage.Code);
        });
    }

    // ---- Colour ----

    [Fact]
    public void Colour_button_shows_its_colour_and_reports_the_stored_form()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var button = new ColorButton();
            var reported = new List<string>();
            button.ColorChanged += reported.Add;

            Assert.Equal(AppSettings.ColorWhite, button.ColorText);
            Assert.Equal(StringResources.Get("Click_Para_Color"), ToolTip.GetTip(button));

            button.Load("255_10_20_30");
            Assert.Empty(reported);
            Assert.Equal(new ArgbColor(10, 20, 30), button.Color);
            Assert.Equal(Color.FromRgb(10, 20, 30), ((ISolidColorBrush)button.Background!).Color);

            button.Accept(new ArgbColor(1, 2, 3));
            Assert.Equal(["255_1_2_3"], reported);
            Assert.Equal(new ArgbColor(1, 2, 3), ArgbColor.Parse(button.ColorText));   // what the engine reads back
        });
    }

    [Fact]
    public void Colour_dialog_offers_the_default_colours_and_remembers_the_chosen_one()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            File.Delete(AppPaths.CustomColorsFile);
            var owner = new Window();
            owner.Show();

            var dialog = new ColorPickerDialog(new ArgbColor(200, 100, 50));
            Task<ArgbColor?> result = dialog.ShowDialog<ArgbColor?>(owner);
            Dispatcher.UIThread.RunJobs();
            WrapPanel palette = dialog.FindControl<WrapPanel>("Palette")!;

            Assert.Equal(new ArgbColor(200, 100, 50), dialog.Color);
            Assert.Equal(ColorPickerDialog.PaletteSize + 1, palette.Children.Count);
            Assert.Equal(ColorPickerDialog.DefaultPalette, ColorPickerDialog.LoadPalette());
            Assert.Equal(ArgbColor.Black, ColorPickerDialog.DefaultPalette[0]);

            // The picker paints its colour field in the background.
            Thread.Sleep(300);
            Screenshots.Capture(dialog, "dialogo-color");

            Click((Button)palette.Children[3]);   // red
            Assert.Equal(new ArgbColor(255, 0, 0), dialog.Color);

            dialog.Color = new ArgbColor(12, 34, 56);
            dialog.Accept();
            Dispatcher.UIThread.RunJobs();

            Assert.True(result.IsCompleted);
            Assert.Equal(new ArgbColor(12, 34, 56), result.Result);
            string[] stored = File.ReadAllLines(AppPaths.CustomColorsFile);
            Assert.Equal(ColorPickerDialog.PaletteSize, stored.Length);
            Assert.Equal("255_12_34_56", stored[0]);
            Assert.Equal(ArgbColor.Black.ToString(), stored[1]);
            Assert.Equal(new ArgbColor(12, 34, 56), ColorPickerDialog.LoadPalette()[0]);
            owner.Close();
            File.Delete(AppPaths.CustomColorsFile);
        });
    }

    [Fact]
    public void Colour_dialog_cancelled_gives_nothing_and_keeps_the_colour_file()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            File.Delete(AppPaths.CustomColorsFile);
            var owner = new Window();
            owner.Show();
            var dialog = new ColorPickerDialog();
            Task<ArgbColor?> result = dialog.ShowDialog<ArgbColor?>(owner);
            Dispatcher.UIThread.RunJobs();

            Click(dialog.FindControl<OkCancelButtons>("Buttons")!.CancelButton);

            Assert.True(result.IsCompleted);
            Assert.Null(result.Result);
            Assert.False(File.Exists(AppPaths.CustomColorsFile));
            owner.Close();
        });
    }

    // ---- Font ----

    [Fact]
    public void Font_variants_are_named_from_weight_slant_and_width()
    {
        Assert.Equal("Regular", FontFace.Regular.Name);
        Assert.Equal("Bold", new FontFace(FontSlant.Normal, 700, 5).Name);
        Assert.Equal("Bold Italic", new FontFace(FontSlant.Italic, 700, 5).Name);
        Assert.Equal("Semi Bold Oblique", new FontFace(FontSlant.Oblique, 600, 5).Name);
        Assert.Equal("Condensed Light", new FontFace(FontSlant.Normal, 300, 3).Name);
        Assert.Equal("450", new FontFace(FontSlant.Normal, 450, 5).Name);
    }

    [Fact]
    public void Font_becomes_the_typeface_the_user_interface_draws()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var font = new FontSpec
            {
                FontFamily = "Liberation Serif",
                FontSize = 18,
                FontStyle = FontSlant.Italic,
                FontWeight = 700,
                FontStretch = 3,
                TextDecorationType = TextDecorationType.Underline,
                Color = "255_0_0_255",
            };
            var text = new TextBlock();

            font.ApplyTo(text);

            Assert.Equal(new Typeface("Liberation Serif", FontStyle.Italic, FontWeight.Bold, FontStretch.Condensed), font.ToTypeface());
            Assert.Equal("Liberation Serif", text.FontFamily.Name);
            Assert.Equal(18.0, text.FontSize);
            Assert.Equal(FontWeight.Bold, text.FontWeight);
            Assert.Equal(FontStyle.Italic, text.FontStyle);
            Assert.Equal(FontStretch.Condensed, text.FontStretch);
            Assert.Same(TextDecorations.Underline, text.TextDecorations);
            Assert.Equal(Colors.Blue, ((ISolidColorBrush)text.Foreground!).Color);
            Assert.Null(TextDecorationType.None.ToAvaloniaDecorations());
        });
    }

    [Fact]
    public void Font_picker_gives_back_the_font_it_was_given()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var font = new FontSpec
            {
                FontFamily = "A family nobody has",
                FontSize = 24,
                FontStyle = FontSlant.Oblique,
                FontWeight = 600,
                FontStretch = 7,
                TextDecorationType = TextDecorationType.Strikethrough,
                Color = "255_9_8_7",
            };
            var picker = new FontPicker { SelectedFont = font };
            Window window = Show(picker);

            FontSpec back = picker.SelectedFont;

            Assert.NotSame(font, back);
            Assert.Equal(font.ToSerializedString(), back.ToSerializedString());   // the form the deck files store
            Assert.Equal("24", picker.FindControl<DecimalTextBox>("SizeBox")!.Text);
            Assert.Equal(new ArgbColor(9, 8, 7), picker.FindControl<ColorButton>("ColorButtonControl")!.Color);
            Assert.Equal(new FontFace(FontSlant.Oblique, 600, 7), picker.Faces[0]);
            Assert.NotEmpty(picker.Families);
            Assert.Equal(picker.Families.Order(StringComparer.CurrentCultureIgnoreCase), picker.Families);
            window.Close();
        });
    }

    [Fact]
    public void Font_picker_changes_a_copy_and_reports_each_change()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var font = new FontSpec();
            string before = font.ToSerializedString();
            var picker = new FontPicker { SelectedFont = font, PreviewText = "Muestra" };
            int changes = 0;
            picker.FontChanged += (_, _) => changes++;
            Window window = Show(picker);
            DecimalTextBox size = picker.FindControl<DecimalTextBox>("SizeBox")!;
            TextBlock preview = picker.FindControl<TextBlock>("PreviewBlock")!;

            size.Text = "30";
            Assert.Equal(30.0, picker.SelectedFont.FontSize);
            Assert.Equal(30.0, preview.FontSize);

            size.Text = string.Empty;   // left as it was
            Assert.Equal(30.0, picker.SelectedFont.FontSize);

            picker.FindControl<ColorButton>("ColorButtonControl")!.Accept(new ArgbColor(0, 128, 0));
            Assert.Equal("255_0_128_0", picker.SelectedFont.Color);
            Assert.Equal(Color.FromRgb(0, 128, 0), ((ISolidColorBrush)preview.Foreground!).Color);

            string family = picker.Families[^1];
            picker.SelectFamily(family);
            Assert.Equal(family, picker.SelectedFont.FontFamily);
            Assert.Equal(family, preview.FontFamily.Name);
            Assert.Contains(FontFace.Of(picker.SelectedFont), picker.Faces);

            Assert.Equal(3, changes);
            Assert.Equal("Muestra", preview.Text);
            Assert.Equal(before, font.ToSerializedString());
            Screenshots.Capture(window, "control-fuente");
            window.Close();
        });
    }

    [Fact]
    public void Font_picker_lists_the_variants_of_a_family()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var picker = new FontPicker();
            var bold = new FontFace(FontSlant.Normal, 700, 5);
            string? family = picker.Families.FirstOrDefault(name => FontFace.Of(name).Contains(bold) && FontFace.Of(name).Contains(FontFace.Regular));
            Assert.SkipWhen(family == null, "No installed font family has a regular and a bold variant.");

            picker.SelectFamily(family!);
            Assert.Equal(FontFace.Regular, FontFace.Of(picker.SelectedFont));

            picker.SelectFace(bold);
            Assert.Equal(700, picker.SelectedFont.FontWeight);
            Assert.Equal(FontWeight.Bold, picker.FindControl<TextBlock>("PreviewBlock")!.FontWeight);

            // A family without that variant goes back to its regular one or its first.
            string? plain = picker.Families.FirstOrDefault(name => !FontFace.Of(name).Contains(bold));
            if (plain != null)
            {
                picker.SelectFamily(plain);
                Assert.Contains(FontFace.Of(picker.SelectedFont), FontFace.Of(plain));
            }
        });
    }

    [Fact]
    public void Font_dialog_gives_the_font_when_accepted_and_nothing_when_cancelled()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var owner = new Window();
            owner.Show();
            var font = new FontSpec { FontSize = 14, Color = "255_1_1_1" };

            var dialog = new FontDialog(null, font);
            Task<FontSpec?> result = dialog.ShowDialog<FontSpec?>(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("lorem ipsum", dialog.FontPicker.PreviewText);
            dialog.FontPicker.FindControl<DecimalTextBox>("SizeBox")!.Text = "40";
            Screenshots.Capture(dialog, "dialogo-fuente");
            dialog.Accept();
            Dispatcher.UIThread.RunJobs();

            Assert.True(result.IsCompleted);
            Assert.Equal(40.0, result.Result!.FontSize);
            Assert.Equal("255_1_1_1", result.Result.Color);
            Assert.Equal(14.0, font.FontSize);

            var cancelled = new FontDialog("Texto", font);
            Task<FontSpec?> nothing = cancelled.ShowDialog<FontSpec?>(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Texto", cancelled.FontPicker.PreviewText);
            cancelled.FontPicker.FindControl<DecimalTextBox>("SizeBox")!.Text = "99";
            cancelled.Close((FontSpec?)null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(nothing.IsCompleted);
            Assert.Null(nothing.Result);
            Assert.Equal(14.0, font.FontSize);
            owner.Close();
        });
    }
}
