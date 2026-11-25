using System.Globalization;
using System.Xml.Serialization;
using PnpTool.Core.Paper;
using PnpTool.Core.Primitives;
using PnpTool.Core.Xml;
using PaperSheet = PnpTool.Core.Paper.Paper;

namespace PnpTool.Core.Tests.Paper;

public class PaperTests
{
    [Theory]
    [InlineData(PaperType.A4, 210, 297)]
    [InlineData(PaperType.A5, 148, 210)]
    [InlineData(PaperType.A3, 297, 420)]
    [InlineData(PaperType.A2, 420, 594)]
    [InlineData(PaperType.A1, 594, 841)]
    [InlineData(PaperType.A0, 841, 1189)]
    [InlineData(PaperType.Letter, 216, 279)]
    [InlineData(PaperType.Legal, 216, 355)]
    [InlineData(PaperType.SorianoCovers, 500, 330)]
    [InlineData(PaperType.Custom, 210, 297)]
    public void Named_sizes_match_the_windows_table(PaperType type, int width, int height)
    {
        Assert.Equal(new SizeI(width, height), PaperSheet.GetSizeInMillimeters(type));

        UnitPoint size = PaperSheet.GetSize(type);
        Assert.Equal(width, size.X.Millimeter);
        Assert.Equal(GraphicsUnit.Millimeter, size.Y.GetGraphicsUnit());
    }

    [Theory]
    [InlineData(210, 297, PaperType.A4)]
    [InlineData(216, 279, PaperType.Letter)]
    [InlineData(216, 355, PaperType.Legal)]
    [InlineData(297, 210, PaperType.Custom)] // landscape is not matched here
    [InlineData(200, 300, PaperType.Custom)]
    public void FindType_matches_exact_portrait_sizes(int width, int height, PaperType expected)
    {
        Assert.Equal(expected, PaperSheet.FindType(width, height));
    }

    [Fact]
    public void Describe_names_the_page_and_flags_landscape()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        UnitType previousUnit = MeasurementSettings.Current;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            MeasurementSettings.Current = UnitType.Millimeter;

            Assert.Equal("210 - 297 A4 ", PaperSheet.Describe(595.28f, 841.89f));
            Assert.Equal("210 - 297 A4  + 90º", PaperSheet.Describe(841.89f, 595.28f));
            Assert.Equal("216 - 279 Carta ", PaperSheet.Describe(612f, 792f));
            Assert.Equal("100 - 150 Personalizado ", PaperSheet.Describe(283.46f, 425.2f));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            MeasurementSettings.Current = previousUnit;
        }
    }

    [Fact]
    public void ParseType_uses_file_names_and_falls_back_to_A4()
    {
        Assert.Equal(PaperType.Letter, PaperSheet.ParseType("Carta"));
        Assert.Equal(PaperType.Custom, PaperSheet.ParseType("Personalizado"));
        Assert.Equal(PaperType.SorianoTickets330x480, PaperSheet.ParseType("SorianoTickets330_480"));
        Assert.Equal(PaperType.A4, PaperSheet.ParseType("Letter"));
        Assert.Equal(PaperType.A4, PaperSheet.ParseType("nonsense"));
    }

    [Fact]
    public void Copy_and_equality()
    {
        var a = new PaperSheet { Size = PaperSheet.GetSize(PaperType.A3), Type = PaperType.A3, Landscape = true };
        var b = new PaperSheet();
        Assert.False(a.IsEqualTo(b));

        b.CopyFrom(a);
        Assert.True(a.IsEqualTo(b));
        Assert.NotSame(a.Size, b.Size);
    }

    [Fact]
    public void Xml_keeps_the_original_spanish_names()
    {
        var paper = new PaperSheet { Size = PaperSheet.GetSize(PaperType.Letter), Type = PaperType.Letter, Landscape = true };
        var serializer = new XmlSerializer(typeof(PaperSheet));
        using var writer = new StringWriter();
        serializer.Serialize(writer, paper);
        string xml = writer.ToString();

        Assert.Contains("<Papel", xml);
        Assert.Contains("<Tamano>", xml);
        Assert.Contains("<NombrePapel>Carta</NombrePapel>", xml);
        Assert.Contains("<Horizontal>true</Horizontal>", xml);

        var loaded = (PaperSheet)serializer.Deserialize(new StringReader(xml))!;
        Assert.True(loaded.IsEqualTo(paper));
    }
}

public class XmlEnumNamesTests
{
    [Fact]
    public void Renamed_members_report_their_original_names()
    {
        Assert.Equal("Sin_trasera", BackImageMode.NoBack.ToFileName());
        Assert.Equal("mm", UnitType.Millimeter.ToFileName());
        Assert.Equal("Gutterfold", ProjectType.Gutterfold.ToFileName());
        Assert.Equal("estucado_130", PaperWeightType.Coated130.ToFileName());
    }

    [Fact]
    public void File_names_parse_back_and_english_names_do_not()
    {
        Assert.True(XmlEnumNames.TryParseFileName("Trasera_igual", out BackImageMode mode));
        Assert.Equal(BackImageMode.SameBack, mode);
        Assert.False(XmlEnumNames.TryParseFileName<BackImageMode>("SameBack", out _));
        Assert.False(XmlEnumNames.TryParseFileName<BackImageMode>(null, out _));
    }

    [Fact]
    public void Numeric_values_are_unchanged_from_the_windows_version()
    {
        Assert.Equal(11, (int)ProjectType.Papermini);
        Assert.Equal(12, (int)SettingsGroup.Tipia);
        Assert.Equal(5, (int)BackImageMode.Pattern);
        Assert.Equal(2, (int)UnitType.Point);
        Assert.Equal(12, (int)PaperType.SorianoTickets225x320);
        Assert.Equal(2, (int)LayoutType.ByRows);
    }
}
