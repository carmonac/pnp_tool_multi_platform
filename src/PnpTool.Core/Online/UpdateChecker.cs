using System.Text.Json;

namespace PnpTool.Core.Online;

/// <summary>A published version of the program.</summary>
/// <param name="Version">Its number, as "1.2.0".</param>
/// <param name="Url">The page it is downloaded from.</param>
/// <param name="Prerelease">Whether it is published for testing.</param>
public sealed record ReleaseInfo(string Version, string Url, bool Prerelease);

/// <summary>How a look for a newer version ended.</summary>
public enum UpdateCheckStatus
{
    /// <summary>The setting says not to look.</summary>
    Disabled,
    UpToDate,
    Newer,

    /// <summary>No connection, or no answer that could be read.</summary>
    Failed,
}

/// <param name="Release">The newer version, when there is one.</param>
/// <param name="Message">What went wrong, when it failed.</param>
public sealed record UpdateCheckResult(UpdateCheckStatus Status, ReleaseInfo? Release = null, string Message = "");

/// <summary>
/// Looks for a newer version among the releases of this project. Original:
/// <c>FuncionesGenerales.Hay_Actualizacion_V2</c>, which read a file from the server of a
/// printing company and, with the same request, reported the use of the program.
/// </summary>
/// <remarks>
/// One request to the public list of releases, with nothing about the user or the machine
/// in it. Nothing is downloaded or installed: the program only tells where the newer version is.
/// </remarks>
public sealed class UpdateChecker : IDisposable
{
    /// <summary>The releases of this project, newest first.</summary>
    public const string ReleasesApi = "https://api.github.com/repos/carmonac/pnp_tool_multi_platform/releases?per_page=20";

    private readonly HttpClient _http;

    /// <param name="handler">Answers the requests instead of the network, for tests.</param>
    public UpdateChecker(HttpMessageHandler? handler = null)
    {
        _http = handler == null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(10);
        // The server refuses requests without a client name.
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("PNPTool");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    /// <summary>
    /// Asks for the published versions and picks the newest one the setting cares about.
    /// </summary>
    /// <param name="channel">Original: <c>ConfiguracionPrograma.AvisarActualizaciones</c>.</param>
    /// <param name="currentVersion">The version of this program.</param>
    /// <param name="asked">
    /// Whether the user asked to look: then it is done whatever the setting, and any newer
    /// version counts. Original: the <c>forzar</c> argument.
    /// </param>
    public async Task<UpdateCheckResult> CheckAsync(UpdateChannel channel, string currentVersion, bool asked = false, CancellationToken cancellation = default)
    {
        if (channel == UpdateChannel.None && !asked)
        {
            return new UpdateCheckResult(UpdateCheckStatus.Disabled);
        }

        try
        {
            using HttpResponseMessage response = await _http.GetAsync(ReleasesApi, cancellation);
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(UpdateCheckStatus.Failed, Message: "HTTP " + (int)response.StatusCode);
            }

            string body = await response.Content.ReadAsStringAsync(cancellation);
            ReleaseInfo? newest = Newest(Parse(body), channel, currentVersion, asked);
            return newest == null
                ? new UpdateCheckResult(UpdateCheckStatus.UpToDate)
                : new UpdateCheckResult(UpdateCheckStatus.Newer, newest);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            return new UpdateCheckResult(UpdateCheckStatus.Failed, Message: e.Message);
        }
    }

    /// <summary>The releases in the answer of the server; drafts and those without a version number are left out.</summary>
    public static IReadOnlyList<ReleaseInfo> Parse(string json)
    {
        var releases = new List<ReleaseInfo>();
        using JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return releases;
        }

        foreach (JsonElement release in document.RootElement.EnumerateArray())
        {
            string tag = Text(release, "tag_name");
            string url = Text(release, "html_url");
            bool draft = release.TryGetProperty("draft", out JsonElement isDraft) && isDraft.ValueKind == JsonValueKind.True;
            bool prerelease = release.TryGetProperty("prerelease", out JsonElement isPrerelease) && isPrerelease.ValueKind == JsonValueKind.True;
            // Only a page of the project is ever offered to the user.
            if (!draft && Number(tag) != null && url.StartsWith(AppLinks.SourceCode + "/", StringComparison.Ordinal))
            {
                releases.Add(new ReleaseInfo(tag.TrimStart('v', 'V'), url, prerelease));
            }
        }

        return releases;
    }

    /// <summary>
    /// The newest release the setting asks to be told about, if it is newer than this program.
    /// Versions published for testing only count with the "test" setting.
    /// </summary>
    public static ReleaseInfo? Newest(IEnumerable<ReleaseInfo> releases, UpdateChannel channel, string currentVersion, bool asked = false)
    {
        if ((channel == UpdateChannel.None && !asked) || Number(currentVersion) is not { } current)
        {
            return null;
        }

        return releases
            .Where(release => channel == UpdateChannel.Tester || !release.Prerelease)
            .Select(release => (Release: release, Number: Number(release.Version)!))
            .Where(candidate => asked ? candidate.Number > current : IsNewer(candidate.Number, current, channel))
            .OrderByDescending(candidate => candidate.Number)
            .Select(candidate => candidate.Release)
            .FirstOrDefault();
    }

    /// <summary>
    /// The three numbers of a version: "v1.2", "1.2.3" and "1.2.3-beta.1+build" are understood.
    /// </summary>
    public static Version? Number(string text)
    {
        string digits = text.Trim().TrimStart('v', 'V').Split('-', '+', ' ')[0];
        string[] parts = digits.Split('.');
        if (parts.Length is < 2 or > 3 || !parts.All(part => part.Length is > 0 and < 9 && part.All(char.IsAsciiDigit)))
        {
            return null;
        }

        return new Version(int.Parse(parts[0]), int.Parse(parts[1]), parts.Length == 3 ? int.Parse(parts[2]) : 0);
    }

    /// <summary>
    /// "Major" only looks at the first number and "minor" at the first two, as the original
    /// does; "test" at all three. Original: <c>Hay_Actualizacion_Mayor</c>,
    /// <c>Hay_Actualizacion_Menor</c>, <c>Hay_Actualizacion_Tester</c>.
    /// </summary>
    private static bool IsNewer(Version candidate, Version current, UpdateChannel channel) => channel switch
    {
        UpdateChannel.Major => candidate.Major > current.Major,
        UpdateChannel.Minor => new Version(candidate.Major, candidate.Minor) > new Version(current.Major, current.Minor),
        _ => candidate > current,
    };

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    public void Dispose() => _http.Dispose();
}
