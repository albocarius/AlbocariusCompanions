using AlbocariusCompanions.Acceptance.Core;
using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Triggers;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Triggers;

// Task 124 (M11 T7): console fire / queue / clearcooldowns text.
public class TriggerCommandTests
{
    static PayloadValue? Resolve(FieldKind kind, string id) =>
        id == "lord_1" && kind == FieldKind.Hero
            ? PayloadValue.Entity(FieldKind.Hero, new Dictionary<string, object> { ["id"] = "lord_1", ["culture"] = "vlandia" })
            : null;

    [Fact]
    public void Parses_entities_and_primitives()
    {
        var payload = TriggerCommands.ParseFire(new[] { "heroKilled", "victim=lord_1", "detail=Executed" }, Resolve, out var t, out var error);
        Assert.Equal("", error);
        Assert.Equal("heroKilled", t!.Name);
        Assert.True(payload!.TryResolve("victim.culture", out var culture));
        Assert.Equal("vlandia", culture);
        Assert.True(payload.TryResolve("detail", out var detail));
        Assert.Equal("Executed", detail);

        var battle = TriggerCommands.ParseFire(new[] { "playerBattleEnded", "playerWon=true" }, Resolve, out _, out _);
        Assert.True(battle!.TryResolve("playerWon", out var won));
        Assert.Equal(true, won);
        Assert.Empty(TriggerCommands.ParseFire(new[] { "daily" }, Resolve, out _, out _)!.Fields);
    }

    [Theory]
    [InlineData(new string[0], "Usage")]
    [InlineData(new[] { "dragon" }, "unknown trigger 'dragon'")]
    [InlineData(new[] { "heroKilled", "victim" }, "is not field=value")]
    [InlineData(new[] { "heroKilled", "nope=1" }, "no field 'nope'")]
    [InlineData(new[] { "heroKilled", "victim=ghost" }, "no hero with id 'ghost'")]
    [InlineData(new[] { "playerBattleEnded", "playerWon=maybe" }, "true/false")]
    public void Bad_arguments_give_an_error(string[] args, string contains)
    {
        Assert.Null(TriggerCommands.ParseFire(args, Resolve, out _, out var error));
        Assert.Contains(contains, error);
    }

    sealed class Ctx : IConversationContext
    {
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

    [Fact]
    public void Formats_fire_results_and_the_queue()
    {
        var d = Fixtures.Dialogs("ana");
        Fixtures.Lines(d).Add(JObject.Parse("""{ "id": "n_1", "trigger": { "on": "daily", "cooldownDays": 2 } }"""));
        Fixtures.Lines(d).Add(JObject.Parse("""{ "id": "n_2", "trigger": { "on": "daily", "chance": 0 } }"""));
        var engine = new TriggerEngine(new[] { Fixtures.Package("ana", Fixtures.Companion("ana"), d) }, new Zero());
        var results = engine.Fire("daily", TriggerPayload.None, 5, (c, p) => new Ctx());
        var text = TriggerCommands.FormatFire("daily", results);
        Assert.Contains("daily: 1 of 2 queued", text);
        Assert.Contains("ana/line/n_1 (notification): queued", text);
        Assert.Contains("ana/line/n_2 (notification): stopped at chance", text);
        Assert.Contains("no loaded content", TriggerCommands.FormatFire("heroKilled", Array.Empty<FireCandidate>()));

        var queue = TriggerCommands.FormatQueue(engine, 6);
        Assert.Contains("Queue (1):", queue);
        Assert.Contains("ana/line/n_1 [notification, priority 0] from daily at hour 5, expires at 29", queue);

        engine.NextDelivery(7, new DeliveryEnvironment { OnMap = true, CanInterrupt = true }, (c, p) => new Ctx());
        queue = TriggerCommands.FormatQueue(engine, 8);
        Assert.Contains("Queue: empty.", queue);
        Assert.Contains("0/1 interruption(s), 1/3 notification(s)", queue);
        Assert.Contains("ana/line/n_1: delivered at hour 7, cooldown until hour 55", queue);
    }
}
