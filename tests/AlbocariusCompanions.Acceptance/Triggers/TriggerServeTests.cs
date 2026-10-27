using AlbocariusCompanions.Acceptance.Core;
using AlbocariusCompanions.Core.Simulation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Triggers;

// Task 125 (M11 T7): serve ops for triggers.
public class TriggerServeTests : IDisposable
{
    readonly TempDir _tmp = new();
    readonly SimulatorProtocol _p = new();
    int _id;

    public TriggerServeTests()
    {
        var d = Fixtures.Dialogs("ana");
        var langs = Fixtures.Langs();
        Fixtures.Lines(d).Add(JObject.Parse("""{ "id": "rip_01", "trigger": { "on": "heroKilled", "subject": "any", "where": { "victim.culture": "vlandia" } } }"""));
        Fixtures.LangLines(langs["en"])["rip_01"] = "Another Vlandian falls.";
        Fixtures.AddConversation(d, langs, JObject.Parse("""
            { "id": "after_fight", "trigger": { "on": "playerBattleEnded", "delivery": "inquiry" }, "start": ["af_q"],
              "nodes": [ { "id": "af_q", "speaker": "npc", "next": ["p_af_a", "p_af_b"] },
                         { "id": "p_af_a", "speaker": "player", "next": ["end"], "effects": { "partyMorale": 5 } },
                         { "id": "p_af_b", "speaker": "player", "next": ["end"] } ] }
            """), new() { ["af_q"] = "Drink to it?", ["p_af_a"] = "Yes.", ["p_af_b"] = "No." });
        Fixtures.Write(_tmp.Path, "ana", Fixtures.Companion("ana"), d, langs);
    }

    public void Dispose() => _tmp.Dispose();

    JObject Send(object request)
    {
        var o = JObject.FromObject(request);
        o["id"] = ++_id;
        var response = JObject.Parse(_p.Handle(o.ToString()));
        return response;
    }

    JObject Ok(object request)
    {
        var r = Send(request);
        Assert.True((bool)r["ok"]!, r.ToString());
        return r;
    }

    string StartSession()
    {
        Assert.Equal(0, (int)Ok(new { op = "load", root = _tmp.Path })["errors"]!);
        var started = Ok(new { op = "triggers", seed = 1, state = new { companions = new { ana = new { met = true, hired = true, inParty = true } } } });
        Assert.Equal(new[] { "heroKilled", "playerBattleEnded" }, started["usedTriggers"]!.Select(t => (string)t!));
        return (string)started["session"]!;
    }

    [Fact]
    public void Fire_reports_each_candidate_and_queues_the_passing_one()
    {
        var s = StartSession();
        var miss = Ok(new { op = "fire", session = s, trigger = "heroKilled", payload = new { victim = new { id = "lord_1", culture = "empire" } }, hours = 1 });
        var c = (JObject)miss["candidates"]![0]!;
        Assert.Equal("where", (string)c["outcome"]!);
        Assert.False((bool)c["whereMatches"]!);
        Assert.Equal(JTokenType.Null, c["present"]!.Type);

        var hit = Ok(new { op = "fire", session = s, trigger = "heroKilled", payload = new { victim = new { id = "lord_2", culture = "vlandia" } }, hours = 2 });
        Assert.Equal("queued", (string)hit["candidates"]![0]!["outcome"]!);
        Assert.Equal("ana/line/rip_01", (string)hit["queue"]!["items"]![0]!["key"]!);
    }

    [Fact]
    public void Deliver_returns_the_line_text_and_counts_it()
    {
        var s = StartSession();
        Ok(new { op = "fire", session = s, trigger = "heroKilled", payload = new { victim = new { culture = "vlandia" } }, hours = 1 });
        var d = Ok(new { op = "deliver", session = s, hours = 2 });
        Assert.Equal("Another Vlandian falls.", (string)d["delivered"]!["text"]!);
        Assert.Equal(1, (int)d["queue"]!["notificationsToday"]!);
        Assert.Equal(JTokenType.Null, Ok(new { op = "deliver", session = s })["delivered"]!.Type);
    }

    [Fact]
    public void Inquiry_delivery_lists_options_and_the_popup_kind()
    {
        var s = StartSession();
        Ok(new { op = "fire", session = s, trigger = "playerBattleEnded", payload = new { playerWon = true }, hours = 1 });
        Assert.Equal(JTokenType.Null, Ok(new { op = "deliver", session = s, canInterrupt = false })["delivered"]!.Type);
        var d = Ok(new { op = "deliver", session = s, hours = 2 })["delivered"]!;
        Assert.Equal("af_q", (string)d["node"]!);
        Assert.Equal("Drink to it?", (string)d["text"]!);
        Assert.Equal(new[] { "p_af_a", "p_af_b" }, d["options"]!.Select(o => (string)o["node"]!));
        Assert.Equal("standard", (string)d["popup"]!);
    }

    [Fact]
    public void Expired_items_are_reported_as_dropped()
    {
        var s = StartSession();
        Ok(new { op = "fire", session = s, trigger = "playerBattleEnded", hours = 0 });
        var d = Ok(new { op = "deliver", session = s, hours = 30 });
        Assert.Equal("expired", (string)d["dropped"]![0]!["reason"]!);
    }

    [Fact]
    public void Queue_and_clear_cooldowns()
    {
        var s = StartSession();
        Ok(new { op = "fire", session = s, trigger = "playerBattleEnded", hours = 0 });
        Ok(new { op = "deliver", session = s, hours = 1 });
        var q = Ok(new { op = "queue", session = s });
        Assert.Equal(1.0, (double)q["companionInterruptions"]!["ana"]!);
        var cleared = Ok(new { op = "clearCooldowns", session = s });
        Assert.Empty((JObject)cleared["companionInterruptions"]!);
        Assert.Equal(0, (int)cleared["interruptionsToday"]!);
    }

    [Theory]
    [InlineData("""{ "op": "fire", "session": "SESSION", "trigger": "dragon" }""", "unknown trigger")]
    [InlineData("""{ "op": "fire", "session": "SESSION", "trigger": "heroKilled", "payload": { "nope": 1 } }""", "no field 'nope'")]
    [InlineData("""{ "op": "fire", "session": "SESSION", "trigger": "heroKilled", "payload": { "victim": "lord_1" } }""", "object of properties")]
    [InlineData("""{ "op": "fire", "session": "SESSION", "trigger": "heroKilled", "payload": { "victim": { "isPlayer": "yes" } } }""", "true/false")]
    [InlineData("""{ "op": "fire", "session": "SESSION", "trigger": "playerBattleEnded", "payload": { "playerWon": 1 } }""", "true/false")]
    [InlineData("""{ "op": "fire", "session": "SESSION", "trigger": "heroKilled", "hours": -1 }""", "before the session time")]
    [InlineData("""{ "op": "queue", "session": "t999" }""", "unknown trigger session")]
    public void Bad_requests_are_errors(string request, string contains)
    {
        var s = StartSession();
        var r = JObject.Parse(_p.Handle(request.Replace("SESSION", s)));
        Assert.False((bool)r["ok"]!);
        Assert.Contains(contains, (string)r["error"]!);
    }

    [Fact]
    public void Triggers_needs_a_load_first()
    {
        var r = Send(new { op = "triggers" });
        Assert.False((bool)r["ok"]!);
    }
}
