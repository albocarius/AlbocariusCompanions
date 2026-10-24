using AlbocariusCompanions.Core.Loading;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 35-required-modules-core. requiredModules replaces cultureFallback / equipment.fallbackTemplateId.
public class RequiredModulesTests
{
    const string Id = "test_companion";

    static IReadOnlyList<ValidationIssue> Validate(Action<JObject> mutate)
    {
        var c = Fixtures.Companion(Id);
        mutate(c);
        return CompanionValidator.Validate(new[] { Fixtures.Package(Id, c, Fixtures.Dialogs(Id)) });
    }

    static CompanionPackage PackageWith(params string[] modules)
    {
        var c = Fixtures.Companion(Id);
        c["requiredModules"] = new JArray(modules);
        return Fixtures.Package(Id, c, Fixtures.Dialogs(Id));
    }

    [Fact]
    public void Required_modules_are_loaded_from_json()
    {
        using var tmp = new TempDir();
        var c = Fixtures.Companion(Id);
        c["requiredModules"] = new JArray("NavalDLC", "EOE_Armory");
        Fixtures.Write(tmp.Path, Id, c, Fixtures.Dialogs(Id));

        var result = CompanionLoader.LoadAll(tmp.Path);

        Assert.Empty(result.Issues);
        Assert.Equal(new[] { "NavalDLC", "EOE_Armory" }, Assert.Single(result.Packages).Companion.RequiredModules);
    }

    [Theory]
    [InlineData("cultureFallback", "vlandia")]
    [InlineData("equipment.fallbackTemplateId", "npc_wanderer_equipment_template_vlandia")]
    public void Removed_fallback_fields_are_rejected_by_the_strict_loader(string field, string value)
    {
        using var tmp = new TempDir();
        var c = Fixtures.Companion(Id);
        if (field.StartsWith("equipment.")) c["equipment"]![field["equipment.".Length..]] = value;
        else c[field] = value;
        Fixtures.Write(tmp.Path, Id, c, Fixtures.Dialogs(Id));

        var issue = Assert.Single(CompanionLoader.LoadAll(tmp.Path).Issues);
        Assert.Equal(IssueCodes.InvalidJson, issue.Code);
        Assert.Contains(field.Split('.')[^1], issue.Message);
    }

    [Fact]
    public void Absent_or_empty_list_is_fine()
    {
        Assert.Empty(Validate(c => c.Remove("requiredModules")));
        Assert.Empty(Validate(c => c["requiredModules"] = new JArray()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Blank_module_id_is_an_error(string bad)
    {
        var error = Assert.Single(Validate(c => c["requiredModules"] = new JArray("NavalDLC", bad)).Errors());
        Assert.Equal(IssueCodes.InvalidRequiredModule, error.Code);
        Assert.Contains("requiredModules", error.Message);
    }

    [Fact]
    public void Duplicate_module_id_is_an_error()
    {
        var error = Assert.Single(Validate(c => c["requiredModules"] = new JArray("NavalDLC", "NavalDLC")).Errors());
        Assert.Equal(IssueCodes.InvalidRequiredModule, error.Code);
        Assert.Contains("NavalDLC", error.Message);
    }

    [Fact]
    public void Missing_lists_the_modules_that_are_not_active_in_declared_order()
    {
        var active = new HashSet<string> { "NavalDLC" };
        var missing = RequiredModules.Missing(PackageWith("EOE_Armory", "NavalDLC", "Other"), active.Contains);
        Assert.Equal(new[] { "EOE_Armory", "Other" }, missing);
    }

    [Fact]
    public void Nothing_is_missing_when_all_are_active_or_none_are_required()
    {
        Assert.Empty(RequiredModules.Missing(PackageWith("NavalDLC"), _ => true));
        Assert.Empty(RequiredModules.Missing(Fixtures.Package(Id), _ => false));
    }

    [Fact]
    public void Fallback_issue_codes_are_gone()
    {
        var codes = typeof(IssueCodes).GetFields().Select(f => f.Name).ToList();
        Assert.DoesNotContain("CultureFallbackUsed", codes);
        Assert.DoesNotContain("EquipmentFallbackUsed", codes);
        Assert.DoesNotContain("SameAsFallback", codes);
    }

    [Fact]
    public void Repo_companions_and_schema_doc_no_longer_mention_fallbacks()
    {
        var root = RepoRoot();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "module", "ModuleData", "Companions"), "*.json", SearchOption.AllDirectories)
                     .Append(Path.Combine(root, "docs", "SCHEMA.md")))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("cultureFallback", text);
            Assert.DoesNotContain("fallbackTemplateId", text);
        }
        Assert.Contains("requiredModules", File.ReadAllText(Path.Combine(root, "docs", "SCHEMA.md")));
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AlbocariusCompanions.sln")))
            dir = dir.Parent;
        return dir!.FullName;
    }
}
