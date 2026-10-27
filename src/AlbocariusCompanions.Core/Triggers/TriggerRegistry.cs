using System;
using System.Collections.Generic;
using System.Linq;

namespace AlbocariusCompanions.Core.Triggers;

/// <summary>
/// Every author-facing trigger (spec T1, M11). The list is <c>orchestra/triggers.tsv</c> (task 112); entries live in one
/// partial file per GAME_MAP question (TriggerRegistry.Q35.cs …) plus the polling triggers here. Validation, the simulator,
/// the CLI docs and the Game adapters read this class and nothing else.
/// </summary>
public static partial class TriggerRegistry
{
    private static readonly Lazy<IReadOnlyList<TriggerDefinition>> Entries = new Lazy<IReadOnlyList<TriggerDefinition>>(() =>
        Polling().Concat(Q35()).Concat(Q36()).Concat(Q37()).Concat(Q38()).Concat(Q39()).Concat(Q40()).Concat(Q41()).Concat(Q42())
            .ToList());

    private static readonly Lazy<Dictionary<string, TriggerDefinition>> ByName = new Lazy<Dictionary<string, TriggerDefinition>>(() =>
        Entries.Value.ToDictionary(t => t.Name, StringComparer.Ordinal));

    public static IReadOnlyList<TriggerDefinition> All => Entries.Value;

    public static bool TryGet(string? name, out TriggerDefinition trigger)
    {
        trigger = null!;
        return name != null && ByName.Value.TryGetValue(name, out trigger!);
    }

    // Short builders for the entry files.
    private static TriggerField F(string name, FieldKind kind, string description, string from) =>
        new TriggerField(name, kind, description, from);

    private static string[] S(params string[] subjectFields) => subjectFields;

    /// <summary>The standard decomposition of a <c>MapEvent</c> argument (battle, raid, siege assault; GAME_MAP Q23).</summary>
    private static TriggerField[] MapEventFields(string arg) => new[]
    {
        F("settlement", FieldKind.Settlement, "the settlement fought at or over; empty in the open field", arg + ".MapEventSettlement"),
        F("attacker", FieldKind.Party, "leading party of the attacking side", arg + ".AttackerSide.LeaderParty"),
        F("defender", FieldKind.Party, "leading party of the defending side", arg + ".DefenderSide.LeaderParty"),
        F("winner", FieldKind.Text, "attacker | defender | none", arg + ".WinningSide"),
        F("playerInvolved", FieldKind.Bool, "the player's party fought in it", arg + ".IsPlayerMapEvent"),
        F("playerWon", FieldKind.Bool, "the player's side won", arg + ".WinningSide == PartyBase.MainParty.Side"),
        F("battleType", FieldKind.Text, "the game's map event type: FieldBattle, Raid, Siege, SallyOut, …", arg + ".EventType"),
    };

    /// <summary>Polling triggers (spec T4): run on internal ticks, not on a CampaignEvents member.</summary>
    private static IEnumerable<TriggerDefinition> Polling() => new[]
    {
        new TriggerDefinition("nearSettlement", "polling",
            "The player's party came within `radius` of a settlement; fires again only after it went beyond `rearmRadius`.",
            "poll:nearSettlement",
            fields: new[] { F("settlement", FieldKind.Settlement, "the settlement approached", "params.settlement") },
            common: true,
            parameters: new[]
            {
                new TriggerParameter("settlement", FieldKind.Text, true, "settlement string id, e.g. town_V1"),
                new TriggerParameter("radius", FieldKind.Number, true, "fire distance in map units (> 0)"),
                new TriggerParameter("rearmRadius", FieldKind.Number, true, "re-arm distance in map units (> radius)"),
            }),
        new TriggerDefinition("daily", "polling", "Once per in-game day; combine with `chance` for random comments.", "poll:daily",
            common: true),
        new TriggerDefinition("varCrossed", "polling",
            "One of this companion's variables crossed `value` in `direction` (up: from below to at-or-above; down: from at-or-above to below).",
            "poll:varCrossed",
            fields: new[]
            {
                F("var", FieldKind.Text, "the variable name", "params.var"),
                F("value", FieldKind.Int, "the variable's new value", "variable store"),
            },
            common: true,
            parameters: new[]
            {
                new TriggerParameter("var", FieldKind.Text, true, "an int variable of this companion, e.g. mood"),
                new TriggerParameter("value", FieldKind.Int, true, "the threshold"),
                new TriggerParameter("direction", FieldKind.Text, true, "up | down"),
            }),
        new TriggerDefinition("roleStarted", "polling", "A companion took a role (governor, party leader, …).", "poll:roleStarted",
            fields: new[]
            {
                F("companion", FieldKind.Hero, "the companion", "polled hero"),
                F("role", FieldKind.Text, "governor | partyLeader | caravanLeader | alleyLeader | questAlternative", "params.role"),
            },
            subjectFields: S("companion"), defaultSubject: Subjects.Self, common: true,
            parameters: new[] { new TriggerParameter("role", FieldKind.Text, true, "governor | partyLeader | caravanLeader | alleyLeader | questAlternative") }),
        new TriggerDefinition("roleEnded", "polling", "A companion left a role.", "poll:roleEnded",
            fields: new[]
            {
                F("companion", FieldKind.Hero, "the companion", "polled hero"),
                F("role", FieldKind.Text, "governor | partyLeader | caravanLeader | alleyLeader | questAlternative", "params.role"),
            },
            subjectFields: S("companion"), defaultSubject: Subjects.Self, common: true,
            parameters: new[] { new TriggerParameter("role", FieldKind.Text, true, "governor | partyLeader | caravanLeader | alleyLeader | questAlternative") }),
    };

    /// <summary>The roles of <c>roleStarted</c> / <c>roleEnded</c> and the <c>role</c> condition (T4; GAME_MAP Q45, Q47).</summary>
    public static readonly IReadOnlyList<string> Roles = new[] { "governor", "partyLeader", "caravanLeader", "alleyLeader", "questAlternative" };
}
