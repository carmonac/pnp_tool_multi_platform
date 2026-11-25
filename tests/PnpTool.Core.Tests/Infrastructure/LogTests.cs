using PnpTool.Core.Infrastructure;
using PnpTool.Core.Tests.Files;

namespace PnpTool.Core.Tests.Infrastructure;

[Collection(GlobalState.Name)]
public sealed class LogTests : IDisposable
{
    private readonly GlobalStateScope _scope = new();
    private readonly TestFiles.TempFolder _temp = new();

    public LogTests() => AppSettings.WriteLogs = false;

    public void Dispose()
    {
        _scope.Dispose();
        _temp.Dispose();
    }

    private string[] Lines() => File.ReadAllLines(_temp.File("logs.txt"));

    [Fact]
    public void A_new_log_starts_with_the_original_header()
    {
        File.WriteAllText(_temp.File("logs.txt"), "from the previous run");

        Log.Start(_temp.File("logs.txt"));

        Assert.Equal(["<!--Fichero de logs programa PNP Tool-->", "<Log>"], Lines());
    }

    [Fact]
    public void Enter_and_exit_wrap_the_value_in_a_tag_naming_file_and_member()
    {
        Log.Start(_temp.File("logs.txt"));

        Log.Enter("opening", force: true);
        Log.Write("<Indice>3</Indice>", force: true);
        Log.Exit("done", force: true);

        Assert.Equal(
            [
                "<LogTests-Enter_and_exit_wrap_the_value_in_a_tag_naming_file_and_member>opening",
                "<Indice>3</Indice>",
                "done</LogTests-Enter_and_exit_wrap_the_value_in_a_tag_naming_file_and_member>",
            ],
            Lines()[2..]);
    }

    [Fact]
    public void Tag_drops_spaces_and_angle_brackets_and_takes_windows_paths()
    {
        Log.Start(_temp.File("logs.txt"));

        Log.Enter(null, force: true, member: "<Main>b__0 1", file: @"C:\src\My File.cs");

        Assert.Equal("<MyFile-Mainb__01>", Lines()[^1]);
    }

    [Fact]
    public void Nothing_is_written_while_logging_is_off_unless_forced()
    {
        Log.Start(_temp.File("logs.txt"));

        Log.Enter("quiet");
        Log.Write("quiet");
        Log.Exit("quiet");
        Assert.Equal(2, Lines().Length);

        Log.Write("forced", force: true);
        Assert.Equal("forced", Lines()[^1]);
    }

    [Fact]
    public void Everything_is_written_while_logging_is_on()
    {
        Log.Start(_temp.File("logs.txt"));
        AppSettings.WriteLogs = true;

        Log.Enter("a");
        Log.Write("b");
        Log.Exit("c");

        Assert.Equal(5, Lines().Length);
    }

    [Fact]
    public void Sink_receives_the_lines_even_without_a_file()
    {
        var lines = new List<string>();
        Log.Sink = lines.Add;

        Log.Write("hello", force: true);

        Assert.Equal(["hello"], lines);
    }

    [Fact]
    public void A_log_that_cannot_be_created_does_not_stop_the_program()
    {
        File.WriteAllText(_temp.File("blocker"), string.Empty);

        Log.Start(_temp.File("blocker", "logs.txt"));
        Log.Write("dropped", force: true);
    }
}
