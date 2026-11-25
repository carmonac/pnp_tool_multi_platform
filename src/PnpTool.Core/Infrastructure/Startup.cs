namespace PnpTool.Core.Infrastructure;

/// <summary>
/// What the program does before showing any window. Original: the parts of
/// <c>FuncionesGeneralesPrograma.IniciarPrograma</c> and of the splash screen that do not
/// touch the user interface.
/// </summary>
/// <remarks>
/// The original went on to download a version blacklist, check for updates and send usage
/// statistics. None of that is ported: starting the program makes no network connection.
/// </remarks>
public static class Startup
{
    /// <summary>
    /// Sets the folders, starts a new log, prepares the per-user folder and loads the saved
    /// settings (a first run has none, which is not an error). Returns an error when the
    /// program cannot work: its assets are missing or its folders cannot be written.
    /// </summary>
    public static AppError Initialize(string? assetsFolder = null, string? userDataFolder = null)
    {
        AppPaths.Initialize(assetsFolder, userDataFolder);
        try
        {
            Directory.CreateDirectory(AppPaths.UserDataFolder);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Reported by PrepareUserData below.
        }

        Log.Start(AppPaths.LogFile);
        Log.Enter(nameof(Initialize), force: true);
        Log.Write("Assets: " + AppPaths.AssetsFolder, force: true);
        Log.Write("User data: " + AppPaths.UserDataFolder, force: true);

        AppError error = AppPaths.PrepareUserData();
        if (!error.HasError && File.Exists(AppPaths.SettingsFile))
        {
            // A damaged settings file must not keep the program from starting; the original
            // had no such guard. The defaults stay in place.
            try
            {
                AppError settingsError = AppSettings.Load();
                if (settingsError.HasError)
                {
                    Log.Write("Settings not loaded: " + settingsError.Message, force: true);
                }
            }
            catch (Exception exception) when (exception is System.Xml.XmlException or InvalidOperationException or FormatException)
            {
                Log.Write("Settings not loaded: " + exception.Message, force: true);
            }
        }

        Log.Exit(nameof(Initialize) + " " + !error.HasError, force: true);
        return error;
    }
}
