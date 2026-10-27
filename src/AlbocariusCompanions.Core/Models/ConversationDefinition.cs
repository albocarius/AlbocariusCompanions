using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Core.Models;

/// <summary>
/// One authored conversation in dialogs.json "conversations": a graph of NPC and player nodes. Entry "hub" adds the start
/// player node(s) to the companion's conversation menu; entry "greeting" makes the companion open the conversation with a
/// start NPC node when the player talks to them.
/// </summary>
public sealed class ConversationDefinition
{
    [JsonProperty("id")] public string? Id { get; set; }
    [JsonProperty("entry")] public string? Entry { get; set; }
    /// <summary>True: offered until it has started once (per campaign).</summary>
    [JsonProperty("once")] public bool Once { get; set; }
    [JsonProperty("when")] public ConditionSet? When { get; set; }
    /// <summary>
    /// Hub: menu order (higher first, ties in file order). Greeting: the offered greeting with the highest priority
    /// opens the talk, ties at random. Absent = 0.
    /// </summary>
    [JsonProperty("priority")] public int? Priority { get; set; }
    /// <summary>Node ids the conversation starts with: player options (hub) or NPC alternatives (greeting).</summary>
    [JsonProperty("start")] public List<string>? Start { get; set; }
    [JsonProperty("nodes")] public List<ConversationNode>? Nodes { get; set; }
    /// <summary>Started by a game event instead of the menu (M11): then no <see cref="Entry"/>, and <see cref="Start"/> holds npc nodes.</summary>
    [JsonProperty("trigger")] public TriggerSpec? Trigger { get; set; }
}

public sealed class ConversationNode : ISpokenLine
{
    /// <summary>Local id, unique within the companion (lines and every conversation's nodes); key in the lang files.</summary>
    [JsonProperty("id")] public string? Id { get; set; }
    [JsonProperty("speaker")] public string? Speaker { get; set; }
    [JsonProperty("voiced")] public bool Voiced { get; set; }
    [JsonProperty("audio")] public string? Audio { get; set; }
    /// <summary>What follows: node ids of this conversation, or exactly one of "hub" / "end".</summary>
    [JsonProperty("next")] public List<string>? Next { get; set; }
    [JsonProperty("when")] public ConditionSet? When { get; set; }
    /// <summary>NPC nodes only: among the passing npc nodes of one "next" list (or greeting start), the highest wins, ties at random. Absent = 0.</summary>
    [JsonProperty("priority")] public int? Priority { get; set; }
    [JsonProperty("effects")] public EffectSet? Effects { get; set; }

    [JsonIgnore] public string? FullId { get; set; }
}

/// <summary>
/// Conditions; every field that is set must hold (an implicit "all"). Relation is the companion's relation with the player.
/// Variables are named "&lt;name&gt;" (this companion's) or "&lt;companionId&gt;.&lt;name&gt;" (another companion's shared one).
/// </summary>
public sealed class ConditionSet
{
    [JsonProperty("met")] public bool? Met { get; set; }
    /// <summary>The companion is in the player's clan.</summary>
    [JsonProperty("hired")] public bool? Hired { get; set; }
    /// <summary>The companion is in the player's party.</summary>
    [JsonProperty("inParty")] public bool? InParty { get; set; }
    [JsonProperty("minRelation")] public int? MinRelation { get; set; }
    [JsonProperty("maxRelation")] public int? MaxRelation { get; set; }
    /// <summary>The player has at least this much gold.</summary>
    [JsonProperty("minGold")] public int? MinGold { get; set; }
    /// <summary>An actor holds at least <see cref="GoldCheck.Min"/> of its own gold.</summary>
    [JsonProperty("hasGold")] public GoldCheck? HasGold { get; set; }
    /// <summary>The player's party inventory holds at least <see cref="ItemCheck.Min"/> of an item.</summary>
    [JsonProperty("hasItem")] public ItemCheck? HasItem { get; set; }
    /// <summary>Bool variables that must all be true.</summary>
    [JsonProperty("flags")] public List<string>? Flags { get; set; }
    /// <summary>Bool variables that must all be false.</summary>
    [JsonProperty("notFlags")] public List<string>? NotFlags { get; set; }
    /// <summary>Nested condition objects that must all hold.</summary>
    [JsonProperty("all")] public List<ConditionSet>? All { get; set; }
    /// <summary>Nested condition objects of which at least one must hold.</summary>
    [JsonProperty("any")] public List<ConditionSet>? Any { get; set; }
    /// <summary>A nested condition object that must not hold.</summary>
    [JsonProperty("not")] public ConditionSet? Not { get; set; }
    /// <summary>The variable compared by <see cref="Min"/>, <see cref="Max"/> or <see cref="Eq"/>.</summary>
    [JsonProperty("var")] public string? Var { get; set; }
    /// <summary>Int variable at least this.</summary>
    [JsonProperty("min")] public int? Min { get; set; }
    /// <summary>Int variable at most this.</summary>
    [JsonProperty("max")] public int? Max { get; set; }
    /// <summary>Variable equals this: a JSON bool for a bool variable, an integer for an int one.</summary>
    [JsonProperty("eq")] public JToken? Eq { get; set; }
}

