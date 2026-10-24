using System.Text.RegularExpressions;
using AlbocariusCompanions.Core.Logging;

namespace AlbocariusCompanions.Core.Tests;

/// <summary>Unit tests for ModLogger (complement the acceptance tests for task 24).</summary>
public class ModLoggerTests
{
    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("albocarius-unit-").FullName;
        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
        }
    }

    private static readonly Regex LinePattern =
        new(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \[(INFO|WARN|ERROR)\] ", RegexOptions.Compiled);

    private static (ModLogger log, string path, TempDir tmp) NewLogger(Func<DateTime>? clock = null)
    {
        var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "a", "b", "AlbocariusCompanions.log");
        return (new ModLogger(path, clock ?? (() => new DateTime(2026, 10, 24, 17, 5, 9, 123))), path, tmp);
    }

    [Fact]
    public void Small_date_and_time_parts_are_zero_padded()
    {
        var (log, path, tmp) = NewLogger(() => new DateTime(2026, 1, 2, 3, 4, 5, 67));
        using (tmp)
        {
            log.Warning("low health");
            Assert.Equal("2026-01-02 03:04:05.067 [WARN] low health", File.ReadAllText(path).TrimEnd());
        }
    }

    [Fact]
    public void Real_clock_is_the_default_and_still_produces_a_valid_line()
    {
        var (log, path, tmp) = NewLogger();
        using (tmp)
        {
            log.Info("no clock given");
            Assert.Matches(LinePattern, File.ReadAllLines(path).Single());
        }
    }

    [Fact]
    public void Broken_clock_still_writes_a_line()
    {
        var (log, path, tmp) = NewLogger(() => throw new InvalidOperationException("no clock"));
        using (tmp)
        {
            log.Info("hi");
            Assert.Equal("0001-01-01 00:00:00.000 [INFO] hi", File.ReadAllText(path).TrimEnd());
        }
    }

    [Fact]
    public void Start_session_replaces_content_that_was_there_before()
    {
        var (log, path, tmp) = NewLogger();
        using (tmp)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "junk from a crashed session\nmore junk\n");

            log.StartSession("fresh");
            Assert.Equal(new[] { "2026-10-24 17:05:09.123 [INFO] fresh" }, File.ReadAllLines(path));
        }
    }

    [Fact]
    public void Error_with_exception_keeps_the_level_on_the_first_line()
    {
        var (log, path, tmp) = NewLogger();
        using (tmp)
        {
            try { throw new InvalidOperationException("boom"); }
            catch (InvalidOperationException ex) { log.Error("patch failed", ex); }

            var lines = File.ReadAllLines(path);
            Assert.Equal("2026-10-24 17:05:09.123 [ERROR] patch failed", lines[0]);
            Assert.Equal("System.InvalidOperationException: boom", lines[1]);
            Assert.Contains(lines, l => l.TrimStart().StartsWith("at ", StringComparison.Ordinal));
            Assert.Contains(lines, l => l.Contains(nameof(Error_with_exception_keeps_the_level_on_the_first_line), StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Exception_that_was_never_thrown_still_logs_type_and_message()
    {
        var (log, path, tmp) = NewLogger();
        using (tmp)
        {
            log.Error("no stack", new InvalidCastException("no camp"));

            Assert.Equal("2026-10-24 17:05:09.123 [ERROR] no stack", File.ReadAllLines(path)[0]);
            Assert.Contains("System.InvalidCastException: no camp", File.ReadAllText(path));
        }
    }

    [Fact]
    public void Log_file_has_no_byte_order_mark()
    {
        var (log, path, tmp) = NewLogger();
        using (tmp)
        {
            log.StartSession("header");
            var bytes = File.ReadAllBytes(path);
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
            Assert.Equal((byte)'2', bytes[0]);
        }
    }

    [Fact]
    public void Side_files_are_overwritten_and_the_folder_is_created_on_demand()
    {
        var (log, path, tmp) = NewLogger();
        using (tmp)
        {
            var folder = Path.GetDirectoryName(path)!;

            Assert.Equal(Path.Combine(folder, "one.xml"), log.WriteSideFile("one.xml", "<a/>"));
            Assert.Equal(Path.Combine(folder, "one.xml"), log.WriteSideFile("one.xml", "<b/>"));
            Assert.Equal("<b/>", File.ReadAllText(Path.Combine(folder, "one.xml")));

            Assert.Equal(Path.Combine(folder, "two.txt"), log.WriteSideFile("two.txt", ""));
            Assert.Equal("", File.ReadAllText(Path.Combine(folder, "two.txt")));
        }
    }

    [Fact]
    public void Empty_path_writes_nothing_and_never_throws()
    {
        var log = new ModLogger("");

        log.StartSession("s");
        log.Info("i");
        log.Warning("w");
        log.Error("e");
        log.Error("e", null!);
        Assert.Null(log.WriteSideFile("x.xml", "<x/>"));
        Assert.Equal("", log.LogFilePath);
    }

    [Fact]
    public void Null_path_writes_nothing_and_never_throws()
    {
        var log = new ModLogger(null!);

        log.StartSession("s");
        log.Info("i");
        log.Error("e", new Exception("x"));
        Assert.Null(log.WriteSideFile(null!, "x"));
        Assert.Null(log.WriteSideFile("", "x"));
    }

    [Fact]
    public void Header_line_and_the_first_entry_do_not_share_a_line()
    {
        var (log, path, tmp) = NewLogger();
        using (tmp)
        {
            log.StartSession("session begins");
            log.Info("first");

            Assert.Equal(new[]
            {
                "2026-10-24 17:05:09.123 [INFO] session begins",
                "2026-10-24 17:05:09.123 [INFO] first",
            }, File.ReadAllLines(path));
        }
    }

    [Fact]
    public void Log_folder_is_recreated_after_the_whole_tree_is_deleted()
    {
        var (log, path, tmp) = NewLogger();
        using (tmp)
        {
            log.Info("one");
            var parent = Path.GetDirectoryName(Path.GetDirectoryName(path)!)!;
            Directory.Delete(parent, recursive: true);
            log.Info("two");

            Assert.Equal(new[] { "2026-10-24 17:05:09.123 [INFO] two" }, File.ReadAllLines(path));
        }
    }

    [Fact]
    public void Writes_from_many_threads_never_interleave_or_get_lost()
    {
        var (log, path, tmp) = NewLogger();
        using (tmp)
        {
            const int threads = 8;
            const int perThread = 25;

            Parallel.For(0, threads, t =>
            {
                for (var i = 0; i < perThread; i++)
                    log.Info($"t{t}-{i}");
            });

            var lines = File.ReadAllLines(path);
            Assert.Equal(threads * perThread, lines.Length);
            Assert.All(lines, l => Assert.Matches(LinePattern, l));
            Assert.Equal(lines.Length, lines.Distinct().Count());
        }
    }
}
