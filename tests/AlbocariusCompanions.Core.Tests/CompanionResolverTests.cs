using System.IO;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Resolution;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json;

namespace AlbocariusCompanions.Core.Tests;

/// <summary>Unit tests for CompanionResolver (complement the acceptance tests for task 25-core-resolver).</summary>
public class CompanionResolverTests
{
    const string Id = "unit_companion";

    sealed class StubGame : IGameObjectLookup
    {
        public HashSet<string> Cultures = new() { "empire" };
        public HashSet<string> Skills = new() { "OneHanded" };
        public HashSet<string> Traits = new() { "Valor" };
        public HashSet<string> Rosters = new() { "tpl_empire" };
        public Dictionary<string, string[]> Items = new() { ["robe"] = new[] { "Body" }, ["sword"] = new[] { "Item0", "Item1" } };

        public bool CultureExists(string id) => Cultures.Contains(id);
        public bool SkillExists(string id) => Skills.Contains(id);
        public bool TraitExists(string id) => Traits.Contains(id);
        public bool EquipmentRosterExists(string id) => Rosters.Contains(id);
        public bool ItemExists(string id) => Items.ContainsKey(id);
        public bool ItemFitsSlot(string slot, string itemId) => Items.TryGetValue(itemId, out var slots) && slots.Contains(slot);
    }

    static string Valid(string equipment = """
        { "templateId": "tpl_empire" }
        """) => $$"""
        { "id": "{{Id}}", "name": "N", "isFemale": true, "voice": "curt", "age": 25,
          "culture": "empire", "bodyProperties": null,
          "attributes": { "vigor": 4, "control": 3, "endurance": 4, "cunning": 2, "social": 2, "intelligence": 2 },
          "skills": { "OneHanded": 100 }, "traits": { "Valor": 1 },
          "equipment": {{equipment}},
          "hireCost": 500, "likes": [], "dislikes": [], "relations": [] }
        """;

    static ResolutionResult Resolve(string companionJson, StubGame? game = null) =>
        CompanionResolver.Resolve(
            new CompanionPackage(Id, Path.Combine(Path.GetTempPath(), "albocarius-unit", Id),
                JsonConvert.DeserializeObject<CompanionDefinition>(companionJson)!, new DialogFile()),
            game ?? new StubGame());

    [Fact]
    public void A_valid_companion_resolves_without_any_issue()
    {
        var r = Resolve(Valid());
        Assert.Empty(r.Issues);
        Assert.NotNull(r.Companion);
    }

    [Fact]
    public void Id_name_voice_age_and_female_are_copied_straight()
    {
        var c = Resolve(Valid()).Companion!;
        Assert.Equal(Id, c.Id);
        Assert.Equal("N", c.Name);
        Assert.True(c.IsFemale);
        Assert.Equal("curt", c.Voice);
        Assert.Equal(25, c.Age);
        Assert.Null(c.BodyPropertiesKey);
    }

