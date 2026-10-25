using System.Collections.Generic;
using Newtonsoft.Json;

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
    /// <summary>Node ids the conversation starts with: player options (hub) or NPC alternatives (greeting).</summary>
    [JsonProperty("start")] public List<string>? Start { get; set; }
    [JsonProperty("nodes")] public List<ConversationNode>? Nodes { get; set; }
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
    [JsonProperty("effects")] public EffectSet? Effects { get; set; }

    [JsonIgnore] public string? FullId { get; set; }
}

/// <summary>Conditions; every field that is set must hold. Relation is the companion's relation with the player.</summary>
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
    /// <summary>Flags of this companion that must all be set.</summary>
    [JsonProperty("flags")] public List<string>? Flags { get; set; }
    /// <summary>Flags of this companion that must all be unset.</summary>
    [JsonProperty("notFlags")] public List<string>? NotFlags { get; set; }
}

/// <summary>What a node does when it is said.</summary>
public sealed class EffectSet
{
    /// <summary>Change of the companion's relation with the player.</summary>
    [JsonProperty("relation")] public int? Relation { get; set; }
    /// <summary>Player gold change: negative = the player pays the companion (needs that much gold), positive = the player receives.</summary>
    [JsonProperty("gold")] public int? Gold { get; set; }
    [JsonProperty("setFlags")] public List<string>? SetFlags { get; set; }
    [JsonProperty("clearFlags")] public List<string>? ClearFlags { get; set; }
    /// <summary>The companion joins the player's clan and party (no payment; offered only while not hired and the clan has room).</summary>
    [JsonProperty("join")] public bool Join { get; set; }
}
