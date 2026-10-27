using AlbocariusCompanions.Core.Triggers;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Triggers;

// Task 113 (M11 G2/G3): payload values, paths, subjects, where filters.
public class TriggerModelTests
{
    static PayloadValue Hero(string id, string companion = "", bool isPlayer = false, bool inParty = false, bool inClan = false, string culture = "empire") =>
        PayloadValue.Entity(FieldKind.Hero, new Dictionary<string, object>
        {
            ["id"] = id, ["heroId"] = id, ["companion"] = companion, ["isPlayer"] = isPlayer,
            ["inPlayerParty"] = inParty, ["inPlayerClan"] = inClan, ["culture"] = culture,
        });

    static PayloadValue Settlement(string id, bool playerOwned = false, string culture = "vlandia") =>
        PayloadValue.Entity(FieldKind.Settlement, new Dictionary<string, object>
        {
            ["id"] = id, ["inPlayerClan"] = playerOwned, ["culture"] = culture, ["kind"] = "village",
        });

    static TriggerDefinition HeroTrigger() => new("testKilled", "test", "d", "HeroKilledEvent",
        fields: new[]
        {
            new TriggerField("victim", FieldKind.Hero, "d", "victim"),
            new TriggerField("killer", FieldKind.Hero, "d", "killer"),
            new TriggerField("days", FieldKind.Int, "d", "x"),
        },
        subjectFields: new[] { "victim", "killer" }, defaultSubject: Subjects.PlayerClan);

    [Fact]
    public void Entity_has_every_property_of_its_kind_with_defaults()
    {
        var hero = PayloadValue.Entity(FieldKind.Hero, new Dictionary<string, object> { ["id"] = "lord_1" });
        Assert.All(EntityProperties.Of(FieldKind.Hero), p => Assert.True(hero.Properties.ContainsKey(p), p));
        Assert.Equal("", hero.StringProperty("culture"));
        Assert.False(hero.BoolProperty("isPlayer"));
        Assert.Equal("lord_1", hero.ToString());
        Assert.Throws<ArgumentException>(() => PayloadValue.Entity(FieldKind.Int, new Dictionary<string, object>()));
    }

    [Fact]
    public void Paths_resolve_fields_properties_and_entity_ids()
    {
        var payload = new TriggerPayload(new Dictionary<string, PayloadValue>
        {
            ["village"] = Settlement("village_1"),
            ["count"] = PayloadValue.Int(3),
        });
        Assert.True(payload.TryResolve("village.culture", out var culture));
        Assert.Equal("vlandia", culture);
        Assert.True(payload.TryResolve("village", out var id));
        Assert.Equal("village_1", id);
        Assert.True(payload.TryResolve("count", out var count));
        Assert.Equal(3L, count);
        Assert.False(payload.TryResolve("village.nope", out _));
        Assert.False(payload.TryResolve("count.x", out _));
        Assert.False(payload.TryResolve("missing", out _));
    }

    [Fact]
    public void Kind_of_path_follows_the_definition()
    {
        var t = HeroTrigger();
        Assert.Equal(FieldKind.Text, t.KindOfPath("victim"));
        Assert.Equal(FieldKind.Text, t.KindOfPath("victim.culture"));
        Assert.Equal(FieldKind.Bool, t.KindOfPath("victim.isPlayer"));
        Assert.Equal(FieldKind.Int, t.KindOfPath("days"));
        Assert.Null(t.KindOfPath("victim.nope"));
        Assert.Null(t.KindOfPath("days.x"));
        Assert.Null(t.KindOfPath("nope"));
    }

    [Theory]
    [InlineData("self", true)]
    [InlineData("player", true)]
    [InlineData("playerParty", true)]
    [InlineData("playerClan", true)]
    [InlineData("any", true)]
    [InlineData("companion:ahmet", true)]
    [InlineData("hero:lord_1", true)]
    [InlineData("settlement:town_V1", false)] // no settlement subject field
    [InlineData("companion", false)]
    [InlineData("self:x", false)]
    [InlineData("nobody", false)]
    [InlineData("", false)]
    public void Subject_validity_depends_on_subject_field_kinds(string subject, bool valid) =>
        Assert.Equal(valid, Subjects.IsValidFor(HeroTrigger(), subject));

