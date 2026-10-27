using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AlbocariusCompanions.Core.Loading;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;
using AlbocariusCompanions.Core.Triggers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Cli;

/// <summary>
/// <c>export schema</c> (SPEC_EDITOR §3): every file type, object, field, type, default and allowed value, plus the
/// catalogue of conditions and effects, as JSON for the editor. Field names and types come from the Core model classes
/// (their <see cref="JsonPropertyAttribute"/>s, the same classes the loader reads); allowed values and limits come from the
/// Core rule classes. Only the descriptions, required flags and defaults are written here, in the words of docs/SCHEMA.md.
/// A test fails when a model property has no entry here or an entry names no model property.
/// </summary>
public static class SchemaExport
{
    /// <summary>Bumped when the shape of this export changes incompatibly.</summary>
    public const int ExportVersion = 1;

    /// <summary>Export object name → the Core model class it describes.</summary>
    public static readonly IReadOnlyDictionary<string, Type> Objects = new Dictionary<string, Type>
    {
        ["companion"] = typeof(CompanionDefinition),
        ["equipment"] = typeof(EquipmentDefinition),
        ["voiceProfile"] = typeof(VoiceProfileDefinition),
        ["reaction"] = typeof(ReactionDefinition),
        ["relation"] = typeof(RelationDefinition),
        ["dialogs"] = typeof(DialogFile),
        ["line"] = typeof(DialogLine),
        ["variable"] = typeof(VariableDeclaration),
        ["conversation"] = typeof(ConversationDefinition),
        ["node"] = typeof(ConversationNode),
        ["conditions"] = typeof(ConditionSet),
        ["goldCheck"] = typeof(GoldCheck),
        ["itemCheck"] = typeof(ItemCheck),
        ["effects"] = typeof(EffectSet),
        ["transfer"] = typeof(TransferEffect),
        ["trigger"] = typeof(TriggerSpec),
        ["lang"] = typeof(LangFile),
    };

    /// <summary>One field's hand-written part: required, description, default, allowed values, range.</summary>
    public sealed class Meta
    {
        public Meta(bool required, string description, JToken? @default = null, IEnumerable<string>? allowed = null,
            double? min = null, double? max = null, string? pattern = null)
        {
            Required = required;
            Description = description;
            Default = @default;
            Allowed = allowed?.ToList();
            Min = min;
            Max = max;
            Pattern = pattern;
        }

        public bool Required { get; }
        public string Description { get; }
        public JToken? Default { get; }
        public IReadOnlyList<string>? Allowed { get; }
        public double? Min { get; }
        public double? Max { get; }
        public string? Pattern { get; }
    }

    private static Meta Req(string description, IEnumerable<string>? allowed = null, double? min = null, double? max = null, string? pattern = null) =>
        new Meta(true, description, null, allowed, min, max, pattern);

    private static Meta Opt(string description, JToken? @default = null, IEnumerable<string>? allowed = null, double? min = null,
        double? max = null, string? pattern = null) =>
        new Meta(false, description, @default, allowed, min, max, pattern);

    private static string Id => LineIds.LocalIdPattern.ToString();

    private static readonly string[] Actors = { "player", "self", "world", "<companionId>" };

