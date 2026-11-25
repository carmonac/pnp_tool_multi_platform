namespace PnpTool.Core.Files;

/// <summary>Names for files the program creates next to others.</summary>
public static class FileNames
{
    /// <summary>
    /// The path itself when no file has it; otherwise the first free one of <c>name_0.ext</c>,
    /// <c>name_1.ext</c>, … in the same folder. Original: <c>FuncionesGenerales.DevuelveNombreUnico(string)</c>.
    /// </summary>
    public static string Unique(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        string folder = Path.GetDirectoryName(path) ?? string.Empty;
        string name = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        for (int index = 0; ; index++)
        {
            string candidate = Path.Combine(folder, name + "_" + index + extension);
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>
    /// An identifier for a document: the seed followed by the time to the millisecond.
    /// Original: <c>FuncionesGenerales.GeneraIDDocumento</c>.
    /// </summary>
    public static string DocumentId(string seed) => seed + DateTime.Now.ToString("yyyyMMddHHmmssfff");
}
