using AlbocariusCompanions.Acceptance.Core;
using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Triggers;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Triggers;

// Task 119 (M11 T4, G10): polling triggers.
public class TriggerPollingTests
{
    sealed class Ctx : IConversationContext, ITriggerContext
    {
        public TriggerPayload? Event { get; set; }
        public string? Role => null;
        public bool HasMet => true;
        public bool IsHired => true;
        public bool IsInPlayerParty => true;
        public int Relation => 0;
        public int PlayerGold => 0;
        public bool ClanIsFull => false;
        public int? GetVariable(string reference) => 0;
        public int? GoldOf(string actor) => 0;
        public int PlayerItemCount(string itemId) => 0;
    }

    sealed class Zero : IRandomSource { public int Next(int maxExclusive) => 0; }

    static IConversationContext? Present(string companion, TriggerPayload payload) => new Ctx { Event = payload };

    static (TriggerEngine, TriggerPolling) Setup(params string[] lines)
    {
        var d = Fixtures.Dialogs("ana");
        foreach (var l in lines) Fixtures.Lines(d).Add(JObject.Parse(l));
        var engine = new TriggerEngine(new[] { Fixtures.Package("ana", Fixtures.Companion("ana"), d) }, new Zero());
        return (engine, new TriggerPolling(engine));
    }

    static PayloadValue Town(string id) =>
        PayloadValue.Entity(FieldKind.Settlement, new Dictionary<string, object> { ["id"] = id, ["culture"] = "vlandia" });

    const string Near = """{ "id": "near_1", "trigger": { "on": "nearSettlement", "params": { "settlement": "town_V1", "radius": 5, "rearmRadius": 8 } } }""";

    [Fact]
    public void Near_settlement_fires_once_per_approach()
    {
        var (engine, polling) = Setup(Near);
        double distance = 20;
        List<FireCandidate> Poll(double d, double hour) { distance = d; return polling.NearSettlements(hour, _ => distance, Town, Present); }

        Assert.Empty(Poll(20, 0));               // first look: outside → armed
        Assert.Empty(Poll(10, 1));
        var fired = Assert.Single(Poll(4, 2));   // inside → fires
        Assert.Equal("queued", fired.Outcome);
        Assert.True(engine.Queue.Single().Payload.TryResolve("settlement.culture", out var culture));
        Assert.Equal("vlandia", culture);
        Assert.Empty(Poll(3, 3));                // still inside
        Assert.Empty(Poll(7, 4));                // between radius and rearmRadius: not re-armed
        Assert.Empty(Poll(4, 5));
        Assert.Empty(Poll(9, 6));                // beyond rearmRadius → armed again
        Assert.Single(Poll(5, 7));               // back in → fires again (radius inclusive)
    }

    [Fact]
    public void Starting_inside_the_radius_does_not_fire()
    {
        var (_, polling) = Setup(Near);
        Assert.Empty(polling.NearSettlements(0, _ => 2, Town, Present));
        Assert.Empty(polling.NearSettlements(1, _ => 2, Town, Present));
        Assert.Empty(polling.NearSettlements(2, _ => 9, Town, Present));
        Assert.Single(polling.NearSettlements(3, _ => 2, Town, Present));
    }

    [Fact]
    public void Unknown_settlement_is_skipped()
    {
        var (_, polling) = Setup(Near);
        Assert.Empty(polling.NearSettlements(0, _ => null, Town, Present));
    }

    [Fact]
    public void Daily_fires_once_per_day()
    {
        var (engine, polling) = Setup("""{ "id": "day_1", "trigger": { "on": "daily", "cooldownDays": 0 } }""");
        Assert.Single(polling.Daily(1, Present));
        Assert.Empty(polling.Daily(20, Present));
        Assert.Single(polling.Daily(25, Present)); // already queued → candidate reported, not queued twice
        Assert.Single(engine.Queue);
    }

