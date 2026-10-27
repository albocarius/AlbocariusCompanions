using AlbocariusCompanions.Acceptance.Core;
using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;
using AlbocariusCompanions.Core.Triggers;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Triggers;

// Task 118 (M11 T3/T5, G7–G11): fire → queue → delivery, limits, expiry, persistence, used triggers.
public class TriggerEngineTests
{
    sealed class FixedRandom : IRandomSource
    {
        readonly Queue<int> _values;
        public FixedRandom(params int[] values) => _values = new Queue<int>(values);
        public int Next(int maxExclusive) => _values.Count > 0 ? Math.Min(_values.Dequeue(), maxExclusive - 1) : 0;
    }

    sealed class Ctx : IConversationContext, ITriggerContext
    {
        public bool InParty = true;
        public int Mood;
        public TriggerPayload? Event { get; set; }
        public string? Role => null;
        public bool HasMet => true;
        public bool IsHired => true;
        public bool IsInPlayerParty => InParty;
        public int Relation => 0;
        public int PlayerGold => 0;
        public bool ClanIsFull => false;
        public int? GetVariable(string reference) => reference == "mood" ? Mood : 0;
        public int? GoldOf(string actor) => 0;
        public int PlayerItemCount(string itemId) => 0;
    }

    static readonly DeliveryEnvironment Map = new() { OnMap = true, CanInterrupt = true };
    static readonly DeliveryEnvironment MapBusy = new() { OnMap = true, CanInterrupt = false };
    static readonly DeliveryEnvironment InScene = new() { OnMap = false, CanInterrupt = false };

    static CompanionPackage Package(string id, params JObject[] content)
    {
        var d = Fixtures.Dialogs(id);
        foreach (var c in content)
        {
            if (c["nodes"] != null)
            {
                if (d["conversations"] is not JArray conversations) d["conversations"] = conversations = new JArray();
                conversations.Add(c);
            }
            else
                Fixtures.Lines(d).Add(c);
        }
        return Fixtures.Package(id, Fixtures.Companion(id), d);
    }

    static JObject Line(string id, string trigger) => JObject.Parse($$"""{ "id": "{{id}}", "trigger": {{trigger}} }""");

    static JObject Conv(string id, string trigger, string? when = null) => JObject.Parse($$"""
        { "id": "{{id}}", "trigger": {{trigger}}, {{(when != null ? $"\"when\": {when}," : "")}} "start": ["{{id}}_a"],
          "nodes": [ { "id": "{{id}}_a", "speaker": "npc", "next": ["end"] } ] }
        """);

    static PayloadValue Hero(string id, string companion = "", bool inClan = false, string culture = "empire") =>
        PayloadValue.Entity(FieldKind.Hero, new Dictionary<string, object>
        {
            ["id"] = id, ["heroId"] = id, ["companion"] = companion, ["inPlayerClan"] = inClan, ["culture"] = culture,
        });

    static TriggerPayload Killed(PayloadValue victim) => new(new Dictionary<string, PayloadValue> { ["victim"] = victim });

    static Func<string, TriggerPayload, IConversationContext?> Contexts(Dictionary<string, Ctx> ctx) =>
        (companion, payload) =>
        {
            if (!ctx.TryGetValue(companion, out var c)) return null;
            c.Event = payload;
            return c;
        };

    static Dictionary<string, Ctx> AllPresent(params string[] ids) => ids.ToDictionary(i => i, _ => new Ctx());

    [Fact]
    public void Used_triggers_are_exactly_those_of_loaded_content()
    {
        var engine = new TriggerEngine(new[]
        {
            Package("ana", Line("a_1", """{ "on": "daily" }"""), Conv("a_c", """{ "on": "playerBattleEnded" }""")),
            Package("bo", Line("b_1", """{ "on": "daily" }"""), Line("b_2", """{ "on": "heroKilled" }""")),
            Package("cy"),
        }, new FixedRandom());
        Assert.Equal(new[] { "daily", "heroKilled", "playerBattleEnded" }, engine.UsedTriggers.OrderBy(t => t));
        Assert.Equal(2, engine.ContentFor("daily").Count);
        Assert.Empty(engine.ContentFor("villageLooted"));
    }

