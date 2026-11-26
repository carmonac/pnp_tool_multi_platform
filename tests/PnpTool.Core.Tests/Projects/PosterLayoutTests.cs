using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;

namespace PnpTool.Core.Tests.Projects;

public sealed class PosterLayoutTests
{
    /// <summary>A sheet of 240 points square with 20 of margin: 200 printed, 20 of overlap.</summary>
    private static PosterSettings Settings(Action<PosterSettings>? change = null)
    {
        var settings = new PosterSettings
        {
            Paper = new UnitPoint { X = Unit.FromPoint(240), Y = Unit.FromPoint(240) },
            Margin = Unit.FromPoint(20),
            Overlap = Unit.FromPoint(20),
            SizeMode = PosterSizeMode.Width,
            Width = Unit.FromPoint(380),
        };
        change?.Invoke(settings);
        return settings;
    }

    private static UnitPoint Image(double width, double height) => new() { X = Unit.FromPoint(width), Y = Unit.FromPoint(height) };

    private static PosterPlan Plan(PosterSettings settings, double width = 400, double height = 400)
    {
        (AppError error, PosterPlan? plan) = PosterLayout.Plan(settings, Image(width, height));
        Assert.False(error.HasError, error.Message);
        return plan!;
    }

    [Fact]
    public void Sheets_step_by_the_printed_area_less_the_overlap()
    {
        PosterPlan plan = Plan(Settings());

        Assert.Equal((2, 2), (plan.Columns, plan.Rows));
        Assert.Equal((380.0, 380.0), (plan.PosterWidth, plan.PosterHeight));
        Assert.Equal((380.0, 380.0), (plan.CoveredWidth, plan.CoveredHeight));
        Assert.Equal(
            [(0, 0, 20.0, 20.0), (0, 1, -160.0, 20.0), (1, 0, 20.0, -160.0), (1, 1, -160.0, -160.0)],
            plan.Sheets.Select(sheet => (sheet.Row, sheet.Column, sheet.ImageLeft, sheet.ImageTop)));
    }

    [Theory]
    [InlineData(200.0, 1)]      // exactly one printed area
    [InlineData(200.5, 2)]
    [InlineData(380.0, 2)]      // exactly two with their overlap
    [InlineData(381.0, 3)]
    [InlineData(560.0, 3)]
    [InlineData(50.0, 1)]
    public void A_width_needs_as_many_sheets_as_cover_it(double width, int columns)
    {
        PosterPlan plan = Plan(Settings(settings => settings.Width = Unit.FromPoint(width)));

        Assert.Equal(columns, plan.Columns);
        Assert.True(plan.CoveredWidth >= plan.PosterWidth - 0.02);
    }

    [Fact]
    public void The_height_follows_the_proportions_of_the_image()
    {
        PosterPlan plan = Plan(Settings(), width: 100, height: 250);

        Assert.Equal(950.0, plan.PosterHeight, 6);
        Assert.Equal((2, 6), (plan.Columns, plan.Rows));       // 200 + 5 × 180 = 1100 ≥ 950
        Assert.Equal(12, plan.Sheets.Count);
    }

    [Theory]
    [InlineData(1.0, 200.0)]
    [InlineData(0.5, 100.0)]
    [InlineData(2.0, 380.0)]
    [InlineData(2.5, 470.0)]
    [InlineData(3.0, 560.0)]
    public void A_size_in_sheets_counts_the_overlaps(double sheets, double width)
    {
        PosterSettings settings = Settings(s =>
        {
            s.SizeMode = PosterSizeMode.Sheets;
            s.Sheets = sheets;
        });

        PosterPlan plan = Plan(settings);

        Assert.Equal(width, plan.PosterWidth, 6);
        Assert.Equal((int)Math.Ceiling(sheets), plan.Columns);
    }

    [Fact]
    public void A_percentage_is_taken_of_the_size_of_the_image()
    {
        PosterSettings settings = Settings(s =>
        {
            s.SizeMode = PosterSizeMode.Percent;
            s.Percent = 150;
        });

        PosterPlan plan = Plan(settings, width: 300, height: 150);

        Assert.Equal((450.0, 225.0), (plan.PosterWidth, plan.PosterHeight));
        Assert.Equal((3, 2), (plan.Columns, plan.Rows));
    }

    [Theory]
    [InlineData(PosterAlignment.Start, 0.0)]
    [InlineData(PosterAlignment.Center, 40.0)]
    [InlineData(PosterAlignment.End, 80.0)]
    public void A_poster_smaller_than_its_sheets_is_aligned(PosterAlignment alignment, double offset)
    {
        PosterSettings settings = Settings(s =>
        {
            s.Width = Unit.FromPoint(300);          // two sheets cover 380
            s.Horizontal = alignment;
            s.Vertical = alignment;
        });

        PosterPlan plan = Plan(settings);

        Assert.Equal((offset, offset), (plan.OffsetX, plan.OffsetY));
        Assert.Equal(20.0 + offset, plan.Sheets[0].ImageLeft);
        Assert.Equal(20.0 + offset - 180.0, plan.Sheets[3].ImageTop);
    }

    [Fact]
    public void Without_overlap_or_margin_the_sheets_tile_the_poster()
    {
        PosterSettings settings = Settings(s =>
        {
            s.Margin = Unit.Zero;
            s.Overlap = Unit.Zero;
            s.Width = Unit.FromPoint(480);
        });

        PosterPlan plan = Plan(settings);

        Assert.Equal((2, 2), (plan.Columns, plan.Rows));
        Assert.Equal([0.0, -240.0, 0.0, -240.0], plan.Sheets.Select(sheet => sheet.ImageLeft));
    }

    [Fact]
    public void Settings_that_cannot_work_are_refused()
    {
        static string Message(Action<PosterSettings> change) => PosterLayout.Check(Settings(change)).Message;

        Assert.False(PosterLayout.Check(Settings()).HasError);
        Assert.Contains("paper", Message(s => s.Paper = new UnitPoint()));
        Assert.Contains("margins", Message(s => s.Margin = Unit.FromPoint(120)));
        Assert.Contains("overlap", Message(s => s.Overlap = Unit.FromPoint(200)));
        Assert.Contains("no size", Message(s => s.Width = Unit.Zero));
        Assert.Contains("no size", Message(s =>
        {
            s.SizeMode = PosterSizeMode.Sheets;
            s.Sheets = 0;
        }));

        (AppError noImage, PosterPlan? none) = PosterLayout.Plan(Settings(), Image(0, 0));
        Assert.True(noImage.HasError);
        Assert.Null(none);

        (AppError tooMany, _) = PosterLayout.Plan(Settings(s => s.Width = Unit.FromPoint(100000)), Image(400, 400));
        Assert.Contains("sheets", tooMany.Message);
    }
}
