using System.IO;
using System.Linq;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json;

namespace AlbocariusCompanions.Core.Tests;

/// <summary>Unit tests for DialogChecks (complement the acceptance tests for task 19).</summary>
public class DialogChecksTests
{
    static string Valid(string id = "a") => $$"""
    { "lines": [
        { "id": "{{id}}_intro_01", "slot": "intro", "text": { "en": "Hello, Captain." }, "voiced": true },
        { "id": "{{id}}_backstory_01", "slot": "backstory", "text": { "en": "A past." }, "voiced": true },
        { "id": "{{id}}_hire_offer_01", "slot": "hire_offer", "text": { "en": "Hire me?" }, "voiced": true },
        { "id": "{{id}}_hire_accept_01", "slot": "hire_accept", "text": { "en": "Lead on." }, "voiced": true },
        { "id": "{{id}}_hire_decline_01", "slot": "hire_decline", "text": { "en": "Another time." }, "voiced": false },
        { "id": "{{id}}_p_ask_about_01", "slot": "player_ask_about", "text": { "en": "Who are you?" } },
        { "id": "{{id}}_p_ask_hire_01", "slot": "player_ask_hire", "text": { "en": "Join me." } },
        { "id": "{{id}}_p_hire_pay_01", "slot": "player_hire_pay", "text": { "en": "Here is the gold." } },
        { "id": "{{id}}_p_hire_full_01", "slot": "player_hire_full", "text": { "en": "I have too many." } },
        { "id": "{{id}}_p_hire_later_01", "slot": "player_hire_later", "text": { "en": "Not now." } }
    ] }
    """;

    static CompanionDefinition Companion(string id) =>
        new CompanionDefinition { Name = "N" };

    /// <summary>
    /// Builds a package from the compact test form (full ids, per-line "text"): ids lose the "&lt;folder&gt;_" prefix and
    /// texts move into one lang file per language, as the loader would see them on disk.
    /// </summary>
    static CompanionPackage Pkg(string folder, string dialogsJson, string? id = null)
    {
        var doc = Newtonsoft.Json.Linq.JObject.Parse(dialogsJson);
        var langs = new Dictionary<string, LangFile>();
        foreach (var line in (doc["lines"] as Newtonsoft.Json.Linq.JArray ?? new Newtonsoft.Json.Linq.JArray())
                     .OfType<Newtonsoft.Json.Linq.JObject>())
        {
            var lineId = (string?)line["id"];
            if (lineId != null && lineId.StartsWith(folder + "_"))
                line["id"] = lineId = lineId.Substring(folder.Length + 1);
            if (line["text"] is Newtonsoft.Json.Linq.JObject text && lineId != null)
                foreach (var entry in text.Properties())
                {
                    if (!langs.TryGetValue(entry.Name, out var file))
                        langs[entry.Name] = file = new LangFile { Lines = new Dictionary<string, string>() };
                    file.Lines![lineId] = (string?)entry.Value!;
                }
            line.Remove("text");
        }
        return new CompanionPackage(folder, Path.Combine(Path.GetTempPath(), "albocarius-unit", folder),
            Companion(id ?? folder), doc.ToObject<DialogFile>()!, langs);
    }

    static List<ValidationIssue> Issues(params CompanionPackage[] packages) =>
        DialogChecks.Validate(packages).ToList();

    [Fact]
    public void Empty_package_list_produces_no_issues() =>
        Assert.Empty(DialogChecks.Validate(new CompanionPackage[0]));

    [Fact]
    public void A_valid_dialog_file_produces_no_issues() => Assert.Empty(Issues(Pkg("a", Valid())));

    [Fact]
    public void Null_lines_report_every_required_slot_once()
    {
        var issues = Issues(Pkg("a", "{ }"));
        Assert.Equal(10, issues.Count);
        Assert.All(issues, i => Assert.Equal(IssueCodes.MissingRequiredSlot, i.Code));
    }

    [Fact]
    public void Null_line_entry_does_not_crash_and_reports_unknown_slot()
    {
        var issues = Issues(Pkg("a", "{ \"lines\": [null] }"));
        Assert.Contains(issues, i => i.Code == IssueCodes.UnknownSlot);
        Assert.All(issues, i => Assert.True(i.IsError));
    }

    [Fact]
    public void Unknown_slot_and_duplicate_line_id_are_separate_errors()
    {
        var issues = Issues(Pkg("a",
            "{ \"lines\": [ { \"id\": \"a_intro_01\", \"slot\": \"greeting\", \"text\": { \"en\": \"Hi\" }, \"voiced\": false }, " +
            "{ \"id\": \"a_intro_01\", \"slot\": \"intro\", \"text\": { \"en\": \"Hello, Captain.\" }, \"voiced\": false } ] }"));

        Assert.Contains(issues, i => i.Code == IssueCodes.UnknownSlot);
        Assert.Equal(2, issues.Count(i => i.Code == IssueCodes.DuplicateLineId || i.Code == IssueCodes.UnknownSlot));
        Assert.Contains(issues, i => i.Code == IssueCodes.DuplicateLineId && i.Message.Contains("intro_01"));
        Assert.Equal(9, issues.Count(i => i.Code == IssueCodes.MissingRequiredSlot));
    }

