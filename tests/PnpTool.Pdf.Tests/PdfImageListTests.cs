using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Core.Templates;
using PnpTool.Pdf.Layout;

namespace PnpTool.Pdf.Tests;

/// <summary>
/// The order in which cards are laid on the sheets for each kind of project. Cards here are
/// plain colours named "255_n_0_0" for card n (fronts) and "255_n_9_9" (backs), so no image
/// files are needed.
/// </summary>
[Collection(GlobalState.Name)]
public sealed class PdfImageListTests
{
    private static string F(int card) => $"255_{card}_0_0";

    private static string B(int card) => $"255_{card}_9_9";

    private static Project Project(ProjectType type, int cards, int rows, int columns, bool backs = false)
    {
        var settings = new ProjectSettings();
        settings.Group.ProjectType = type;
        settings.PrintLayout.Rows = rows;
        settings.PrintLayout.Columns = columns;
        settings.Book.Spine = BookSpineType.Left;
        var deck = new Deck
        {
            Cards =
            [
                .. Enumerable.Range(0, cards).Select(i => new Card
                {
                    Copies = 1,
                    Front = new CardFace(F(i), FaceType.Front, ImageKind.Blank),
                    Back = backs ? new CardFace(B(i), FaceType.Back, ImageKind.Blank) : new CardFace(),
                }),
            ],
        };
        deck.RecountCopies();
        return new Project { Settings = settings, Deck = deck };
    }

    /// <summary>The cells as their paths, "-" for an empty cell.</summary>
    private static string[] Paths(PdfImage[] cells) =>
        [.. cells.Select(cell => cell.Kind == ImageKind.Empty ? "-" : cell.Path)];

    private static int[] Angles(PdfImage[] cells) => [.. cells.Select(cell => cell.Rotation.Angle)];

    [Fact]
    public void Fronts_fill_whole_sheets_and_copies_are_marked_as_repeats()
    {
        Project project = Project(ProjectType.Card, cards: 4, rows: 2, columns: 3);
        project.Deck.Cards[1].Copies = 3;
        project.Deck.RecountCopies();

        PdfImage[] cells = PdfGenerator.Prepare(project);

        Assert.Equal(6, project.Settings.PrintLayout.CardsPerSheet);
        Assert.Equal([F(0), F(1), F(1), F(1), F(2), F(3)], Paths(cells));
        Assert.Equal([false, false, true, true, false, false], cells.Select(cell => cell.SameAsPrevious));
    }

    [Fact]
    public void Incomplete_last_sheet_is_padded_with_empty_cells()
    {
        PdfImage[] cells = PdfGenerator.Prepare(Project(ProjectType.Card, cards: 7, rows: 2, columns: 3));

        Assert.Equal(12, cells.Length);
        Assert.Equal([F(6), "-", "-", "-", "-", "-"], Paths(cells)[6..]);
    }

    [Fact]
    public void Deck_without_cards_gives_one_empty_sheet()
    {
        PdfImage[] cells = PdfGenerator.Prepare(Project(ProjectType.Card, cards: 0, rows: 3, columns: 3));

        // One sheet is reserved, all of it empty.
        Assert.Equal(9, cells.Length);
        Assert.All(cells, cell => Assert.Equal(ImageKind.Empty, cell.Kind));
    }

    [Fact]
    public void Book_bound_on_the_right_runs_backwards()
    {
        Project project = Project(ProjectType.Book, cards: 4, rows: 1, columns: 1);
        project.Settings.Book.Spine = BookSpineType.Right;

        Assert.Equal([F(3), F(2), F(1), F(0)], Paths(PdfGenerator.Prepare(project)));
    }

    [Fact]
    public void Different_backs_follow_each_sheet_of_fronts_with_every_row_reversed()
    {
        Project project = Project(ProjectType.Card, cards: 7, rows: 2, columns: 3, backs: true);
        project.Settings.PrintLayout.BackImage = BackImageMode.DifferentBack;

        PdfImage[] cells = PdfGenerator.Prepare(project);

        Assert.Equal(
            [
                F(0), F(1), F(2), F(3), F(4), F(5),
                B(2), B(1), B(0), B(5), B(4), B(3),
                F(6), "-", "-", "-", "-", "-",
                "-", "-", B(6), "-", "-", "-",
            ],
            Paths(cells));
    }