    [Fact]
    public void No_triggered_content_means_no_used_triggers() =>
        Assert.Empty(new TriggerEngine(new[] { Package("ana") }, new FixedRandom()).UsedTriggers);

    [Fact]
    public void Content_with_an_unknown_trigger_is_not_indexed() =>
        Assert.Empty(new TriggerEngine(new[] { Package("ana", Line("a_1", """{ "on": "dragonSighted" }""")) }, new FixedRandom()).UsedTriggers);

    [Fact]
    public void Fire_checks_subject_where_presence_chance_and_when_in_order()
    {
        var engine = new TriggerEngine(new[]
        {
            Package("ana",
                Line("a_clan", """{ "on": "heroKilled", "subject": "playerClan" }"""),
                Line("a_vl", """{ "on": "heroKilled", "subject": "any", "where": { "victim.culture": "vlandia" } }"""),
                Line("a_mood", """{ "on": "heroKilled", "subject": "any" }""") is var l ? WithWhen(l, """{ "var": "mood", "min": 5 }""") : null!,
                Line("a_rare", """{ "on": "heroKilled", "subject": "any", "chance": 0.25 }"""),
                Line("a_any", """{ "on": "heroKilled", "subject": "any" }""")),
        }, new FixedRandom(900_000));
        var results = engine.Fire("heroKilled", Killed(Hero("lord_1", culture: "empire")), 10, Contexts(AllPresent("ana")));

        string Outcome(string id) => results.Single(r => r.Content.Id == id).Outcome;
        Assert.Equal("subject", Outcome("a_clan"));
        Assert.Equal("where", Outcome("a_vl"));
        Assert.Equal("when", Outcome("a_mood"));
        Assert.Equal("chance", Outcome("a_rare")); // 900000 / 1e6 = 0.9 ≥ 0.25
        Assert.Equal("queued", Outcome("a_any"));
        Assert.Single(engine.Queue);
    }

    static JObject WithWhen(JObject o, string when) { o["when"] = JObject.Parse(when); return o; }

    [Fact]
    public void Absent_companion_is_skipped_unless_allow_absent()
    {
        var engine = new TriggerEngine(new[]
        {
            Package("ana", Line("a_1", """{ "on": "daily" }"""), Line("a_2", """{ "on": "daily", "allowAbsent": true }""")),
        }, new FixedRandom());
        var ctx = AllPresent("ana");
        ctx["ana"].InParty = false;
        var results = engine.Fire("daily", TriggerPayload.None, 1, Contexts(ctx));
        Assert.Equal("not in party", results.Single(r => r.Content.Id == "a_1").Outcome);
        Assert.Equal("queued", results.Single(r => r.Content.Id == "a_2").Outcome);
    }

    [Fact]
    public void Companion_without_hero_is_skipped()
    {
        var engine = new TriggerEngine(new[] { Package("ana", Line("a_1", """{ "on": "daily" }""")) }, new FixedRandom());
        Assert.Equal("no hero", engine.Fire("daily", TriggerPayload.None, 1, Contexts(new())).Single().Outcome);
    }

    [Fact]
    public void Self_subject_matches_only_the_own_companion()
    {
        var engine = new TriggerEngine(new[]
        {
            Package("ana", Line("a_1", """{ "on": "heroKilled", "subject": "self" }""")),
            Package("bo", Line("b_1", """{ "on": "heroKilled", "subject": "self" }""")),
        }, new FixedRandom());
        var results = engine.Fire("heroKilled", Killed(Hero("ana_hero", companion: "ana")), 1, Contexts(AllPresent("ana", "bo")));
        Assert.Equal("queued", results.Single(r => r.Content.CompanionId == "ana").Outcome);
        Assert.Equal("subject", results.Single(r => r.Content.CompanionId == "bo").Outcome);
    }

