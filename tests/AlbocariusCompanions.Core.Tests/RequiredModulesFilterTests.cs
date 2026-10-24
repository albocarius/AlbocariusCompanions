using System.IO;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json;

namespace AlbocariusCompanions.Core.Tests;

/// <summary>Unit tests for RequiredModules.Missing (complement the acceptance tests for task 35).</summary>
public class RequiredModulesFilterTests
{
    static CompanionPackage Pkg(string requiredModulesJson) =>
        new CompanionPackage("a", Path.Combine(Path.GetTempPath(), "albocarius-unit", "a"),
            JsonConvert.DeserializeObject<CompanionDefinition>($$"""
            { "id": "a", "name": "N", "isFemale": false, "voice": "earnest", "age": 30,
              "culture": "empire", "bodyProperties": null,
              "attributes": { "vigor": 4, "control": 3, "endurance": 4, "cunning": 2, "social": 2, "intelligence": 2 },
              "skills": { "OneHanded": 100 }, "traits": {},
              "equipment": { "templateId": "tpl_empire" },
              "hireCost": 500, "likes": [], "dislikes": [], "relations": []{{requiredModulesJson}} }
            """)!,
            new DialogFile());

    [Fact]
    public void An_absent_list_needs_no_module_and_never_calls_the_predicate()
    {
        var missing = RequiredModules.Missing(Pkg(""), id => throw new System.Exception("must not be called"));
        Assert.Empty(missing);
    }

    [Fact]
    public void An_empty_list_needs_no_module() => Assert.Empty(RequiredModules.Missing(Pkg(@", ""requiredModules"": []"), _ => false));

    [Fact]
    public void Inactive_modules_come_back_in_declared_order()
    {
        var active = new HashSet<string> { "NavalDLC" };
        var missing = RequiredModules.Missing(
            Pkg(@", ""requiredModules"": [""EOE_Armory"", ""NavalDLC"", ""Other"", ""eoe""]"), active.Contains);

        Assert.Equal(new[] { "EOE_Armory", "Other", "eoe" }, missing);
    }

    [Fact]
    public void Blank_entries_are_a_validation_error_not_a_module_to_look_for()
    {
        var missing = RequiredModules.Missing(Pkg(@", ""requiredModules"": ["""", null, ""  "", ""NavalDLC""]"), _ => false);
        Assert.Equal(new[] { "NavalDLC" }, missing);
    }
}
