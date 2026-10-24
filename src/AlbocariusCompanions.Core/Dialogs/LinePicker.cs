using System;
using System.Collections.Generic;

namespace AlbocariusCompanions.Core.Dialogs;

/// <summary>Random number source, injectable so slot variation is testable (Game: MBRandom).</summary>
public interface IRandomSource
{
    /// <summary>A value in [0, maxExclusive).</summary>
    int Next(int maxExclusive);
}

public sealed class SystemRandomSource : IRandomSource
{
    private readonly Random _random;

    public SystemRandomSource(int? seed = null) => _random = seed.HasValue ? new Random(seed.Value) : new Random();

    public int Next(int maxExclusive) => _random.Next(maxExclusive);
}

/// <summary>Slot variation (SPEC_CORE_GAPS T5): one of a slot's lines at random each time the slot is used.</summary>
public static class LinePicker
{
    /// <summary>Null for an empty list; the only line for a single-line slot (no random call); otherwise a random one.</summary>
    public static T? Pick<T>(IReadOnlyList<T> lines, IRandomSource random) where T : class
    {
        if (lines == null) throw new ArgumentNullException(nameof(lines));
        if (random == null) throw new ArgumentNullException(nameof(random));

        switch (lines.Count)
        {
            case 0: return null;
            case 1: return lines[0];
        }

        var index = random.Next(lines.Count);
        // A misbehaving source must not crash a conversation: clamp into range.
        return lines[Math.Min(Math.Max(index, 0), lines.Count - 1)];
    }
}
