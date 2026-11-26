using PnpTool.Core.Infrastructure;

namespace PnpTool.Core.Tests.Infrastructure;

public sealed class CalculatorTests
{
    [Fact]
    public void Each_system_starts_its_own_calculator()
    {
        Assert.Equal("open -a Calculator", Command(macOS: true, linux: false));
        Assert.Equal("calc.exe", Command(macOS: false, linux: false));
        Assert.Equal("kcalc", Command(macOS: false, linux: true, "xcalc", "kcalc"));
        Assert.Equal("gnome-calculator", Command(macOS: false, linux: true, "kcalc", "gnome-calculator"));
        Assert.Null(DesktopPlatformServices.CalculatorCommand(macOS: false, linux: true, _ => false));
    }

    [Fact]
    public void Services_that_do_not_say_otherwise_have_no_calculator()
    {
        IPlatformServices plain = new Plain();
        Assert.False(plain.OpenCalculator());
    }

    private static string Command(bool macOS, bool linux, params string[] installed)
    {
        (string Program, string[] Arguments)? command = DesktopPlatformServices.CalculatorCommand(macOS, linux, installed.Contains);
        Assert.NotNull(command);
        return string.Join(" ", command.Value.Arguments.Prepend(command.Value.Program));
    }

    private sealed class Plain : IPlatformServices
    {
        public void OpenUrl(string url)
        {
        }

        public void OpenFile(string path)
        {
        }

        public void OpenFolder(string path)
        {
        }

        public void RevealFile(string path)
        {
        }
    }
}