/// <summary>A <c>hasGold</c> condition: one actor's own gold (actors and the game's gold rules: GAME_MAP Q29).</summary>
public sealed class GoldCheck
{
    /// <summary>Whose gold: "player", "self" (the companion being talked to) or another companion's id; never "world".</summary>
    [JsonProperty("who")] public string? Who { get; set; }
    /// <summary>The least gold (required, at least 1).</summary>
    [JsonProperty("min")] public int? Min { get; set; }
}

/// <summary>A <c>hasItem</c> condition: an item count in the player's party inventory (GAME_MAP Q29).</summary>
public sealed class ItemCheck
{
    /// <summary>Only "player": companions have no inventory of their own (the ones in the party share the party's).</summary>
    [JsonProperty("who")] public string? Who { get; set; }
    /// <summary>The item string id.</summary>
    [JsonProperty("id")] public string? Id { get; set; }
    /// <summary>The least items; default 1.</summary>
    [JsonProperty("min")] public int? Min { get; set; }
}

/// <summary>What a node does when it is said.</summary>
public sealed class EffectSet
{
    /// <summary>Change of the companion's relation with the player.</summary>
    [JsonProperty("relation")] public int? Relation { get; set; }
    /// <summary>A transfer of gold or items between two actors (I3).</summary>
    [JsonProperty("transfer")] public TransferEffect? Transfer { get; set; }
    /// <summary>Bool variables set to true (sugar for "set": { name: true }).</summary>
    [JsonProperty("setFlags")] public List<string>? SetFlags { get; set; }
    /// <summary>Bool variables set to false (sugar for "set": { name: false }).</summary>
    [JsonProperty("clearFlags")] public List<string>? ClearFlags { get; set; }
    /// <summary>Variable → new value: a JSON bool for a bool variable, an integer for an int one.</summary>
    [JsonProperty("set")] public Dictionary<string, JToken>? Set { get; set; }
    /// <summary>Int variable → amount added (may be negative).</summary>
    [JsonProperty("add")] public Dictionary<string, int>? Add { get; set; }
    /// <summary>The companion joins the player's clan and party (no payment; offered only while not hired and the clan has room).</summary>
    [JsonProperty("join")] public bool Join { get; set; }
}

/// <summary>
/// A transfer of gold or items from one actor to another. Actors: "player", "self" (the companion being talked to),
/// "world" (source/sink: gold or items appear / disappear) or another companion's id (the companion's own id means the
/// same actor as "self"). Exactly one of <see cref="Gold"/> / <see cref="Item"/>; gold moves each hero's own gold,
/// items only move between "player" and "world" (the party inventory, GAME_MAP Q29).
/// </summary>
public sealed class TransferEffect
{
    /// <summary>The gold amount (positive).</summary>
    [JsonProperty("gold")] public int? Gold { get; set; }
    /// <summary>The item string id.</summary>
    [JsonProperty("item")] public string? Item { get; set; }
    /// <summary>How many items (at least 1); only with "item", default 1.</summary>
    [JsonProperty("count")] public int? Count { get; set; }
    /// <summary>Who gives.</summary>
    [JsonProperty("from")] public string? From { get; set; }
    /// <summary>Who receives.</summary>
    [JsonProperty("to")] public string? To { get; set; }
}
