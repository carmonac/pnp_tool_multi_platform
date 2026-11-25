using System.ComponentModel;
using System.Globalization;
using System.Xml.Serialization;

namespace PnpTool.Core.Primitives;

/// <summary>
/// A length that remembers the unit it was entered in. Original: <c>XUnit</c>.
/// The arithmetic is kept identical to the Windows version so layouts match.
/// </summary>
/// <remarks>
/// Serialised to XML through <see cref="Point"/> only; files written by older
/// versions also carry Inch, Millimeter, Centimeter and Presentation elements,
/// which are ignored on load.
/// </remarks>
[XmlType("XUnit")]
public struct Unit : IFormattable
{
    public static readonly Unit Zero;

    private double _value;
    private GraphicsUnit _type;

    public Unit(double point)
    {
        _value = point;
        _type = GraphicsUnit.Point;
    }

    public Unit(double value, GraphicsUnit type)
    {
        if (!Enum.IsDefined(type))
        {
            throw new InvalidEnumArgumentException(nameof(type), (int)type, typeof(GraphicsUnit));
        }

        _value = value;
        _type = type;
    }

    public double Point
    {
        readonly get => _type switch
        {
            GraphicsUnit.Point => _value,
            GraphicsUnit.Inch => _value * 72.0,
            GraphicsUnit.Millimeter => _value * 72.0 / 25.4,
            GraphicsUnit.Centimeter => _value * 72.0 / 2.54,
            GraphicsUnit.Presentation => _value * 72.0 / 96.0,
            _ => throw new InvalidCastException(),
        };
        set
        {
            _value = value;
            _type = GraphicsUnit.Point;
        }
    }

    [XmlIgnore]
    public double Inch
    {
        readonly get => _type switch
        {
            GraphicsUnit.Point => _value / 72.0,
            GraphicsUnit.Inch => _value,
            GraphicsUnit.Millimeter => _value / 25.4,
            GraphicsUnit.Centimeter => _value / 2.54,
            GraphicsUnit.Presentation => _value / 96.0,
            _ => throw new InvalidCastException(),
        };
        set
        {
            _value = value;
            _type = GraphicsUnit.Inch;
        }
    }

    [XmlIgnore]
    public double Millimeter
    {
        readonly get => _type switch
        {
            GraphicsUnit.Point => _value * 25.4 / 72.0,
            GraphicsUnit.Inch => _value * 25.4,
            GraphicsUnit.Millimeter => _value,
            GraphicsUnit.Centimeter => _value * 10.0,
            GraphicsUnit.Presentation => _value * 25.4 / 96.0,
            _ => throw new InvalidCastException(),
        };
        set
        {
            _value = value;
            _type = GraphicsUnit.Millimeter;
        }
    }

    [XmlIgnore]
    public double Centimeter
    {
        readonly get => _type switch
        {
            GraphicsUnit.Point => _value * 2.54 / 72.0,
            GraphicsUnit.Inch => _value * 2.54,
            GraphicsUnit.Millimeter => _value / 10.0,
            GraphicsUnit.Centimeter => _value,
            GraphicsUnit.Presentation => _value * 2.54 / 96.0,
            _ => throw new InvalidCastException(),
        };
        set
        {
            _value = value;
            _type = GraphicsUnit.Centimeter;
        }
    }

    [XmlIgnore]
    public double Presentation
    {
        readonly get => _type switch
        {
            GraphicsUnit.Point => _value * 96.0 / 72.0,
            GraphicsUnit.Inch => _value * 96.0,
            GraphicsUnit.Millimeter => _value * 96.0 / 25.4,
            GraphicsUnit.Centimeter => _value * 96.0 / 2.54,
            GraphicsUnit.Presentation => _value,
            _ => throw new InvalidCastException(),
        };
        set
        {
            _value = value;
            _type = GraphicsUnit.Presentation;
        }
    }

    /// <summary>The stored number, in whatever unit <see cref="GetGraphicsUnit"/> reports.</summary>
    public readonly double GetValue() => _value;

    public readonly GraphicsUnit GetGraphicsUnit() => _type;

    public static Unit FromPoint(double value) => new() { _value = value, _type = GraphicsUnit.Point };

    public static Unit FromInch(double value) => new() { _value = value, _type = GraphicsUnit.Inch };

    public static Unit FromMillimeter(double value) => new() { _value = value, _type = GraphicsUnit.Millimeter };

    public static Unit FromCentimeter(double value) => new() { _value = value, _type = GraphicsUnit.Centimeter };

    public static Unit FromPresentation(double value) => new() { _value = value, _type = GraphicsUnit.Presentation };

    // ---- formatting and parsing

    public readonly string ToString(IFormatProvider formatProvider) => _value.ToString(formatProvider) + GetSuffix();

    readonly string IFormattable.ToString(string? format, IFormatProvider? formatProvider) =>
        _value.ToString(format, formatProvider) + GetSuffix();

    public readonly string ToStringWithSuffix() => _value.ToString(CultureInfo.InvariantCulture) + GetSuffix();

    private readonly string GetSuffix() => _type switch
    {
        GraphicsUnit.Point => "pt",
        GraphicsUnit.Inch => "in",
        GraphicsUnit.Millimeter => "mm",
        GraphicsUnit.Centimeter => "cm",
        GraphicsUnit.Presentation => "pu",
        _ => throw new InvalidCastException(),
    };

    /// <summary>Parses text such as "63.5mm", "2,5in" or "12" (points when no suffix is given).</summary>
    public static Unit Parse(string value) => value;

