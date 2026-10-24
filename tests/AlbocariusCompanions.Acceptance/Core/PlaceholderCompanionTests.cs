using AlbocariusCompanions.Core.Loading;
using AlbocariusCompanions.Core.Rules;
using AlbocariusCompanions.Core.Validation;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 21-placeholder-companion (SPEC §5): the shipped fixture loads and validates without any issue.
public class PlaceholderCompanionTests
{
    static readonly string CompanionsRoot = Path.Combine(RepoPaths.Root, "module", "ModuleData", "Companions");

    // Vanilla ids (GAME_MAP Q4 orchestrator notes): DefaultSkills / DefaultTraits string ids, SandBoxCore rosters, base cultures.
    static readonly string[] VanillaSkills = { "OneHanded", "TwoHanded", "Polearm", "Bow", "Crossbow", "Throwing", "Riding", "Athletics", "Crafting",
        "Tactics", "Scouting", "Roguery", "Charm", "Trade", "Leadership", "Steward", "Medicine", "Engineering" };
    static readonly string[] PersonalityTraits = { "Mercy", "Valor", "Honor", "Generosity", "Calculating" };
    static readonly string[] MainCultures = { "empire", "aserai", "sturgia", "vlandia", "battania", "khuzait" };

    [Fact]
    public void Placeholder_loads_and_validates_cleanly()
    {
        var load = CompanionLoader.LoadAll(CompanionsRoot);
        Assert.Empty(load.Issues);
        Assert.Empty(CompanionValidator.Validate(load.Packages));
        Assert.Contains(load.Packages, p => p.Id == "test_companion");
    }

    [Fact]
    public void Placeholder_uses_only_vanilla_base_game_ids()
    {
        var c = CompanionLoader.LoadAll(CompanionsRoot).Packages.Single(p => p.Id == "test_companion").Companion;

        Assert.Contains(c.Culture, MainCultures);
        Assert.All(c.Skills!.Keys, k => Assert.Contains(k, VanillaSkills));
        Assert.All(c.Traits!.Keys, k => Assert.Contains(k, PersonalityTraits));
        Assert.Equal("npc_wanderer_equipment_template_empire", c.Equipment!.TemplateId);
    }

    [Fact]
    public void Placeholder_dialogs_cover_required_slots_and_one_translation()
    {
        var lines = CompanionLoader.LoadAll(CompanionsRoot).Packages.Single(p => p.Id == "test_companion").Dialogs.Lines!;

        Assert.All(DialogSlots.Required, slot => Assert.Contains(lines, l => l.Slot == slot));
        Assert.Single(lines, l => l.Text!.ContainsKey("tr"));
        Assert.All(lines.Where(l => l.Voiced), l => Assert.Contains("Captain", l.Text!["en"]));
        Assert.All(lines, l => Assert.Null(l.Audio)); // no audio this run
    }
}
