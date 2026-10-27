using AlbocariusCompanions.Core.Loading;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Registry;
using AlbocariusCompanions.Core.Rules;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 93 (M9 I4): optional companion.json "noble" (bool, default false). Schema, model, validation only.
public class NobleTests
{
    static LoadResult Load(JToken? noble)
    {
        using var tmp = new TempDir();
        var companion = Fixtures.Companion("noble_one");
        if (noble != null)
            companion["noble"] = noble;
        Fixtures.Write(tmp.Path, "noble_one", companion, Fixtures.Dialogs("noble_one"));
        return CompanionLoader.LoadAll(tmp.Path);
    }

    [Fact]
    public void Absent_is_false()
    {
        var result = Load(null);
        Assert.Empty(result.Issues);
        Assert.False(Assert.Single(result.Packages).Companion.Noble);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_bool_is_read(bool value)
    {
        var result = Load(value);
        Assert.Empty(result.Issues);
        Assert.Equal(value, Assert.Single(result.Packages).Companion.Noble);
    }

    [Fact]
    public void A_valid_noble_companion_passes_validation()
    {
        using var tmp = new TempDir();
        var companion = Fixtures.Companion("noble_one");
        companion["noble"] = true;
        Fixtures.Write(tmp.Path, "noble_one", companion, Fixtures.Dialogs("noble_one"));
        Assert.Empty(CompanionCatalog.Load(tmp.Path).Issues);
    }

    [Fact]
    public void A_non_bool_is_an_error()
    {
        foreach (var bad in new JToken[] { "yes", new JObject(), new JArray(true) })
        {
            var result = Load(bad);
            Assert.Empty(result.Packages);
            var issue = Assert.Single(result.Issues);
            Assert.True(issue.IsError);
            Assert.Equal(IssueCodes.InvalidJson, issue.Code);
        }
    }

    [Fact]
    public void Template_declares_noble_false()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "AlbocariusCompanions.sln"))) root = root.Parent;
        var template = JObject.Parse(File.ReadAllText(Path.Combine(root!.FullName, "tools", "companion-template", "companion.json")));
        Assert.Equal(JTokenType.Boolean, template["noble"]!.Type);
        Assert.False((bool)template["noble"]!);
    }
    // M10: the noble mechanic. Commoner made a lord → player loses 4 with each affected clan leader of the player's kingdom.
    [Fact]
    public void Commoner_costs_relation_noble_does_not()
    {
        Assert.True(NobleRules.CostsRelation(new CompanionDefinition { Noble = false }));
        Assert.False(NobleRules.CostsRelation(new CompanionDefinition { Noble = true }));
        Assert.True(NobleRules.CostsRelation(new CompanionDefinition()));
    }

    [Fact]
    public void Change_is_a_fixed_minus_four_with_a_message()
    {
        Assert.Equal(-4, NobleRules.CommonerRaisedRelationChange);
        Assert.Equal("The lords of the realm frown upon raising a commoner to nobility.", NobleRules.CommonerRaisedMessage);
    }

    [Fact]
    public void Only_other_living_non_mercenary_clans_are_affected()
    {
        Assert.True(NobleRules.IsAffectedClan(isPlayerClan: false, isNewClan: false, isEliminated: false, isMercenary: false, hasLivingLeader: true));
        Assert.False(NobleRules.IsAffectedClan(isPlayerClan: true, isNewClan: false, isEliminated: false, isMercenary: false, hasLivingLeader: true));
        Assert.False(NobleRules.IsAffectedClan(isPlayerClan: false, isNewClan: true, isEliminated: false, isMercenary: false, hasLivingLeader: true));
        Assert.False(NobleRules.IsAffectedClan(isPlayerClan: false, isNewClan: false, isEliminated: true, isMercenary: false, hasLivingLeader: true));
        Assert.False(NobleRules.IsAffectedClan(isPlayerClan: false, isNewClan: false, isEliminated: false, isMercenary: true, hasLivingLeader: true));
        Assert.False(NobleRules.IsAffectedClan(isPlayerClan: false, isNewClan: false, isEliminated: false, isMercenary: false, hasLivingLeader: false));
    }

    [Fact]
    public void Game_layer_hooks_companion_clan_creation_and_changes_player_relation_only()
    {
        var game = Path.Combine(RepoPaths.Root, "src", "AlbocariusCompanions.Game");
        var behavior = File.ReadAllText(Path.Combine(game, "AlbocariusCompanionsCampaignBehavior.cs"));
        Assert.Contains("CampaignEvents.OnClanCreatedEvent.AddNonSerializedListener", behavior);
        Assert.Contains("NoblePenalty.OnClanCreated", behavior);

        var penalty = File.ReadAllText(Path.Combine(game, "NoblePenalty.cs"));
        Assert.Contains("if (!isCompanion)", penalty);
        Assert.Contains("ModRuntime.IsModHero(lord)", penalty);
        Assert.Contains("NobleRules.CostsRelation(package.Companion)", penalty);
        Assert.Contains("Clan.PlayerClan?.Kingdom", penalty);
        Assert.Contains("ChangeRelationAction.ApplyPlayerRelation(leader, NobleRules.CommonerRaisedRelationChange", penalty);
        // The new lord's relations are untouched: no hero-to-hero write.
        Assert.DoesNotContain("ApplyRelationChangeBetweenHeroes", penalty);
        Assert.DoesNotContain("SetPersonalRelation", penalty);
        Assert.Contains("NobleRules.CommonerRaisedMessage", penalty);
    }
}
