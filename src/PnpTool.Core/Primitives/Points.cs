using System.Xml.Serialization;

namespace PnpTool.Core.Primitives;

/// <summary>Integer X/Y pair. Original: <c>PuntoXY</c>.</summary>
[XmlType("PuntoXY")]
public class PointI
{
    public int X { get; set; }

    public int Y { get; set; }

    public PointI()
    {
    }

    public PointI(int x, int y)
    {
        X = x;
        Y = y;
    }

    public PointI(PointI other)
    {
        X = other.X;
        Y = other.Y;
    }

    /// <summary>Original: <c>EsIgual</c>.</summary>
    public bool IsEqualTo(PointI other) => other.X == X && other.Y == Y;
}

/// <summary>Double X/Y pair. Original: <c>PuntoXYDoble</c>.</summary>
[XmlType("PuntoXYDoble")]
public class PointD
{
    public double X { get; set; }

    public double Y { get; set; }

    public PointD()
    {
    }

    public PointD(double x, double y)
    {
        X = x;
        Y = y;
    }

    public PointD(PointD other)
    {
        X = other.X;
        Y = other.Y;
    }

    /// <summary>Original: <c>CambiarValorAmbosEjes</c>.</summary>
    public void AddToBothAxes(double amount)
    {
        X += amount;
        Y += amount;
    }
}

/// <summary>Float X/Y pair. Original: <c>PuntoXYFloat</c>.</summary>
[XmlType("PuntoXYFloat")]
public class PointF
{
    public float X { get; set; }

    public float Y { get; set; }

    public PointF()
    {
    }

    public PointF(float x, float y)
    {
        X = x;
        Y = y;
    }

    public PointF(PointF other) => CopyFrom(other);

    /// <summary>Original: <c>Copiar</c>.</summary>
    public void CopyFrom(PointF other)
    {
        X = other.X;
        Y = other.Y;
    }
}

/// <summary>Unsigned X/Y pair. Original: <c>PuntoXYUint</c>.</summary>
[XmlType("PuntoXYUint")]
public class PointU
{
    public uint X { get; set; }

    public uint Y { get; set; }

    public PointU()
    {
    }

    public PointU(uint x, uint y)
    {
        X = x;
        Y = y;
    }

    public PointU(PointU other)
    {
        X = other.X;
        Y = other.Y;
    }
}

/// <summary>X/Y pair of lengths. Original: <c>PuntoXUnit</c>.</summary>
/// <remarks>
/// As in the original, the constructors store both values re-expressed in points,
/// whatever unit they arrive in; only the property setters keep the incoming unit.
/// </remarks>
[XmlType("PuntoXUnit")]
public class UnitPoint
{
    public Unit X { get; set; }

    public Unit Y { get; set; }

    public UnitPoint()
    {
        X = Unit.FromPoint(0);
        Y = Unit.FromPoint(0);
    }

    public UnitPoint(Unit x, Unit y)
    {
        X = Unit.FromPoint(x.Point);
        Y = Unit.FromPoint(y.Point);
    }

    public UnitPoint(UnitPoint other)
    {
        X = Unit.FromPoint(other.X.Point);
        Y = Unit.FromPoint(other.Y.Point);
    }

    /// <summary>Original: <c>IsEqualMM</c>.</summary>
    public bool EqualsInMillimeters(UnitPoint other) =>
        X.EqualsInMillimeters(other.X) && Y.EqualsInMillimeters(other.Y);

    /// <summary>Original: <c>CambiarValorAmbosEjes</c>. The result is stored in points.</summary>
    public void AddToBothAxes(Unit amount)
    {
        X = X.Point + amount.Point;
        Y = Y.Point + amount.Point;
    }
}

/// <summary>Line between two points. Original: <c>LineaPuntoXYDoble</c>.</summary>
[XmlType("LineaPuntoXYDoble")]
public class LineD
{
    [XmlElement("Desde")]
    public PointD From { get; set; }

    [XmlElement("Hasta")]
    public PointD To { get; set; }

    public LineD()
    {
        From = new PointD();
        To = new PointD();
    }

    public LineD(PointD from, PointD to)
    {
        From = new PointD(from);
        To = new PointD(to);
    }

    public LineD(double fromX, double fromY, double toX, double toY)
    {
        From = new PointD(fromX, fromY);
        To = new PointD(toX, toY);
    }

    /// <summary>Builds the line in points.</summary>
    public LineD(UnitPoint from, UnitPoint to)
    {
        From = new PointD(from.X.Point, from.Y.Point);
        To = new PointD(to.X.Point, to.Y.Point);
    }
}

/// <summary>Whole-number width and height. Replaces <c>System.Drawing.Size</c>.</summary>
public readonly record struct SizeI(int Width, int Height);
