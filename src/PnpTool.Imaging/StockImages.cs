using PnpTool.Core;
using PnpTool.Core.Infrastructure;

namespace PnpTool.Imaging;

/// <summary>Images shipped with the program. Original: <c>ImagenesGeneral.ImagenesEnResources</c>, without the printing company's logo.</summary>
public enum StockImage
{
    /// <summary>Strip printed to check the printer's scale. Original: <c>calibracion</c>.</summary>
    Calibration,

    /// <summary>Target mark. Original: <c>marcacalibracion</c>.</summary>
    CalibrationMark,

    /// <summary>Shown in place of an image that cannot be found. Original: <c>imagennoencontrada</c>.</summary>
    NotFound,
}

/// <summary>Paths of the images shipped with the program, in <see cref="AppSettings.ImagesFolder"/>.</summary>
public static class StockImages
{
    /// <summary>Original: <c>DevuelveRuta</c>.</summary>
    public static string PathOf(StockImage image) => Path.Combine(
        AppSettings.ImagesFolder,
        image switch
        {
            StockImage.CalibrationMark => "calibracion_marca.jpg",
            StockImage.Calibration => "calibracion.jpg",
            _ => "Image_NotFound.jpg",
        });

    /// <summary>Hexagon outline used for dice. Original: <c>CreaHexagonoDado</c>.</summary>
    public static string HexagonPath => Path.Combine(AppSettings.ImagesFolder, "hexa.png");

    /// <summary>
    /// The path itself, or the "not found" image with an error when the file does not exist.
    /// Original: <c>CambiaRutaSiNoExiste</c>.
    /// </summary>
    public static (string Path, AppError Error) OrNotFound(string path)
    {
        var error = new AppError();
        if (!File.Exists(path))
        {
            error.Kind = ErrorKind.Error;
            error.Message = "Error while generating bitmap";
            path = PathOf(StockImage.NotFound);
        }

        return (path, error);
    }
}
