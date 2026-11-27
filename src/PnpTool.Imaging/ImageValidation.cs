using PnpTool.Core;
using PnpTool.Core.Infrastructure;

namespace PnpTool.Imaging;

/// <summary>
/// The check of the images of a folder before a PDF is made of them.
/// Original: <c>FormValidarImagenesModelo</c>.
/// </summary>
public static class ImageValidation
{
    /// <summary>
    /// The images that can be used, in the order given. One that cannot be read is left out.
    /// One whose resolution is outside the limits of the settings, or that states none, is
    /// replaced by a copy in the temporary folder that states the program's target
    /// resolution; when the copy cannot be made the image is used as it is.
    /// Original: <c>Validar</c>.
    /// </summary>
    /// <exception cref="OperationCanceledException">The check was cancelled.</exception>
    public static string[] Usable(IReadOnlyList<string> images, Action<int>? report = null, CancellationToken cancellation = default)
    {
        var usable = new List<string>(images.Count);
        report?.Invoke(0);
        for (int i = 0; i < images.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            string image = images[i];
            if (!ImageFormats.CheckReadable(image).HasError)
            {
                if (ImageDensity.Validate(image).HasError)
                {
                    (AppError error, string copy) = ImageDensity.RaiseDpi(image, force: false, AppSettings.TargetDpi);
                    if (!error.HasError)
                    {
                        image = copy;
                    }
                }

                usable.Add(image);
            }

            report?.Invoke(Convert.ToInt32((double)i / images.Count * 100.0));
        }

        return [.. usable];
    }
}