    /// <summary>Object name → JSON field name → its hand-written part.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, Meta>> Fields =
        new Dictionary<string, IReadOnlyDictionary<string, Meta>>
        {
            ["companion"] = new Dictionary<string, Meta>
            {
                ["schemaVersion"] = Req("schema version of the file", min: SchemaVersions.Current, max: SchemaVersions.Current),
                ["name"] = Req("display name, non-empty"),
                ["isFemale"] = Req("the hero is female"),
                ["voice"] = Req("vanilla voice type", CompanionRules.VoiceTypes),
                ["age"] = Req("age in years", min: CompanionRules.AgeMin, max: CompanionRules.AgeMax),
                ["culture"] = Req("game culture id"),
                ["bodyProperties"] = Opt("BodyProperties key; null = template default", JValue.CreateNull()),
                ["attributes"] = Req("all six attributes, each " + CompanionRules.AttributeMin + ".." + CompanionRules.AttributeMax,
                    CompanionRules.AttributeNames, CompanionRules.AttributeMin, CompanionRules.AttributeMax),
                ["skills"] = Req("game skill id → value", min: CompanionRules.SkillMin, max: CompanionRules.SkillMax),
                ["traits"] = Opt("game trait id → level"),
                ["equipment"] = Req("equipment template and fixed sets"),
                ["hireCost"] = Req("gold the player pays to hire; hireable with exactly this much", min: 0),
                ["noble"] = Opt("noble birth: making a non-noble companion a lord costs relation with the realm's clan leaders", false),
                ["voiceProfile"] = Opt("authoring notes, unused in game"),
                ["likes"] = Opt("shape-validated only, no effect yet", new JArray()),
                ["dislikes"] = Opt("same as likes", new JArray()),
                ["relations"] = Opt("shape only: other companions as friend or rival", new JArray()),
                ["requiredModules"] = Opt("module ids from SubModule.xml <Id>; the companion is skipped when one is not loaded", new JArray()),
            },
            ["equipment"] = new Dictionary<string, Meta>
            {
                ["templateId"] = Req("game equipment roster id"),
                ["battle"] = Opt("slot → item id", allowed: CompanionRules.EquipmentSlots),
                ["civilian"] = Opt("slot → item id", allowed: CompanionRules.EquipmentSlots),
            },
            ["voiceProfile"] = new Dictionary<string, Meta>
            {
                ["reference"] = Opt("reference voice (note)", JValue.CreateNull()),
                ["style"] = Opt("voice style (note)", JValue.CreateNull()),
            },
            ["reaction"] = new Dictionary<string, Meta>
            {
                ["event"] = Req("event name"),
                ["reaction"] = Req("reaction strength"),
                ["slot"] = Req("dialog slot said as the reaction", DialogSlots.All),
            },
            ["relation"] = new Dictionary<string, Meta>
            {
                ["companion"] = Req("another companion's id", pattern: CompanionRules.IdPattern.ToString()),
                ["attitude"] = Req("attitude towards it", CompanionRules.RelationAttitudes),
            },
            ["dialogs"] = new Dictionary<string, Meta>
            {
                ["schemaVersion"] = Req("schema version of the file", min: SchemaVersions.Current, max: SchemaVersions.Current),
                ["lines"] = Req("slot lines and triggered lines"),
                ["conversations"] = Opt("authored conversations", new JArray()),
                ["variables"] = Opt("declared conversation variables by name; 'mood' is built in", new JObject()),
            },
            ["line"] = new Dictionary<string, Meta>
            {
                ["id"] = Req("local id, unique within the companion (lines and nodes share one namespace)", pattern: Id),
                ["slot"] = Opt("dialog slot; required unless the line has a trigger", allowed: DialogSlots.All),
                ["trigger"] = Opt("a triggered line: no slot, delivered as a notification"),
                ["when"] = Opt("the line is a candidate only while these conditions hold"),
                ["priority"] = Opt("selection rule: the highest passing priority wins", 0),
                ["voiced"] = Opt("the line has recorded audio; never on player slots", false),
                ["audio"] = Opt("audio path relative to the companion folder, ends in .ogg; default audio/<id>.ogg"),
            },
            ["variable"] = new Dictionary<string, Meta>
            {
                ["type"] = Req("value type", VariableRules.Types),
                ["scope"] = Opt("local: only this companion; shared: others use it as <companionId>.<name>", VariableRules.ScopeLocal,
                    VariableRules.Scopes),
                ["default"] = Opt("initial value of the type (false / 0 if absent)"),
            },
            ["conversation"] = new Dictionary<string, Meta>
            {
                ["id"] = Req("unique among the companion's conversations", pattern: Id),
                ["entry"] = Opt("hub: start player nodes are menu options; greeting: a start npc node opens the talk. Absent for a triggered conversation",
                    allowed: ConversationRules.Entries),
                ["once"] = Opt("offered until it has started once per campaign", false),
                ["when"] = Opt("conditions of the conversation"),
                ["priority"] = Opt("hub: menu order (higher first); greeting: the highest offered greeting opens the talk", 0),
                ["start"] = Req("start node ids: player nodes (hub) or npc nodes (greeting, triggered)"),
                ["trigger"] = Opt("started by a game event instead of the menu; then no entry"),
                ["nodes"] = Req("the conversation's nodes, at least one"),
            },
            ["node"] = new Dictionary<string, Meta>
            {
                ["id"] = Req("local id (same namespace as lines; 'hub' and 'end' are reserved); key of the text in the lang files", pattern: Id),
                ["speaker"] = Req("who says it", ConversationRules.Speakers),
                ["voiced"] = Opt("the line has recorded audio; never on player nodes", false),
                ["audio"] = Opt("audio path relative to the companion folder, ends in .ogg; default audio/<id>.ogg"),
                ["next"] = Req("node ids of this conversation, or exactly [\"hub\"] / [\"end\"]"),
                ["when"] = Opt("a node whose condition fails is not offered / not said"),
                ["priority"] = Opt("npc nodes only: selection rule among the replies of one next list", 0),
                ["effects"] = Opt("applied when the node is said"),
            },
            ["conditions"] = new Dictionary<string, Meta>
            {
                ["met"] = Opt("the companion has met the player"),
                ["hired"] = Opt("the companion is in the player's clan"),
                ["inParty"] = Opt("the companion is in the player's party"),
                ["minRelation"] = Opt("relation with the player at least this", min: ConversationRules.MinRelation, max: ConversationRules.MaxRelation),
                ["maxRelation"] = Opt("relation with the player at most this", min: ConversationRules.MinRelation, max: ConversationRules.MaxRelation),
                ["minGold"] = Opt("the player's gold at least this", min: 0),
                ["hasGold"] = Opt("an actor's own gold at least min"),
                ["hasItem"] = Opt("item count in the player's party inventory at least min"),
                ["flags"] = Opt("bool variables that must all be true"),
                ["notFlags"] = Opt("bool variables that must all be false"),
                ["all"] = Opt("every nested condition object holds (not empty)"),
                ["any"] = Opt("at least one nested condition object holds (not empty)"),
                ["not"] = Opt("the nested condition object does not hold"),
                ["var"] = Opt("variable compared by min/max/eq: <name>, <companionId>.<name> or event.<path>"),
                ["min"] = Opt("with var: int variable at least this"),
                ["max"] = Opt("with var: int variable at most this"),
                ["eq"] = Opt("with var: equals (bool, int, or text/bool/number for event fields); not combined with min/max"),
                ["role"] = Opt("the companion's current role", allowed: TriggerRegistry.Roles),
            },
            ["goldCheck"] = new Dictionary<string, Meta>
            {
                ["who"] = Req("whose gold; never world", new[] { "player", "self", "<companionId>" }),
                ["min"] = Req("the least gold", min: 1),
            },
            ["itemCheck"] = new Dictionary<string, Meta>
            {
                ["who"] = Req("only player: items live in the party inventory", new[] { "player" }),
                ["id"] = Req("item string id"),
                ["min"] = Opt("the least count", 1, min: 1),
            },
            ["effects"] = new Dictionary<string, Meta>
            {
                ["relation"] = Opt("non-zero change of the companion's relation with the player", min: ConversationRules.MinRelation,
                    max: ConversationRules.MaxRelation),
                ["transfer"] = Opt("gold or items moved between two actors"),
                ["setFlags"] = Opt("bool variables set to true"),
                ["clearFlags"] = Opt("bool variables set to false"),
                ["set"] = Opt("variable → new value of its type"),
                ["add"] = Opt("int variable → non-zero amount added"),
                ["join"] = Opt("the companion joins the player's clan and party without payment", false),
                ["partyMorale"] = Opt("non-zero one-off change of the player's party morale", min: DeliveryRules.PartyMoraleMin,
                    max: DeliveryRules.PartyMoraleMax),
            },
            ["transfer"] = new Dictionary<string, Meta>
            {
                ["gold"] = Opt("positive gold amount; exactly one of gold / item", min: 1),
                ["item"] = Opt("item string id; exactly one of gold / item; only between player and world"),
                ["count"] = Opt("item count, only with item", 1, min: 1),
                ["from"] = Req("who gives", Actors),
                ["to"] = Req("who receives; differs from from", Actors),
            },
            ["trigger"] = new Dictionary<string, Meta>
            {
                ["on"] = Req("trigger name (export triggers)"),
                ["subject"] = Opt("who/where the event must be about; default: the trigger's", allowed: SubjectForms()),
                ["where"] = Opt("payload path → value that must be equal, e.g. \"village.culture\": \"vlandia\""),
                ["chance"] = Opt("probability of firing", DeliveryRules.DefaultChance, min: 0, max: 1),
                ["delivery"] = Opt("how it reaches the player; conversation (conversations) / notification (lines, the only choice)",
                    allowed: DeliveryRules.Deliveries),
                ["expiresAfterHours"] = Opt("queued and not delivered by then → dropped", DeliveryRules.DefaultExpiresAfterHours, min: 0),
                ["cooldownDays"] = Opt("in-game days before this item can fire again", DeliveryRules.DefaultCooldownDays, min: 0),
                ["allowAbsent"] = Opt("fires while the companion is not in the player's party", false),
                ["params"] = Opt("polling triggers only: their parameters (export triggers)"),
            },
            ["lang"] = new Dictionary<string, Meta>
            {
                ["schemaVersion"] = Req("schema version of the file", min: SchemaVersions.Current, max: SchemaVersions.Current),
                ["lines"] = Req("local id → text: every line and conversation node (en: required for all)"),
            },
        };

