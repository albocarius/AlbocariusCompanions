using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Core.Tests;

// AuthoringChecks.Validate works on in-memory packages; the end-to-end wiring in CompanionCatalog is covered by
// the acceptance tests.
public class AuthoringChecksTests
{
    private static CompanionPackage Package(string folder, string dialogsJson) => new(folder, folder,
        JObject.Parse("""
            { "schemaVersion": 1, "name": "T", "isFemale": false, "voice": "earnest", "age": 32, "culture": "empire",
              "attributes": { "vigor": 4, "control": 3, "endurance": 4, "cunning": 2, "social": 2, "intelligence": 2 },
              "skills": { "OneHanded": 100 }, "equipment": { "templateId": "t" }, "hireCost": 500 }
            """).ToObject<CompanionDefinition>()!,
        JObject.Parse(dialogsJson).ToObject<DialogFile>()!);

    // ---- variable never changes ----

    [Fact]
    public void Never_written_variable_at_its_type_default_warns_on_the_owner()
    {
        var one = Package("one", """
        { "schemaVersion": 1,
          "variables": { "asked": { "type": "bool" }, "count": { "type": "int", "default": 0 },
                         "kept": { "type": "bool", "default": true }, "seen": { "type": "int", "default": 2 } },
          "conversations": [ { "id": "c", "entry": "hub", "start": ["a"], "nodes": [
            { "id": "a", "speaker": "player", "next": ["end"] } ] } ] }
        """);

        var issues = AuthoringChecks.Validate(new[] { one });

        Assert.Equal(2, issues.Count);
        Assert.All(issues, i =>
        {
            Assert.False(i.IsError);
            Assert.Equal(IssueCodes.VariableNeverChanges, i.Code);
            Assert.Equal("one", i.Folder);
        });
        Assert.Contains(issues, i => i.Message.Contains("'asked'"));
        Assert.Contains(issues, i => i.Message.Contains("'count'"));
    }

    [Fact]
    public void A_write_anywhere_in_the_loaded_set_counts_when_it_resolves_to_the_owner()
    {
        var owner = Package("one", """
        { "schemaVersion": 1,
          "variables": { "flag_x": { "type": "bool", "scope": "shared" }, "flag_y": { "type": "bool", "scope": "shared" } },
          "conversations": [ { "id": "c", "entry": "hub", "start": ["a"], "nodes": [
            { "id": "a", "speaker": "player", "next": ["end"] } ] } ] }
        """);
        // "one.flag_x" resolves to the owner's variable; a bare "flag_y" in "two" is "two"'s own and never writes the owner's.
        var writer = Package("two", """
        { "schemaVersion": 1,
          "conversations": [ { "id": "c", "entry": "hub", "start": ["a"], "nodes": [
            { "id": "a", "speaker": "npc", "next": ["end"], "effects": { "setFlags": ["one.flag_x", "flag_y"] } } ] } ] }
        """);

        var issues = AuthoringChecks.Validate(new[] { owner, writer });

        var warning = Assert.Single(issues);
        Assert.Equal(IssueCodes.VariableNeverChanges, warning.Code);
        Assert.Equal("one", warning.Folder);
        Assert.Contains("'flag_y'", warning.Message);
    }

    // ---- transfer without a matching check ----

    /// <summary>a → b (the transfer node, with <paramref name="bEffects"/>) and the sibling branch a → a2.</summary>
    private static CompanionPackage Transfer(string folder, string conversationWhen, string aWhen, string a2When,
        string bWhen, string bEffects) =>
        Package(folder, $$"""
        { "schemaVersion": 1, "conversations": [ { "id": "c", "entry": "hub", "when": {{conversationWhen}},
            "start": ["a"], "nodes": [
            { "id": "a", "speaker": "player", "next": ["b", "a2"], "when": {{aWhen}} },
            { "id": "a2", "speaker": "npc", "next": ["end"], "when": {{a2When}} },
            { "id": "b", "speaker": "player", "next": ["end"], "when": {{bWhen}}, "effects": {{bEffects}} } ] } ] }
        """);

