using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AlbocariusCompanions.Core.Triggers;

/// <summary>What a payload field holds (M11 G2). Entities are game objects reduced to fixed properties; the rest are primitives.</summary>
public enum FieldKind
{
    Hero,
    Settlement,
    Clan,
    Kingdom,
    Party,
    Item,
    Int,
    Number,
    Bool,
    /// <summary>A string: an enum value as the game names it, an id, a name.</summary>
    Text,
}

/// <summary>
/// The fixed properties of each entity kind (M11 G2). Every entity has <c>id</c> and <c>name</c> plus the subject flags
/// (<see cref="Subjects"/>): <c>isPlayer</c>, <c>inPlayerParty</c>, <c>inPlayerClan</c>, <c>companion</c> (mod companion id
/// of the hero / party leader, empty if none) and <c>heroId</c> (the hero / party leader string id). Property values are
/// strings or bools; references to other objects are their string ids.
/// </summary>
public static class EntityProperties
{
    public const string Id = "id";
    public const string Name = "name";
    public const string IsPlayer = "isPlayer";
    public const string InPlayerParty = "inPlayerParty";
    public const string InPlayerClan = "inPlayerClan";
    public const string Companion = "companion";
    public const string HeroId = "heroId";

    private static readonly string[] Common = { Id, Name, IsPlayer, InPlayerParty, InPlayerClan, Companion, HeroId };

    private static readonly Dictionary<FieldKind, string[]> Extra = new Dictionary<FieldKind, string[]>
    {
        // occupation: the game's Occupation name (Lord, Wanderer, …); isCompanion: a companion of the player's clan.
        [FieldKind.Hero] = new[] { "culture", "clan", "kingdom", "occupation", "isCompanion", "isAlive" },
        // kind: town | castle | village | hideout | other.
        [FieldKind.Settlement] = new[] { "culture", "kind", "ownerClan", "kingdom" },
        [FieldKind.Clan] = new[] { "culture", "kingdom", "leader" },
        [FieldKind.Kingdom] = new[] { "culture", "leader" },
        [FieldKind.Party] = new[] { "leader", "clan", "kingdom", "isMainParty" },
        [FieldKind.Item] = new[] { "type" },
    };

    /// <summary>Properties that hold a bool; every other property holds a string.</summary>
    public static readonly IReadOnlyCollection<string> BoolProperties =
        new HashSet<string> { IsPlayer, InPlayerParty, InPlayerClan, "isCompanion", "isAlive", "isMainParty" };

    public static bool IsEntity(FieldKind kind) => Extra.ContainsKey(kind);

    /// <summary>All properties of an entity kind (common first); empty for primitives.</summary>
    public static IReadOnlyList<string> Of(FieldKind kind) =>
        Extra.TryGetValue(kind, out var extra) ? Common.Concat(extra).ToList() : (IReadOnlyList<string>)Array.Empty<string>();
}

/// <summary>One payload value: a primitive (long / double / bool / string) or an entity with its properties.</summary>
public sealed class PayloadValue
{
    public FieldKind Kind { get; }
    /// <summary>The primitive value; null for entities.</summary>
    public object? Value { get; }
    /// <summary>Entity properties (string or bool values); empty for primitives.</summary>
    public IReadOnlyDictionary<string, object> Properties { get; }

    private PayloadValue(FieldKind kind, object? value, IReadOnlyDictionary<string, object> properties)
    {
        Kind = kind;
        Value = value;
        Properties = properties;
    }

    public static PayloadValue Int(long value) => new PayloadValue(FieldKind.Int, value, Empty);
    public static PayloadValue Number(double value) => new PayloadValue(FieldKind.Number, value, Empty);
    public static PayloadValue Bool(bool value) => new PayloadValue(FieldKind.Bool, value, Empty);
    public static PayloadValue Text(string value) => new PayloadValue(FieldKind.Text, value ?? string.Empty, Empty);

    /// <summary>An entity; properties not given read as "" (strings) or false (bools).</summary>
    public static PayloadValue Entity(FieldKind kind, IDictionary<string, object> properties)
    {
        if (!EntityProperties.IsEntity(kind))
            throw new ArgumentException($"{kind} is not an entity kind", nameof(kind));
        var props = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var name in EntityProperties.Of(kind))
        {
            properties.TryGetValue(name, out var given);
            props[name] = EntityProperties.BoolProperties.Contains(name) ? given is bool b && b : (object)(given as string ?? string.Empty);
        }
        return new PayloadValue(kind, null, props);
    }

    private static readonly IReadOnlyDictionary<string, object> Empty = new Dictionary<string, object>();

    public bool IsEntity => EntityProperties.IsEntity(Kind);

    public string StringProperty(string name) => Properties.TryGetValue(name, out var v) && v is string s ? s : string.Empty;
    public bool BoolProperty(string name) => Properties.TryGetValue(name, out var v) && v is bool b && b;

    /// <summary>Text for logs and the simulator: primitives as invariant text, entities as their id.</summary>
    public override string ToString() => IsEntity
        ? StringProperty(EntityProperties.Id)
        : Convert.ToString(Value, CultureInfo.InvariantCulture) ?? string.Empty;
}

/// <summary>The fields of one fired trigger, by name (M11 G2). Paths are "field" or "field.property".</summary>
public sealed class TriggerPayload
{
    private readonly Dictionary<string, PayloadValue> _fields;

    public TriggerPayload(IDictionary<string, PayloadValue>? fields = null) =>
        _fields = new Dictionary<string, PayloadValue>(fields ?? new Dictionary<string, PayloadValue>(), StringComparer.Ordinal);

    public IReadOnlyDictionary<string, PayloadValue> Fields => _fields;

    public static readonly TriggerPayload None = new TriggerPayload();

    /// <summary>
    /// The value at a path: a primitive field's value, an entity property's value (string/bool), or an entity field's id
    /// when the path names the entity itself. False when the field is missing or the property unknown.
    /// </summary>
    public bool TryResolve(string path, out object? value)
    {
        value = null;
        if (string.IsNullOrEmpty(path))
            return false;
        var dot = path.IndexOf('.');
        var field = dot < 0 ? path : path.Substring(0, dot);
        if (!_fields.TryGetValue(field, out var v))
            return false;
        if (dot < 0)
        {
            value = v.IsEntity ? v.StringProperty(EntityProperties.Id) : v.Value;
            return true;
        }
        if (!v.IsEntity)
            return false;
        return v.Properties.TryGetValue(path.Substring(dot + 1), out value);
    }
}