    private static IEnumerable<string> SubjectForms() =>
        new[] { Subjects.Self, Subjects.Player, Subjects.PlayerParty, Subjects.PlayerClan, Subjects.Companion + ":<id>",
            Subjects.Hero + ":<stringId>", Subjects.Settlement + ":<stringId>", Subjects.Any };

    /// <summary>A condition of the catalogue: the JSON keys it uses in a condition object and its parameters.</summary>
    private sealed class Entry
    {
        public Entry(string name, string description, params string[] keys)
        {
            Name = name;
            Description = description;
            Keys = keys;
        }

        public string Name { get; }
        public string Description { get; }
        public IReadOnlyList<string> Keys { get; }
    }

    private static readonly Entry[] ConditionCatalogue =
    {
        new Entry("met", "The companion has met the player (true/false).", "met"),
        new Entry("hired", "The companion is in the player's clan (true/false).", "hired"),
        new Entry("inParty", "The companion is in the player's party (true/false).", "inParty"),
        new Entry("relation", "The companion's relation with the player is within minRelation..maxRelation (either may be absent).", "minRelation", "maxRelation"),
        new Entry("minGold", "The player has at least this much gold.", "minGold"),
        new Entry("hasGold", "An actor (player, self or another companion) holds at least min of its own gold.", "hasGold"),
        new Entry("hasItem", "The player's party inventory holds at least min of an item.", "hasItem"),
        new Entry("flags", "Bool variables that must all be true.", "flags"),
        new Entry("notFlags", "Bool variables that must all be false.", "notFlags"),
        new Entry("var", "A variable (or event.<path>) compared with eq, or with min / max.", "var", "min", "max", "eq"),
        new Entry("role", "The companion's current role.", "role"),
        new Entry("all", "Every nested condition object holds.", "all"),
        new Entry("any", "At least one nested condition object holds.", "any"),
        new Entry("not", "The nested condition object does not hold.", "not"),
    };

