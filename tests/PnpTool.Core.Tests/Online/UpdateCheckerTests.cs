using System.Net;
using PnpTool.Core.Online;

namespace PnpTool.Core.Tests.Online;

public sealed class UpdateCheckerTests
{
    private const string Page = "https://github.com/carmonac/pnp_tool_multi_platform/releases/tag/";

    /// <summary>Answers every request with the same text, and keeps the requests.</summary>
    private sealed class FakeServer(string body, HttpStatusCode status = HttpStatusCode.OK, bool offline = false) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (offline)
            {
                throw new HttpRequestException("No connection");
            }

            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    private static string Release(string tag, bool prerelease = false, bool draft = false, string? url = null) =>
        "{\"tag_name\":\"" + tag + "\",\"html_url\":\"" + (url ?? Page + tag) + "\",\"prerelease\":" + (prerelease ? "true" : "false")
        + ",\"draft\":" + (draft ? "true" : "false") + "}";

    private static string List(params string[] releases) => "[" + string.Join(",", releases) + "]";

    private static UpdateCheckResult Check(string body, UpdateChannel channel, string current, bool asked = false)
    {
        using var checker = new UpdateChecker(new FakeServer(body));
        return checker.CheckAsync(channel, current, asked, TestContext.Current.CancellationToken).GetAwaiter().GetResult();
    }

