using System.Collections.Generic;
using AlbocariusCompanions.Core.Models;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Core.Dialogs;

/// <summary>The variable part of node effects (M7); relation, gold and joining stay in the game layer.</summary>
public static class ConversationEffects
{
    /// <summary>
    /// Applies setFlags, clearFlags, set and add in that order. A write to a variable whose companion is not loaded is
    /// skipped; the skipped references are returned for the log.
    /// </summary>
    public static List<string> ApplyVariables(string companionId, EffectSet? effects, VariableStore store)
    {
        var skipped = new List<string>();
        if (effects == null)
            return skipped;
        void Write(string? reference, bool done)
        {
            if (!done)
                skipped.Add(reference ?? "(null)");
        }

        foreach (var flag in effects.SetFlags ?? new List<string>())
            Write(flag, flag != null && store.Set(companionId, flag, 1));
        foreach (var flag in effects.ClearFlags ?? new List<string>())
            Write(flag, flag != null && store.Set(companionId, flag, 0));
        foreach (var pair in effects.Set ?? new Dictionary<string, JToken>())
            Write(pair.Key, VariableUses.TypeOf(pair.Value) != null && store.Set(companionId, pair.Key, VariableUses.ValueOf(pair.Value)));
        foreach (var pair in effects.Add ?? new Dictionary<string, int>())
            Write(pair.Key, store.Add(companionId, pair.Key, pair.Value));
        return skipped;
    }
}