    [Fact]
    public void Missing_skills_and_traits_fields_resolve_to_empty_maps()
    {
        var json = Valid().Replace(@"""skills"": { ""OneHanded"": 100 }, ""traits"": { ""Valor"": 1 },", "");
        var r = Resolve(json);

        Assert.Empty(r.Issues);
        Assert.Empty(r.Companion!.Skills);
        Assert.Empty(r.Companion.Traits);
    }

    [Fact]
    public void Every_missing_skill_and_trait_gets_its_own_warning()
    {
        var json = Valid()
            .Replace(@"""OneHanded"": 100", @"""OneHanded"": 100, ""Mariner"": 50, ""Smithing"": 20")
            .Replace(@"""Valor"": 1", @"""Valor"": 1, ""Generous"": 2");
        var r = Resolve(json);

        var skills = r.Issues.Where(i => i.Code == IssueCodes.SkillMissing).ToList();
        var traits = r.Issues.Where(i => i.Code == IssueCodes.TraitMissing).ToList();
        Assert.Equal(2, skills.Count);
        Assert.Single(traits);
        Assert.Contains("Mariner", skills[0].Message);
        Assert.Contains("Smithing", skills[1].Message);
        Assert.Contains("Generous", Assert.Single(traits).Message);
        Assert.All(r.Issues, i => Assert.False(i.IsError));
        Assert.Equal(new Dictionary<string, int> { ["OneHanded"] = 100 }, r.Companion!.Skills);
        Assert.Equal(new Dictionary<string, int> { ["Valor"] = 1 }, r.Companion.Traits);
    }

    [Fact]
    public void An_explicitly_empty_fixed_set_falls_back_to_the_roster()
    {
        var r = Resolve(Valid("""{ "templateId": "tpl_empire", "battle": {} }"""));

        var issue = Assert.Single(r.Issues);
        Assert.Equal(IssueCodes.FixedSetEmpty, issue.Code);
        Assert.Equal(IssueSeverity.Warning, issue.Severity);
        Assert.Null(r.Companion!.BattleItems);
        Assert.Equal("tpl_empire", r.Companion.BattleEquipmentRosterId);
    }

    [Fact]
    public void Only_the_set_that_ran_out_of_items_falls_back()
    {
        var r = Resolve(Valid("""
            { "templateId": "tpl_empire",
              "battle": { "Body": "robe", "Item0": "dlc_sword" },
              "civilian": { "Body": "dlc_robe", "Head": "dlc_hood" } }
            """));

        Assert.Equal(new Dictionary<string, string> { ["Body"] = "robe" }, r.Companion!.BattleItems);
        Assert.Null(r.Companion.CivilianItems);
        Assert.Equal(
            new[] { IssueCodes.ItemMissing, IssueCodes.ItemMissing, IssueCodes.ItemMissing, IssueCodes.FixedSetEmpty },
            r.Issues.Select(i => i.Code));
    }

    [Fact]
    public void A_culture_that_is_not_in_the_game_is_one_error_and_no_companion()
    {
        var json = Valid().Replace(@"""culture"": ""empire""", @"""culture"": ""nord_dlc_only""");
        var r = Resolve(json);

        Assert.Null(r.Companion);
        var issue = Assert.Single(r.Issues);
        Assert.Equal(IssueCodes.CultureMissing, issue.Code);
        Assert.True(issue.IsError);
        Assert.Contains("nord_dlc_only", issue.Message);
    }

    [Fact]
    public void A_roster_that_is_not_in_the_game_skips_the_companion()
    {
        var r = Resolve(Valid("""{ "templateId": "dlc_tpl" }"""));

        Assert.Null(r.Companion);
        var issue = Assert.Single(r.Issues);
        Assert.Equal(IssueCodes.EquipmentMissing, issue.Code);
        Assert.True(issue.IsError);
        Assert.Contains("dlc_tpl", issue.Message);
    }

    [Fact]
    public void The_civilian_twin_is_derived_from_the_resolved_roster()
    {
        var game = new StubGame();
        game.Rosters.Add("tpl_empire_civilian");

        Assert.Equal("tpl_empire_civilian", Resolve(Valid(), game).Companion!.CivilianEquipmentRosterId);
    }

    [Fact]
    public void An_item_that_exists_but_fits_no_listed_slot_is_a_slot_warning_not_a_missing_warning()
    {
        var r = Resolve(Valid("""{ "templateId": "tpl_empire", "battle": { "Leg": "robe" } }"""));

        Assert.Equal(new[] { IssueCodes.ItemDoesNotFitSlot, IssueCodes.FixedSetEmpty }, r.Issues.Select(i => i.Code));
        Assert.Contains("robe", r.Issues[0].Message);
        Assert.Null(r.Companion!.BattleItems);
    }

    [Fact]
    public void Every_issue_is_filed_under_the_package_folder()
    {
        var r = Resolve(Valid("""{ "templateId": "dlc_tpl", "battle": { "Body": "dlc_robe" } }"""));

        Assert.NotEmpty(r.Issues);
        Assert.All(r.Issues, i => Assert.Equal(Id, i.Folder));
    }
}
