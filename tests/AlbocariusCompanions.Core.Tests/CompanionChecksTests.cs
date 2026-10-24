using System.IO;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json;

namespace AlbocariusCompanions.Core.Tests;

/// <summary>Unit tests for CompanionChecks (complement the acceptance tests for task 18).</summary>
public class CompanionChecksTests
{
    static string Valid(string id = "a") => $$"""
    { "id": "{{id}}", "name": "N", "isFemale": false, "voice": "earnest", "age": 30,
      "culture": "empire", "cultureFallback": null, "bodyProperties": null,
      "attributes": { "vigor": 4, "control": 3, "endurance": 4, "cunning": 2, "social": 2, "intelligence": 2 },
      "skills": { "OneHanded": 100 }, "traits": {},
      "equipment": { "templateId": "tpl_empire", "fallbackTemplateId": null },
      "hireCost": 500, "likes": [], "dislikes": [], "relations": [] }
    """;

    static CompanionPackage Pkg(string folder, string companionJson) =>
        new CompanionPackage(folder, Path.Combine(Path.GetTempPath(), "albocarius-unit", folder),
            JsonConvert.DeserializeObject<CompanionDefinition>(companionJson)!, new DialogFile());

    static List<ValidationIssue> Issues(params CompanionPackage[] packages) =>
        CompanionChecks.Validate(packages).ToList();

    [Fact]
    public void Empty_package_list_produces_no_issues() =>
        Assert.Empty(CompanionChecks.Validate(new CompanionPackage[0]));

    [Fact]
    public void A_valid_companion_produces_no_issues() => Assert.Empty(Issues(Pkg("a", Valid())));

    [Fact]
    public void Unknown_attribute_is_reported_once_and_its_value_is_not_range_checked()
    {
        var json = Valid().Replace("\"social\": 2", "\"social\": 2, \"charisma\": 999");
        var issues = Issues(Pkg("a", json));

        var issue = Assert.Single(issues);
        Assert.Equal(IssueCodes.UnknownAttribute, issue.Code);
        Assert.Contains("charisma", issue.Message);
    }

    [Fact]
    public void Every_out_of_range_skill_gets_its_own_error()
    {
        var json = Valid().Replace("\"OneHanded\": 100", "\"OneHanded\": -5, \"Ride\": 999");
        var issues = Issues(Pkg("a", json));

        Assert.Equal(2, issues.Count(i => i.Code == IssueCodes.OutOfRange));
        Assert.All(issues, i => Assert.False(i.Severity == IssueSeverity.Warning));
    }

    [Fact]
    public void Empty_skills_and_traits_are_allowed()
    {
        Assert.Empty(Issues(Pkg("a", Valid())));
    }

    [Fact]
    public void Relation_with_both_parts_broken_is_a_single_error()
    {
        var json = Valid().Replace("\"relations\": []", "\"relations\": [ { \"attitude\": \"enemy\" } ]");
        var issues = Issues(Pkg("a", json));

        var issue = Assert.Single(issues);
        Assert.Equal(IssueCodes.InvalidRelation, issue.Code);
    }

    [Fact]
    public void Reaction_with_multiple_problems_is_a_single_error()
    {
        var json = Valid().Replace("\"likes\": []", "\"likes\": [ { \"slot\": \"nope\" } ]");
        var issues = Issues(Pkg("a", json));

        var issue = Assert.Single(issues);
        Assert.Equal(IssueCodes.InvalidReaction, issue.Code);
    }

    [Fact]
    public void Relation_target_is_matched_case_sensitively()
    {
        var json = Valid().Replace("\"relations\": []", "\"relations\": [ { \"companion\": \"B\", \"attitude\": \"friend\" } ]");
        var issues = Issues(Pkg("a", json), Pkg("b", Valid("b")));

        var issue = Assert.Single(issues);
        Assert.Equal(IssueCodes.UnknownRelationTarget, issue.Code);
        Assert.Equal(IssueSeverity.Warning, issue.Severity);
        Assert.Equal("a", issue.Folder);
    }

    [Fact]
    public void Voice_comparison_is_case_sensitive()
    {
        var json = Valid().Replace("\"voice\": \"earnest\"", "\"voice\": \"Earnest\"");
        var issues = Issues(Pkg("a", json));

        Assert.Equal(IssueCodes.InvalidVoice, Assert.Single(issues).Code);
    }

    [Fact]
    public void Blank_equipment_template_is_a_missing_field_not_a_fallback_issue()
    {
        var json = Valid().Replace("\"templateId\": \"tpl_empire\", \"fallbackTemplateId\": null",
            "\"templateId\": \"  \", \"fallbackTemplateId\": \"tpl_empire\"");
        var issues = Issues(Pkg("a", json));

        var issue = Assert.Single(issues);
        Assert.Equal(IssueCodes.MissingField, issue.Code);
        Assert.Contains("templateId", issue.Message);
    }

    [Fact]
    public void Null_like_relation_entries_are_reported_as_invalid()
    {
        var json = Valid().Replace("\"relations\": []", "\"relations\": [ null ]")
            .Replace("\"likes\": []", "\"likes\": [ null ]");
        var issues = Issues(Pkg("a", json));

        Assert.Contains(issues, i => i.Code == IssueCodes.InvalidRelation);
        Assert.Contains(issues, i => i.Code == IssueCodes.InvalidReaction);
        Assert.All(issues, i => Assert.True(i.IsError));
    }
}
