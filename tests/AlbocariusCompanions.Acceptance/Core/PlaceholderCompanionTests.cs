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
        // M7 I8: the sample is complete in every language and passes validation with zero warnings.
        Assert.Empty(CompanionValidator.Validate(load.Packages));
        Assert.Contains(load.Packages, p => p.Id == "test_companion");
        Assert.DoesNotContain(load.Packages, p => p.FolderName == "_shared");
    }

    [Fact]
    public void Placeholder_has_its_own_player_lines_and_example_conversations()
    {
        var package = AlbocariusCompanions.Core.Registry.CompanionCatalog.Load(CompanionsRoot).Registry.All.Single(p => p.Id == "test_companion");
        Assert.All(PlayerSlots.All, slot => Assert.Contains(package.Dialogs.Lines!, l => l.Slot == slot));
        Assert.Equal(new[] { "the_scar", "spare_coin", "after_scar" }, package.Conversations.Select(c => c.Id));
        Assert.False(Directory.Exists(Path.Combine(CompanionsRoot, "_shared")));
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
    public void Placeholder_exercises_fixed_equipment_with_vanilla_items()
    {
        // Vanilla SandBoxCore items: monk_robe (BodyArmor), pilgrim_hood (HeadArmor), bo_staff (Polearm).
        var eq = CompanionLoader.LoadAll(CompanionsRoot).Packages.Single(p => p.Id == "test_companion").Companion.Equipment!;

        Assert.Equal(new Dictionary<string, string> { ["Body"] = "monk_robe", ["Head"] = "pilgrim_hood", ["Item0"] = "bo_staff" }, eq.Battle);
        Assert.Equal(new Dictionary<string, string> { ["Body"] = "monk_robe", ["Head"] = "pilgrim_hood" }, eq.Civilian);
    }

    [Fact]
    public void Placeholder_dialogs_cover_required_slots_and_one_translation()
    {
        var package = CompanionLoader.LoadAll(CompanionsRoot).Packages.Single(p => p.Id == "test_companion");
        var lines = package.Dialogs.Lines!;

        Assert.All(DialogSlots.Required, slot => Assert.Contains(lines, l => l.Slot == slot));
        Assert.All(lines, l => Assert.Equal("test_companion_" + l.Id, l.FullId));
        Assert.Equal("Selam, Kaptan. Ben yer tutucu bir yoldaşım.", package.Text("intro_01", "tr"));
        Assert.Equal("Başka zaman, öyleyse.", package.Text("hire_decline_01", "tr"));
        Assert.All(lines.Where(l => l.Voiced), l => Assert.Contains("Captain", package.Text(l.Id!, "en")));
        Assert.All(lines, l => Assert.Null(l.Audio)); // default audio/<local id>.ogg
    }
}
