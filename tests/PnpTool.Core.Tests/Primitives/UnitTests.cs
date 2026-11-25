using System.Globalization;
using System.Xml.Serialization;
using PnpTool.Core.Primitives;

namespace PnpTool.Core.Tests.Primitives;

public class UnitTests
{
    private const int Precision = 10;

    [Fact]
    public void Poker_card_width_converts_between_all_units()
    {
        // 63.5 mm = 2.5 in = 180 pt = 240 px at 96 dpi; values as stored in Cartas_Standar.pnps.
        Unit width = Unit.FromMillimeter(63.5);

        Assert.Equal(180.0, width.Point, Precision);
        Assert.Equal(2.5, width.Inch, Precision);
        Assert.Equal(6.35, width.Centimeter, Precision);
        Assert.Equal(240.0, width.Presentation, Precision);
        Assert.Equal(63.5, width.Millimeter);
    }

    [Theory]
    [InlineData(GraphicsUnit.Point, 72.0)]
    [InlineData(GraphicsUnit.Inch, 1.0)]
    [InlineData(GraphicsUnit.Millimeter, 25.4)]
    [InlineData(GraphicsUnit.Centimeter, 2.54)]
    [InlineData(GraphicsUnit.Presentation, 96.0)]
    public void One_inch_in_any_unit_is_72_points(GraphicsUnit type, double value)
    {
        var unit = new Unit(value, type);

        Assert.Equal(72.0, unit.Point, Precision);
        Assert.Equal(1.0, unit.Inch, Precision);
        Assert.Equal(25.4, unit.Millimeter, Precision);
        Assert.Equal(2.54, unit.Centimeter, Precision);
        Assert.Equal(96.0, unit.Presentation, Precision);
    }

    [Fact]
    public void Plain_numbers_are_points()
    {
        Unit fromDouble = 12.5;
        Unit fromInt = 3;
        double back = Unit.FromInch(1);

        Assert.Equal(GraphicsUnit.Point, fromDouble.GetGraphicsUnit());
        Assert.Equal(12.5, fromDouble.GetValue());
        Assert.Equal(3.0, fromInt.Point);
        Assert.Equal(72.0, back);
    }

    [Theory]
    [InlineData("63.5mm", 63.5, GraphicsUnit.Millimeter)]
    [InlineData(" 2,5 in ", 2.5, GraphicsUnit.Inch)]
    [InlineData("1.2CM", 1.2, GraphicsUnit.Centimeter)]
    [InlineData("-4pt", -4.0, GraphicsUnit.Point)]
    [InlineData("10", 10.0, GraphicsUnit.Point)]
    [InlineData("96pu", 96.0, GraphicsUnit.Presentation)]
    public void Parse_reads_number_and_suffix(string text, double value, GraphicsUnit type)
    {
        Unit unit = Unit.Parse(text);

        Assert.Equal(value, unit.GetValue());
        Assert.Equal(type, unit.GetGraphicsUnit());
    }

    [Theory]
    [InlineData("5 furlongs")]
    [InlineData("mm")]
    public void Parse_rejects_unknown_suffix_or_missing_number(string text)
    {
        Assert.Throws<ArgumentException>(() => Unit.Parse(text));
    }

    [Fact]
    public void Equality_requires_same_unit_but_millimeter_comparison_does_not()
    {
        Unit inch = Unit.FromInch(1);
        Unit points = Unit.FromPoint(72);

        Assert.True(inch != points);
        Assert.True(inch == Unit.FromInch(1));
        Assert.True(inch.EqualsInMillimeters(points));
        Assert.False(inch.IsGreaterInMillimeters(points));
        Assert.True(Unit.FromMillimeter(25.41).IsGreaterInMillimeters(inch));
        Assert.False(Unit.FromMillimeter(25.404).IsGreaterInMillimeters(inch)); // same once rounded to 0.01 mm
    }

    [Fact]
    public void ConvertType_keeps_the_length()
    {
        Unit unit = Unit.FromMillimeter(25.4);
        unit.ConvertType(GraphicsUnit.Inch);

        Assert.Equal(GraphicsUnit.Inch, unit.GetGraphicsUnit());
        Assert.Equal(1.0, unit.GetValue(), Precision);
    }