    [Fact]
    public void Voiced_line_with_tokens_in_two_languages_reports_one_error()
    {
        var issues = Issues(Pkg("a",
            "{ \"lines\": [ { \"id\": \"a_intro_01\", \"slot\": \"intro\", \"text\": { \"en\": \"{PLAYER_NAME}\", \"de\": \"{PLAYER.GIVENNAME}\" }, \"voiced\": true } ] }"));
        Assert.Single(issues, i => i.Code == IssueCodes.PlayerTokenInVoicedLine);
    }

    [Fact]
    public void Each_bad_language_key_gets_its_own_error()
    {
        var issues = Issues(Pkg("a",
            "{ \"lines\": [ { \"id\": \"a_intro_01\", \"slot\": \"intro\", \"text\": { \"en\": \"Hi\", \"EN\": \"x\", \"English\": \"y\" }, \"voiced\": false } ] }"));
        Assert.Equal(2, issues.Count(i => i.Code == IssueCodes.InvalidLanguageCode));
    }

    [Fact]
    public void En_null_value_is_missing_english_text()
    {
        var issues = Issues(Pkg("a",
            "{ \"lines\": [ { \"id\": \"a_intro_01\", \"slot\": \"intro\", \"text\": { \"en\": null, \"tr\": \"Merhaba\" }, \"voiced\": false } ] }"));
        Assert.Single(issues, i => i.Code == IssueCodes.MissingEnglishText);
        Assert.DoesNotContain(issues, i => i.Code == IssueCodes.InvalidLanguageCode);
    }

    [Theory]
    [InlineData(".")]
    [InlineData("audio/./x.ogg")]
    [InlineData("audio/x.ogg")]
    public void Dot_segments_and_plain_relative_paths_are_accepted(string audio)
    {
        var issues = Issues(Pkg("a",
            "{ \"lines\": [ { \"id\": \"a_intro_01\", \"slot\": \"intro\", \"text\": { \"en\": \"Hi\" }, \"voiced\": false, \"audio\": \"" + audio + "\" } ] }"));
        Assert.DoesNotContain(issues, i => i.Code == IssueCodes.InvalidAudioPath);
    }

    [Theory]
    [InlineData("audio/x.wav")]
    [InlineData("audio/x.mp3")]
    [InlineData("audio/x")]
    [InlineData("audio/x.ogg.wav")]
    [InlineData("audio/x.oggx")]
    [InlineData("audio/ogg")]
    [InlineData(".ogg")]
    public void Audio_must_be_an_ogg_file(string audio)
    {
        var issues = Issues(Pkg("a",
            "{ \"lines\": [ { \"id\": \"a_intro_01\", \"slot\": \"intro\", \"text\": { \"en\": \"Hi\" }, \"voiced\": false, \"audio\": \"" + audio + "\" } ] }"));

        var issue = Assert.Single(issues, i => i.Code == IssueCodes.InvalidAudioExtension);
        Assert.Contains("a_intro_01", issue.Message);
        Assert.Contains(audio, issue.Message);
        Assert.Contains(".ogg", issue.Message);
    }

    [Fact]
    public void Voiced_line_audio_extension_is_checked_too()
    {
        var issues = Issues(Pkg("a",
            "{ \"lines\": [ { \"id\": \"a_intro_01\", \"slot\": \"intro\", \"text\": { \"en\": \"Hi\" }, \"voiced\": true, \"audio\": \"audio/x.wav\" } ] }"));
        Assert.Contains(issues, i => i.Code == IssueCodes.InvalidAudioExtension);
    }

    [Theory]
    [InlineData("audio/x.OGG")]
    [InlineData("audio/x.Ogg")]
    public void Ogg_extension_any_case_is_accepted(string audio)
    {
        var issues = Issues(Pkg("a",
            "{ \"lines\": [ { \"id\": \"a_intro_01\", \"slot\": \"intro\", \"text\": { \"en\": \"Hi\" }, \"voiced\": false, \"audio\": \"" + audio + "\" } ] }"));
        Assert.DoesNotContain(issues, i => i.Code == IssueCodes.InvalidAudioExtension);
    }

    [Fact]
    public void Unsafe_audio_path_reports_only_the_path_error()
    {
        var issues = Issues(Pkg("a",
            "{ \"lines\": [ { \"id\": \"a_intro_01\", \"slot\": \"intro\", \"text\": { \"en\": \"Hi\" }, \"voiced\": false, \"audio\": \"../x.wav\" } ] }"));

        Assert.Single(issues, i => i.Code == IssueCodes.InvalidAudioPath);
        Assert.DoesNotContain(issues, i => i.Code == IssueCodes.InvalidAudioExtension);
    }
}
