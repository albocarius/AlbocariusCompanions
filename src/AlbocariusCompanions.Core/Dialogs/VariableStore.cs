using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;

namespace AlbocariusCompanions.Core.Dialogs;

/// <summary>A declared (or built-in) variable of a loaded companion.</summary>
public sealed class VariableDefinition
{
    public VariableDefinition(string owner, string name, VariableType type, bool shared, int defaultValue)
    {
        Owner = owner;
        Name = name;
        Type = type;
        Shared = shared;
        Default = defaultValue;
    }

    public string Owner { get; }
    public string Name { get; }
    public VariableType Type { get; }
    public bool Shared { get; }
    /// <summary>Bool as 0/1.</summary>
    public int Default { get; }
}

/// <summary>
/// Conversation variables of one campaign (M7). Values are ints (bool as 0/1); a variable that was never written reads as
/// its declared default, so a changed default in JSON applies to it. Saved as "&lt;owner&gt;.&lt;name&gt;=&lt;value&gt;"
/// for every written variable; on load, unknown names are dropped and a value of the wrong type falls back to the default.
/// </summary>
public sealed class VariableStore
{
    private readonly Dictionary<string, Dictionary<string, VariableDefinition>> _definitions =
        new Dictionary<string, Dictionary<string, VariableDefinition>>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _values = new Dictionary<string, int>(StringComparer.Ordinal);

    public VariableStore()
    {
    }

    /// <summary>Definitions of <paramref name="packages"/> (validated; an invalid declaration is skipped) plus each one's "mood".</summary>
    public VariableStore(IEnumerable<CompanionPackage> packages)
    {
        foreach (var package in packages)
        {
            var own = new Dictionary<string, VariableDefinition>(StringComparer.Ordinal)
            {
                [VariableRules.Mood] = new VariableDefinition(package.Id, VariableRules.Mood, VariableType.Int, false, 0),
            };
            foreach (var pair in package.Dialogs?.Variables ?? new Dictionary<string, VariableDeclaration>())
            {
                if (pair.Value == null || pair.Key == VariableRules.Mood || !VariableRules.TryParseType(pair.Value.Type, out var type))
                    continue;
                var defaultValue = pair.Value.Default != null && VariableUses.TypeOf(pair.Value.Default) == type
                    ? VariableUses.ValueOf(pair.Value.Default)
                    : 0;
                own[pair.Key] = new VariableDefinition(package.Id, pair.Key, type, pair.Value.Scope == VariableRules.ScopeShared, defaultValue);
            }
            _definitions[package.Id] = own;
        }
    }

    public bool IsLoaded(string companionId) => _definitions.ContainsKey(companionId);

    public IEnumerable<string> Companions => _definitions.Keys.OrderBy(k => k, StringComparer.Ordinal);

    /// <summary>The definitions of one companion, sorted by name; empty when it is not loaded.</summary>
    public IEnumerable<VariableDefinition> Definitions(string companionId) =>
        _definitions.TryGetValue(companionId, out var own) ? own.Values.OrderBy(d => d.Name, StringComparer.Ordinal) : Enumerable.Empty<VariableDefinition>();

    /// <summary>Resolves <paramref name="reference"/> as used by <paramref name="currentCompanion"/>; null when unknown.</summary>
    public VariableDefinition? Find(string currentCompanion, string reference) =>
        VariableRules.TryParseReference(currentCompanion, reference, out var r)
        && _definitions.TryGetValue(r.Owner, out var own) && own.TryGetValue(r.Name, out var definition)
            ? definition
            : null;

    /// <summary>The current value (bool as 0/1), or null when the owner is not loaded or the variable is unknown.</summary>
    public int? Get(string currentCompanion, string reference)
    {
        var definition = Find(currentCompanion, reference);
        return definition == null ? (int?)null : Value(definition);
    }

    public int Value(VariableDefinition definition) =>
        _values.TryGetValue(Key(definition), out var value) ? value : definition.Default;