    private static readonly Entry[] EffectCatalogue =
    {
        new Entry("relation", "Changes the companion's relation with the player (the game scales positive changes).", "relation"),
        new Entry("transfer", "Moves gold or items between two actors; the node is offered only when the source has them.", "transfer"),
        new Entry("join", "The companion joins the player's clan and party without payment.", "join"),
        new Entry("partyMorale", "One-off change of the player's party morale (Recent Events, fades 10 % a day).", "partyMorale"),
        new Entry("setFlags", "Sets bool variables to true.", "setFlags"),
        new Entry("clearFlags", "Sets bool variables to false.", "clearFlags"),
        new Entry("set", "Sets variables to a value of their type.", "set"),
        new Entry("add", "Adds a non-zero amount to int variables.", "add"),
    };

    /// <summary>The order effects are applied in (docs/SCHEMA.md "Effects").</summary>
    private static readonly string[] EffectOrder = { "relation", "transfer", "join", "partyMorale", "setFlags", "clearFlags", "set", "add" };

    /// <summary>Catalogue names → keys, for the sync test.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ConditionKeys =>
        ConditionCatalogue.ToDictionary(e => e.Name, e => e.Keys);

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> EffectKeys =>
        EffectCatalogue.ToDictionary(e => e.Name, e => e.Keys);