    [Fact]
    public void Trigger_without_subject_fields_only_takes_any()
    {
        var t = new TriggerDefinition("plain", "test", "d", "X");
        Assert.Equal(new[] { "any" }, t.SubjectKinds());
    }

    [Fact]
    public void Subject_matches_any_subject_field()
    {
        var t = HeroTrigger();
        var payload = new TriggerPayload(new Dictionary<string, PayloadValue>
        {
            ["victim"] = Hero("lord_1"),
            ["killer"] = Hero("ahmet_hero", companion: "ahmet", inParty: true, inClan: true),
        });
        Assert.True(Subjects.Matches(t, "self", payload, "ahmet"));
        Assert.False(Subjects.Matches(t, "self", payload, "jeremus"));
        Assert.True(Subjects.Matches(t, "playerParty", payload, "jeremus"));
        Assert.True(Subjects.Matches(t, "playerClan", payload, "jeremus"));
        Assert.False(Subjects.Matches(t, "player", payload, "jeremus"));
        Assert.True(Subjects.Matches(t, "hero:lord_1", payload, "x"));
        Assert.False(Subjects.Matches(t, "hero:lord_2", payload, "x"));
        Assert.True(Subjects.Matches(t, "companion:ahmet", payload, "x"));
        Assert.True(Subjects.Matches(t, "any", payload, "x"));
    }

    [Fact]
    public void Self_never_matches_a_non_companion_hero()
    {
        var t = HeroTrigger();
        var payload = new TriggerPayload(new Dictionary<string, PayloadValue> { ["victim"] = Hero("lord_1") });
        Assert.False(Subjects.Matches(t, "self", payload, ""));
    }

    [Fact]
    public void Settlement_subject_matches_by_id_and_player_ownership()
    {
        var t = new TriggerDefinition("v", "test", "d", "VillageLooted",
            fields: new[] { new TriggerField("village", FieldKind.Settlement, "d", "village") }, subjectFields: new[] { "village" },
            defaultSubject: Subjects.PlayerClan);
        var mine = new TriggerPayload(new Dictionary<string, PayloadValue> { ["village"] = Settlement("village_1", playerOwned: true) });
        Assert.True(Subjects.Matches(t, "settlement:village_1", mine, "x"));
        Assert.False(Subjects.Matches(t, "settlement:village_2", mine, "x"));
        Assert.True(Subjects.Matches(t, "playerClan", mine, "x"));
        Assert.False(Subjects.IsValidFor(t, "self"));
    }

    [Fact]
    public void Where_compares_strings_bools_and_numbers()
    {
        var payload = new TriggerPayload(new Dictionary<string, PayloadValue>
        {
            ["victim"] = Hero("lord_1", isPlayer: false, culture: "vlandia"),
            ["days"] = PayloadValue.Int(4),
            ["rate"] = PayloadValue.Number(0.5),
        });
        Assert.True(Subjects.WhereMatches(null, payload));
        Assert.True(Subjects.WhereMatches(new Dictionary<string, JToken> { ["victim.culture"] = "vlandia", ["days"] = 4, ["rate"] = 0.5 }, payload));
        Assert.True(Subjects.WhereMatches(new Dictionary<string, JToken> { ["victim.isPlayer"] = false }, payload));
        Assert.False(Subjects.WhereMatches(new Dictionary<string, JToken> { ["victim.culture"] = "empire" }, payload));
        Assert.False(Subjects.WhereMatches(new Dictionary<string, JToken> { ["days"] = "4" }, payload));
        Assert.False(Subjects.WhereMatches(new Dictionary<string, JToken> { ["missing"] = "x" }, payload));
    }

    [Theory]
    [InlineData(FieldKind.Int, "12", true)]
    [InlineData(FieldKind.Int, "1.5", false)]
    [InlineData(FieldKind.Number, "1.5", true)]
    [InlineData(FieldKind.Bool, "true", true)]
    [InlineData(FieldKind.Bool, "yes", false)]
    [InlineData(FieldKind.Text, "anything", true)]
    [InlineData(FieldKind.Hero, "lord_1", false)]
    public void Primitive_parsing_for_console_and_simulator(FieldKind kind, string text, bool ok) =>
        Assert.Equal(ok, Subjects.TryParsePrimitive(kind, text, out _));
}
