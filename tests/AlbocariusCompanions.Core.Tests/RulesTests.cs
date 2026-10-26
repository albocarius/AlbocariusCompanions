using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;
using Newtonsoft.Json;

namespace AlbocariusCompanions.Core.Tests;

public class DialogSlotsTests
{
    [Fact]
    public void Required_slots_match_spec()
    {
        Assert.Equal(new[] { "intro", "backstory", "hire_offer", "hire_accept", "hire_decline" }, DialogSlots.RequiredCompanion);
        Assert.Equal(DialogSlots.RequiredCompanion.Concat(new[] { "player_ask_about", "player_ask_hire", "player_hire_pay",
            "player_hire_full", "player_hire_later" }).Concat(VanillaDialogPoints.Slots), DialogSlots.Required);
        Assert.Equal(new[] { "leave_threat", "relation_complaint", "like_reaction", "dislike_reaction", "idle" }, DialogSlots.Optional);
    }

    [Fact]
    public void All_contains_required_and_optional_without_duplicates()
    {
        Assert.Equal(15 + VanillaDialogPoints.All.Count, DialogSlots.All.Count);
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
    [Theory]
    [InlineData("intro_01", true)]
    [InlineData("greeting_when_raining", true)]
    [InlineData("Intro_01", false)]
    [InlineData("intro-01", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Local_id_pattern(string? id, bool expected) => Assert.Equal(expected, LineIds.IsValidLocal(id));

    [Fact]
    public void Full_id_prefixes_the_companion() => Assert.Equal("test_companion_intro_01", LineIds.Full("test_companion", "intro_01"));

    [Fact]
    public void Default_audio_path_uses_the_local_id() => Assert.Equal("audio/intro_01.ogg", LineIds.DefaultAudioPath("intro_01"));
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
    [InlineData("pt-br", false)]
    [InlineData("tur", false)]
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
          "schemaVersion": 1, "name": "Test Companion", "isFemale": false, "voice": "earnest", "age": 32,
          "culture": "empire", "bodyProperties": null,
          "attributes": { "vigor": 4, "control": 3, "endurance": 4, "cunning": 2, "social": 2, "intelligence": 2 },
          "skills": { "OneHanded": 100 }, "traits": { "Valor": 1 },
          "equipment": { "templateId": "x" },
          "hireCost": 500, "voiceProfile": { "reference": null, "style": null },
          "likes": [], "dislikes": [], "relations": [], "requiredModules": ["NavalDLC"]
        }
        """;
        var c = JsonConvert.DeserializeObject<CompanionDefinition>(json)!;
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
        { "schemaVersion": 1, "lines": [ { "id": "intro_01", "slot": "intro", "voiced": true, "audio": "audio/intro_01.ogg" } ] }
        """;
        var line = JsonConvert.DeserializeObject<DialogFile>(json)!.Lines!.Single();
        Assert.Equal("intro", line.Slot);
        Assert.Equal("intro_01", line.Id);
        Assert.True(line.Voiced);
    }
}

public class VanillaDialogPointsTests
{
    [Fact]
    public void Slots_and_sentence_ids_are_unique()
    {
        Assert.Equal(VanillaDialogPoints.All.Count, VanillaDialogPoints.Slots.Distinct().Count());
        var ids = VanillaDialogPoints.All.SelectMany(p => p.SentenceIds).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Vanilla_slots_are_required_companion_slots_and_not_player_slots()
    {
        Assert.All(VanillaDialogPoints.Slots, s => Assert.True(DialogSlots.IsKnown(s) && DialogSlots.IsRequired(s)));
        Assert.All(VanillaDialogPoints.Slots, s => Assert.False(PlayerSlots.IsKnown(s)));
    }

    [Fact]
    public void Every_point_has_english_text_without_a_localization_id()
    {
        Assert.All(VanillaDialogPoints.All, p => Assert.False(string.IsNullOrWhiteSpace(p.EnglishText)));
        Assert.All(VanillaDialogPoints.All, p => Assert.DoesNotContain("{=", p.EnglishText));
    }

    [Theory]
    [InlineData("companion_fire", "fire_reaction")]
    [InlineData("companion_role_unassign_response", "role_ok")]
    [InlineData("prison_break_start_2", "prison_break_thanks")]
    public void Sentence_ids_map_to_their_slot(string sentenceId, string slot) =>
        Assert.Equal(slot, VanillaDialogPoints.ForSentence(sentenceId)!.Slot);

    [Fact]
    public void Unknown_sentence_has_no_point()
    {
        Assert.Null(VanillaDialogPoints.ForSentence("albocarius_test_companion_intro"));
        Assert.Null(VanillaDialogPoints.ForSentence(null));
    }
}
