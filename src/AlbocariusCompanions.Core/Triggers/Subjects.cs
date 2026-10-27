using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Core.Triggers;

/// <summary>
/// Trigger subjects (spec T3, M11 G3): <c>self</c>, <c>player</c>, <c>playerParty</c>, <c>playerClan</c>,
/// <c>companion:&lt;id&gt;</c>, <c>hero:&lt;stringId&gt;</c>, <c>settlement:&lt;stringId&gt;</c>, <c>any</c>.
/// </summary>
public static class Subjects
{
    public const string Self = "self";
    public const string Player = "player";
    public const string PlayerParty = "playerParty";
    public const string PlayerClan = "playerClan";
    public const string Companion = "companion";
    public const string Hero = "hero";
    public const string Settlement = "settlement";
    public const string Any = "any";

    /// <summary>Kinds that take an id after a colon.</summary>
    public static readonly IReadOnlyCollection<string> WithId = new HashSet<string> { Companion, Hero, Settlement };

    /// <summary>Subject kinds a field of this kind can be matched against.</summary>
    public static IReadOnlyList<string> KindsFor(FieldKind kind)
    {
        switch (kind)
        {
            case FieldKind.Hero:
            case FieldKind.Party:
                return new[] { Self, Player, PlayerParty, PlayerClan, Companion, Hero };
            case FieldKind.Settlement:
                return new[] { Settlement, PlayerClan };
            case FieldKind.Clan:
            case FieldKind.Kingdom:
                return new[] { Player, PlayerClan };
            default:
                return Array.Empty<string>();
        }
    }

    /// <summary>Splits "kind" / "kind:id". False when the text is malformed (unknown kind, id missing or not allowed).</summary>
    public static bool TryParse(string? subject, out string kind, out string id)
    {
        kind = string.Empty;
        id = string.Empty;
        if (string.IsNullOrEmpty(subject)) return false;
        var colon = subject!.IndexOf(':');
        kind = colon < 0 ? subject : subject.Substring(0, colon);
        id = colon < 0 ? string.Empty : subject.Substring(colon + 1);
        var known = kind == Self || kind == Player || kind == PlayerParty || kind == PlayerClan || kind == Any || WithId.Contains(kind);
        if (!known) return false;
        return WithId.Contains(kind) ? id.Length > 0 : colon < 0;
    }

    /// <summary>True when the subject is valid for the trigger (parsable and its kind among the trigger's subject kinds).</summary>
    public static bool IsValidFor(TriggerDefinition trigger, string subject) =>
        TryParse(subject, out var kind, out _) && trigger.SubjectKinds().Contains(kind);

    /// <summary>
    /// Whether a fired payload matches the subject for the content of companion <paramref name="selfId"/>: "any" always;
    /// otherwise any of the trigger's subject fields matches (G3).
    /// </summary>
    public static bool Matches(TriggerDefinition trigger, string subject, TriggerPayload payload, string selfId)
    {
        if (!TryParse(subject, out var kind, out var id)) return false;
        if (kind == Any) return true;
        foreach (var name in trigger.SubjectFields)
        {
            if (!payload.Fields.TryGetValue(name, out var value) || !value.IsEntity) continue;
            if (!KindsFor(value.Kind).Contains(kind)) continue;
            if (MatchesValue(kind, id, value, selfId)) return true;
        }
        return false;
    }

    private static bool MatchesValue(string kind, string id, PayloadValue v, string selfId)
    {
        switch (kind)
        {
            case Self: return v.StringProperty(EntityProperties.Companion) == selfId && selfId.Length > 0;
            case Player: return v.BoolProperty(EntityProperties.IsPlayer);
            case PlayerParty: return v.BoolProperty(EntityProperties.InPlayerParty);
            case PlayerClan: return v.BoolProperty(EntityProperties.InPlayerClan);
            case Companion: return v.StringProperty(EntityProperties.Companion) == id;
            case Hero: return v.StringProperty(EntityProperties.HeroId) == id;
            case Settlement: return v.Kind == FieldKind.Settlement && v.StringProperty(EntityProperties.Id) == id;
            default: return false;
        }
    }

    /// <summary>
    /// The <c>where</c> filters (spec T3): every path must resolve and equal the JSON value (strings ordinal, bools,
    /// numbers by value). An empty/absent filter passes.
    /// </summary>
    public static bool WhereMatches(IReadOnlyDictionary<string, JToken>? where, TriggerPayload payload)
    {
        if (where == null) return true;
        foreach (var pair in where)
            if (!payload.TryResolve(pair.Key, out var actual) || !ValueEquals(actual, pair.Value))
                return false;
        return true;
    }

    /// <summary>A resolved payload value equals a JSON value (used by <c>where</c> and <c>event.</c> conditions).</summary>
    public static bool ValueEquals(object? actual, JToken? expected)
    {
        if (expected == null) return false;
        switch (expected.Type)
        {
            case JTokenType.String: return actual is string s && s == (string)expected!;
            case JTokenType.Boolean: return actual is bool b && b == (bool)expected;
            case JTokenType.Integer:
            case JTokenType.Float:
                return TryNumber(actual, out var n) && n == (double)expected;
            default: return false;
        }
    }

    /// <summary>A resolved payload value as a number (ints and numbers only).</summary>
    public static bool TryNumber(object? value, out double number)
    {
        switch (value)
        {
            case long l: number = l; return true;
            case int i: number = i; return true;
            case double d: number = d; return true;
            default: number = 0; return false;
        }
    }

    /// <summary>Parses a console / simulator text value into a payload primitive of the given kind.</summary>
    public static bool TryParsePrimitive(FieldKind kind, string text, out PayloadValue? value)
    {
        value = null;
        switch (kind)
        {
            case FieldKind.Int when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l):
                value = PayloadValue.Int(l); return true;
            case FieldKind.Number when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d):
                value = PayloadValue.Number(d); return true;
            case FieldKind.Bool when bool.TryParse(text, out var b):
                value = PayloadValue.Bool(b); return true;
            case FieldKind.Text:
                value = PayloadValue.Text(text); return true;
            default: return false;
        }
    }
}
