using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AlbocariusCompanions.Core.Rules;

public enum VariableType
{
    Bool,
    Int,
}

/// <summary>Fixed words of dialogs.json "variables" (M7) and the variable reference format.</summary>
public static class VariableRules
{
    public const string TypeBool = "bool";
    public const string TypeInt = "int";
    public static readonly IReadOnlyList<string> Types = new[] { TypeBool, TypeInt };

    public const string ScopeLocal = "local";
    public const string ScopeShared = "shared";
    public static readonly IReadOnlyList<string> Scopes = new[] { ScopeLocal, ScopeShared };

    /// <summary>Every companion's built-in local int, default 0. No mechanics use it yet.</summary>
    public const string Mood = "mood";

    /// <summary>Variable names: [a-z0-9_]+ (the same rule as flags and companion ids).</summary>
    public static readonly Regex NamePattern = ConversationRules.FlagPattern;

    public static bool TryParseType(string? text, out VariableType type)
    {
        type = text == TypeInt ? VariableType.Int : VariableType.Bool;
        return text == TypeBool || text == TypeInt;
    }

    public static string Name(VariableType type) => type == VariableType.Int ? TypeInt : TypeBool;

    /// <summary>
    /// Splits a reference: "name" → this companion's variable, "companion.name" → that companion's. False when the text is
    /// not one of these two forms with valid parts.
    /// </summary>
    public static bool TryParseReference(string currentCompanion, string? reference, out VariableRef result)
    {
        result = default;
        if (reference == null)
            return false;
        var parts = reference.Split('.');
        if (parts.Length == 1 && NamePattern.IsMatch(parts[0]))
        {
            result = new VariableRef(currentCompanion, parts[0]);
            return true;
        }
        if (parts.Length == 2 && NamePattern.IsMatch(parts[0]) && NamePattern.IsMatch(parts[1]))
        {
            result = new VariableRef(parts[0], parts[1]);
            return true;
        }
        return false;
    }
}

/// <summary>A resolved variable reference: the owning companion and the variable name.</summary>
public readonly struct VariableRef
{
    public VariableRef(string owner, string name)
    {
        Owner = owner;
        Name = name;
    }

    public string Owner { get; }
    public string Name { get; }

    public override string ToString() => Owner + "." + Name;
}
