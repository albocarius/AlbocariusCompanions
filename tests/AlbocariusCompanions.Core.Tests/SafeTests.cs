using AlbocariusCompanions.Core.Logging;

namespace AlbocariusCompanions.Core.Tests;

/// <summary>Unit tests for Safe (complement the acceptance tests for task 24).</summary>
public class SafeTests
{
    private static ModLogger Logger(string folderPath) =>
        new ModLogger(Path.Combine(folderPath, "nested", "AlbocariusCompanions.log"),
            () => new DateTime(2026, 10, 24, 17, 5, 9, 123));

    [Fact]
    public void Run_logs_exactly_one_error_per_thrown_exception()
    {
        var folder = Directory.CreateTempSubdirectory("albocarius-unit-").FullName;
        try
        {
            var log = Logger(folder);
            Safe.Run(log, "OnGameLoad", () => throw new InvalidCastException("no camp"));
            Safe.Run(log, "OnGameLoad", () => throw new InvalidCastException("no camp"));

            var lines = File.ReadAllLines(Path.Combine(folder, "nested", "AlbocariusCompanions.log"));
            Assert.Equal(2, lines.Count(l => l.Contains("[ERROR] OnGameLoad", StringComparison.Ordinal)));
            Assert.Contains(lines, l => l.StartsWith("System.InvalidCastException: no camp", StringComparison.Ordinal));
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public void Run_does_not_touch_the_log_when_the_body_succeeds()
    {
        var folder = Directory.CreateTempSubdirectory("albocarius-unit-").FullName;
        try
        {
            var log = Logger(folder);
            Safe.Run(log, "OnSubModuleLoad", () => { });
            Safe.Run(log, "DailyTick", () => { });
            Safe.Run(log, "OnApplicationTick", () => { });

            Assert.False(File.Exists(Path.Combine(folder, "nested", "AlbocariusCompanions.log")));
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public void Run_creates_the_log_folder_itself_when_it_logs()
    {
        var folder = Directory.CreateTempSubdirectory("albocarius-unit-").FullName;
        try
        {
            Safe.Run(Logger(folder), "DailyTick", () => throw new Exception("x"));
            Assert.True(File.Exists(Path.Combine(folder, "nested", "AlbocariusCompanions.log")));
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public void Missing_logger_still_swallows_and_still_returns_the_fallback()
    {
        Safe.Run(null!, "ctx", () => throw new Exception("x"));
        Assert.Equal(0, Safe.Run<int>(null!, "ctx", () => throw new Exception("y"), 0));
    }

    [Fact]
    public void Value_run_passes_through_the_body_result_including_defaults()
    {
        var folder = Directory.CreateTempSubdirectory("albocarius-unit-").FullName;
        try
        {
            var log = Logger(folder);

            Assert.True(Safe.Run(log, "condition", () => true, false));
            Assert.False(Safe.Run(log, "condition", () => false, true));
            Assert.Null(Safe.Run<string>(log, "lookup", () => null!, "fallback"));
            Assert.Equal("fallback", Safe.Run<string>(log, "lookup", () => throw new KeyNotFoundException(), "fallback"));
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public void Nested_safe_calls_do_not_multiply_the_error()
    {
        var folder = Directory.CreateTempSubdirectory("albocarius-unit-").FullName;
        try
        {
            var log = Logger(folder);
            Safe.Run(log, "outer", () => Safe.Run(log, "inner", () => throw new Exception("deep")));

            var text = File.ReadAllText(Path.Combine(folder, "nested", "AlbocariusCompanions.log"));
            Assert.Contains("[ERROR] inner", text);
            Assert.DoesNotContain("[ERROR] outer", text);
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { }
        }
    }
}
