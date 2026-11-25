namespace PnpTool.Core.Primitives;

/// <summary>Unit in which a <see cref="Unit"/> stores its value. Original: <c>XGraphicsUnit</c>.</summary>
public enum GraphicsUnit
{
    Point,
    Inch,
    Millimeter,
    Centimeter,

    /// <summary>Device-independent pixel, 1/96 inch.</summary>
    Presentation,
}
