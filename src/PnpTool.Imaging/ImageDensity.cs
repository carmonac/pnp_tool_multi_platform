using ImageMagick;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;

namespace PnpTool.Imaging;

/// <summary>
/// Resolution of images (dots per inch) and what follows from it: the printed size of an
/// image and changes of resolution. Original: the density members of <c>ImagenesGeneral</c>.
/// </summary>
public static class ImageDensity
{
    /// <summary>Resolution assumed for an image that states none. Original: <c>DensidadDefecto</c>.</summary>
    public const int Default = 96;

    private const string UpscaleError = "error while upscaling dpi";

    /// <summary>
    /// The density in dots per inch. PNG files record dots per metre, which Magick reports
    /// per centimetre: a 300 dpi PNG arrives as 118.11.
    /// </summary>
    /// <remarks>
    /// The original read the numbers as dots per inch whatever the unit, so it took every PNG
    /// to be 2.54 times larger on paper than a JPEG of the same pixels and resolution. That
    /// is corrected here; see "Decisions taken while porting" in docs/porting-map.md.
    /// </remarks>
    public static Density? PerInch(Density? density) =>
        density is { Units: DensityUnit.PixelsPerCentimeter } ? density.ChangeUnits(DensityUnit.PixelsPerInch) : density;

    /// <summary>
    /// The stated resolution in dots per inch rounded to whole numbers, with
    /// <see cref="Default"/> wherever none is stated. Original: <c>Comprueba_Modifica_Densidad</c>.
    /// </summary>
    public static PointU Normalize(Density? density)
    {
        density = PerInch(density);
        if (density == null)
        {
            return new PointU(Default, Default);
        }

        var result = new PointU(Convert.ToUInt32(density.X), Convert.ToUInt32(density.Y));
        if (result.X == 0)
        {
            result.X = Default;
        }

        if (result.Y == 0)
        {
            result.Y = Default;
        }

        return result;
    }

    /// <summary>Original: <c>Comprueba_Modifica_Densidad(string)</c>.</summary>
    public static PointU Normalize(string path) => Normalize(new MagickImageInfo(path).Density);

    /// <summary>
    /// The size an image of the given pixel size prints at; <see cref="Default"/> is assumed
    /// where no resolution is stated. Original: <c>AjustaTamanoXDensidad</c>.
    /// </summary>
    public static UnitPoint PrintedSize(Density? density, uint width, uint height)
    {
        density = PerInch(density);
        var size = new UnitPoint();
        int dpi = density != null ? Convert.ToInt32(density.X) : 0;
        if (dpi == 0)
        {
            dpi = Default;
        }

        size.X = Unit.FromInch((double)width / dpi);
        dpi = density != null ? Convert.ToInt32(density.Y) : 0;
        if (dpi == 0)
        {
            dpi = Default;
        }

        size.Y = Unit.FromInch((double)height / dpi);
        return size;
    }

    public static UnitPoint PrintedSize(IMagickImageInfo info) => PrintedSize(info.Density, info.Width, info.Height);

    /// <summary>
    /// The resolution the file states in dots per inch, (0, 0) when it states none.
    /// Original: <c>DevuelveDPIImagen</c> and the <c>PuntoXYDoble(Density)</c> constructor.
    /// </summary>
    public static PointD Of(string path)
    {
        Density? density = PerInch(new MagickImageInfo(path).Density);
        return density == null ? new PointD() : new PointD(density.X, density.Y);
    }

    /// <summary>
    /// Writes a copy of the image in the temporary folder that states the given resolution;
    /// the pixels are not changed. Returns the path of the copy. Original: <c>CambiarDPI</c>.
    /// </summary>
    public static (AppError Error, string Path) ChangeDpi(string path, int targetDpi)
    {
        var error = new AppError();
        string copy = string.Empty;
        try
        {
            var info = new MagickImageInfo(path);
            (copy, bool mustCreate) = TempImageFiles.UniqueName(path, info.Format.ToString());
            if (mustCreate)
            {
                using var image = new MagickImage(path) { Density = new Density(targetDpi, DensityUnit.PixelsPerInch) };
                image.ResetPage();
                image.Write(copy);
            }

            if (!File.Exists(copy))
            {
                error.Kind = ErrorKind.Error;
                error.Message = "Fichero creado no existe - " + copy;
            }
        }
        catch (Exception exception) when (exception is MagickException or IOException or UnauthorizedAccessException)
        {
            error.Kind = ErrorKind.Error;
            error.Message = UpscaleError;
        }

        return (error, copy);
    }

