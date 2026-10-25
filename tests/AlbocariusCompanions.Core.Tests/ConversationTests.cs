using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Models;
using Xunit;

namespace AlbocariusCompanions.Core.Tests;

public class ConversationGraphTests
{
    private static ConversationNode Node(string id, string speaker, params string[] next) =>
        new ConversationNode { Id = id, Speaker = speaker, Next = next.ToList() };

    private static ConversationDefinition Conversation(string entry, string[] start, params ConversationNode[] nodes) =>
        new ConversationDefinition { Id = "talk", Entry = entry, Start = start.ToList(), Nodes = nodes.ToList() };

    private static List<(string Id, string In, string Out, bool Start)> Edges(ConversationDefinition c) =>
        ConversationGraph.Build("jeremus", c).Select(e => (e.SentenceId, e.InputToken, e.OutputToken, e.IsStart)).ToList();

    [Fact]
    public void Menu_conversation_starts_on_the_hub_and_chains_node_tokens()
    {
        var c = Conversation("hub", new[] { "p_ask" },
            Node("p_ask", "player", "story"),
            Node("story", "npc", "p_ok"),
            Node("p_ok", "player", "end"));

        Assert.Equal(new List<(string, string, string, bool)>
        {
            ("albocarius:jeremus:talk:start:p_ask", "hero_main_options", "albocarius:jeremus:p_ask", true),
            ("albocarius:jeremus:talk:p_ask:story", "albocarius:jeremus:p_ask", "albocarius:jeremus:story", false),
            ("albocarius:jeremus:talk:story:p_ok", "albocarius:jeremus:story", "close_window", false),
        }, Edges(c));
    }

    [Fact]
    public void Greeting_starts_on_the_conversation_start_token()
    {
        var c = Conversation("greeting", new[] { "hello" }, Node("hello", "npc", "hub"));
        var edge = Assert.Single(ConversationGraph.Build("jeremus", c));
        Assert.Equal("start", edge.InputToken);
        Assert.Equal("hero_main_options", edge.OutputToken); // NPC line → straight back to the menu
        Assert.False(edge.IsPlayer);
    }

    [Fact]
    public void Player_line_back_to_the_hub_goes_through_the_companion_pretalk()
    {
        var c = Conversation("hub", new[] { "p_bye" }, Node("p_bye", "player", "hub"));
        Assert.Equal("lord_pretalk", Assert.Single(ConversationGraph.Build("jeremus", c)).OutputToken);
    }

    [Fact]
    public void A_node_with_two_parents_is_registered_once_per_parent()
    {
        var c = Conversation("hub", new[] { "p_a", "p_b" },
            Node("p_a", "player", "reply"),
            Node("p_b", "player", "reply"),
            Node("reply", "npc", "hub"));

        var replies = ConversationGraph.Build("jeremus", c).Where(e => e.Node.Id == "reply").ToList();
        Assert.Equal(new[] { "albocarius:jeremus:p_a", "albocarius:jeremus:p_b" }, replies.Select(e => e.InputToken));
        Assert.Equal(2, ConversationGraph.Build("jeremus", c).Count(e => e.IsStart));
    }

    [Fact]
    public void Node_tokens_cannot_collide_across_conversations_or_companions()
    {
        Assert.NotEqual(ConversationGraph.NodeToken("a", "b_c"), ConversationGraph.NodeToken("a_b", "c"));
        Assert.DoesNotContain(':', "albocarius_a_b_c");
    }
}

public class ConversationConditionsTests
{
    private sealed class FakeContext : IConversationContext
    {
        public bool HasMet { get; set; } = true;
        public bool IsHired { get; set; }
        public bool IsInPlayerParty { get; set; }
        public int Relation { get; set; }
        public int PlayerGold { get; set; } = 1000;
        public bool ClanIsFull { get; set; }
        public HashSet<string> Flags { get; } = new HashSet<string>();
        public bool HasFlag(string flag) => Flags.Contains(flag);
    }

    [Fact]
    public void No_condition_holds() => Assert.True(ConversationConditions.Holds(null, new FakeContext()));

    [Fact]
    public void Each_condition_is_checked()
    {
        var context = new FakeContext { IsHired = true, IsInPlayerParty = true, Relation = 10, PlayerGold = 100 };
        context.Flags.Add("told");

        Assert.True(ConversationConditions.Holds(new ConditionSet { Met = true, Hired = true, InParty = true, MinRelation = 10,
            MaxRelation = 10, MinGold = 100, Flags = new List<string> { "told" }, NotFlags = new List<string> { "other" } }, context));

        Assert.False(ConversationConditions.Holds(new ConditionSet { Met = false }, context));
        Assert.False(ConversationConditions.Holds(new ConditionSet { Hired = false }, context));
        Assert.False(ConversationConditions.Holds(new ConditionSet { InParty = false }, context));
        Assert.False(ConversationConditions.Holds(new ConditionSet { MinRelation = 11 }, context));
        Assert.False(ConversationConditions.Holds(new ConditionSet { MaxRelation = 9 }, context));
        Assert.False(ConversationConditions.Holds(new ConditionSet { MinGold = 101 }, context));
        Assert.False(ConversationConditions.Holds(new ConditionSet { Flags = new List<string> { "missing" } }, context));
        Assert.False(ConversationConditions.Holds(new ConditionSet { NotFlags = new List<string> { "told" } }, context));
    }

