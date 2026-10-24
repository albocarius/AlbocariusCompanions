using AlbocariusCompanions.Core.Resolution;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 25-core-resolver: SPEC rule 9 resolution against the running game (faked here).
public class ResolverTests
{
    const string Id = "test_companion";
    const string Roster = "npc_wanderer_equipment_template_empire";

    sealed class FakeGame : IGameObjectLookup
    {
        public HashSet<string> Cultures = new() { "empire", "vlandia" };
        public HashSet<string> Skills = new() { "OneHanded", "Athletics", "Riding" };
        public HashSet<string> Traits = new() { "Valor", "Mercy" };
        public HashSet<string> Rosters = new() { Roster, Roster + "_civilian", "npc_wanderer_equipment_template_vlandia" };
        public Dictionary<string, string[]> Items = new()
        {
            ["monk_robe"] = new[] { "Body" },
            ["pilgrim_hood"] = new[] { "Head" },
            ["bo_staff"] = new[] { "Item0", "Item1", "Item2", "Item3" },
        };

        public bool CultureExists(string id) => Cultures.Contains(id);
        public bool SkillExists(string id) => Skills.Contains(id);
        public bool TraitExists(string id) => Traits.Contains(id);
        public bool EquipmentRosterExists(string id) => Rosters.Contains(id);
        public bool ItemExists(string id) => Items.ContainsKey(id);
        public bool ItemFitsSlot(string slot, string itemId) => Items.TryGetValue(itemId, out var slots) && slots.Contains(slot);
    }

    static ResolutionResult Resolve(Action<JObject>? mutate = null, FakeGame? game = null)
    {
        var c = Fixtures.Companion(Id);
        mutate?.Invoke(c);
        return CompanionResolver.Resolve(Fixtures.Package(Id, c, Fixtures.Dialogs(Id)), game ?? new FakeGame());
    }

    static ValidationIssue Single(ResolutionResult r, string code, IssueSeverity severity)
    {
        var issue = Assert.Single(r.Issues);
        Assert.Equal(code, issue.Code);
        Assert.Equal(severity, issue.Severity);
        Assert.Equal(Id, issue.Folder);
        return issue;
    }

    [Fact]
    public void Everything_present_maps_all_fields()
    {
        var r = Resolve();

        Assert.Empty(r.Issues);
        var c = r.Companion!;
        Assert.Equal(Id, c.Id);
        Assert.Equal("Test Companion", c.Name);
        Assert.False(c.IsFemale);
        Assert.Equal("earnest", c.Voice);
        Assert.Equal(32, c.Age);
        Assert.Equal("empire", c.CultureId);
        Assert.Equal(new Dictionary<string, int> { ["OneHanded"] = 100, ["Athletics"] = 60 }, c.Skills);
        Assert.Equal(new Dictionary<string, int> { ["Valor"] = 1 }, c.Traits);
        Assert.Equal(Roster, c.BattleEquipmentRosterId);
        Assert.Equal(Roster + "_civilian", c.CivilianEquipmentRosterId);
        Assert.Null(c.BodyPropertiesKey);
        Assert.Null(c.BattleItems);
        Assert.Null(c.CivilianItems);
    }

    [Fact]
    public void Body_properties_are_passed_through() =>
        Assert.Equal("0xAB", Resolve(c => c["bodyProperties"] = "0xAB").Companion!.BodyPropertiesKey);

    [Fact]
    public void No_civilian_twin_roster_means_no_civilian_set()
    {
        var game = new FakeGame();
        game.Rosters.Remove(Roster + "_civilian");
        var r = Resolve(game: game);

        Assert.Empty(r.Issues);
        Assert.Null(r.Companion!.CivilianEquipmentRosterId);
    }

    [Fact]
    public void Missing_culture_skips_the_companion()
    {
        var r = Resolve(c => c["culture"] = "nord_dlc_only");
        Assert.Null(r.Companion);
        Single(r, IssueCodes.CultureMissing, IssueSeverity.Error);
    }