    /// <summary>JSON property name → CLR property of a model class, in declaration order.</summary>
    public static IReadOnlyList<(string Name, PropertyInfo Property)> ModelProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (Attr: p.GetCustomAttribute<JsonPropertyAttribute>(), Property: p))
            .Where(x => x.Attr != null && x.Property.GetCustomAttribute<JsonIgnoreAttribute>() == null)
            .Select(x => (x.Attr!.PropertyName ?? x.Property.Name, x.Property))
            .ToList();

    /// <summary>The export's type word for a model property type.</summary>
    public static string TypeName(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t == typeof(string)) return "string";
        if (t == typeof(int) || t == typeof(long)) return "int";
        if (t == typeof(double) || t == typeof(float)) return "number";
        if (t == typeof(bool)) return "bool";
        if (typeof(JToken).IsAssignableFrom(t)) return "any";
        var name = Objects.FirstOrDefault(o => o.Value == t).Key;
        if (name != null) return name;
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            return TypeName(t.GetGenericArguments()[0]) + "[]";
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            return "map<" + TypeName(t.GetGenericArguments()[1]) + ">";
        throw new InvalidOperationException($"No export type for {t}.");
    }

    public static JObject Build()
    {
        var objects = new JObject();
        foreach (var name in Objects.Keys)
            objects[name] = ObjectFields(name);

        return new JObject
        {
            ["exportVersion"] = ExportVersion,
            ["schemaVersion"] = SchemaVersions.Current,
            ["files"] = new JObject
            {
                ["companion.json"] = "companion",
                ["dialogs.json"] = "dialogs",
                ["lang/<code>.json"] = "lang",
            },
            ["objects"] = objects,
            ["conditions"] = Catalogue(ConditionCatalogue, "conditions"),
            ["effects"] = Catalogue(EffectCatalogue, "effects"),
            ["effectOrder"] = new JArray(EffectOrder),
            ["slots"] = new JObject
            {
                ["companion"] = new JArray(DialogSlots.RequiredCompanion),
                ["player"] = new JArray(PlayerSlots.All),
                ["vanilla"] = new JArray(VanillaDialogPoints.All.Select(p => new JObject
                {
                    ["slot"] = p.Slot,
                    ["englishText"] = p.EnglishText,
                    ["sentenceIds"] = new JArray(p.SentenceIds),
                })),
                ["required"] = new JArray(DialogSlots.Required),
                ["optional"] = new JArray(DialogSlots.Optional),
            },
            ["enums"] = new JObject
            {
                ["voice"] = new JArray(CompanionRules.VoiceTypes),
                ["attributes"] = new JArray(CompanionRules.AttributeNames),
                ["equipmentSlots"] = new JArray(CompanionRules.EquipmentSlots),
                ["relationAttitudes"] = new JArray(CompanionRules.RelationAttitudes),
                ["entries"] = new JArray(ConversationRules.Entries),
                ["speakers"] = new JArray(ConversationRules.Speakers),
                ["reservedNext"] = new JArray(ConversationRules.NextHub, ConversationRules.NextEnd),
                ["variableTypes"] = new JArray(VariableRules.Types),
                ["variableScopes"] = new JArray(VariableRules.Scopes),
                ["builtinVariables"] = new JArray(new JObject { ["name"] = VariableRules.Mood, ["type"] = VariableRules.TypeInt, ["default"] = 0 }),
                ["roles"] = new JArray(TriggerRegistry.Roles),
                ["deliveries"] = new JArray(DeliveryRules.Deliveries),
                ["actors"] = new JArray(Actors),
                ["subjects"] = new JArray(SubjectForms()),
            },
            ["patterns"] = new JObject
            {
                ["companionId"] = CompanionRules.IdPattern.ToString(),
                ["localId"] = LineIds.LocalIdPattern.ToString(),
                ["variableName"] = VariableRules.NamePattern.ToString(),
                ["languageCode"] = CompanionRules.LanguageCodePattern.ToString(),
                ["playerToken"] = CompanionRules.PlayerTokenPattern.ToString(),
            },
            ["rules"] = new JObject
            {
                ["englishCode"] = CompanionRules.EnglishCode,
                ["fullId"] = "<companionId>_<localId>",
                ["defaultAudio"] = LineIds.DefaultAudioPath("<localId>"),
                ["relationRange"] = new JArray(ConversationRules.MinRelation, ConversationRules.MaxRelation),
                ["partyMoraleRange"] = new JArray(DeliveryRules.PartyMoraleMin, DeliveryRules.PartyMoraleMax),
                ["reservedFolder"] = "_shared",
            },
        };
    }

    /// <summary>Each catalogue entry with its parameters: the fields of the condition/effect object it uses.</summary>
    private static JArray Catalogue(IEnumerable<Entry> entries, string objectName)
    {
        var fields = (JArray)ObjectFields(objectName)["fields"]!;
        var byName = fields.Cast<JObject>().ToDictionary(f => (string)f["name"]!);
        return new JArray(entries.Select(e => new JObject
        {
            ["name"] = e.Name,
            ["description"] = e.Description,
            ["params"] = new JArray(e.Keys.Select(k => byName[k].DeepClone())),
        }));
    }

    /// <summary>A whole number as an integer token (1, not 1.0).</summary>
    private static JToken Number(double value) => value == Math.Floor(value) ? new JValue((long)value) : new JValue(value);

    /// <summary>One object's fields: name and type from the model class, the rest from <see cref="Fields"/>.</summary>
    private static JObject ObjectFields(string objectName)
    {
        var fields = new JArray();
        foreach (var (name, property) in ModelProperties(Objects[objectName]))
        {
            var m = Fields[objectName][name];
            var field = new JObject
            {
                ["name"] = name,
                ["type"] = TypeName(property.PropertyType),
                ["required"] = m.Required,
                ["description"] = m.Description,
            };
            if (m.Default != null) field["default"] = m.Default.DeepClone();
            if (m.Allowed != null) field["allowed"] = new JArray(m.Allowed);
            if (m.Min != null) field["min"] = Number(m.Min.Value);
            if (m.Max != null) field["max"] = Number(m.Max.Value);
            if (m.Pattern != null) field["pattern"] = m.Pattern;
            fields.Add(field);
        }
        return new JObject { ["fields"] = fields };
    }
}