    [Fact]
    public void Default_subject_comes_from_the_registry()
    {
        // heroKilled defaults to playerClan.
        var engine = new TriggerEngine(new[] { Package("ana", Line("a_1", """{ "on": "heroKilled" }""")) }, new FixedRandom());
        Assert.Equal("subject", engine.Fire("heroKilled", Killed(Hero("lord_1")), 1, Contexts(AllPresent("ana"))).Single().Outcome);
        Assert.Equal("queued", engine.Fire("heroKilled", Killed(Hero("ana_hero", inClan: true)), 1, Contexts(AllPresent("ana"))).Single().Outcome);
    }

    [Fact]
    public void Same_item_is_not_queued_twice()
    {
        var engine = new TriggerEngine(new[] { Package("ana", Line("a_1", """{ "on": "daily" }""")) }, new FixedRandom());
        engine.Fire("daily", TriggerPayload.None, 1, Contexts(AllPresent("ana")));
        Assert.Equal("already queued", engine.Fire("daily", TriggerPayload.None, 2, Contexts(AllPresent("ana"))).Single().Outcome);
        Assert.Single(engine.Queue);
    }

    [Fact]
    public void Notifications_are_capped_per_day()
    {
        var lines = Enumerable.Range(1, 5).Select(i => Line($"n_{i}", """{ "on": "daily" }""")).ToArray();
        var engine = new TriggerEngine(new[] { Package("ana", lines) }, new FixedRandom());
        var ctx = Contexts(AllPresent("ana"));
        engine.Fire("daily", TriggerPayload.None, 1, ctx);
        var delivered = 0;
        while (engine.NextDelivery(2, Map, ctx) != null) delivered++;
        Assert.Equal(DeliveryRules.NotificationsPerDay, delivered);
        Assert.Equal(3, delivered);
        // Next day the rest goes out (they were queued with 24 h expiry at hour 1 → still valid at hour 24.5).
        Assert.NotNull(engine.NextDelivery(24.5, Map, ctx));
    }

    [Fact]
    public void Interruptions_are_capped_per_day_and_per_companion()
    {
        var engine = new TriggerEngine(new[]
        {
            Package("ana", Conv("a_c", """{ "on": "daily", "expiresAfterHours": 200 }""")),
            Package("bo", Conv("b_c", """{ "on": "daily", "expiresAfterHours": 200 }""")),
        }, new FixedRandom());
        var ctx = Contexts(AllPresent("ana", "bo"));
        engine.Fire("daily", TriggerPayload.None, 0, ctx);
        var first = engine.NextDelivery(1, Map, ctx);
        Assert.NotNull(first);
        Assert.Null(engine.NextDelivery(2, Map, ctx)); // 1 per day
        var second = engine.NextDelivery(25, Map, ctx); // next day: the other companion
        Assert.NotNull(second);
        Assert.NotEqual(first!.CompanionId, second!.CompanionId);

        // The first companion fires again; its 3-day pause holds it back until day 3.
        engine.Fire("daily", TriggerPayload.None, 26, ctx);
        Assert.Null(engine.NextDelivery(49, Map, ctx)); // day 2, 48 h after its interruption
        Assert.NotNull(engine.NextDelivery(73, Map, ctx)); // 72 h after
    }

    [Fact]
    public void Interruptions_wait_while_the_map_is_busy_but_notifications_do_not()
    {
        var engine = new TriggerEngine(new[] { Package("ana", Conv("a_c", """{ "on": "daily" }"""), Line("a_n", """{ "on": "daily" }""")) }, new FixedRandom());
        var ctx = Contexts(AllPresent("ana"));
        engine.Fire("daily", TriggerPayload.None, 0, ctx);
        var item = engine.NextDelivery(1, MapBusy, ctx);
        Assert.Equal("a_n", item!.ContentId);
        Assert.Null(engine.NextDelivery(1, MapBusy, ctx));
        Assert.Equal("a_c", engine.NextDelivery(1, Map, ctx)!.ContentId);
    }

