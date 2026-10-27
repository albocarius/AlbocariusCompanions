using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Core.Simulation;

/// <summary>
/// Everything conditions and effects read or write, as plain data (docs/SIMULATOR_PROTOCOL.md "State"). Missing fields
/// take the documented defaults: a companion not listed is unmet, unhired, not in the party, relation 0, gold 0; a
/// variable not listed has its declared default; nothing has started.
/// </summary>
public sealed class SimState
{
    /// <summary>Companion id → its hero's state.</summary>
    [JsonProperty("companions")] public Dictionary<string, SimCompanion> Companions { get; set; } = new Dictionary<string, SimCompanion>();
    [JsonProperty("player")] public SimPlayer Player { get; set; } = new SimPlayer();
    /// <summary>The player's clan is at its companion limit (hiring and "join" are refused).</summary>
    [JsonProperty("clanIsFull")] public bool ClanIsFull { get; set; }
    /// <summary>"&lt;companionId&gt;.&lt;name&gt;" → true/false or an integer.</summary>
    [JsonProperty("variables")] public Dictionary<string, JToken> Variables { get; set; } = new Dictionary<string, JToken>();
    /// <summary>"&lt;companionId&gt;:&lt;conversationId&gt;" of every conversation that has started (for "once").</summary>
    [JsonProperty("started")] public List<string> Started { get; set; } = new List<string>();

    public SimState Clone() => JToken.FromObject(this).ToObject<SimState>()!;
}

public sealed class SimCompanion
{
    [JsonProperty("met")] public bool Met { get; set; }
    /// <summary>In the player's clan.</summary>
    [JsonProperty("hired")] public bool Hired { get; set; }
    /// <summary>In the player's party.</summary>
    [JsonProperty("inParty")] public bool InParty { get; set; }
    /// <summary>Relation with the player, −100..100.</summary>
    [JsonProperty("relation")] public int Relation { get; set; }
    /// <summary>The hero's own gold.</summary>
    [JsonProperty("gold")] public int Gold { get; set; }
}

public sealed class SimPlayer
{
    [JsonProperty("gold")] public int Gold { get; set; }
    /// <summary>Item string id → count in the party inventory.</summary>
    [JsonProperty("items")] public Dictionary<string, int> Items { get; set; } = new Dictionary<string, int>();
}