    [Fact]
    public void Booklet_pairs_outer_pages_first_and_swaps_sides_on_every_other_spread()
    {
        // Eight pages, two per sheet side: 8-1, 2-7, 6-3, 4-5.
        Project project = Project(ProjectType.Booklet, cards: 8, rows: 1, columns: 2);
        project.Settings.Book.FlipEdge = PrintFlipEdge.Short;

        PdfImage[] cells = PdfGenerator.Prepare(project);

        Assert.Equal([F(7), F(0), F(1), F(6), F(5), F(2), F(3), F(4)], Paths(cells));
        Assert.All(cells, cell => Assert.Equal(0, cell.Rotation.Angle));
    }

    [Fact]
    public void Booklet_flipped_on_the_long_edge_turns_the_reverse_sides_half_round()
    {
        Project project = Project(ProjectType.Booklet, cards: 8, rows: 1, columns: 2);
        project.Settings.Book.FlipEdge = PrintFlipEdge.Long;

        PdfImage[] cells = PdfGenerator.Prepare(project);

        Assert.Equal([F(7), F(0), F(1), F(6), F(5), F(2), F(3), F(4)], Paths(cells));
        Assert.Equal([0, 0, 180, 180, 0, 0, 180, 180], Angles(cells));
    }

    [Fact]
    public void Gutterfold_alternates_rows_of_fronts_and_backs_with_the_far_row_turned()
    {
        Project project = Project(ProjectType.Gutterfold, cards: 3, rows: 2, columns: 2, backs: true);
        project.Settings.PrintLayout.BackImage = BackImageMode.DifferentBack;
        project.Settings.PrintLayout.FoldBottom = true;

        PdfImage[] cells = PdfGenerator.Prepare(project);

        Assert.Equal([F(0), F(1), B(0), B(1), F(2), "-", B(2), "-"], Paths(cells));
        Assert.Equal([0, 0, 180, 180, 0, 0, 180, 0], Angles(cells));

        project.Settings.PrintLayout.FoldBottom = false;
        Assert.Equal([180, 180, 0, 0, 180, 0, 0, 0], Angles(PdfGenerator.Prepare(project)));
    }

    [Fact]
    public void Gutterfold_with_a_shared_back_repeats_it_under_every_front()
    {
        Project project = Project(ProjectType.Gutterfold, cards: 2, rows: 2, columns: 2);
        project.Settings.PrintLayout.BackImage = BackImageMode.SameBack;
        project.Settings.SharedBack = new CardFace("255_7_7_7", FaceType.Back, ImageKind.Blank);

        PdfImage[] cells = PdfGenerator.Prepare(project);

        Assert.Equal([F(0), F(1), "255_7_7_7", "255_7_7_7"], Paths(cells));
        Assert.True(cells[3].SameAsPrevious);
    }

    [Fact]
    public void Pocketmod_places_eight_pages_in_two_columns_turned_towards_the_fold()
    {
        PdfImage[] cells = PdfGenerator.Prepare(Project(ProjectType.Pocketmod, cards: 8, rows: 4, columns: 2));

        Assert.Equal([F(0), F(1), F(7), F(2), F(6), F(3), F(5), F(4)], Paths(cells));
        Assert.Equal([270, 90, 270, 90, 270, 90, 270, 90], Angles(cells));
    }

    [Fact]
    public void Pocketmod_with_fewer_pages_leaves_the_missing_ones_empty()
    {
        PdfImage[] cells = PdfGenerator.Prepare(Project(ProjectType.Pocketmod, cards: 5, rows: 4, columns: 2));

        Assert.Equal([F(0), F(1), "-", F(2), "-", F(3), "-", F(4)], Paths(cells));
    }

    [Fact]
    public void Pocketfold_reverses_and_turns_the_first_four_pages()
    {
        PdfImage[] cells = PdfGenerator.Prepare(Project(ProjectType.Pocketfold, cards: 10, rows: 2, columns: 4));

        Assert.Equal(16, cells.Length);
        Assert.Equal([F(3), F(2), F(1), F(0), F(4), F(5), F(6), F(7)], Paths(cells)[..8]);
        Assert.Equal([180, 180, 180, 180, 0, 0, 0, 0], Angles(cells)[..8]);
        Assert.Equal(["-", "-", F(9), F(8), "-", "-", "-", "-"], Paths(cells)[8..]);
    }

    [Fact]
    public void Tabletop_lists_front_then_back_of_every_copy()
    {
        Project project = Project(ProjectType.Tabletop, cards: 2, rows: 1, columns: 1, backs: true);
        project.Settings.DoublePanel = true;
        project.Deck.Cards[0].Copies = 2;
        project.Deck.RecountCopies();

        Assert.Equal([F(0), B(0), F(0), B(0), F(1), B(1)], Paths(PdfImageList.Generate(project)));

        project.Settings.DoublePanel = false;
        Assert.Equal([F(0), F(0), F(1)], Paths(PdfImageList.Generate(project)));
    }

