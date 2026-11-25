namespace PnpTool.Core.Files;

/// <summary>
/// Sorts the image files of a folder into two subfolders, <c>front</c> and <c>back</c>, by
/// their names. Original: <c>FormSepararFrontalesTraserasModelo</c>.
/// </summary>
public static class FrontBackSplitter
{
    public const string FrontFolder = "front";

    public const string BackFolder = "back";

    /// <summary>
    /// Moves every file directly inside <paramref name="folder"/>: those whose name contains
    /// <paramref name="frontMarker"/> to <c>front</c>, the rest to <c>back</c>. A file already
    /// there under the same name is replaced. Returns how many files went to each.
    /// </summary>
    public static (int Fronts, int Backs) Split(string folder, string frontMarker)
    {
        string[] files = Directory.GetFiles(folder);
        string fronts = Path.Combine(folder, FrontFolder);
        string backs = Path.Combine(folder, BackFolder);
        Directory.CreateDirectory(fronts);
        Directory.CreateDirectory(backs);
        int frontCount = 0;
        int backCount = 0;
        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            bool isFront = name.Contains(frontMarker, StringComparison.Ordinal);
            File.Move(file, Path.Combine(isFront ? fronts : backs, name), overwrite: true);
            if (isFront)
            {
                frontCount++;
            }
            else
            {
                backCount++;
            }
        }

        return (frontCount, backCount);
    }
}
