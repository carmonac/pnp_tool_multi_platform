using ImageMagick;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;

namespace PnpTool.Imaging;

/// <summary>
/// Changes a list of images in one go: a new size, an effect and another format.
/// Original: <c>FormEditarImagenesModelo</c>.
/// </summary>
public sealed class ImageBatchEditor
{
    /// <summary>The extensions offered for the result; the first is the one offered.</summary>
    public static readonly string[] Extensions = [".jpg", ".png", ".bmp", ".gif", ".tiff"];

    /// <summary>The new width, as a percentage of the old one.</summary>
    public double ScaleX { get; set; } = 100.0;

    public double ScaleY { get; set; } = 100.0;

    public ImageEffectType Effect { get; set; } = ImageEffectType.None;

    /// <summary>The size of the effect, for those that have one.</summary>
    public int EffectSize { get; set; } = 20;

    /// <summary>The colour of what an effect cuts away.</summary>
    public ArgbColor Background { get; set; } = ArgbColor.Parse(AppSettings.ColorWhite);

    public string Extension { get; set; } = Extensions[0];

    public FilterType Filter { get; set; } = FilterType.Lanczos;

    /// <summary>
    /// Whether an image is written over itself when the format chosen is its own; otherwise,
    /// and always when the format changes, a file "name_new" is written in the folder.
    /// </summary>
    public bool Replace { get; set; }

    /// <summary>
    /// Changes every image. Gives the files written and the problems found, an image each.
    /// Original: <c>EditarImagenes</c>.
    /// </summary>
    /// <param name="settings">The project, for the effects that need the size of a card; it is not changed.</param>
    /// <param name="progress">Told how many images are done.</param>
    /// <remarks>
    /// Differences from the original: replacing works (it compared ".Jpg" with ".jpg" and so
    /// never replaced), the filter is the one chosen in the window (it took the project's),
    /// new files get their extension in lower case, and the project is left as it was (a
    /// project without card size got the size of the last image, in points).
    /// </remarks>
    public (List<string> Written, List<string> Problems) Edit(IReadOnlyList<string> images, string folder, ProjectSettings settings, Action<int>? progress = null)
    {
        Directory.CreateDirectory(folder);
        var written = new List<string>();
        var problems = new List<string>();
        MagickFormat format = ImageFormats.FormatFromExtension(Extension);
        for (int index = 0; index < images.Count; index++)
        {
            string path = images[index];
            try
            {
                MagickImage image = new(path);
                image.ResetPage();
                uint width = Convert.ToUInt32(Math.Max(1.0, image.Width * (ScaleX / 100.0)));
                uint height = Convert.ToUInt32(Math.Max(1.0, image.Height * (ScaleY / 100.0)));
                image.FilterType = Filter;
                image.Resize(new MagickGeometry(width, height) { IgnoreAspectRatio = true });

                var local = new ProjectSettings();
                local.CopyFrom(settings);
                if (local.Grid.CardSize.X.EqualsInMillimeters(Unit.Zero))
                {
                    local.Grid.CardSize.X = Unit.FromPoint(width);
                }

                if (local.Grid.CardSize.Y.EqualsInMillimeters(Unit.Zero))
                {
                    local.Grid.CardSize.Y = Unit.FromPoint(height);
                }

                (AppError error, MagickImage changed) = ImageEffects.Apply(image, Effect, EffectSize, Background, local, FaceType.Front);
                if (error.HasError)
                {
                    problems.Add(Path.GetFileName(path) + ": " + error.Message);
                }

                using (changed)
                {
                    bool sameFormat = string.Equals(NormalExtension(Extension), NormalExtension(Path.GetExtension(path)), StringComparison.OrdinalIgnoreCase);
                    string target = Replace && sameFormat
                        ? path
                        : Path.Combine(folder, Path.GetFileNameWithoutExtension(path) + "_new" + Extension);
                    changed.Write(target, format);
                    written.Add(target);
                }

                if (!ReferenceEquals(changed, image))
                {
                    image.Dispose();
                }
            }
            catch (Exception exception) when (exception is MagickException or IOException or UnauthorizedAccessException)
            {
                problems.Add(Path.GetFileName(path) + ": " + exception.Message);
            }

            progress?.Invoke(index + 1);
        }

        return (written, problems);
    }

    private static string NormalExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpeg" => ".jpg",
        ".tif" => ".tiff",
        string other => other,
    };
}