    [Theory]
    [InlineData("""{ "hired": true }""", "{}", "{}", "{}", """{ "transfer": { "gold": 100, "from": "world", "to": "player" } }""")] // source is world
    [InlineData("""{ "hired": true }""", """{ "hasGold": { "who": "player", "min": 100 } }""", "{}", "{}", """{ "transfer": { "gold": 100, "from": "player", "to": "self" } }""")] // ancestor check
    [InlineData("""{ "minGold": 120 }""", "{}", "{}", "{}", """{ "transfer": { "gold": 100, "from": "player", "to": "self" } }""")] // conversation when
    [InlineData("""{ "hired": true }""", "{}", "{}", """{ "minGold": 100 }""", """{ "transfer": { "gold": 100, "from": "player", "to": "self" } }""")] // own when
    [InlineData("""{ "hired": true }""", "{}", "{}", """{ "all": [ { "all": [ { "hasGold": { "who": "player", "min": 100 } } ] } ] }""", """{ "transfer": { "gold": 100, "from": "player", "to": "self" } }""")] // nested all
    [InlineData("""{ "hired": true }""", """{ "hasGold": { "who": "one", "min": 100 } }""", "{}", "{}", """{ "transfer": { "gold": 100, "from": "self", "to": "player" } }""")] // own id names self
    [InlineData("""{ "hired": true }""", "{}", "{}", """{ "hasItem": { "who": "player", "id": "grain", "min": 2 } }""", """{ "transfer": { "item": "grain", "count": 2, "from": "player", "to": "world" } }""")]
    [InlineData("""{ "hired": true }""", "{}", "{}", """{ "hasItem": { "who": "player", "id": "grain" } }""", """{ "transfer": { "item": "grain", "from": "player", "to": "world" } }""")] // count defaults to 1
    public void Transfer_with_a_matching_check_or_a_world_source_is_fine(string conversationWhen, string aWhen, string a2When, string bWhen, string bEffects) =>
        Assert.Empty(AuthoringChecks.Validate(new[] { Transfer("one", conversationWhen, aWhen, a2When, bWhen, bEffects) }));

    [Theory]
    [InlineData("""{ "hired": true }""", "{}", "{}", "{}", """{ "transfer": { "gold": 100, "from": "player", "to": "self" } }""")] // no check at all
    [InlineData("""{ "hired": true }""", "{}", "{}", """{ "hasGold": { "who": "player", "min": 99 } }""", """{ "transfer": { "gold": 100, "from": "player", "to": "self" } }""")] // too little
    [InlineData("""{ "hired": true }""", "{}", "{}", """{ "minGold": 99 }""", """{ "gold": -100 }""")] // old gold sugar
    [InlineData("""{ "hired": true }""", "{}", "{}", """{ "hasGold": { "who": "self", "min": 100 } }""", """{ "transfer": { "gold": 100, "from": "player", "to": "self" } }""")] // wrong actor
    [InlineData("""{ "hired": true }""", "{}", """{ "hasGold": { "who": "player", "min": 100 } }""", "{}", """{ "transfer": { "gold": 100, "from": "player", "to": "self" } }""")] // check on the sibling branch only
    [InlineData("""{ "hired": true }""", "{}", "{}", """{ "any": [ { "hasGold": { "who": "player", "min": 100 } } ] }""", """{ "transfer": { "gold": 100, "from": "player", "to": "self" } }""")] // inside any
    [InlineData("""{ "hired": true }""", "{}", "{}", """{ "not": { "not": { "hasGold": { "who": "player", "min": 100 } } } }""", """{ "transfer": { "gold": 100, "from": "player", "to": "self" } }""")] // inside not
    [InlineData("""{ "hired": true }""", "{}", "{}", """{ "hasItem": { "who": "player", "id": "grain", "min": 1 } }""", """{ "transfer": { "item": "grain", "count": 2, "from": "player", "to": "world" } }""")] // too few items
    [InlineData("""{ "hired": true }""", "{}", "{}", """{ "hasItem": { "who": "player", "id": "wine", "min": 5 } }""", """{ "transfer": { "item": "grain", "count": 2, "from": "player", "to": "world" } }""")] // other item
    public void Transfer_without_a_matching_check_warns(string conversationWhen, string aWhen, string a2When, string bWhen, string bEffects)
    {
        var issue = Assert.Single(AuthoringChecks.Validate(new[] { Transfer("one", conversationWhen, aWhen, a2When, bWhen, bEffects) }));

        Assert.Equal(IssueCodes.TransferNotChecked, issue.Code);
        Assert.False(issue.IsError);
        Assert.Equal("one", issue.Folder);
        Assert.Contains("node 'b'", issue.Message);
        Assert.Contains("without a matching", issue.Message);
    }

    [Fact]
    public void The_message_names_the_conversation_the_transfer_and_the_needed_check()
    {
        var issue = Assert.Single(AuthoringChecks.Validate(new[] {
            Transfer("one", """{ "hired": true }""", "{}", "{}", "{}", """{ "transfer": { "gold": 100, "from": "player", "to": "self" } }""") }));

        Assert.Equal("Conversation 'c' node 'b' transfers 100 gold from 'player' without a matching hasGold (min 100) or minGold on its path.",
            issue.Message);
    }
}
