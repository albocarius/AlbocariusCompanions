using System.Diagnostics;
using AlbocariusCompanions.Cli;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance;

// Task 95 (M9 I3): CLI "serve" — the simulator over JSON lines on stdin/stdout (docs/SIMULATOR_PROTOCOL.md).
public class ServeTests
{
    static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AlbocariusCompanions.sln"))) dir = dir.Parent;
            return dir!.FullName;
        }
    }

    static string Companions => Path.Combine(RepoRoot, "module", "ModuleData", "Companions");

    /// <summary>The sample sequence of the run report's checklist item 3.</summary>
    static string[] SampleRequests() => new[]
    {
        new JObject { ["id"] = 1, ["op"] = "load", ["root"] = Companions },
        new JObject { ["id"] = 2, ["op"] = "start", ["companion"] = "test_companion", ["entry"] = "hub", ["seed"] = 5,
            ["state"] = JObject.Parse("""{ "player": { "gold": 1000 } }""") },
        new JObject { ["id"] = 3, ["op"] = "hire", ["session"] = "s1" },
        new JObject { ["id"] = 4, ["op"] = "hub", ["session"] = "s1" },
        new JObject { ["id"] = 5, ["op"] = "choose", ["session"] = "s1", ["conversation"] = "the_scar", ["node"] = "p_scar_ask" },
        new JObject { ["id"] = 6, ["op"] = "choose", ["session"] = "s1", ["conversation"] = "the_scar", ["node"] = "p_scar_sorry" },
        new JObject { ["id"] = 7, ["op"] = "choose", ["session"] = "s1", ["conversation"] = "spare_coin", ["node"] = "p_coin_offer" },
        new JObject { ["id"] = 8, ["op"] = "nonsense" },
        new JObject { ["id"] = 9, ["op"] = "quit" },
    }.Select(r => r.ToString(Newtonsoft.Json.Formatting.None)).ToArray();

    static void AssertSampleResponses(IReadOnlyList<JObject> responses)
    {
        Assert.Equal(9, responses.Count);
        for (var i = 0; i < 9; i++)
            Assert.Equal(i + 1, (int)responses[i]["id"]!);

        Assert.True((bool)responses[0]["ok"]!);
        Assert.Contains("test_companion", responses[0]["companions"]!.Values<string>());
        Assert.Equal(0, (int)responses[0]["errors"]!);

        Assert.Equal("s1", (string?)responses[1]["session"]);
        Assert.True((bool)responses[2]["hired"]!);
        Assert.Equal(500, (int)responses[2]["state"]!["player"]!["gold"]!);

        var hub = responses[3]["step"]!;
        Assert.Equal("options", (string?)hub["kind"]);
        Assert.True((bool)hub["options"]!.Single(o => (string?)o["node"] == "p_scar_ask")["shown"]!);

        var story = responses[4]["step"]!;
        Assert.Equal(new[] { "p_scar_ask", "scar_story" }, story["said"]!.Select(s => (string?)s["node"]));
        Assert.Equal("scar_story", (string?)story["said"]![1]!["selection"]!["winner"]);

        var coin = responses[6]["step"]!;
        Assert.Equal("end", (string?)coin["kind"]);
        Assert.Equal(new[] { "p_coin_offer", "coin_thanks" }, coin["said"]!.Select(s => (string?)s["node"]));
        Assert.Equal(400, (int)coin["state"]!["player"]!["gold"]!);
        Assert.Equal(600, (int)coin["state"]!["companions"]!["test_companion"]!["gold"]!);
        Assert.True((bool)coin["state"]!["variables"]!["test_companion.got_coin"]!);

        Assert.False((bool)responses[7]["ok"]!);
        Assert.Contains("unknown op", (string?)responses[7]["error"]);
        Assert.True((bool)responses[8]["ok"]!);
    }

    [Fact]
    public void Serve_answers_the_sample_sequence_in_process()
    {
        var stdout = new StringWriter { NewLine = "\n" };
        var code = CliProgram.Run(new[] { "serve" }, new StringReader(string.Join("\n", SampleRequests()) + "\n"), stdout, new StringWriter());
        Assert.Equal(0, code);
        AssertSampleResponses(stdout.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(JObject.Parse).ToList());
    }

    [Fact]
    public void Serve_drives_the_real_process_over_stdin_and_stdout()
    {
        var dll = new[] { "Debug", "Release" }
            .Select(c => Path.Combine(RepoRoot, "tools", "AlbocariusCompanions.Cli", "bin", c, "net8.0", "AlbocariusCompanions.Cli.dll"))
            .Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).First();
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(dll);
        start.ArgumentList.Add("serve");
        using var process = Process.Start(start)!;

        var responses = new List<JObject>();
        foreach (var request in SampleRequests())
        {
            // One request, one response line, before the next request is sent (how an editor talks to it).
            process.StandardInput.WriteLine(request);
            process.StandardInput.Flush();
            responses.Add(JObject.Parse(process.StandardOutput.ReadLine()!));
        }
        Assert.True(process.WaitForExit(30000));
        Assert.Equal(0, process.ExitCode);
        AssertSampleResponses(responses);
    }

    [Fact]
    public void A_bad_line_is_an_error_response_and_the_server_keeps_going()
    {
        var stdout = new StringWriter { NewLine = "\n" };
        var input = "not json\n\n{\"id\":\"x\",\"op\":\"choose\",\"session\":\"s9\",\"conversation\":\"a\",\"node\":\"b\"}\n{\"id\":2,\"op\":\"hello\"}\n";
        Assert.Equal(0, CliProgram.Run(new[] { "serve" }, new StringReader(input), stdout, new StringWriter()));
        var lines = stdout.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(JObject.Parse).ToList();
        Assert.Equal(3, lines.Count);
        Assert.False((bool)lines[0]["ok"]!);
        Assert.Equal(JTokenType.Null, lines[0]["id"]!.Type);
        Assert.False((bool)lines[1]["ok"]!);
        Assert.Equal("x", (string?)lines[1]["id"]);
        Assert.Contains("unknown session", (string?)lines[1]["error"]);
        Assert.True((bool)lines[2]["ok"]!);
        Assert.Equal(1, (int)lines[2]["version"]!);
    }

    [Fact]
    public void Start_before_load_and_a_bad_state_are_errors()
    {
        var stdout = new StringWriter { NewLine = "\n" };
        var input = string.Join("\n",
            "{\"id\":1,\"op\":\"start\",\"companion\":\"test_companion\",\"entry\":\"hub\"}",
            new JObject { ["id"] = 2, ["op"] = "load", ["root"] = Companions }.ToString(Newtonsoft.Json.Formatting.None),
            "{\"id\":3,\"op\":\"start\",\"companion\":\"test_companion\",\"entry\":\"hub\",\"state\":{\"playr\":{}}}",
            "{\"id\":4,\"op\":\"start\",\"companion\":\"nobody\",\"entry\":\"hub\"}") + "\n";
        CliProgram.Run(new[] { "serve" }, new StringReader(input), stdout, new StringWriter());
        var lines = stdout.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(JObject.Parse).ToList();
        Assert.Contains("load", (string?)lines[0]["error"]);
        Assert.True((bool)lines[1]["ok"]!);
        Assert.False((bool)lines[2]["ok"]!);
        Assert.Contains("not loaded", (string?)lines[3]["error"]);
    }
}
