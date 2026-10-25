using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Registry;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 71 (M7, SPEC_BEHAVIOR_FOUNDATION I3): transfer effects, hasGold/hasItem conditions (Core part).
public class TransferTests
{
    const string Id = "test_companion";

    static IReadOnlyList<ValidationIssue> Validate(JObject? effects = null, JObject? when = null)
    {
        var d = Fixtures.Dialogs(Id);
        var langs = Fixtures.Langs();
        var conversation = Fixtures.Conversation();
        if (effects != null)
            Fixtures.Node(conversation, "p_wound_skip")["effects"] = effects;
        if (when != null)
            Fixtures.Node(conversation, "p_wound_skip")["when"] = when;
        Fixtures.AddConversation(d, langs, conversation);
        return DialogChecks.Validate(new[] { Fixtures.Package(Id, Fixtures.Companion(Id), d, langs: langs) });
    }

    static void SingleError(IReadOnlyList<ValidationIssue> issues, string code)
    {
        var error = Assert.Single(issues.Errors());
        Assert.Equal(code, error.Code);
    }

    // ---- validation ----

    [Theory]
    [InlineData("""{ "transfer": { "gold": 100, "from": "player", "to": "self" } }""")]
    [InlineData("""{ "transfer": { "gold": 100, "from": "self", "to": "player" } }""")]
    [InlineData("""{ "transfer": { "gold": 5, "from": "world", "to": "self" } }""")]
    [InlineData("""{ "transfer": { "gold": 5, "from": "self", "to": "world" } }""")]
    [InlineData("""{ "transfer": { "gold": 5, "from": "player", "to": "jeremus" } }""")]
    [InlineData("""{ "transfer": { "item": "grain", "count": 3, "from": "world", "to": "player" } }""")]
    [InlineData("""{ "transfer": { "item": "grain", "from": "player", "to": "world" } }""")]
    public void Valid_transfers_have_no_errors(string effects) => Assert.Empty(Validate(JObject.Parse(effects)).Errors());

    [Theory]
    [InlineData("""{ "transfer": { "from": "player", "to": "self" } }""")]
    [InlineData("""{ "transfer": { "gold": 5, "item": "grain", "from": "player", "to": "world" } }""")]
    [InlineData("""{ "transfer": { "gold": 0, "from": "player", "to": "self" } }""")]
    [InlineData("""{ "transfer": { "gold": -5, "from": "player", "to": "self" } }""")]
    [InlineData("""{ "transfer": { "gold": 5, "count": 2, "from": "player", "to": "self" } }""")]
    [InlineData("""{ "transfer": { "item": "", "from": "world", "to": "player" } }""")]
    [InlineData("""{ "transfer": { "item": "grain", "count": 0, "from": "world", "to": "player" } }""")]
    [InlineData("""{ "transfer": { "gold": 5, "to": "self" } }""")]
    [InlineData("""{ "transfer": { "gold": 5, "from": "player" } }""")]
    [InlineData("""{ "transfer": { "gold": 5, "from": "Player", "to": "self" } }""")]
    [InlineData("""{ "transfer": { "gold": 5, "from": "player", "to": "player" } }""")]
    [InlineData("""{ "transfer": { "gold": 5, "from": "world", "to": "world" } }""")]
    [InlineData("""{ "transfer": { "gold": 5, "from": "self", "to": "test_companion" } }""")]
    [InlineData("""{ "transfer": { "item": "grain", "from": "self", "to": "player" } }""")]
    [InlineData("""{ "transfer": { "item": "grain", "from": "world", "to": "jeremus" } }""")]
    [InlineData("""{ "gold": 5, "transfer": { "gold": 5, "from": "world", "to": "player" } }""")]
    public void Invalid_transfer_is_an_effect_error(string effects) =>
        SingleError(Validate(JObject.Parse(effects)), IssueCodes.InvalidEffect);

    [Theory]
    [InlineData("""{ "hasGold": { "who": "player", "min": 100 } }""")]
    [InlineData("""{ "hasGold": { "who": "self", "min": 1 } }""")]
    [InlineData("""{ "hasGold": { "who": "jeremus", "min": 1 } }""")]
    [InlineData("""{ "hasItem": { "who": "player", "id": "grain", "min": 2 } }""")]
    [InlineData("""{ "hasItem": { "who": "player", "id": "grain" } }""")]
    [InlineData("""{ "any": [ { "hasGold": { "who": "self", "min": 10 } }, { "hasItem": { "who": "player", "id": "grain" } } ] }""")]
    public void Valid_has_conditions_have_no_errors(string when) => Assert.Empty(Validate(when: JObject.Parse(when)).Errors());

