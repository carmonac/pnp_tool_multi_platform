using PnpTool.Core.Cards;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;

namespace PnpTool.Core.Tests.Projects;

[Collection(GlobalState.Name)]
public sealed class SettingsValidatorTests : IDisposable
{
    private readonly GlobalStateScope _scope = new();

    public SettingsValidatorTests()
    {
        AppPaths.Initialize(AppPaths.FindAssetsFolder(AppContext.BaseDirectory), Path.GetTempPath());
        AppLanguage.Change(new SupportedLanguage(LanguageId.Spanish));
    }

    public void Dispose() => _scope.Dispose();

    private static ProjectSettings Style(string file)
    {
        var settings = new ProjectSettings();
        Assert.False(ProjectFileStore.LoadStyle(StyleCatalog.PathOf(SettingsGroup.Cards, file), ref settings).Error.HasError);
        return settings;
    }

    [Theory]
    [InlineData("Cartas_Standar.pnps")]
    [InlineData("Cartas_Euro.pnps")]
    [InlineData("Cartas_Tarot.pnps")]
    [InlineData("Tokens_25.pnps")]
    [InlineData("Papel_A4280.pnps")]
    public void Bundled_styles_are_valid(string file)
    {
        AppError error = SettingsValidator.Check(Style(file), 9);

        Assert.False(error.HasError, error.Message);
        Assert.Equal(ErrorKind.None, error.Kind);
    }

    [Fact]
    public void Cards_that_do_not_fit_the_sheet_are_reported()
    {
        ProjectSettings settings = Style("Cartas_Standar.pnps");
        settings.PrintLayout.Columns = 4;

        AppError error = SettingsValidator.Check(settings, 9);

        Assert.True(error.HasError);
        Assert.Contains(StringResources.Get("Cartas_no_entran_ancho"), error.Message);
    }

    [Fact]
    public void Everything_wrong_is_reported()
    {
        ProjectSettings settings = Style("Cartas_Standar.pnps");
        settings.PrintLayout.Rows = 0;
        settings.Watermark.Type = WatermarkType.OnPage;
        settings.Front.Effect = ImageEffectType.Mirror;
        settings.Front.ShowOuterFrame = true;
        settings.Front.CutLine.LineType = CutLineType.Normal;
        settings.Front.CutLine.Thickness = Unit.Zero;

        string message = SettingsValidator.Check(settings, 9).Message;

        Assert.Contains(StringResources.Get("Filas") + "=0", message);
        Assert.Contains(StringResources.Get("imagen_marca_agua_vacia"), message);
        Assert.Contains(StringResources.Get("Sangrado_no_puede_ser_0"), message);
        Assert.Contains(StringResources.Get("grosor") + "<=0", message);
        Assert.Contains(StringResources.Get("Form_Setup_Caras_CaraFrontal_CheckboxMarcoExterior") + "<=0", message);
    }

    [Fact]
    public void Gutterfold_needs_an_even_number_of_rows_and_is_marked_as_such()
    {
        ProjectSettings settings = Style("Cartas_Standar.pnps");
        settings.PrintLayout.BackImage = BackImageMode.DifferentBack;
        settings.PrintLayout.Rows = 3;

        AppError error = SettingsValidator.Check(settings, 9);

        Assert.Contains(StringResources.Get("Gutter_paginas_par"), error.Message);
        Assert.Equal(ProjectType.Gutterfold, settings.Group.ProjectType);
        settings.PrintLayout.Rows = 2;
        Assert.False(SettingsValidator.Check(settings, 9).HasError);
    }

    [Fact]
    public void Double_sided_with_a_shared_back_needs_the_back_image()
    {
        ProjectSettings settings = Style("Cartas_Standar.pnps");
        settings.PrintLayout.DoubleSided = true;
        settings.PrintLayout.BackImage = BackImageMode.SameBack;

        Assert.Contains(StringResources.Get("falta_imagen_trasera"), SettingsValidator.Check(settings, 9).Message);

        settings.SharedBack.Path = "/somewhere/back.png";
        Assert.False(SettingsValidator.Check(settings, 9).HasError);
    }

    [Fact]
    public void Faces_larger_than_the_card_and_mirror_sizes_are_reported()
    {
        ProjectSettings settings = Style("Cartas_Standar.pnps");
        settings.Front.Size = new UnitPoint { X = Unit.FromMillimeter(70), Y = Unit.FromMillimeter(88.9) };
        Assert.Contains(StringResources.Get("Carta_Escala_Ancho_Frontal_Error"), SettingsValidator.Check(settings, 9).Message);

        // With bleed, a mirrored face must leave the bleed free on each side.
        settings = Style("Cartas_Standar.pnps");
        settings.Grid.Bleed = Unit.FromMillimeter(3);
        settings.Front.Effect = ImageEffectType.Mirror;
        Assert.True(SettingsValidator.Check(settings, 9).HasError);
        settings.Front.Size = new UnitPoint { X = Unit.FromMillimeter(57.5), Y = Unit.FromMillimeter(82.9) };
        Assert.False(SettingsValidator.Check(settings, 9).HasError, SettingsValidator.Check(settings, 9).Message);
    }

    [Fact]
    public void Masked_decks_need_their_number_of_cards()
    {
        ProjectSettings settings = Style("Cartas_Espanola_Personalizado.pnps");
        settings.Group.ProjectType = ProjectType.SpanishDeck;
        settings.Group.SubgroupIndex = 1;

        Assert.Contains(StringResources.Get("Baraja_Espanola_No40"), SettingsValidator.Check(settings, 39).Message);
        Assert.DoesNotContain(StringResources.Get("Baraja_Espanola_No40"), SettingsValidator.Check(settings, 40).Message);
        settings.Group.SubgroupIndex = 2;
        Assert.Contains(StringResources.Get("Baraja_Poker_No52"), SettingsValidator.Check(settings, 40).Message);
    }

    [Fact]
    public void Fill_faces_and_missing_files()
    {
        var settings = new ProjectSettings();
        Assert.Equal(FaceType.None, SettingsValidator.FillFaces(settings));
        settings.Back.PaintBackground = true;
        Assert.Equal(FaceType.Back, SettingsValidator.FillFaces(settings));
        settings.Front.PaintBackground = true;
        Assert.Equal(FaceType.Both, SettingsValidator.FillFaces(settings));

        string here = Path.GetTempFileName();
        try
        {
            Card[] cards =
            [
                new() { Front = new CardFace(here, FaceType.Front, ImageKind.Normal) },
                new() { Front = new CardFace("255_0_0_0", FaceType.Front, ImageKind.Blank) },
            ];
            Assert.False(SettingsValidator.MissingFiles(cards).HasError);

            cards[0].Front.Path = here + ".gone";
            AppError error = SettingsValidator.MissingFiles(cards);
            Assert.True(error.HasError);
            Assert.Contains("#0 Path not found", error.Message);
        }
        finally
        {
            File.Delete(here);
        }
    }
}