    public static implicit operator Unit(string value)
    {
        value = value.Trim();
        value = value.Replace(',', '.');
        int count = value.Length;
        int numberLength;
        for (numberLength = 0; numberLength < count; numberLength++)
        {
            char ch = value[numberLength];
            if (ch != '.' && ch != '-' && ch != '+' && !char.IsNumber(ch))
            {
                break;
            }
        }

        Unit unit = default;
        try
        {
            unit._value = double.Parse(value[..numberLength].Trim(), CultureInfo.InvariantCulture);
        }
        catch (Exception innerException)
        {
            throw new ArgumentException($"String '{value}' is not a valid value for structure 'Unit'.", innerException);
        }

        unit._type = ParseSuffix(value[numberLength..].Trim().ToLower());
        return unit;
    }

    private static GraphicsUnit ParseSuffix(string suffix) => suffix switch
    {
        "" or "pt" => GraphicsUnit.Point,
        "cm" => GraphicsUnit.Centimeter,
        "in" => GraphicsUnit.Inch,
        "mm" => GraphicsUnit.Millimeter,
        "pu" => GraphicsUnit.Presentation,
        _ => throw new ArgumentException("Unknown unit type: '" + suffix + "'"),
    };

    // ---- conversions and equality

    public static implicit operator Unit(int value) => FromPoint(value);

    public static implicit operator Unit(double value) => FromPoint(value);

    /// <summary>The length in points.</summary>
    public static implicit operator double(Unit value) => value.Point;

    /// <summary>Equal only when both the unit and the stored number match; 1in and 72pt are different.</summary>
    public static bool operator ==(Unit value1, Unit value2) =>
        value1._type == value2._type && value1._value == value2._value;

    public static bool operator !=(Unit value1, Unit value2) => !(value1 == value2);

    public override readonly bool Equals(object? obj) => obj is Unit unit && this == unit;

    public override readonly int GetHashCode() => _value.GetHashCode() ^ _type.GetHashCode();

    /// <summary>Original: <c>RedondeaMM</c>.</summary>
    public readonly double RoundedMillimeters() => Math.Round(Millimeter, 2);

    /// <summary>Original: <c>RedondeaInch</c>.</summary>
    public readonly double RoundedInches() => Math.Round(Inch, 2);

    /// <summary>Original: <c>RedondeaPt</c>.</summary>
    public readonly double RoundedPoints() => Math.Round(Point, 2);

    /// <summary>Same length to 0.01 mm, whatever the units. Original: <c>IsEqualMM</c>.</summary>
    public readonly bool EqualsInMillimeters(Unit other) => Math.Round(Millimeter, 2) == Math.Round(other.Millimeter, 2);

    /// <summary>Original: <c>IsGreaterMM</c>.</summary>
    public readonly bool IsGreaterInMillimeters(Unit other) => Math.Round(Millimeter, 2) > Math.Round(other.Millimeter, 2);

    /// <summary>Re-expresses the same length in another unit.</summary>
    public void ConvertType(GraphicsUnit type)
    {
        if (_type == type)
        {
            return;
        }

        _value = type switch
        {
            GraphicsUnit.Point => Point,
            GraphicsUnit.Inch => Inch,
            GraphicsUnit.Centimeter => Centimeter,
            GraphicsUnit.Millimeter => Millimeter,
            GraphicsUnit.Presentation => Presentation,
            _ => throw new ArgumentException("Unknown unit type: '" + type + "'"),
        };
        _type = type;
    }

    // ---- values typed by or shown to the user, in the current measurement system

    /// <summary>
    /// Takes this value's size in points as a bare number and re-labels that number
    /// with the user's measurement system: 10pt becomes 10mm when working in millimetres.
    /// Original: <c>DevuelveValorDobleAXunit</c>.
    /// </summary>
    public readonly Unit ReinterpretInCurrentUnit() => MeasurementSettings.Current switch
    {
        UnitType.Millimeter => FromMillimeter(this),
        UnitType.Inch => FromInch(this),
        UnitType.Point => FromPoint(this),
        _ => FromPresentation(this),
    };

    /// <summary>Original: <c>DevuelveValorXUnitAString</c>.</summary>
    public readonly string ToDisplayString() => ToDisplayString(MeasurementSettings.Current, withSuffix: false, newLine: false);

    public readonly string ToDisplayString(bool withSuffix, bool newLine) =>
        ToDisplayString(MeasurementSettings.Current, withSuffix, newLine);

    /// <summary>
    /// The length rounded to two decimals in <paramref name="unit"/>, always with a dot
    /// as decimal separator.
    /// </summary>
    public readonly string ToDisplayString(UnitType unit, bool withSuffix, bool newLine)
    {
        string text = unit switch
        {
            UnitType.Millimeter => RoundedMillimeters().ToString(),
            UnitType.Inch => RoundedInches().ToString(),
            UnitType.Point => RoundedPoints().ToString(),
            _ => RoundedMillimeters().ToString(),
        };
        if (withSuffix)
        {
            text += MeasurementSettings.Suffix;
        }

        if (newLine)
        {
            text += Environment.NewLine;
        }

        return text.Replace(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0], '.');
    }

    /// <summary>Original: <c>DevuelveValorStringAXUnit</c>.</summary>
    public static Unit FromDisplayString(string text) => FromDisplayString(text, MeasurementSettings.Current);

    /// <summary>Reads a number typed with a dot as decimal separator; empty, "-" and "." count as zero.</summary>
    public static Unit FromDisplayString(string text, UnitType sourceUnit)
    {
        if (text == string.Empty || text == "-" || text == ".")
        {
            text = "0";
        }

        double number = Convert.ToDouble(text.Replace('.', CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0]));
        return sourceUnit switch
        {
            UnitType.Point => FromPoint(number),
            UnitType.Millimeter => FromMillimeter(number),
            UnitType.Inch => FromInch(number),
            _ => Zero,
        };
    }
}
