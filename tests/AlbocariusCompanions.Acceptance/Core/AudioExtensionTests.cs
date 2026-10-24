using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 32-schema-version-ogg. Audio must be OGG; anything else is a validation error, not a silent skip.
public class AudioExtensionTests
{
    const string Id = "test_companion";

    static IReadOnlyList<ValidationIssue> Validate(Action<JObject> mutate)
    {
        var d = Fixtures.Dialogs(Id);
        mutate(d);
        return CompanionValidator.Validate(new[] { Fixtures.Package(Id, Fixtures.Companion(Id), d) });
    }

    static JObject Line(JObject d, int index) => (JObject)Fixtures.Lines(d)[index];

    [Theory]
    [InlineData("audio/x.ogg")]
    [InlineData("audio/x.OGG")]
    [InlineData("audio/x.Ogg")]
    public void Ogg_is_accepted_in_any_case(string audio) => Assert.Empty(Validate(d => Line(d, 0)["audio"] = audio));

    [Theory]
    [InlineData("audio/x.wav")]
    [InlineData("audio/x.mp3")]
    [InlineData("audio/x")]
    [InlineData("audio/x.ogg.wav")]
    [InlineData("audio/x.oggx")]
    [InlineData("audio/ogg")]
    public void Other_extensions_are_an_error(string audio)
    {
        var issues = Validate(d => Line(d, 0)["audio"] = audio);
        var error = Assert.Single(issues.Errors());
        Assert.Equal(IssueCodes.InvalidAudioExtension, error.Code);
        Assert.Equal(Id, error.Folder);
        Assert.Contains(Id + "_intro_01", error.Message);
        Assert.Contains(audio, error.Message);
        Assert.Contains(".ogg", error.Message);
    }

    [Fact]
    public void Unvoiced_lines_are_checked_too()
    {
        // index 4 = hire_decline, voiced:false in the fixture
        var issues = Validate(d => Line(d, 4)["audio"] = "audio/x.wav");
        Assert.Equal(IssueCodes.InvalidAudioExtension, Assert.Single(issues.Errors()).Code);
    }

    [Theory]
    [InlineData("../x.wav")]
    [InlineData("")]
    public void An_unsafe_path_reports_only_the_path_error(string audio)
    {
        var issues = Validate(d => Line(d, 0)["audio"] = audio);
        Assert.Equal(IssueCodes.InvalidAudioPath, Assert.Single(issues.Errors()).Code);
    }

    [Fact]
    public void No_audio_field_means_default_ogg_and_no_error() => Assert.Empty(Validate(d => Line(d, 0).Remove("audio")));
}