    [Fact]
    public void Rounding_helpers_use_two_decimals()
    {
        Unit unit = Unit.FromPoint(100);

        Assert.Equal(35.28, unit.RoundedMillimeters());
        Assert.Equal(1.39, unit.RoundedInches());
        Assert.Equal(100.0, unit.RoundedPoints());
    }

    [Fact]
    public void ToStringWithSuffix_is_invariant()
    {
        Assert.Equal("63.5mm", Unit.FromMillimeter(63.5).ToStringWithSuffix());
        Assert.Equal("2.5in", Unit.FromInch(2.5).ToStringWithSuffix());
    }

    [Fact]
    public void Display_strings_use_a_dot_even_in_comma_cultures()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        UnitType previousUnit = MeasurementSettings.Current;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("es-ES");
            MeasurementSettings.Current = UnitType.Millimeter;
            Unit width = Unit.FromInch(2.5);

            Assert.Equal("63.5", width.ToDisplayString());
            Assert.Equal("63.5mm", width.ToDisplayString(withSuffix: true, newLine: false));
            Assert.Equal("2.5", width.ToDisplayString(UnitType.Inch, withSuffix: false, newLine: false));
            Assert.Equal("180", width.ToDisplayString(UnitType.Point, withSuffix: false, newLine: false));

            Unit typed = Unit.FromDisplayString("63.5");
            Assert.Equal(GraphicsUnit.Millimeter, typed.GetGraphicsUnit());
            Assert.Equal(63.5, typed.GetValue());
            Assert.Equal(1.25, Unit.FromDisplayString("1.25", UnitType.Inch).Inch);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            MeasurementSettings.Current = previousUnit;
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("-")]
    [InlineData(".")]
    public void Incomplete_input_reads_as_zero(string text)
    {
        Assert.Equal(0.0, Unit.FromDisplayString(text, UnitType.Millimeter).Millimeter);
    }

    [Fact]
    public void ReinterpretInCurrentUnit_relabels_the_point_value()
    {
        UnitType previousUnit = MeasurementSettings.Current;
        try
        {
            MeasurementSettings.Current = UnitType.Millimeter;
            Unit result = Unit.FromPoint(10).ReinterpretInCurrentUnit();

            Assert.Equal(GraphicsUnit.Millimeter, result.GetGraphicsUnit());
            Assert.Equal(10.0, result.GetValue());
        }
        finally
        {
            MeasurementSettings.Current = previousUnit;
        }
    }

    [Fact]
    public void Xml_writes_only_points_and_reads_files_from_older_versions()
    {
        var serializer = new XmlSerializer(typeof(UnitPoint));
        using var writer = new StringWriter();
        serializer.Serialize(writer, new UnitPoint { X = Unit.FromMillimeter(63.5), Y = Unit.FromInch(3.5) });
        string xml = writer.ToString();

        Assert.Contains("<X><Point>180</Point></X>", xml.Replace("\r", "").Replace("\n", "").Replace(" ", ""));
        Assert.DoesNotContain("Millimeter", xml);

        // Shape written by version 20.9.3 (Cartas_Standar.pnps): all five values present.
        const string Old = """
            <PuntoXUnit>
              <X><Point>180</Point><Inch>2.5</Inch><Millimeter>63.5</Millimeter><Centimeter>6.35</Centimeter><Presentation>240</Presentation></X>
              <Y><Point>252.00000000000003</Point><Inch>3.5000000000000004</Inch><Millimeter>88.9</Millimeter><Centimeter>8.89</Centimeter><Presentation>336.00000000000006</Presentation></Y>
            </PuntoXUnit>
            """;
        var loaded = (UnitPoint)serializer.Deserialize(new StringReader(Old))!;

        Assert.Equal(180.0, loaded.X.Point);
        Assert.Equal(88.9, loaded.Y.Millimeter, Precision);
        Assert.Equal(GraphicsUnit.Point, loaded.Y.GetGraphicsUnit());
    }
}
