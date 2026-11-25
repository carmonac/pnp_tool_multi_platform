using PnpTool.Core.Primitives;

namespace PnpTool.Core.Tests.Primitives;

public class PointAndRotationTests
{
    [Fact]
    public void UnitPoint_constructors_store_points_and_compare_by_length()
    {
        var card = new UnitPoint(Unit.FromMillimeter(63.5), Unit.FromInch(3.5));
        var copy = new UnitPoint(card);

        Assert.Equal(GraphicsUnit.Point, card.X.GetGraphicsUnit());
        Assert.Equal(180.0, card.X.GetValue(), 10);
        Assert.Equal(252.0, copy.Y.Point, 10);
        Assert.True(copy.EqualsInMillimeters(new UnitPoint { X = Unit.FromInch(2.5), Y = Unit.FromMillimeter(88.9) }));
    }

    [Fact]
    public void UnitPoint_AddToBothAxes_adds_in_points()
    {
        var point = new UnitPoint { X = Unit.FromInch(1), Y = Unit.FromPoint(10) };
        point.AddToBothAxes(Unit.FromInch(0.5));

        Assert.Equal(108.0, point.X.Point, 10);
        Assert.Equal(46.0, point.Y.Point, 10);
        Assert.Equal(GraphicsUnit.Point, point.X.GetGraphicsUnit());
    }

    [Fact]
    public void Line_from_unit_points_is_in_points_and_copies_its_ends()
    {
        var line = new LineD(new UnitPoint { X = Unit.FromInch(1), Y = Unit.FromInch(2) }, new UnitPoint());
        Assert.Equal(72.0, line.From.X, 10);
        Assert.Equal(144.0, line.From.Y, 10);

        var from = new PointD(1, 2);
        var copied = new LineD(from, new PointD(3, 4));
        from.AddToBothAxes(10);
        Assert.Equal(1.0, copied.From.X);
        Assert.Equal(11.0, from.X);
    }

    [Theory]
    [InlineData(-90, 270)]
    [InlineData(450, 90)]
    [InlineData(360, 0)]
    [InlineData(-720, 0)]
    public void Rotation_angle_is_normalised(int input, int expected)
    {
        Assert.Equal(expected, new RotationProperties(input, false, false).Angle);
    }

    [Fact]
    public void Rotation_combine_adds_angles_and_toggles_flips()
    {
        var rotation = new RotationProperties(270, flipHorizontal: true, flipVertical: false);
        rotation.Combine(new RotationProperties(180, flipHorizontal: true, flipVertical: true));

        Assert.True(rotation.IsEqualTo(new RotationProperties(90, flipHorizontal: false, flipVertical: true)));
    }

    [Theory]
    [InlineData(1, 0, false, false)]
    [InlineData(2, 0, true, false)]
    [InlineData(3, 180, false, false)]
    [InlineData(4, 0, false, true)]
    [InlineData(5, 90, true, false)]
    [InlineData(6, 90, false, false)]
    [InlineData(7, 90, true, false)]
    [InlineData(8, 270, false, false)]
    [InlineData(0, 0, false, false)]
    public void Exif_orientation_table_matches_the_windows_version(int orientation, int angle, bool flipH, bool flipV)
    {
        RotationProperties rotation = RotationProperties.FromExifOrientation(orientation);

        Assert.Equal(angle, rotation.Angle);
        Assert.Equal(flipH, rotation.FlipHorizontal);
        Assert.Equal(flipV, rotation.FlipVertical);
    }
}
