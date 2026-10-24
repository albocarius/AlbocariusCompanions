using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 18-core-validation-companion. Each test breaks exactly one rule and expects exactly that error.
public class CompanionValidationTests
{
    const string Id = "test_companion";

    static IReadOnlyList<ValidationIssue> Validate(Action<JObject> mutate, string folder = Id)
    {
        var c = Fixtures.Companion(Id);
        mutate(c);
        return CompanionChecks.Validate(new[] { Fixtures.Package(folder, c, Fixtures.Dialogs(Id)) });
    }

    static ValidationIssue SingleError(IReadOnlyList<ValidationIssue> issues, string code)
    {
        var error = Assert.Single(issues.Errors());
        Assert.Equal(code, error.Code);
        return error;
    }

    [Fact]
    public void Valid_companion_has_no_issues() => Assert.Empty(Validate(_ => { }));

    [Fact]
    public void Validation_never_throws_on_an_empty_definition()
    {
        var pkg = new CompanionPackage("x", "/tmp/x", new CompanionDefinition(), new DialogFile());
        var issues = CompanionChecks.Validate(new[] { pkg });
        Assert.NotEmpty(issues.Errors());
    }

    [Theory]
    [InlineData("Test_Companion")]
    [InlineData("test-companion")]
    [InlineData("test companion")]
    public void Folder_name_is_the_id_and_must_be_lowercase_snake(string folder)
    {
        var issues = CompanionChecks.Validate(new[] { Fixtures.Package(folder, Fixtures.Companion(folder), Fixtures.Dialogs(folder)) });
        var error = Assert.Single(issues.Errors());
        Assert.Equal(IssueCodes.InvalidId, error.Code);
        Assert.Equal(folder, error.Folder);
    }

    [Fact]
    public void Id_field_is_gone_from_companion_json() =>
        Assert.Null(typeof(CompanionDefinition).GetProperty("Id"));

    [Fact]
    public void Package_id_is_the_folder_name() => Assert.Equal("some_folder", Fixtures.Package("some_folder").Id);

    [Theory]
    [InlineData("name")]
    [InlineData("isFemale")]
    [InlineData("voice")]
    [InlineData("age")]
    [InlineData("culture")]
    [InlineData("attributes")]
    [InlineData("skills")]
    [InlineData("equipment")]
    [InlineData("hireCost")]
    public void Required_field_missing(string field)
    {
        var issues = Validate(c => c.Remove(field));
        Assert.Contains(field, SingleError(issues, IssueCodes.MissingField).Message);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("culture")]
    public void Required_string_blank(string field)
    {
        var issues = Validate(c => c[field] = "  ");
        Assert.Contains(field, SingleError(issues, IssueCodes.MissingField).Message);
    }

    [Fact]
    public void Equipment_template_id_missing()
    {
        var issues = Validate(c => ((JObject)c["equipment"]!).Remove("templateId"));
        Assert.Contains("templateId", SingleError(issues, IssueCodes.MissingField).Message);
    }

    [Theory]
    [InlineData("vigor")]
    [InlineData("intelligence")]
    public void Each_attribute_is_required(string attribute)
    {
        var issues = Validate(c => ((JObject)c["attributes"]!).Remove(attribute));
        Assert.Contains(attribute, SingleError(issues, IssueCodes.MissingField).Message);
    }

    [Fact]
    public void Unknown_attribute()
    {
        var issues = Validate(c => c["attributes"]!["charisma"] = 3);
        Assert.Contains("charisma", SingleError(issues, IssueCodes.UnknownAttribute).Message);
    }

    [Fact]
    public void Optional_fields_may_be_absent()
    {
        var issues = Validate(c =>
        {
            foreach (var f in new[] { "bodyProperties", "traits", "voiceProfile", "likes", "dislikes", "relations", "requiredModules" })
                c.Remove(f);
        });
        Assert.Empty(issues);
    }

    [Theory]
    [InlineData("skill", -1)]
    [InlineData("skill", 331)]
    [InlineData("attribute", 0)]
    [InlineData("attribute", 11)]
    [InlineData("age", 17)]
    [InlineData("age", 81)]
    [InlineData("hireCost", -1)]
    public void Value_out_of_range(string what, int value)
    {
        var issues = Validate(c => Set(c, what, value));
        SingleError(issues, IssueCodes.OutOfRange);
    }

    [Theory]
    [InlineData("skill", 0)]
    [InlineData("skill", 330)]
    [InlineData("attribute", 1)]
    [InlineData("attribute", 10)]
    [InlineData("age", 18)]
    [InlineData("age", 80)]
    [InlineData("hireCost", 0)]
    public void Range_bounds_are_inclusive(string what, int value) => Assert.Empty(Validate(c => Set(c, what, value)));