    [Fact]
    public void In_mission_items_wait_for_the_map()
    {
        // heroWounded is an in-mission trigger (registry).
        var engine = new TriggerEngine(new[] { Package("ana", Line("a_n", """{ "on": "heroWounded", "subject": "any" }""")) }, new FixedRandom());
        var ctx = Contexts(AllPresent("ana"));
        engine.Fire("heroWounded", new TriggerPayload(new Dictionary<string, PayloadValue> { ["hero"] = Hero("x") }), 0, ctx);
        Assert.True(engine.Queue.Single().InMission);
        Assert.Null(engine.NextDelivery(1, InScene, ctx));
        Assert.NotNull(engine.NextDelivery(1, Map, ctx));
    }

    [Fact]
    public void Expired_items_are_dropped()
    {
        var engine = new TriggerEngine(new[] { Package("ana", Conv("a_c", """{ "on": "daily", "expiresAfterHours": 5 }""")) }, new FixedRandom());
        var ctx = Contexts(AllPresent("ana"));
        engine.Fire("daily", TriggerPayload.None, 0, ctx);
        var dropped = new List<string>();
        Assert.Null(engine.NextDelivery(6, Map, ctx, (i, why) => dropped.Add(why)));
        Assert.Equal(new[] { "expired" }, dropped);
        Assert.Empty(engine.Queue);
    }

    [Fact]
    public void Default_expiry_is_24_hours()
    {
        var engine = new TriggerEngine(new[] { Package("ana", Line("a_n", """{ "on": "daily" }""")) }, new FixedRandom());
        engine.Fire("daily", TriggerPayload.None, 10, Contexts(AllPresent("ana")));
        Assert.Equal(34, engine.Queue.Single().ExpiresAtHours);
    }

    [Fact]
    public void Conditions_are_checked_again_at_delivery()
    {
        var engine = new TriggerEngine(new[] { Package("ana", Conv("a_c", """{ "on": "daily" }""", when: """{ "var": "mood", "min": 1 }""")) }, new FixedRandom());
        var present = AllPresent("ana");
        present["ana"].Mood = 3;
        var ctx = Contexts(present);
        engine.Fire("daily", TriggerPayload.None, 0, ctx);
        present["ana"].Mood = 0;
        var dropped = new List<string>();
        Assert.Null(engine.NextDelivery(1, Map, ctx, (i, why) => dropped.Add(why)));
        Assert.Equal(new[] { "conditions no longer hold" }, dropped);
    }

    [Fact]
    public void Companion_that_left_the_party_is_dropped_at_delivery()
    {
        var engine = new TriggerEngine(new[] { Package("ana", Line("a_n", """{ "on": "daily" }""")) }, new FixedRandom());
        var present = AllPresent("ana");
        var ctx = Contexts(present);
        engine.Fire("daily", TriggerPayload.None, 0, ctx);
        present["ana"].InParty = false;
        Assert.Null(engine.NextDelivery(1, Map, ctx));
        Assert.Empty(engine.Queue);
    }

    [Fact]
    public void Item_cooldown_days_block_firing_again()
    {
        var engine = new TriggerEngine(new[] { Package("ana", Line("a_n", """{ "on": "daily", "cooldownDays": 2 }""")) }, new FixedRandom());
        var ctx = Contexts(AllPresent("ana"));
        engine.Fire("daily", TriggerPayload.None, 0, ctx);
        Assert.NotNull(engine.NextDelivery(1, Map, ctx));
        Assert.Equal("cooldown", engine.Fire("daily", TriggerPayload.None, 30, ctx).Single().Outcome);
        Assert.Equal("queued", engine.Fire("daily", TriggerPayload.None, 49, ctx).Single().Outcome);
    }