    [Theory]
    [InlineData("""{ "hasGold": { "min": 100 } }""")]
    [InlineData("""{ "hasGold": { "who": "world", "min": 100 } }""")]
    [InlineData("""{ "hasGold": { "who": "player" } }""")]
    [InlineData("""{ "hasGold": { "who": "player", "min": 0 } }""")]
    [InlineData("""{ "hasItem": { "who": "self", "id": "grain" } }""")]
    [InlineData("""{ "hasItem": { "who": "player" } }""")]
    [InlineData("""{ "hasItem": { "who": "player", "id": "grain", "min": 0 } }""")]
    [InlineData("""{ "not": { "hasGold": { "who": "Bad Actor", "min": 1 } } }""")]
    public void Invalid_has_condition_is_a_condition_error(string when) =>
        SingleError(Validate(when: JObject.Parse(when)), IssueCodes.InvalidCondition);

    [Fact]
    public void Unknown_field_in_a_transfer_is_a_load_error()
    {
        using var tmp = new TempDir();
        var d = Fixtures.Dialogs(Id);
        var langs = Fixtures.Langs();
        var conversation = Fixtures.Conversation();
        Fixtures.Node(conversation, "p_wound_skip")["effects"] = JObject.Parse("""{ "transfer": { "gold": 5, "from": "world", "to": "player", "via": "x" } }""");
        Fixtures.AddConversation(d, langs, conversation);
        Fixtures.Write(tmp.Path, Id, Fixtures.Companion(Id), d, langs);
        Assert.Equal(0, CompanionCatalog.Load(tmp.Path).Registry.Count);
    }

    // ---- other companions as actors (catalog) ----

    static void WriteCompanion(string root, string id, JObject? effects = null, JObject? when = null)
    {
        var d = Fixtures.Dialogs(id);
        var langs = Fixtures.Langs();
        var conversation = Fixtures.Conversation();
        if (effects != null)
            Fixtures.Node(conversation, "p_wound_skip")["effects"] = effects;
        if (when != null)
            Fixtures.Node(conversation, "p_wound_skip")["when"] = when;
        Fixtures.AddConversation(d, langs, conversation);
        Fixtures.Write(root, id, Fixtures.Companion(id), d, langs);
    }

    [Fact]
    public void Companion_actor_that_is_loaded_is_fine()
    {
        using var tmp = new TempDir();
        WriteCompanion(tmp.Path, "jeremus");
        WriteCompanion(tmp.Path, "ahmet", JObject.Parse("""{ "transfer": { "gold": 5, "from": "world", "to": "jeremus" } }"""),
            JObject.Parse("""{ "hasGold": { "who": "jeremus", "min": 1 } }"""));
        var result = CompanionCatalog.Load(tmp.Path);
        Assert.DoesNotContain(result.Issues, i => i.Code == IssueCodes.MissingCompanionReference);
        Assert.Equal(2, result.Registry.Count);
    }

    [Theory]
    [InlineData("""{ "transfer": { "gold": 5, "from": "world", "to": "jeremus" } }""", null)]
    [InlineData("""{ "transfer": { "gold": 5, "from": "jeremus", "to": "player" } }""", null)]
    [InlineData(null, """{ "hasGold": { "who": "jeremus", "min": 1 } }""")]
    public void Companion_actor_that_is_not_loaded_is_a_warning(string? effects, string? when)
    {
        using var tmp = new TempDir();
        WriteCompanion(tmp.Path, "ahmet", effects == null ? null : JObject.Parse(effects), when == null ? null : JObject.Parse(when));
        var result = CompanionCatalog.Load(tmp.Path);
        Assert.Contains(result.Issues, i => i.Code == IssueCodes.MissingCompanionReference && i.Folder == "ahmet" && !i.IsError);
        Assert.Empty(result.Issues.Errors());
        Assert.True(result.Registry.Contains("ahmet"));
    }

    // ---- normalization ----