    const string VarUp = """{ "id": "mood_up", "trigger": { "on": "varCrossed", "params": { "var": "mood", "value": 10, "direction": "up" } } }""";
    const string VarDown = """{ "id": "mood_down", "trigger": { "on": "varCrossed", "params": { "var": "mood", "value": 10, "direction": "down" } } }""";

    [Fact]
    public void Var_crossed_up_and_down()
    {
        var (engine, polling) = Setup(VarUp, VarDown);
        var mood = 5;
        List<FireCandidate> Poll(int v, double hour) { mood = v; return polling.Variables(hour, (c, name) => name == "mood" ? mood : null, Present); }

        Assert.Empty(Poll(5, 0));                         // first look records only
        Assert.Empty(Poll(9, 1));
        Assert.Equal("mood_up", Assert.Single(Poll(10, 2)).Content.Id);   // 9 → 10 crosses up (at-or-above)
        Assert.Empty(Poll(15, 3));
        Assert.Equal("mood_down", Assert.Single(Poll(9, 4)).Content.Id);  // 15 → 9 crosses down
        Assert.True(engine.Queue.Single(q => q.ContentId == "mood_down").Payload.TryResolve("value", out var value));
        Assert.Equal(9L, value);
    }

    [Fact]
    public void Var_crossed_skips_unknown_values()
    {
        var (_, polling) = Setup(VarUp);
        Assert.Empty(polling.Variables(0, (c, n) => null, Present));
    }

    static PayloadValue AnaHero() => PayloadValue.Entity(FieldKind.Hero, new Dictionary<string, object> { ["id"] = "ana_h", ["companion"] = "ana" });

    [Fact]
    public void Role_changes_fire_ended_then_started_for_matching_roles()
    {
        var (_, polling) = Setup(
            """{ "id": "gov_on", "trigger": { "on": "roleStarted", "params": { "role": "governor" } } }""",
            """{ "id": "gov_off", "trigger": { "on": "roleEnded", "params": { "role": "governor" } } }""",
            """{ "id": "lead_on", "trigger": { "on": "roleStarted", "params": { "role": "partyLeader" } } }""");
        string? role = null;
        List<FireCandidate> Poll(string? r, double hour) { role = r; return polling.Roles(hour, new[] { "ana" }, _ => role, _ => AnaHero(), Present); }

        Assert.Empty(Poll(null, 0));
        Assert.Equal("gov_on", Assert.Single(Poll("governor", 1)).Content.Id);
        Assert.Empty(Poll("governor", 2));
        var change = Poll("partyLeader", 3);
        Assert.Equal(new[] { "gov_off", "lead_on" }, change.Select(c => c.Content.Id));
        Assert.All(change, c => Assert.True(c.SubjectMatches)); // default subject self = this companion
    }

    [Fact]
    public void Save_and_load_keep_latches_values_roles_and_the_day()
    {
        var (engine, polling) = Setup(Near, VarUp, """{ "id": "day_1", "trigger": { "on": "daily" } }""");
        polling.NearSettlements(0, _ => 20, Town, Present);
        polling.NearSettlements(1, _ => 3, Town, Present);       // fired, disarmed
        polling.Variables(1, (c, n) => 4, Present);
        polling.Roles(1, new[] { "ana" }, _ => "governor", _ => AnaHero(), Present);
        polling.Daily(1, Present);

        var saved = polling.Save();
        var restored = new TriggerPolling(engine);
        Assert.Equal(saved.Count, restored.Load(saved));
        Assert.Equal(saved, restored.Save());
        Assert.Empty(restored.NearSettlements(2, _ => 3, Town, Present)); // still disarmed
        Assert.Single(restored.Variables(2, (c, n) => 12, Present));     // 4 → 12 crosses up
        Assert.Empty(restored.Daily(5, Present));                         // same day
        var warnings = new List<string>();
        Assert.Equal(0, restored.Load(new[] { "{}" }, warnings.Add));
        Assert.Single(warnings);
    }
}
