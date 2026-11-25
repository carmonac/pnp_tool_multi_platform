using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Imaging;

namespace PnpTool.App.Services;

/// <summary>
/// The pictures of card faces shown on screen. They are made away from the user-interface
/// thread, a few at a time. Original: <c>ImagenesWpf.BitmapDesdeCara</c>,
/// <c>CreaImagenBlanca</c> and <c>CargaImagenVacia</c>.
/// </summary>
/// <remarks>
/// The original asked the Windows shell for a thumbnail and then applied only one of the
/// face's changes (a turn, or else one of the mirrorings). Here the thumbnail is made from the
/// file and gets all of them, as the PDF does.
/// </remarks>
public static class FaceImages
{
    /// <summary>How many pictures are made at the same time.</summary>
    private static readonly SemaphoreSlim Gate = new(Math.Clamp(Environment.ProcessorCount / 2, 2, 6));

    /// <summary>The picture shown for a face without image. Original: <c>vacia.png</c>.</summary>
    public static string EmptyImagePath => Path.Combine(AppSettings.ImagesFolder, "vacia.png");

    /// <summary>
    /// The pixels of the picture of a face, at most <paramref name="longestSide"/> pixels on
    /// its longest side (0 for full size). A face in a plain colour gives a small square of
    /// that colour, and a file that is missing gives the "not found" picture.
    /// </summary>
    public static DisplayImage Render(CardFace face, int longestSide)
    {
        switch (face.ImageKind)
        {
            case ImageKind.Blank:
                return Solid(ArgbColor.Parse(face.Path));
            case ImageKind.Empty:
                return DisplayImages.Load(EmptyImagePath, longestSide);
            default:
                string path = StockImages.OrNotFound(face.Path).Path;
                return DisplayImages.Load(path, longestSide, face.Rotation);
        }
    }

    /// <summary>
    /// Makes the picture of a face on another thread. Gives null when the picture cannot be
    /// made (a damaged file, say) or the work is cancelled.
    /// </summary>
    public static async Task<Bitmap?> LoadAsync(CardFace face, int longestSide, CancellationToken cancellation = default)
    {
        // The face may change while the picture is waiting its turn: work on a copy.
        var copy = new CardFace();
        copy.CopyFrom(face);
        try
        {
            await Gate.WaitAsync(cancellation).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        try
        {
            return await Task.Run(
                () =>
                {
                    try
                    {
                        return cancellation.IsCancellationRequested ? null : ToBitmap(Render(copy, longestSide));
                    }
                    catch (Exception e) when (e is ImageMagick.MagickException or IOException or UnauthorizedAccessException)
                    {
                        Log.Write("Picture of " + copy.Path + ": " + e.Message, force: true);
                        return null;
                    }
                },
                CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Wraps pixels as a bitmap the user interface can draw.</summary>
    public static Bitmap ToBitmap(DisplayImage image)
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(image.Width, image.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using ILockedFramebuffer buffer = bitmap.Lock();
        for (int row = 0; row < image.Height; row++)
        {
            Marshal.Copy(image.Pixels, row * image.Stride, buffer.Address + (row * buffer.RowBytes), image.Stride);
        }

        return bitmap;
    }

    private static DisplayImage Solid(ArgbColor color)
    {
        const int Side = 8;
        byte[] pixels = new byte[Side * Side * 4];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = color.B;
            pixels[i + 1] = color.G;
            pixels[i + 2] = color.R;
            pixels[i + 3] = 255;
        }

        return new DisplayImage(Side, Side, pixels, 96, 96);
    }
}
