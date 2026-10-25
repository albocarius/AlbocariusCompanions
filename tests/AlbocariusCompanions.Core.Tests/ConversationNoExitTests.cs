using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Validation;
using Xunit;

namespace AlbocariusCompanions.Core.Tests;

/// <summary>Unit tests for the no-exit rule of ConversationChecks (task 73; the acceptance tests are the spec).</summary>
public class ConversationNoExitTests
{
    private static ConversationNode Node(string id, string speaker, params string[] next) =>
        new ConversationNode { Id = id, Speaker = speaker, Next = next.ToList() };

    /// <summary>Validates one menu conversation that starts at the first node.</summary>
    private static List<ValidationIssue> Issues(params ConversationNode[] nodes)
    {
        var conversation = new ConversationDefinition { Id = "talk", Entry = "hub",
            Start = new List<string> { nodes[0].Id! }, Nodes = nodes.ToList() };
        var dialogs = new DialogFile { Conversations = new List<ConversationDefinition> { conversation } };
        var package = new CompanionPackage("jeremus", "/m/Companions/jeremus", new CompanionDefinition { Name = "N" }, dialogs);

        var issues = new List<ValidationIssue>();
        ConversationChecks.Validate(package, new HashSet<string>(), new HashSet<string>(), issues);
        return issues;
    }

    [Fact]
    public void A_loop_that_reaches_the_hub_has_no_no_exit_error() =>
        Assert.DoesNotContain(Issues(
            Node("p_ask", "player", "story"),
            Node("story", "npc", "p_more", "p_enough"),
            Node("p_more", "player", "story"),
            Node("p_enough", "player", "hub")), i => i.Code == IssueCodes.ConversationNoExit);

    [Fact]
    public void A_trapped_loop_is_one_error_naming_its_nodes_in_file_order()
    {
        var issues = Issues(
            Node("p_ask", "player", "story"),
            Node("story", "npc", "p_more", "p_enough"),
            Node("p_more", "player", "again"),
            Node("again", "npc", "again"),
            Node("p_enough", "player", "hub"));

        var error = Assert.Single(issues, i => i.Code == IssueCodes.ConversationNoExit);
        Assert.Equal("jeremus", error.Folder);
        Assert.Equal("Conversation 'talk': nodes 'p_more', 'again' can never reach 'hub' or 'end'; add a way out.", error.Message);
        Assert.DoesNotContain(issues, i => i.Code == IssueCodes.UnreachableNode);
    }

    [Fact]
    public void A_node_without_next_is_reported_once_and_counts_as_a_way_out()
    {
        var broken = Node("p_end", "player");
        broken.Next = new List<string>();

        var issues = Issues(
            Node("p_ask", "player", "story"),
            Node("story", "npc", "p_end"),
            broken);

        Assert.DoesNotContain(issues, i => i.Code == IssueCodes.ConversationNoExit);
        var error = Assert.Single(issues, i => i.IsError);
        Assert.Equal(IssueCodes.InvalidConversation, error.Code);
        Assert.Contains("'next'", error.Message);
    }
}