    [Fact]
    public void Old_gold_key_is_sugar_for_a_player_world_transfer()
    {
        var pay = Transfers.Of(new EffectSet { Gold = -100 })!;
        Assert.Equal((100, (string?)null, "player", "world"), (pay.Gold, pay.Item, pay.From, pay.To));

        var receive = Transfers.Of(new EffectSet { Gold = 50 })!;
        Assert.Equal((50, "world", "player"), (receive.Gold, receive.From, receive.To));

        var explicitTransfer = new TransferEffect { Item = "grain", Count = 2, From = "world", To = "player" };
        Assert.Same(explicitTransfer, Transfers.Of(new EffectSet { Transfer = explicitTransfer }));
        Assert.Null(Transfers.Of(new EffectSet { Relation = 2 }));
        Assert.Null(Transfers.Of(null));
    }

    [Fact]
    public void Item_count_defaults_to_one()
    {
        Assert.Equal(1, Transfers.CountOf(new TransferEffect { Item = "grain", From = "world", To = "player" }));
        Assert.Equal(4, Transfers.CountOf(new TransferEffect { Item = "grain", Count = 4, From = "world", To = "player" }));
    }

    // ---- runtime ----

    sealed class Context : IConversationContext
    {
        public Dictionary<string, int> Gold { get; } = new() { ["player"] = 100, ["self"] = 30 };
        public Dictionary<string, int> Items { get; } = new() { ["grain"] = 2 };
        public bool HasMet => true;
        public bool IsHired => true;
        public bool IsInPlayerParty => true;
        public int Relation => 0;
        public int PlayerGold => Gold["player"];
        public bool ClanIsFull => false;
        public int? GetVariable(string reference) => 0;
        public int? GoldOf(string actor) => Gold.TryGetValue(actor, out var g) ? g : null;
        public int PlayerItemCount(string itemId) => Items.GetValueOrDefault(itemId);
    }

    static bool Holds(string when) => ConversationConditions.Holds(JObject.Parse(when).ToObject<ConditionSet>(), new Context());
    static bool CanApply(string effects) => ConversationConditions.EffectsCanApply(JObject.Parse(effects).ToObject<EffectSet>(), new Context());

    [Theory]
    [InlineData("""{ "hasGold": { "who": "player", "min": 100 } }""", true)]
    [InlineData("""{ "hasGold": { "who": "player", "min": 101 } }""", false)]
    [InlineData("""{ "hasGold": { "who": "self", "min": 30 } }""", true)]
    [InlineData("""{ "hasGold": { "who": "jeremus", "min": 1 } }""", false)]
    [InlineData("""{ "hasItem": { "who": "player", "id": "grain", "min": 2 } }""", true)]
    [InlineData("""{ "hasItem": { "who": "player", "id": "grain", "min": 3 } }""", false)]
    [InlineData("""{ "hasItem": { "who": "player", "id": "grain" } }""", true)]
    [InlineData("""{ "hasItem": { "who": "player", "id": "no_such_item" } }""", false)]
    [InlineData("""{ "not": { "hasGold": { "who": "self", "min": 31 } } }""", true)]
    public void Has_conditions_read_the_context(string when, bool expected) => Assert.Equal(expected, Holds(when));

    [Theory]
    [InlineData("""{ "transfer": { "gold": 100, "from": "player", "to": "self" } }""", true)]
    [InlineData("""{ "transfer": { "gold": 101, "from": "player", "to": "self" } }""", false)]
    [InlineData("""{ "transfer": { "gold": 31, "from": "self", "to": "player" } }""", false)]
    [InlineData("""{ "transfer": { "gold": 5, "from": "jeremus", "to": "player" } }""", false)]
    [InlineData("""{ "transfer": { "gold": 5000, "from": "world", "to": "player" } }""", true)]
    [InlineData("""{ "transfer": { "gold": 5, "from": "player", "to": "jeremus" } }""", true)]
    [InlineData("""{ "transfer": { "item": "grain", "count": 2, "from": "player", "to": "world" } }""", true)]
    [InlineData("""{ "transfer": { "item": "grain", "count": 3, "from": "player", "to": "world" } }""", false)]
    [InlineData("""{ "transfer": { "item": "grain", "count": 9, "from": "world", "to": "player" } }""", true)]
    [InlineData("""{ "gold": -100 }""", true)]
    [InlineData("""{ "gold": -101 }""", false)]
    [InlineData("""{ "gold": 500 }""", true)]
    public void Transfer_source_must_be_able_to_give(string effects, bool expected) => Assert.Equal(expected, CanApply(effects));
}
