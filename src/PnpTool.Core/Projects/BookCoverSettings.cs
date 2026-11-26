using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Paper;
using PnpTool.Core.Primitives;

namespace PnpTool.Core.Projects;

/// <summary>The three images of the cover of a book, from left to right. Original: <c>FormPortadaLibroModelo.RutasImagenes</c>.</summary>
public enum BookCoverPart
{
    Left,
    Spine,
    Right,
}

/// <summary>A kind of paper with the thickness of one of its pages. Original: <c>ComboboxItemVolumenPapel</c>.</summary>
public sealed record PaperStockOption(string Text, PaperWeightType Value, double PageThickness)
{
    public override string ToString() => Text;

    /// <summary>
    /// The kinds of paper offered, with the thickness of a page in millimetres. The original
    /// named five of them after the printing company; here they are named by what they are.
    /// </summary>
    public static PaperStockOption[] All() =>
    [
        new("A4 Standar", PaperWeightType.Plain80, 0.010695187),
        new("120gr Crema", PaperWeightType.Cream120, 0.01417),
        new("100gr Blanco", PaperWeightType.White100, 0.01206),
        new("Estucado 130gr", PaperWeightType.Coated130, 0.01006),
        new("Estucado 150gr", PaperWeightType.Coated150, 0.01205),
        new("Estucado 170gr", PaperWeightType.Coated170, 0.01398),
    ];
}

/// <summary>
/// The arithmetic and checks of the cover of a book: back cover, spine and front cover side
/// by side on one sheet, with bleed. Original: <c>FormPortadaLibroModelo</c>.
/// </summary>
public static class BookCover
{
    /// <summary>The width of the spine, in millimetres, for the pages and paper chosen. Original: <c>DevuelveTamanoLomo</c>.</summary>
    public static double EstimatedSpine(BookCoverSettings settings) => (settings.PageCount * settings.PaperVolume) + 0.015;

    /// <summary>
    /// The sheet as it is printed: with its long side across when it is marked as landscape.
    /// </summary>
    /// <remarks>
    /// The original swapped width and height whenever the sheet was marked as landscape. That
    /// works for the papers stored upright, but the sheet for covers is stored as 500 by 330,
    /// already on its side, so the swap stood it upright and covers that fit it were refused.
    /// </remarks>
    public static UnitPoint OrientedPaper(BookCoverSettings settings)
    {
        Unit width = settings.PaperSize.X;
        Unit height = settings.PaperSize.Y;
        return settings.PaperLandscape && (double)height > (double)width
            ? new UnitPoint { X = height, Y = width }
            : new UnitPoint { X = width, Y = height };
    }

    /// <summary>The room the whole cover takes, bleed included. Original: <c>TamanoMarco</c>.</summary>
    public static UnitPoint Frame(BookCoverSettings settings) => new()
    {
        X = (((double)settings.CoverSize.X + (double)settings.Bleed) * 2.0) + (double)settings.SpineWidth,
        Y = (double)settings.CoverSize.Y + ((double)settings.Bleed * 2.0),
    };

    /// <summary>
    /// What is wrong with the settings, if anything; the size of the frame is noted in them.
    /// Original: <c>ComprobarConfiguracion</c>.
    /// </summary>
    public static AppError Check(BookCoverSettings settings)
    {
        var error = new AppError();
        string? problem = null;
        if ((double)settings.CoverSize.X <= 0.0 || (double)settings.CoverSize.Y <= 0.0)
        {
            problem = "Cover Size " + StringResources.Get("Tamano") + "<=0";
        }
        else if (settings.SpineWidth.Millimeter <= 0.0)
        {
            problem = "Spine Size <=0";
        }
        else if ((double)settings.PaperSize.X <= 0.0 || (double)settings.PaperSize.Y <= 0.0)
        {
            problem = "Paper Size " + StringResources.Get("Tamano") + "<=0";
        }
        else if (settings.PaperVolume <= 0.0)
        {
            problem = StringResources.Get("Papel") + " " + StringResources.Get("grosor") + "<=0";
        }
        else if (settings.LeftImagePath.Length == 0 || settings.SpineImagePath.Length == 0 || settings.RightImagePath.Length == 0)
        {
            problem = StringResources.Get("ShellMenuBarItem_Imagenes_AnadirImagen");
        }
        else
        {
            UnitPoint paper = OrientedPaper(settings);
            UnitPoint frame = Frame(settings);
            settings.FrameSize = frame;
            if ((double)frame.X > (double)paper.X || (double)frame.Y > (double)paper.Y)
            {
                problem = StringResources.Get("Form_PortadaLibro_No_Caben")
                    + "X:" + Millimetres(frame.X) + ">" + Millimetres(paper.X) + "; Y:" + Millimetres(frame.Y) + ">" + Millimetres(paper.Y);
            }

            if (settings.Effect != ImageEffectType.None && (double)settings.Bleed <= 0.0)
            {
                problem = StringResources.Get("Form_Setup_LabelSangrado") + "<=0";
            }
        }

        if (problem != null)
        {
            error.Kind = ErrorKind.Error;
            error.Message = problem;
        }

        return error;
    }

    private static string Millimetres(Unit length) =>
        Math.Round(length.Millimeter, 2).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
