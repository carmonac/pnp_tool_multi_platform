namespace PnpTool.Core.Tests.Files;

internal static class TestFiles
{
    public static string Path(string name) => System.IO.Path.Combine(AppContext.BaseDirectory, "TestData", name);

    /// <summary>A fresh empty folder under the system temp folder, deleted on dispose.</summary>
    public sealed class TempFolder : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("pnptool-tests-").FullName;

        public string File(params string[] parts) => System.IO.Path.Combine([Root, .. parts]);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
