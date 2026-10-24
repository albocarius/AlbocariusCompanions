using AlbocariusCompanions.Core.Audio;
using AlbocariusCompanions.Core.Models;
using Newtonsoft.Json;

namespace AlbocariusCompanions.Core.Tests;

/// <summary>Unit tests for MissingAudioReport (complement the acceptance tests for task 20).</summary>
public class MissingAudioReportTests
{
    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("albocarius-unit-").FullName;
        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
        }
    }

    private static CompanionPackage Pkg(string id, string folderPath, string dialogsJson) =>
        new CompanionPackage(id, folderPath,
            new CompanionDefinition { Id = id },
            JsonConvert.DeserializeObject<DialogFile>(dialogsJson)!);

    [Fact]
    public void Line_without_id_or_null_line_is_skipped()
    {
        var pkg = Pkg("a", Path.Combine(Path.GetTempPath(), "albocarius-unit", "a"), """
        { "lines": [ { "slot": "intro", "text": { "en": "x" }, "voiced": true },
                     { "id": "", "slot": "intro", "text": { "en": "x" }, "voiced": true },
                     null ] }
        """);
        Assert.Empty(MissingAudioReport.Find(new[] { pkg }));
    }

    [Fact]
    public void Missing_lines_section_produces_no_entries() =>
        Assert.Empty(MissingAudioReport.Find(new[] { Pkg("a", Path.GetTempPath(), "{}") }));

    [Fact]
    public void Backslashes_in_audio_path_are_normalized_to_slash()
    {
        var pkg = Pkg("a", Path.Combine(Path.GetTempPath(), "albocarius-unit", "a"), """
        { "lines": [ { "id": "a_intro_01", "slot": "intro", "text": { "en": "x" }, "voiced": true,
                       "audio": "voice\\custom.ogg" } ] }
        """);

        var entry = Assert.Single(MissingAudioReport.Find(new[] { pkg }));
        Assert.Equal("voice/custom.ogg", entry.RelativePath);
    }

    [Fact]
    public void Existing_file_and_lipsync_xml_only_differ()
    {
        using var tmp = new TempDir();
        var dialogs = """{ "lines": [ { "id": "a_intro_01", "slot": "intro", "text": { "en": "x" }, "voiced": true } ] }""";

        Directory.CreateDirectory(Path.Combine(tmp.Path, "audio"));
        File.WriteAllText(Path.Combine(tmp.Path, "audio", "a_intro_01.xml"), "");
        Assert.Single(MissingAudioReport.Find(new[] { Pkg("a", tmp.Path, dialogs) })); // .xml alone is not audio

        File.WriteAllText(Path.Combine(tmp.Path, "audio", "a_intro_01.ogg"), "");
        Assert.Empty(MissingAudioReport.Find(new[] { Pkg("a", tmp.Path, dialogs) }));
    }

    [Fact]
    public void Order_follows_package_then_line_order()
    {
        var root = Path.Combine(Path.GetTempPath(), "albocarius-unit");
        var one = Pkg("z_first", Path.Combine(root, "z_first"), """
        { "lines": [ { "id": "z_first_intro_01", "slot": "intro", "text": { "en": "x" }, "voiced": true },
                     { "id": "z_first_intro_02", "slot": "intro", "text": { "en": "x" }, "voiced": true } ] }
        """);
        var two = Pkg("a_second", Path.Combine(root, "a_second"), """
        { "lines": [ { "id": "a_second_intro_01", "slot": "intro", "text": { "en": "x" }, "voiced": true } ] }
        """);

        Assert.Equal(new[] { "z_first_intro_01", "z_first_intro_02", "a_second_intro_01" },
            MissingAudioReport.Find(new[] { one, two }).Select(e => e.LineId));
    }
}
