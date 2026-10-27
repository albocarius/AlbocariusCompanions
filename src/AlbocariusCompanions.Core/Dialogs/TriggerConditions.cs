using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Triggers;

namespace AlbocariusCompanions.Core.Dialogs;

/// <summary>
/// Extra state for triggered content (M11): the payload of the trigger that started it and the companion's role. A context
/// that does not implement it has no event (event conditions fail) and no role.
/// </summary>
public interface ITriggerContext
{
    /// <summary>The fired trigger's payload while its content runs; null otherwise.</summary>
    TriggerPayload? Event { get; }
    /// <summary>The companion's current role (TriggerRegistry.Roles), null when it has none.</summary>
    string? Role { get; }
}

/// <summary><c>"var": "event.&lt;path&gt;"</c> conditions (M11 G4).</summary>
public static class TriggerConditions
{
    public const string EventPrefix = "event.";

    public static bool IsEventReference(string? reference) =>
        reference != null && reference.StartsWith(EventPrefix, System.StringComparison.Ordinal);

    public static string PathOf(string reference) => reference.Substring(EventPrefix.Length);

    /// <summary>
    /// The event condition holds: the path resolves in the payload and equals <c>eq</c>, or as a number lies within
    /// <c>min</c>/<c>max</c>. No payload or a missing field → false.
    /// </summary>
    public static bool EventHolds(ConditionSet when, TriggerPayload? payload)
    {
        if (payload == null || when.Var == null || !payload.TryResolve(PathOf(when.Var), out var value))
            return false;
        if (when.Eq != null && !Subjects.ValueEquals(value, when.Eq))
            return false;
        if (when.Min != null || when.Max != null)
        {
            if (!Subjects.TryNumber(value, out var n))
                return false;
            if (when.Min is { } min && n < min)
                return false;
            if (when.Max is { } max && n > max)
                return false;
        }
        return true;
    }

    /// <summary>Every condition object (nested ones too) that reads <c>event.</c>.</summary>
    public static IEnumerable<ConditionSet> EventConditions(ConditionSet? when)
    {
        if (when == null)
            yield break;
        if (IsEventReference(when.Var))
            yield return when;
        foreach (var nested in (when.All ?? new List<ConditionSet>()).Concat(when.Any ?? new List<ConditionSet>()).Append(when.Not))
            foreach (var c in EventConditions(nested))
                yield return c;
    }
}
