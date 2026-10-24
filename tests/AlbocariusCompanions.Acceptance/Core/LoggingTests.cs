using AlbocariusCompanions.Core.Logging;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 24-core-logging (GAME_MAP Q15: the game catches no exception from mod code).
public class LoggingTests
{
    static readonly DateTime T = new(2026, 10, 24, 17, 5, 9, 123);

    static (ModLogger log, string path, TempDir tmp) NewLogger()
    {
        var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "nested", "dir", "AlbocariusCompanions.log");
        return (new ModLogger(path, () => T), path, tmp);
    }

    [Fact]
    public void Start_session_creates_folders_and_truncates()
    {
        var (log, path, tmp) = NewLogger();
        using (tmp)
        {
            log.StartSession("first");
            log.Info("old line");
            log.StartSession("second run");

            var lines = File.ReadAllLines(path);
            Assert.Single(lines);
            Assert.Equal("2026-10-24 17:05:09.123 [INFO] second run", lines[0]);
            Assert.Equal(path, log.LogFilePath);
        }
    }

    [Fact]
    public void Levels_and_order()
    {
        var (log, path, tmp) = NewLogger();
        using (tmp)
        {
            log.StartSession("start");
            log.Info("a");
            log.Warning("b");
            log.Error("c");

            Assert.Equal(new[]
            {
                "2026-10-24 17:05:09.123 [INFO] start",
                "2026-10-24 17:05:09.123 [INFO] a",
                "2026-10-24 17:05:09.123 [WARN] b",
                "2026-10-24 17:05:09.123 [ERROR] c",
            }, File.ReadAllLines(path));
        }
    }

    [Fact]
    public void Appends_without_a_session()
    {
        var (log, path, tmp) = NewLogger();
        using (tmp)
        {
            log.Info("x");
            log.Info("y");
            Assert.Equal(2, File.ReadAllLines(path).Length);
        }
    }

    [Fact]
    public void Exception_details_are_logged()
    {
        var (log, path, tmp) = NewLogger();
        using (tmp)
        {
            try { throw new InvalidOperationException("boom"); }
            catch (Exception ex) { log.Error("while spawning", ex); }

            var text = File.ReadAllText(path);
            Assert.Contains("[ERROR] while spawning", text);
            Assert.Contains("InvalidOperationException", text);
            Assert.Contains("boom", text);
            Assert.Contains(nameof(Exception_details_are_logged), text); // stack trace
        }
    }

    [Fact]
    public void Side_file_is_written_next_to_the_log()
    {
        var (log, path, tmp) = NewLogger();
        using (tmp)
        {
            var full = log.WriteSideFile("AlbocariusCompanions_NPCCharacters.xml", "<a/>");
            log.WriteSideFile("AlbocariusCompanions_NPCCharacters.xml", "<b/>");

            Assert.Equal(Path.Combine(Path.GetDirectoryName(path)!, "AlbocariusCompanions_NPCCharacters.xml"), full);
            Assert.Equal("<b/>", File.ReadAllText(full!));
        }
    }

    [Fact]
    public void Unwritable_path_never_throws()
    {
        using var tmp = new TempDir();
        var blocker = Path.Combine(tmp.Path, "file");
        File.WriteAllText(blocker, "");
        var log = new ModLogger(Path.Combine(blocker, "sub", "x.log"));  // parent is a file

        log.StartSession("s");
        log.Info("i");
        log.Warning("w");
        log.Error("e");
        log.Error("e", new Exception("x"));
        Assert.Null(log.WriteSideFile("y.xml", "<y/>"));
    }

    [Fact]
    public void Safe_run_logs_and_swallows_exceptions()
    {
        var (log, path, tmp) = NewLogger();
        using (tmp)
        {
            var ran = false;
            Safe.Run(log, "OnSubModuleLoad", () => ran = true);
            Safe.Run(log, "DailyTick", () => throw new ArgumentException("bad"));

            Assert.True(ran);
            var text = File.ReadAllText(path);
            Assert.Contains("[ERROR]", text);
            Assert.Contains("DailyTick", text);
            Assert.Contains("bad", text);
        }
    }

    [Fact]
    public void Safe_run_with_result_returns_value_or_fallback()
    {
        var (log, path, tmp) = NewLogger();
        using (tmp)
        {
            Assert.Equal(5, Safe.Run(log, "ok", () => 5, -1));
            Assert.Equal(-1, Safe.Run<int>(log, "condition", () => throw new Exception("nope"), -1));
            Assert.Contains("condition", File.ReadAllText(path));
        }
    }

    [Fact]
    public void Safe_run_survives_a_broken_logger()
    {
        using var tmp = new TempDir();
        var blocker = Path.Combine(tmp.Path, "file");
        File.WriteAllText(blocker, "");
        var log = new ModLogger(Path.Combine(blocker, "x.log"));

        Safe.Run(log, "ctx", () => throw new Exception("x"));
        Assert.False(Safe.Run(log, "ctx", () => throw new Exception("y"), false));
    }
}
