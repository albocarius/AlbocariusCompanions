using System;
using System.Collections.Generic;

namespace AlbocariusCompanions.Core.Logging;

public sealed class Notification
{
    public Notification(LogLevel level, string text)
    {
        Level = level;
        Text = text;
    }

    public LogLevel Level { get; }
    public string Text { get; }
}

/// <summary>
/// Warnings and errors waiting to be shown in game (SPEC_CORE_GAPS T2.5). Info is file-only and never queued; each
/// distinct text is queued once per session (process), however often it is logged. Thread-safe, never throws.
/// </summary>
public sealed class NotificationQueue
{
    private readonly object _gate = new object();
    private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);
    private readonly List<Notification> _pending = new List<Notification>();

    public void Add(LogLevel level, string text)
    {
        if (level == LogLevel.Info || string.IsNullOrEmpty(text))
            return;

        lock (_gate)
        {
            if (_seen.Add(text))
                _pending.Add(new Notification(level, text));
        }
    }

    /// <summary>Returns the pending notifications in the order they were added and empties the queue.</summary>
    public IReadOnlyList<Notification> Drain()
    {
        lock (_gate)
        {
            var drained = _pending.ToArray();
            _pending.Clear();
            return drained;
        }
    }

    /// <summary>One-line campaign start/load summary; points to the log when anything was skipped.</summary>
    public static string Summary(int loaded, int skipped)
    {
        var text = $"Albocarius's Companions: {loaded} companion{(loaded == 1 ? "" : "s")} loaded";
        return skipped > 0 ? text + $", {skipped} skipped — see log." : text + ".";
    }
}
