using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Core.Dialogs;

/// <summary>One place a condition or effect reads or writes a variable, with the type that place needs.</summary>
public sealed class VariableUse
{
    public VariableUse(string? reference, VariableType? type, bool isWrite, string key)
    {
        Reference = reference;
        Type = type;
        IsWrite = isWrite;
        Key = key;
    }

    /// <summary>As written: "name" or "companion.name" (may be invalid or null).</summary>
    public string? Reference { get; }
    /// <summary>The type the use needs; null when an "eq"/"set" value is neither a JSON bool nor an integer.</summary>
    public VariableType? Type { get; }
    public bool IsWrite { get; }
    /// <summary>The JSON key the reference came from (flags, var, setFlags, set, …), for messages.</summary>
    public string Key { get; }
}

/// <summary>Lists the variable uses of conditions and effects (validation, runtime "missing companion" check).</summary>
public static class VariableUses
{
    public static IEnumerable<VariableUse> InCondition(ConditionSet? when)
    {
        if (when == null)
            yield break;
        foreach (var flag in when.Flags ?? new List<string>())
            yield return new VariableUse(flag, VariableType.Bool, false, "flags");
        foreach (var flag in when.NotFlags ?? new List<string>())
            yield return new VariableUse(flag, VariableType.Bool, false, "notFlags");
        if (when.Var != null)
        {
            if (when.Eq != null)
                yield return new VariableUse(when.Var, TypeOf(when.Eq), false, "var");
            else if (when.Min != null || when.Max != null)
                yield return new VariableUse(when.Var, VariableType.Int, false, "var");
        }
        foreach (var nested in (when.All ?? new List<ConditionSet>()).Concat(when.Any ?? new List<ConditionSet>()).Append(when.Not))
            foreach (var use in InCondition(nested))
                yield return use;
    }

    public static IEnumerable<VariableUse> InEffects(EffectSet? effects)
    {
        if (effects == null)
            yield break;
        foreach (var flag in effects.SetFlags ?? new List<string>())
            yield return new VariableUse(flag, VariableType.Bool, true, "setFlags");
        foreach (var flag in effects.ClearFlags ?? new List<string>())
            yield return new VariableUse(flag, VariableType.Bool, true, "clearFlags");
        foreach (var pair in effects.Set ?? new Dictionary<string, JToken>())
            yield return new VariableUse(pair.Key, TypeOf(pair.Value), true, "set");
        foreach (var pair in effects.Add ?? new Dictionary<string, int>())
            yield return new VariableUse(pair.Key, VariableType.Int, true, "add");
    }

    /// <summary>JSON bool → bool, JSON integer → int, anything else (string, float, null…) → null.</summary>
    public static VariableType? TypeOf(JToken? value) => value?.Type switch
    {
        JTokenType.Boolean => VariableType.Bool,
        JTokenType.Integer => VariableType.Int,
        _ => null,
    };

    /// <summary>The value as the store keeps it: bool as 0/1. Only valid for a token whose <see cref="TypeOf"/> is not null.</summary>
    public static int ValueOf(JToken value) => value.Type == JTokenType.Boolean ? (value.Value<bool>() ? 1 : 0) : value.Value<int>();
}
