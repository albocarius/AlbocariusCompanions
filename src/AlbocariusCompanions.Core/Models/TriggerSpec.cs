using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Core.Models;

/// <summary>
/// A conversation's or slot-free line's <c>trigger</c> (SPEC_TRIGGERS_DELIVERY T3): which trigger fires it, for which
/// subject, extra filters, and how it reaches the player. Defaults: <see cref="Rules.DeliveryRules"/>.
/// </summary>
public sealed class TriggerSpec
{
    /// <summary>The trigger name (docs/TRIGGERS.md).</summary>
    [JsonProperty("on")] public string? On { get; set; }
    /// <summary>self | player | playerParty | playerClan | companion:&lt;id&gt; | hero:&lt;id&gt; | settlement:&lt;id&gt; | any; absent = the trigger's default.</summary>
    [JsonProperty("subject")] public string? Subject { get; set; }
    /// <summary>Payload path → value that must be equal (e.g. "settlement.culture": "vlandia").</summary>
    [JsonProperty("where")] public Dictionary<string, JToken>? Where { get; set; }
    /// <summary>0..1, default 1.</summary>
    [JsonProperty("chance")] public double? Chance { get; set; }
    /// <summary>conversation | notification | inquiry; default conversation (conversations) / notification (lines).</summary>
    [JsonProperty("delivery")] public string? Delivery { get; set; }
    /// <summary>Queued and not delivered by then → dropped. Default 24 in-game hours.</summary>
    [JsonProperty("expiresAfterHours")] public double? ExpiresAfterHours { get; set; }
    /// <summary>Days before this content can fire again; default 0.</summary>
    [JsonProperty("cooldownDays")] public double? CooldownDays { get; set; }
    /// <summary>True: fires while the companion is not in the player's party.</summary>
    [JsonProperty("allowAbsent")] public bool AllowAbsent { get; set; }
    /// <summary>Polling triggers only: their parameters (nearSettlement: settlement, radius, rearmRadius; …).</summary>
    [JsonProperty("params")] public Dictionary<string, JToken>? Params { get; set; }
}