    [Theory]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData("v1.2", 1, 2, 0)]
    [InlineData("V0.10.0", 0, 10, 0)]
    [InlineData("1.2.3-beta.1", 1, 2, 3)]
    [InlineData("1.2.3+build5", 1, 2, 3)]
    [InlineData("20.11.1 - With Magic", 20, 11, 1)]
    public void Version_numbers_are_read_from_tags(string text, int major, int minor, int patch) =>
        Assert.Equal(new Version(major, minor, patch), UpdateChecker.Number(text));

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("1")]
    [InlineData("1.2.3.4")]
    [InlineData("1.x.3")]
    [InlineData("1..3")]
    [InlineData("1.2.9999999999")]
    public void What_is_not_a_version_number_is_refused(string text) => Assert.Null(UpdateChecker.Number(text));

    [Fact]
    public void Newest_release_is_reported_with_its_page()
    {
        UpdateCheckResult result = Check(List(Release("v0.3.0"), Release("v0.2.0"), Release("v0.1.0")), UpdateChannel.Minor, "0.1.0");

        Assert.Equal(UpdateCheckStatus.Newer, result.Status);
        Assert.Equal("0.3.0", result.Release!.Version);
        Assert.Equal(Page + "v0.3.0", result.Release.Url);
    }

    [Fact]
    public void Order_of_the_list_does_not_matter()
    {
        UpdateCheckResult result = Check(List(Release("v0.2.0"), Release("v0.10.0"), Release("v0.9.0")), UpdateChannel.Minor, "0.1.0");

        Assert.Equal("0.10.0", result.Release!.Version);
    }

    [Fact]
    public void Same_or_older_release_means_up_to_date()
    {
        Assert.Equal(UpdateCheckStatus.UpToDate, Check(List(Release("v0.1.0")), UpdateChannel.Minor, "0.1.0").Status);
        Assert.Equal(UpdateCheckStatus.UpToDate, Check(List(Release("v0.1.0")), UpdateChannel.Tester, "0.2.0").Status);
        Assert.Equal(UpdateCheckStatus.UpToDate, Check("[]", UpdateChannel.Minor, "0.1.0").Status);
    }

    // As the original: "major" looks at the first number, "minor" at the first two, "test" at all.
    [Theory]
    [InlineData(UpdateChannel.Major, "1.0.0", "1.5.0", false)]
    [InlineData(UpdateChannel.Major, "1.9.9", "2.0.0", true)]
    [InlineData(UpdateChannel.Minor, "1.2.0", "1.2.7", false)]
    [InlineData(UpdateChannel.Minor, "1.2.7", "1.3.0", true)]
    [InlineData(UpdateChannel.Minor, "1.9.0", "2.0.0", true)]
    [InlineData(UpdateChannel.Tester, "1.2.0", "1.2.1", true)]
    public void Setting_decides_which_changes_count(UpdateChannel channel, string current, string published, bool told)
    {
        UpdateCheckResult result = Check(List(Release("v" + published)), channel, current);

        Assert.Equal(told ? UpdateCheckStatus.Newer : UpdateCheckStatus.UpToDate, result.Status);
    }

    [Fact]
    public void Versions_for_testing_only_count_with_the_test_setting()
    {
        string body = List(Release("v0.3.0-beta.1", prerelease: true), Release("v0.2.0"));

        Assert.Equal("0.2.0", Check(body, UpdateChannel.Minor, "0.1.0").Release!.Version);
        Assert.Equal("0.3.0-beta.1", Check(body, UpdateChannel.Tester, "0.1.0").Release!.Version);
        Assert.Equal("0.2.0", Check(body, UpdateChannel.Minor, "0.1.0", asked: true).Release!.Version);
    }

    [Fact]
    public void Drafts_odd_tags_and_pages_elsewhere_are_ignored()
    {
        string body = List(
            Release("v9.0.0", draft: true),
            Release("nightly"),
            Release("v8.0.0", url: "https://example.com/download"),
            Release("v0.2.0"));

        Assert.Equal("0.2.0", Check(body, UpdateChannel.Minor, "0.1.0").Release!.Version);
    }

    [Fact]
    public void Setting_none_makes_no_request_unless_the_user_asks()
    {
        var server = new FakeServer(List(Release("v0.1.5")));
        using var checker = new UpdateChecker(server);

        UpdateCheckResult quiet = checker.CheckAsync(UpdateChannel.None, "0.1.0", cancellation: TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        Assert.Equal(UpdateCheckStatus.Disabled, quiet.Status);
        Assert.Empty(server.Requests);

        // Asked for: any newer version counts, whatever the setting.
        UpdateCheckResult asked = checker.CheckAsync(UpdateChannel.None, "0.1.0", asked: true, TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        Assert.Equal("0.1.5", asked.Release!.Version);
        Assert.Single(server.Requests);
    }

    [Fact]
    public void Request_goes_to_the_releases_of_the_project_and_says_nothing_about_the_user()
    {
        var server = new FakeServer("[]");
        using var checker = new UpdateChecker(server);

        checker.CheckAsync(UpdateChannel.Minor, "0.1.0", cancellation: TestContext.Current.CancellationToken).GetAwaiter().GetResult();

        HttpRequestMessage request = Assert.Single(server.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(UpdateChecker.ReleasesApi, request.RequestUri!.ToString());
        Assert.Equal("PNPTool", request.Headers.UserAgent.ToString());
        Assert.Null(request.Content);
        Assert.DoesNotContain(request.Headers, header => header.Key is "Cookie" or "Authorization");
        // The address and the client name are all there is: no query made from the machine.
        Assert.Equal(["Accept", "User-Agent"], request.Headers.Select(header => header.Key).Order());
    }

    [Fact]
    public void Trouble_with_the_network_or_the_answer_is_a_failure_not_an_exception()
    {
        using var notFound = new UpdateChecker(new FakeServer("{}", HttpStatusCode.NotFound));
        UpdateCheckResult refused = notFound.CheckAsync(UpdateChannel.Minor, "0.1.0", cancellation: TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        Assert.Equal(UpdateCheckStatus.Failed, refused.Status);
        Assert.Equal("HTTP 404", refused.Message);

        using var offline = new UpdateChecker(new FakeServer("", offline: true));
        UpdateCheckResult down = offline.CheckAsync(UpdateChannel.Minor, "0.1.0", cancellation: TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        Assert.Equal(UpdateCheckStatus.Failed, down.Status);
        Assert.Equal("No connection", down.Message);

        Assert.Equal(UpdateCheckStatus.Failed, Check("<html>", UpdateChannel.Minor, "0.1.0").Status);
    }
}