    [Fact]
    public void Dice_net_has_fifteen_cells_with_six_faces()
    {
        Project project = Project(ProjectType.Card, cards: 6, rows: 1, columns: 1);
        project.Settings.Group.Group = SettingsGroup.Dice;
        project.Settings.BlankFace = new CardFace("255_255_255_255", FaceType.Front, ImageKind.Blank);

        PdfImage[] cells = PdfImageList.Generate(project);

        Assert.Equal(15, cells.Length);
        int[] faces = [1, 3, 4, 5, 7, 10];
        Assert.Equal([F(0), F(1), F(2), F(3), F(4), F(5)], faces.Select(i => cells[i].Path));

        // The other cells are glue tabs: the hexagon image where it exists, white otherwise.
        Assert.All(new[] { 0, 2, 6, 8, 9, 11, 13 }, i => Assert.EndsWith("hexa.png", cells[i].Path));
        Assert.Equal("255_255_255_255", cells[12].Path);
        Assert.Equal("255_255_255_255", cells[14].Path);
        Assert.True(cells[2].Rotation.FlipHorizontal);
        Assert.Equal(270, cells[13].Rotation.Angle);
    }

    [Fact]
    public void Advanced_template_adds_the_rotation_of_each_place()
    {
        Project project = Project(ProjectType.AdvancedStyles, cards: 5, rows: 1, columns: 3);
        project.AdvancedTemplate = new AdvancedTemplate();
        foreach (int rotation in new[] { 0, 90, 180 })
        {
            project.AdvancedTemplate.AddCard(new AdvancedTemplateCard { Rotation = rotation });
        }

        PdfImage[] cells = PdfGenerator.Prepare(project);

        Assert.Equal(3, project.Settings.PrintLayout.CardsPerSheet);
        Assert.Equal([F(0), F(1), F(2), F(3), F(4), "-"], Paths(cells));
        Assert.Equal([0, 90, 180, 0, 90, 0], Angles(cells));
    }

    [Fact]
    public void Exif_orientation_of_image_files_is_added_to_their_rotation()
    {
        Func<string, RotationProperties> original = PdfImageList.ExifRotation;
        string file = Path.GetTempFileName();
        try
        {
            PdfImageList.ExifRotation = _ => new RotationProperties(90, flipHorizontal: true, flipVertical: false);
            Project project = Project(ProjectType.Card, cards: 1, rows: 1, columns: 1);
            project.Deck.Cards[0].Front = new CardFace(file, FaceType.Front, ImageKind.Normal);
            project.Deck.Cards[0].Front.Rotation.Angle = 180;

            PdfImage cell = Assert.Single(PdfGenerator.Prepare(project));

            Assert.Equal(270, cell.Rotation.Angle);
            Assert.True(cell.Rotation.FlipHorizontal);
        }
        finally
        {
            PdfImageList.ExifRotation = original;
            File.Delete(file);
        }
    }

    [Fact]
    public void Warnings_tell_the_cut_size_when_there_is_bleed_and_list_images_in_the_wrong_colour_space()
    {
        Project project = Project(ProjectType.Card, cards: 1, rows: 1, columns: 1);
        project.Settings.Grid.CardSize = new UnitPoint { X = Unit.FromMillimeter(69), Y = Unit.FromMillimeter(94) };
        project.Settings.Grid.Bleed = Unit.FromMillimeter(3);
        PdfImage[] cells = [new PdfImage(new CardFace(F(0), FaceType.Front, ImageKind.Blank)) { Kind = ImageKind.Normal, IsCmyk = true, Path = "/cards/cmyk.jpg" }];

        AppError warning = PdfGenerator.Warnings(project.Settings, cells, project.Deck);

        Assert.True(warning.HasWarning);
        Assert.Contains(": 63", warning.Message);
        Assert.Contains(": 88", warning.Message);
        Assert.Contains("#0->/cards/cmyk.jpg", warning.Message);
        Assert.DoesNotContain("Need Translation", warning.Message);

        project.Settings.Grid.Bleed = Unit.FromMillimeter(0);
        cells[0].IsCmyk = false;
        Assert.False(PdfGenerator.Warnings(project.Settings, cells, project.Deck).HasWarning);
    }
}
