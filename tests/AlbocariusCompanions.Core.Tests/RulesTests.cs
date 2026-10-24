using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;
using Newtonsoft.Json;

namespace AlbocariusCompanions.Core.Tests;

public class DialogSlotsTests
{
    [Fact]
    public void Required_slots_match_spec()
    {
        Assert.Equal(new[] { "intro", "backstory", "hire_offer", "hire_accept", "hire_decline" }, DialogSlots.Required);
    }

    [Fact]
    public void All_contains_required_and_optional_without_duplicates()
    {
        Assert.Equal(10, DialogSlots.All.Count);
        Assert.Equal(DialogSlots.All.Count, DialogSlots.All.Distinct().Count());
        Assert.All(DialogSlots.Optional, s => Assert.False(DialogSlots.IsRequired(s)));
    }

    [Theory]
    [InlineData("idle", true)]
    [InlineData("hire_offer", true)]
    [InlineData("Intro", false)]
    [InlineData("greeting", false)]
    [InlineData(null, false)]
    public void IsKnown(string? slot, bool expected) => Assert.Equal(expected, DialogSlots.IsKnown(slot));
}

public class LineIdsTests
{
    [Fact]
    public void Format_pads_to_two_digits() => Assert.Equal("test_companion_intro_01", LineIds.Format("test_companion", "intro", 1));

    [Theory]
    [InlineData("test_companion_intro_01", true)]
    [InlineData("test_companion_intro_12", true)]
    [InlineData("test_companion_intro_1", false)]
    [InlineData("test_companion_intro_001", false)]
    [InlineData("test_companion_backstory_01", false)]
    [InlineData("other_intro_01", false)]
    [InlineData(null, false)]
    public void IsValid(string? id, bool expected) => Assert.Equal(expected, LineIds.IsValid(id, "test_companion", "intro"));

    [Fact]
    public void Slot_with_underscore_is_matched_literally() =>
        Assert.True(LineIds.IsValid("a_hire_offer_03", "a", "hire_offer"));

    [Fact]
    public void Default_audio_path() => Assert.Equal("audio/x_intro_01.ogg", LineIds.DefaultAudioPath("x_intro_01"));
}

public class CompanionRulesTests
{
    [Theory]
    [InlineData("Welcome, Captain.", false)]
    [InlineData("Hello {PLAYER_NAME}.", true)]
    [InlineData("Hello {PLAYER.NAME}.", true)]
    [InlineData("Hello {player.name}.", true)]
    [InlineData("Hello { PLAYER }.", true)]
    [InlineData("Hello {NPC_NAME}.", false)]
    public void Player_token_detection(string text, bool expected) =>
        Assert.Equal(expected, CompanionRules.PlayerTokenPattern.IsMatch(text));

    [Theory]
    [InlineData("en", true)]
    [InlineData("tr", true)]
    [InlineData("pt-br", true)]
    [InlineData("EN", false)]
    [InlineData("english", false)]
    [InlineData("", false)]
    public void Language_codes(string code, bool expected) =>
        Assert.Equal(expected, CompanionRules.LanguageCodePattern.IsMatch(code));

    [Theory]
    [InlineData("test_companion", true)]
    [InlineData("a1_b2", true)]
    [InlineData("Test", false)]
    [InlineData("with-dash", false)]
    [InlineData("", false)]
    public void Id_pattern(string id, bool expected) => Assert.Equal(expected, CompanionRules.IdPattern.IsMatch(id));

    [Fact]
    public void Voice_types_are_the_vanilla_persona_traits() =>
        Assert.Equal(new[] { "curt", "earnest", "ironic", "softspoken" }, CompanionRules.VoiceTypes);
}

public class ModelJsonTests
{
    [Fact]
    public void Spec_example_companion_deserializes()
    {
        const string json = """
        {
          "id": "test_companion", "name": "Test Companion", "isFemale": false, "voice": "earnest", "age": 32,
          "culture": "empire", "bodyProperties": null,
          "attributes": { "vigor": 4, "control": 3, "endurance": 4, "cunning": 2, "social": 2, "intelligence": 2 },
          "skills": { "OneHanded": 100 }, "traits": { "Valor": 1 },
          "equipment": { "templateId": "x" },
          "hireCost": 500, "voiceProfile": { "reference": null, "style": null },
          "likes": [], "dislikes": [], "relations": [], "requiredModules": ["NavalDLC"]
        }
        """;
        var c = JsonConvert.DeserializeObject<CompanionDefinition>(json)!;
        Assert.Equal("test_companion", c.Id);
        Assert.Equal(4, c.Attributes!["vigor"]);
        Assert.Equal(100, c.Skills!["OneHanded"]);
        Assert.Equal("x", c.Equipment!.TemplateId);
        Assert.Equal(500, c.HireCost);
        Assert.Empty(c.Relations!);
        Assert.Equal(new[] { "NavalDLC" }, c.RequiredModules);
    }

    [Fact]
    public void Spec_example_dialog_line_deserializes()
    {
        const string json = """
        { "lines": [ { "id": "test_companion_intro_01", "slot": "intro", "text": { "en": "Hi", "tr": "Selam" }, "voiced": true, "audio": "audio/test_companion_intro_01.ogg" } ] }
        """;
        var line = JsonConvert.DeserializeObject<DialogFile>(json)!.Lines!.Single();
        Assert.Equal("intro", line.Slot);
        Assert.Equal("Selam", line.Text!["tr"]);
        Assert.True(line.Voiced);
    }
}