    [Fact]
    public void Missing_roster_skips_the_companion()
    {
        var r = Resolve(c => c["equipment"]!["templateId"] = "dlc_roster");
        Assert.Null(r.Companion);
        Single(r, IssueCodes.EquipmentMissing, IssueSeverity.Error);
    }

    [Fact]
    public void Missing_skill_is_dropped_with_warning()
    {
        var r = Resolve(c => c["skills"]!["Mariner"] = 50);

        Assert.Equal(new Dictionary<string, int> { ["OneHanded"] = 100, ["Athletics"] = 60 }, r.Companion!.Skills);
        Assert.Contains("Mariner", Single(r, IssueCodes.SkillMissing, IssueSeverity.Warning).Message);
    }

    [Fact]
    public void Missing_trait_is_dropped_with_warning()
    {
        var r = Resolve(c => c["traits"]!["NavalSoldierX"] = 1);

        Assert.Equal(new Dictionary<string, int> { ["Valor"] = 1 }, r.Companion!.Traits);
        Assert.Contains("NavalSoldierX", Single(r, IssueCodes.TraitMissing, IssueSeverity.Warning).Message);
    }

    [Fact]
    public void Absent_traits_resolve_to_an_empty_map()
    {
        var r = Resolve(c => c.Remove("traits"));
        Assert.Empty(r.Issues);
        Assert.Empty(r.Companion!.Traits);
    }

    [Fact]
    public void Fixed_items_are_resolved()
    {
        var r = Resolve(c =>
        {
            c["equipment"]!["battle"] = new JObject { ["Body"] = "monk_robe", ["Head"] = "pilgrim_hood", ["Item0"] = "bo_staff" };
            c["equipment"]!["civilian"] = new JObject { ["Body"] = "monk_robe" };
        });

        Assert.Empty(r.Issues);
        Assert.Equal(new Dictionary<string, string> { ["Body"] = "monk_robe", ["Head"] = "pilgrim_hood", ["Item0"] = "bo_staff" }, r.Companion!.BattleItems);
        Assert.Equal(new Dictionary<string, string> { ["Body"] = "monk_robe" }, r.Companion.CivilianItems);
    }

    [Fact]
    public void Missing_item_is_dropped_with_warning()
    {
        var r = Resolve(c => c["equipment"]!["battle"] = new JObject { ["Body"] = "monk_robe", ["Cape"] = "dlc_cape" });

        Assert.Equal(new Dictionary<string, string> { ["Body"] = "monk_robe" }, r.Companion!.BattleItems);
        Assert.Contains("dlc_cape", Single(r, IssueCodes.ItemMissing, IssueSeverity.Warning).Message);
    }

    [Fact]
    public void Item_in_the_wrong_slot_is_dropped_with_warning()
    {
        var r = Resolve(c => c["equipment"]!["battle"] = new JObject { ["Body"] = "monk_robe", ["Item0"] = "pilgrim_hood" });

        Assert.Equal(new Dictionary<string, string> { ["Body"] = "monk_robe" }, r.Companion!.BattleItems);
        Assert.Contains("pilgrim_hood", Single(r, IssueCodes.ItemDoesNotFitSlot, IssueSeverity.Warning).Message);
    }

    [Fact]
    public void Set_with_no_usable_item_falls_back_to_the_roster()
    {
        var r = Resolve(c => c["equipment"]!["civilian"] = new JObject { ["Body"] = "dlc_robe" });

        Assert.Null(r.Companion!.CivilianItems);
        Assert.Equal(Roster + "_civilian", r.Companion.CivilianEquipmentRosterId);
        Assert.Equal(new[] { IssueCodes.ItemMissing, IssueCodes.FixedSetEmpty }, r.Issues.Select(i => i.Code));
        Assert.All(r.Issues, i => Assert.Equal(IssueSeverity.Warning, i.Severity));
    }
}
