using AlbocariusCompanions.Core.Logging;

namespace AlbocariusCompanions.Core.Tests;

public class NotificationQueueTests
{
    [Fact]
    public void Info_is_never_queued()
    {
        var queue = new NotificationQueue();
        queue.Add(LogLevel.Info, "loaded");
        Assert.Empty(queue.Drain());
    }

    [Fact]
    public void Warnings_and_errors_keep_order_and_level()
    {
        var queue = new NotificationQueue();
        queue.Add(LogLevel.Error, "a");
        queue.Add(LogLevel.Warning, "b");

        var drained = queue.Drain();
        Assert.Equal(new[] { "a", "b" }, drained.Select(n => n.Text));
        Assert.Equal(new[] { LogLevel.Error, LogLevel.Warning }, drained.Select(n => n.Level));
    }

    [Fact]
    public void A_text_is_queued_once_per_session_even_after_a_drain()
    {
        var queue = new NotificationQueue();
        queue.Add(LogLevel.Warning, "same");
        queue.Add(LogLevel.Warning, "same");
        Assert.Single(queue.Drain());

        queue.Add(LogLevel.Error, "same");
        Assert.Empty(queue.Drain());
    }

    [Fact]
    public void Drain_empties_the_queue()
    {
        var queue = new NotificationQueue();
        queue.Add(LogLevel.Warning, "x");
        queue.Drain();
        Assert.Empty(queue.Drain());
    }

    [Theory]
    [InlineData(1, 0, "Albocarius's Companions: 1 companion loaded.")]
    [InlineData(3, 0, "Albocarius's Companions: 3 companions loaded.")]
    [InlineData(0, 2, "Albocarius's Companions: 0 companions loaded, 2 skipped — see log.")]
    public void Summary_mentions_the_log_only_when_something_was_skipped(int loaded, int skipped, string expected) =>
        Assert.Equal(expected, NotificationQueue.Summary(loaded, skipped));

    [Fact]
    public void Logger_forwards_warnings_and_errors_but_not_info()
    {
        var seen = new List<(LogLevel, string)>();
        var log = new ModLogger(string.Empty) { Notify = (level, text) => seen.Add((level, text)) };

        log.Info("i");
        log.Warning("w");
        log.Error("e");
        log.Error("x", new InvalidOperationException("boom"));

        Assert.Equal(new[] { (LogLevel.Warning, "w"), (LogLevel.Error, "e"), (LogLevel.Error, "x") }, seen);
    }

    [Fact]
    public void A_throwing_listener_does_not_break_logging()
    {
        var log = new ModLogger(string.Empty) { Notify = (_, _) => throw new InvalidOperationException() };
        log.Warning("still fine");
    }
}