    static void Set(JObject c, string what, int value)
    {
        switch (what)
        {
            case "skill": c["skills"]!["OneHanded"] = value; break;
            case "attribute": c["attributes"]!["vigor"] = value; break;
            default: c[what] = value; break;
        }
    }

    [Theory]
    [InlineData("angry")]
    [InlineData("Earnest")]
    [InlineData("")]
    public void Voice_must_be_a_vanilla_voice_type(string voice)
    {
        var issues = Validate(c => c["voice"] = voice);
        SingleError(issues, IssueCodes.InvalidVoice);
    }

    [Theory]
    [InlineData("curt")]
    [InlineData("earnest")]
    [InlineData("ironic")]
    [InlineData("softspoken")]
    public void Vanilla_voice_types_are_accepted(string voice) => Assert.Empty(Validate(c => c["voice"] = voice));

    [Fact]
    public void Fixed_equipment_sets_are_accepted() => Assert.Empty(Validate(c =>
    {
        c["equipment"]!["battle"] = new JObject { ["Body"] = "monk_robe", ["Head"] = "pilgrim_hood", ["Item0"] = "bo_staff" };
        c["equipment"]!["civilian"] = new JObject { ["Body"] = "monk_robe" };
    }));

    [Theory]
    [InlineData("battle", "Weapon")]
    [InlineData("civilian", "body")]
    public void Unknown_equipment_slot(string set, string slot)
    {
        var issues = Validate(c => c["equipment"]![set] = new JObject { [slot] = "monk_robe" });
        Assert.Contains(slot, SingleError(issues, IssueCodes.UnknownEquipmentSlot).Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Empty_equipment_item_id(string item)
    {
        var issues = Validate(c => c["equipment"]!["battle"] = new JObject { ["Body"] = item });
        SingleError(issues, IssueCodes.InvalidEquipmentItem);
    }

    [Theory]
    [InlineData("likes")]
    [InlineData("dislikes")]
    public void Valid_reaction_entry(string list) => Assert.Empty(Validate(c =>
        c[list] = new JArray(new JObject { ["event"] = "battle_won", ["reaction"] = 2, ["slot"] = "like_reaction" })));

    [Theory]
    [InlineData("likes", "event")]
    [InlineData("likes", "reaction")]
    [InlineData("likes", "slot")]
    [InlineData("dislikes", "event")]
    public void Reaction_entry_missing_part(string list, string missing)
    {
        var issues = Validate(c =>
        {
            var entry = new JObject { ["event"] = "battle_won", ["reaction"] = -1, ["slot"] = "dislike_reaction" };
            entry.Remove(missing);
            c[list] = new JArray(entry);
        });
        SingleError(issues, IssueCodes.InvalidReaction);
    }

    [Fact]
    public void Reaction_entry_with_unknown_slot()
    {
        var issues = Validate(c => c["likes"] = new JArray(new JObject { ["event"] = "e", ["reaction"] = 1, ["slot"] = "nope" }));
        SingleError(issues, IssueCodes.InvalidReaction);
    }

    [Theory]
    [InlineData("enemy")]
    [InlineData(null)]
    public void Relation_attitude_must_be_friend_or_rival(string? attitude)
    {
        var issues = Validate(c => c["relations"] = new JArray(new JObject { ["companion"] = "someone", ["attitude"] = attitude }));
        Assert.Contains(issues.Errors(), i => i.Code == IssueCodes.InvalidRelation);
        Assert.All(issues.Errors(), i => Assert.Equal(IssueCodes.InvalidRelation, i.Code));
    }

    [Fact]
    public void Relation_without_companion_is_invalid()
    {
        var issues = Validate(c => c["relations"] = new JArray(new JObject { ["attitude"] = "friend" }));
        SingleError(issues, IssueCodes.InvalidRelation);
    }

    [Fact]
    public void Relation_to_itself_is_invalid()
    {
        var issues = Validate(c => c["relations"] = new JArray(new JObject { ["companion"] = Id, ["attitude"] = "friend" }));
        SingleError(issues, IssueCodes.InvalidRelation);
    }

    [Fact]
    public void Relation_to_unknown_companion_is_only_a_warning()
    {
        var issues = Validate(c => c["relations"] = new JArray(new JObject { ["companion"] = "not_loaded", ["attitude"] = "rival" }));

        Assert.Empty(issues.Errors());
        var warning = Assert.Single(issues);
        Assert.Equal(IssueCodes.UnknownRelationTarget, warning.Code);
        Assert.Equal(IssueSeverity.Warning, warning.Severity);
    }

    [Fact]
    public void Relation_to_loaded_companion_is_clean()
    {
        var a = Fixtures.Companion("a");
        a["relations"] = new JArray(new JObject { ["companion"] = "b", ["attitude"] = "friend" });
        var issues = CompanionChecks.Validate(new[]
        {
            Fixtures.Package("a", a, Fixtures.Dialogs("a")),
            Fixtures.Package("b"),
        });
        Assert.Empty(issues);
    }
}
