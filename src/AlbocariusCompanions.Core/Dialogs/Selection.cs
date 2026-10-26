using System;
using System.Collections.Generic;
using System.Linq;

namespace AlbocariusCompanions.Core.Dialogs;

/// <summary>
/// The selection rule (SPEC_SELECTION_AND_EVENTS §0): among the candidates whose conditions pass, the highest priority
/// wins (absent = 0), ties are broken uniformly at random. Player options are never selected (all are shown).
/// </summary>
public static class Selection
{
    public const int DefaultPriority = 0;

    public static int PriorityOf(int? priority) => priority ?? DefaultPriority;

    /// <summary>The passing candidates that share the highest priority, in input order; empty when none pass.</summary>
    public static List<T> TopGroup<T>(IEnumerable<T> passing, Func<T, int?> priority)
    {
        if (passing == null) throw new ArgumentNullException(nameof(passing));
        if (priority == null) throw new ArgumentNullException(nameof(priority));

        var list = passing.ToList();
        if (list.Count == 0)
            return list;
        var top = list.Max(c => PriorityOf(priority(c)));
        return list.Where(c => PriorityOf(priority(c)) == top).ToList();
    }

    /// <summary>The winner: one of <see cref="TopGroup{T}"/> at random (no random call for a single one); null when none pass.</summary>
    public static T? Pick<T>(IEnumerable<T> passing, Func<T, int?> priority, IRandomSource random) where T : class =>
        LinePicker.Pick(TopGroup(passing, priority), random);

    /// <summary>Highest priority first; equal priorities keep their input order (hub menu order).</summary>
    public static List<T> Ordered<T>(IEnumerable<T> items, Func<T, int?> priority) =>
        items.OrderByDescending(i => PriorityOf(priority(i))).ToList();
}

/// <summary>
/// Picks kept for one talk (user, M9 F2): each selection set is decided once and re-decided only when the kept winner
/// no longer passes. The game clears it when a conversation ends; the simulator per session.
/// </summary>
public sealed class SelectionMemory
{
    private readonly Dictionary<string, object> _kept = new Dictionary<string, object>(StringComparer.Ordinal);

    public T? Decide<T>(string key, IReadOnlyList<T> passing, Func<T, int?> priority, IRandomSource random) where T : class
    {
        if (_kept.TryGetValue(key, out var kept) && passing.Any(c => ReferenceEquals(c, kept)))
            return (T)kept;

        var pick = Selection.Pick(passing, priority, random);
        if (pick == null)
            _kept.Remove(key);
        else
            _kept[key] = pick;
        return pick;
    }

    public void Clear() => _kept.Clear();
}
