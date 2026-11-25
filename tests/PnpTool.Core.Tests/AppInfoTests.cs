using PnpTool.Core;

namespace PnpTool.Core.Tests;

public class AppInfoTests
{
    [Fact]
    public void Version_is_a_parseable_version()
    {
        Assert.True(Version.TryParse(AppInfo.Version, out _), $"Unexpected version '{AppInfo.Version}'");
    }
}