    /// <summary>
    /// <see cref="ChangeDpi"/> with the resolution chosen for the image: <paramref name="dpi"/>
    /// as given, or with <paramref name="force"/> the program's target resolution for an image
    /// below <paramref name="dpi"/> and the image's own resolution otherwise.
    /// Original: <c>SubirDPI(string, bool, int)</c>.
    /// </summary>
    public static (AppError Error, string Path) RaiseDpi(string path, bool force, int dpi)
    {
        double current = Of(path).X;
        int target = dpi;
        if (force)
        {
            target = current < dpi ? AppSettings.TargetDpi : Convert.ToInt32(current);
        }

        return ChangeDpi(path, target);
    }

    /// <summary>
    /// Resamples the image to <paramref name="targetDpi"/>, so that it keeps its printed size
    /// with more or fewer pixels. An image that states no resolution is first given the one
    /// that makes it print at <paramref name="printedSize"/>, or <see cref="Default"/>.
    /// Original: <c>SubirDPI(ref MagickImage, int, PuntoXUnit)</c>.
    /// </summary>
    public static AppError RaiseDpi(MagickImage image, int targetDpi, UnitPoint? printedSize)
    {
        var error = new AppError();
        try
        {
            switch (image.Density.Units)
            {
                case DensityUnit.Undefined:
                    if (printedSize == null)
                    {
                        image.Density = new Density(Default, DensityUnit.PixelsPerInch);
                        break;
                    }

                    int x = Convert.ToInt32(image.Width / printedSize.X.Inch);
                    int y = Convert.ToInt32(image.Height / printedSize.Y.Inch);
                    image.Density = new Density(x, y, DensityUnit.PixelsPerInch);
                    break;
                case DensityUnit.PixelsPerCentimeter:
                    image.Density = new Density(Math.Round(image.Density.X * 2.54, 0), DensityUnit.PixelsPerInch);
                    break;
            }

            if (Convert.ToInt32(image.Density.X) != targetDpi)
            {
                image.Resample(targetDpi, targetDpi);
                image.Density = new Density(targetDpi, DensityUnit.PixelsPerInch);
            }
        }
        catch (Exception exception) when (exception is MagickException or OverflowException or DivideByZeroException)
        {
            error.Kind = ErrorKind.Error;
            error.Message = UpscaleError;
        }

        return error;
    }

    /// <summary>
    /// Checks the resolution of a file against the limits in the settings. The message of the
    /// error is a code: "_1" below the minimum, "_2" above the maximum. Original: <c>ValidarFormatoImagen</c>.
    /// </summary>
    /// <remarks>
    /// The original also had "_0 Unknown" and "_3" for a file without density information, but
    /// such a file fails the minimum first, so "_1" is what comes out; kept as it is.
    /// </remarks>
    public static AppError Validate(string path)
    {
        var error = new AppError();
        var info = new MagickImageInfo(path);
        PointI dpi;
        bool unknown = false;
        if (info.Density == null)
        {
            dpi = new PointI(0, 0);
            unknown = true;
        }
        else
        {
            Density perInch = info.Density.ChangeUnits(DensityUnit.PixelsPerInch);
            dpi = new PointI(Convert.ToInt32(perInch.X), Convert.ToInt32(perInch.Y));
        }

        if (dpi.X < AppSettings.MinimumDpi || dpi.Y < AppSettings.MinimumDpi)
        {
            return new AppError { Kind = ErrorKind.Error, Message = "_1" };
        }

        if (dpi.X > AppSettings.MaximumDpi || dpi.Y > AppSettings.MaximumDpi)
        {
            return new AppError { Kind = ErrorKind.Error, Message = "_2" };
        }

        if (unknown)
        {
            return new AppError { Kind = ErrorKind.Error, Message = "_3 " + dpi.X + "X -- " + dpi.Y + "Y" };
        }

        return error;
    }
}
