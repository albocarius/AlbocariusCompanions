using AlbocariusCompanions.Core.Loading;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Core.Tests;

/// <summary>Unit tests for SchemaVersions, the single version check point (SPEC_CORE_GAPS T2.4).</summary>
public class SchemaVersionsTests
{
    static ValidationIssue? Check(string json) =>
        SchemaVersions.Check(JObject.Parse(json), "some_folder", "companion.json");

    [Fact]
    public void Current_is_the_only_supported_version_and_the_field_name_is_stable()
    {
        Assert.Equal(1, SchemaVersions.Current);
        Assert.Equal("schemaVersion", SchemaVersions.PropertyName);
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 1 }""")]
    [InlineData("""{ "schemaVersion": 1, "id": "x" }""")]
    public void Supported_version_returns_no_issue(string json) => Assert.Null(Check(json));

    [Theory]
    [InlineData("""{ "id": "x" }""")]
    [InlineData("""{ "schemaVersion": null }""")]
    public void Absent_or_null_version_is_missing(string json)
    {
        var issue = Check(json);

        Assert.NotNull(issue);
        Assert.Equal(IssueCodes.MissingSchemaVersion, issue!.Code);
        Assert.Contains("some_folder/companion.json", issue.Message);
        Assert.Contains("schemaVersion", issue.Message);
    }

    [Theory]
    [InlineData("""{ "schemaVersion": "1" }""")]
    [InlineData("""{ "schemaVersion": 1.5 }""")]
    [InlineData("""{ "schemaVersion": true }""")]
    [InlineData("""{ "schemaVersion": [1] }""")]
    [InlineData("""{ "schemaVersion": { "major": 1 } }""")]
    public void Non_integer_version_is_invalid(string json)
    {
        var issue = Check(json);

        Assert.Equal(IssueCodes.InvalidSchemaVersion, issue!.Code);
        Assert.Contains("some_folder/companion.json", issue.Message);
        Assert.Contains("schemaVersion", issue.Message);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("99")]
    [InlineData("0")]
    [InlineData("-1")]
    public void Other_integers_are_unsupported_and_the_message_names_both_values(string found)
    {
        var issue = Check($$"""{ "schemaVersion": {{found}} }""");

        Assert.Equal(IssueCodes.UnsupportedSchemaVersion, issue!.Code);
        Assert.Contains(found, issue.Message);
        Assert.Contains("1", issue.Message);
    }

    [Fact]
    public void Issue_folder_is_the_companion_folder() =>
        Assert.Equal("some_folder", Check("""{ "schemaVersion": 2 }""")!.Folder);
}