    [Fact]
    public void Highest_priority_is_delivered_first()
    {
        var low = Line("a_low", """{ "on": "daily" }""");
        var high = Line("a_high", """{ "on": "daily" }""");
        high["priority"] = 5;
        var engine = new TriggerEngine(new[] { Package("ana", low, high) }, new FixedRandom());
        var ctx = Contexts(AllPresent("ana"));
        engine.Fire("daily", TriggerPayload.None, 0, ctx);
        Assert.Equal("a_high", engine.NextDelivery(1, Map, ctx)!.ContentId);
    }

    [Fact]
    public void Clear_cooldowns_resets_items_companions_and_counters()
    {
        var engine = new TriggerEngine(new[] { Package("ana", Conv("a_c", """{ "on": "daily", "cooldownDays": 9 }""")) }, new FixedRandom());
        var ctx = Contexts(AllPresent("ana"));
        engine.Fire("daily", TriggerPayload.None, 0, ctx);
        engine.NextDelivery(1, Map, ctx);
        engine.ClearCooldowns();
        Assert.Equal("queued", engine.Fire("daily", TriggerPayload.None, 2, ctx).Single().Outcome);
        Assert.NotNull(engine.NextDelivery(3, Map, ctx));
    }

    [Fact]
    public void Save_and_load_keep_queue_payload_cooldowns_and_counters()
    {
        var packages = new[] { Package("ana", Line("a_n", """{ "on": "heroKilled", "subject": "any" }"""), Conv("a_c", """{ "on": "daily", "cooldownDays": 5 }""")) };
        var engine = new TriggerEngine(packages, new FixedRandom());
        var ctx = Contexts(AllPresent("ana"));
        engine.Fire("daily", TriggerPayload.None, 0, ctx);
        engine.NextDelivery(1, Map, ctx); // a_c delivered: cooldown + interruption + counter
        engine.Fire("heroKilled", new TriggerPayload(new Dictionary<string, PayloadValue>
        {
            ["victim"] = Hero("lord_1", culture: "vlandia"),
            ["detail"] = PayloadValue.Text("Executed"),
        }), 2, ctx);

        var saved = engine.Save();
        var loaded = new TriggerEngine(packages, new FixedRandom());
        var warnings = new List<string>();
        Assert.Equal(saved.Count, loaded.Load(saved, warnings.Add));
        Assert.Empty(warnings);

        var item = Assert.Single(loaded.Queue);
        Assert.Equal("ana/line/a_n", item.Key);
        Assert.Equal(2, item.FiredAtHours);
        Assert.True(item.Payload.TryResolve("victim.culture", out var culture));
        Assert.Equal("vlandia", culture);
        Assert.True(item.Payload.TryResolve("detail", out var detail));
        Assert.Equal("Executed", detail);
        Assert.Equal(1, loaded.LastDelivered["ana/conversation/a_c"]);
        Assert.Equal(1, loaded.LastInterruption["ana"]);
        Assert.Equal(1, loaded.InterruptionsToday);
        Assert.Equal("cooldown", loaded.Fire("daily", TriggerPayload.None, 3, ctx).Single().Outcome);
        Assert.Equal(saved, loaded.Save());
    }

    [Fact]
    public void Broken_save_entries_are_skipped_with_a_warning()
    {
        var engine = new TriggerEngine(Array.Empty<CompanionPackage>(), new FixedRandom());
        var warnings = new List<string>();
        Assert.Equal(1, engine.Load(new[] { "not json", """{"t":"x"}""", """{"t":"i","companion":"ana","at":4.5}""" }, warnings.Add));
        Assert.Equal(2, warnings.Count);
        Assert.Equal(4.5, engine.LastInterruption["ana"]);
    }
}