    /// <summary>Sets a value (bool: any non-zero is true). False when the variable is unknown (nothing changes).</summary>
    public bool Set(string currentCompanion, string reference, int value)
    {
        var definition = Find(currentCompanion, reference);
        if (definition == null)
            return false;
        _values[Key(definition)] = definition.Type == VariableType.Bool ? (value != 0 ? 1 : 0) : value;
        return true;
    }

    /// <summary>Adds to an int variable. False when the variable is unknown or not an int (nothing changes).</summary>
    public bool Add(string currentCompanion, string reference, int amount)
    {
        var definition = Find(currentCompanion, reference);
        if (definition == null || definition.Type != VariableType.Int)
            return false;
        _values[Key(definition)] = unchecked(Value(definition) + amount);
        return true;
    }

    /// <summary>Back to the defaults for every variable of one companion; returns how many had a written value.</summary>
    public int Reset(string companionId)
    {
        var keys = _values.Keys.Where(k => k.StartsWith(companionId + ".", StringComparison.Ordinal)).ToList();
        foreach (var key in keys)
            _values.Remove(key);
        return keys.Count;
    }

    /// <summary>The save list: "owner.name=value" for every written variable, sorted (ordinal).</summary>
    public List<string> ToList() => _values
        .Select(p => p.Key + "=" + Format(p.Key, p.Value))
        .OrderBy(s => s, StringComparer.Ordinal)
        .ToList();

    /// <summary>
    /// Replaces the written values with saved entries. Unknown or malformed entries are dropped, a value of the wrong type
    /// falls back to the default; each case is reported to <paramref name="log"/>. Returns how many values were restored.
    /// </summary>
    public int Load(IEnumerable<string>? saved, Action<string> log)
    {
        _values.Clear();
        var restored = 0;
        foreach (var entry in saved ?? Enumerable.Empty<string>())
        {
            var eq = entry?.IndexOf('=') ?? -1;
            if (entry == null || eq <= 0)
            {
                log($"Saved variable entry '{entry}' is malformed; ignored.");
                continue;
            }
            var key = entry.Substring(0, eq);
            var text = entry.Substring(eq + 1);
            var dot = key.IndexOf('.');
            var definition = dot > 0 ? Find(key.Substring(0, dot), key) : null;
            if (definition == null)
            {
                log($"Saved variable '{key}' is no longer declared (or its companion is not loaded); ignored.");
                continue;
            }
            if (!TryParse(definition.Type, text, out var value))
            {
                log($"Saved variable '{key}' has '{text}', which is not a {VariableRules.Name(definition.Type)}; using the default.");
                continue;
            }
            _values[Key(definition)] = value;
            restored++;
        }
        return restored;
    }

    /// <summary>
    /// M6 saves kept flags as "&lt;companion&gt;:&lt;flag&gt;" keys: each becomes true for a declared bool of that name, the rest
    /// are dropped with a log line. Returns how many were imported.
    /// </summary>
    public int ImportLegacyFlags(IEnumerable<string> keys, Action<string> log)
    {
        var imported = 0;
        foreach (var key in keys)
        {
            var colon = key.IndexOf(':');
            var definition = colon > 0 ? Find(key.Substring(0, colon), key.Substring(colon + 1)) : null;
            if (definition == null || definition.Type != VariableType.Bool)
            {
                log($"Old conversation flag '{key}' has no declared bool variable; dropped.");
                continue;
            }
            _values[Key(definition)] = 1;
            imported++;
        }
        return imported;
    }

    public static string Format(VariableType type, int value) =>
        type == VariableType.Bool ? (value != 0 ? "true" : "false") : value.ToString(CultureInfo.InvariantCulture);

    public static bool TryParse(VariableType type, string text, out int value)
    {
        value = 0;
        if (type == VariableType.Bool)
        {
            if (text != "true" && text != "false")
                return false;
            value = text == "true" ? 1 : 0;
            return true;
        }
        return int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }

    private string Format(string key, int value)
    {
        var dot = key.IndexOf('.');
        var definition = Find(key.Substring(0, dot), key);
        return Format(definition?.Type ?? VariableType.Int, value);
    }

    private static string Key(VariableDefinition definition) => definition.Owner + "." + definition.Name;
}