    [Fact]
    public void Payment_needs_the_gold_and_receiving_needs_nothing()
    {
        var context = new FakeContext { PlayerGold = 99 };
        Assert.False(ConversationConditions.EffectsCanApply(new EffectSet { Gold = -100 }, context));
        context.PlayerGold = 100;
        Assert.True(ConversationConditions.EffectsCanApply(new EffectSet { Gold = -100 }, context));
        Assert.True(ConversationConditions.EffectsCanApply(new EffectSet { Gold = 500 }, new FakeContext { PlayerGold = 0 }));
    }

    [Fact]
    public void Joining_needs_an_unhired_companion_and_room_in_the_clan()
    {
        var join = new EffectSet { Join = true };
        Assert.True(ConversationConditions.EffectsCanApply(join, new FakeContext()));
        Assert.False(ConversationConditions.EffectsCanApply(join, new FakeContext { IsHired = true }));
        Assert.False(ConversationConditions.EffectsCanApply(join, new FakeContext { ClanIsFull = true }));
    }

    private static (ConversationDefinition Conversation, ConversationEdge Start, ConversationEdge Inner) Talk(string entry, bool once,
        ConditionSet? when)
    {
        var first = new ConversationNode { Id = "first", Speaker = entry == "greeting" ? "npc" : "player", Next = new List<string> { "second" } };
        var second = new ConversationNode { Id = "second", Speaker = "npc", Next = new List<string> { "end" } };
        var c = new ConversationDefinition { Id = "talk", Entry = entry, Once = once, When = when,
            Start = new List<string> { "first" }, Nodes = new List<ConversationNode> { first, second } };
        var edges = ConversationGraph.Build("jeremus", c);
        return (c, edges.Single(e => e.IsStart), edges.Single(e => !e.IsStart));
    }

    [Fact]
    public void Once_hides_the_start_after_the_first_time_but_not_the_rest()
    {
        var (c, start, inner) = Talk("hub", once: true, when: null);
        var context = new FakeContext();
        Assert.True(ConversationConditions.CanSay(c, start, context, alreadyStarted: false));
        Assert.False(ConversationConditions.CanSay(c, start, context, alreadyStarted: true));
        Assert.True(ConversationConditions.CanSay(c, inner, context, alreadyStarted: true));
    }

    [Fact]
    public void Conversation_condition_applies_to_the_start_only()
    {
        var (c, start, inner) = Talk("hub", once: false, when: new ConditionSet { Hired = true });
        var context = new FakeContext { IsHired = false };
        Assert.False(ConversationConditions.CanSay(c, start, context, false));
        Assert.True(ConversationConditions.CanSay(c, inner, context, false));
    }

    [Fact]
    public void Greeting_needs_a_companion_already_met()
    {
        var (c, start, _) = Talk("greeting", once: false, when: null);
        Assert.False(ConversationConditions.CanSay(c, start, new FakeContext { HasMet = false }, false));
        Assert.True(ConversationConditions.CanSay(c, start, new FakeContext { HasMet = true }, false));
    }
}

public class ConversationFlagsTests
{
    [Fact]
    public void Flags_are_scoped_per_companion_and_round_trip_through_the_save_list()
    {
        var flags = new ConversationFlags();
        flags.Set("a", "told");
        flags.MarkStarted("a", "talk");
        flags.Set("b", "other");
        flags.Clear("b", "other");

        Assert.True(flags.Has("a", "told"));
        Assert.False(flags.Has("b", "told"));
        Assert.False(flags.Has("b", "other"));
        Assert.True(flags.HasStarted("a", "talk"));
        Assert.False(flags.Has("a", "talk")); // a started marker is not a flag

        var restored = new ConversationFlags();
        restored.Load(flags.ToList());
        Assert.Equal(flags.ToList(), restored.ToList());
        Assert.Equal(new[] { "a#started:talk", "a:told" }, restored.ToList());
    }

    [Fact]
    public void Loading_nothing_empties_the_flags()
    {
        var flags = new ConversationFlags();
        flags.Set("a", "x");
        flags.Load(null);
        Assert.Equal(0, flags.Count);
    }
}
