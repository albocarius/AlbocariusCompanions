using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Models;
using Xunit;

namespace AlbocariusCompanions.Core.Tests;

public class TransferRuleTests
{
    [Fact]
    public void The_explicit_transfer_of_a_node_is_returned_as_is()
    {
        var transfer = new TransferEffect { Item = "grain", Count = 3, From = "world", To = "player" };
        var effects = new EffectSet { Transfer = transfer };
        Assert.Same(transfer, Transfers.Of(effects));
    }

    [Fact]
    public void The_gold_key_is_sugar_for_a_player_world_transfer()
    {
        var pays = Transfers.Of(new EffectSet { Gold = -100 });
        Assert.Equal(100, pays.Gold);
        Assert.Equal(Transfers.Player, pays.From);
        Assert.Equal(Transfers.World, pays.To);

        var receives = Transfers.Of(new EffectSet { Gold = 50 });
        Assert.Equal(50, receives.Gold);
        Assert.Equal(Transfers.World, receives.From);
        Assert.Equal(Transfers.Player, receives.To);
    }

    [Fact]
    public void A_node_that_transfers_nothing_has_no_transfer()
    {
        Assert.Null(Transfers.Of(null));
        Assert.Null(Transfers.Of(new EffectSet()));
        Assert.Null(Transfers.Of(new EffectSet { Gold = 0 }));
        Assert.Null(Transfers.Of(new EffectSet { Relation = 5 }));
    }

    [Fact]
    public void An_explicit_transfer_wins_over_the_gold_shorthand()
    {
        // Loading them together is a validation error; Of just never applies the shorthand then.
        var transfer = new TransferEffect { Gold = 5, From = Transfers.World, To = Transfers.World };
        Assert.Same(transfer, Transfers.Of(new EffectSet { Gold = -50, Transfer = transfer }));
    }

    [Fact]
    public void The_item_count_defaults_to_one()
    {
        Assert.Equal(1, Transfers.CountOf(new TransferEffect { Item = "grain", From = Transfers.Player, To = Transfers.World }));
        Assert.Equal(3, Transfers.CountOf(new TransferEffect { Item = "grain", Count = 3, From = Transfers.Player, To = Transfers.World }));
    }

    [Theory]
    [InlineData("player", true)]
    [InlineData("self", true)]
    [InlineData("world", true)]
    [InlineData("jeremus", true)]
    [InlineData("jeremus_2", true)]
    [InlineData("Player", false)]
    [InlineData("the player", false)]
    [InlineData("jeremus-2", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Actors_are_the_fixed_words_and_companion_ids(string actor, bool valid) =>
        Assert.Equal(valid, Transfers.IsValidActor(actor));

    [Fact]
    public void A_companion_talking_about_itself_by_id_means_self()
    {
        Assert.Equal(Transfers.Self, Transfers.NormalizeActor("jeremus", "jeremus"));
        Assert.Equal("ahmet", Transfers.NormalizeActor("ahmet", "jeremus"));
        Assert.Equal(Transfers.World, Transfers.NormalizeActor(Transfers.World, "jeremus"));
        Assert.Null(Transfers.NormalizeActor(null, "jeremus"));
    }
}

public class TransferChecksTests
{
    [Fact]
    public void Nested_hasGold_about_a_missing_companion_is_a_warning()
    {
        var when = new AlbocariusCompanions.Core.Models.ConditionSet
        {
            Any = new System.Collections.Generic.List<AlbocariusCompanions.Core.Models.ConditionSet>
            {
                new() { Not = new() { HasGold = new AlbocariusCompanions.Core.Models.GoldCheck { Who = "jeremus", Min = 1 } } },
            },
        };
        var dialogs = new AlbocariusCompanions.Core.Models.DialogFile
        {
            Conversations = new() { new() { Id = "talk", When = when } },
        };
        var package = new AlbocariusCompanions.Core.Models.CompanionPackage("ahmet", "/x/ahmet", new(), dialogs);

        var warning = Xunit.Assert.Single(AlbocariusCompanions.Core.Validation.TransferChecks.CrossCompanion(new[] { package }));
        Xunit.Assert.Equal(AlbocariusCompanions.Core.Validation.IssueCodes.MissingCompanionReference, warning.Code);
    }
}
