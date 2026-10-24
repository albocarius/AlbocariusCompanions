using AlbocariusCompanions.Core.Audio;
using AlbocariusCompanions.Core.Loading;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 20-core-missing-audio; 41: default audio file is audio/<local id>.ogg, reports carry the full id.
public class MissingAudioTests
{
    const string Id = "test_companion";

    static IReadOnlyList<MissingAudioEntry> Run(TempDir tmp, Action<Newtonsoft.Json.Linq.JObject>? mutate = null, params string[] existingFiles)
    {
        var d = Fixtures.Dialogs(Id);
        mutate?.Invoke(d);
        Fixtures.Write(tmp.Path, Id, Fixtures.Companion(Id), d);
        foreach (var f in existingFiles)
        {
            var full = Path.Combine(tmp.Path, Id, f);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, "");
        }
        return MissingAudioReport.Find(CompanionLoader.LoadAll(tmp.Path).Packages);
    }

    [Fact]
    public void Voiced_lines_without_files_are_reported_with_default_path()
    {
        using var tmp = new TempDir();
        var report = Run(tmp);

        // Fixture: 4 voiced lines, hire_decline is unvoiced.
        Assert.Equal(new[] { Id + "_intro_01", Id + "_backstory_01", Id + "_hire_offer_01", Id + "_hire_accept_01" },
            report.Select(e => e.LineId));
        var first = report[0];
        Assert.Equal(Id, first.CompanionId);
        Assert.Equal("audio/intro_01.ogg", first.RelativePath);
        Assert.Equal(Path.Combine(tmp.Path, Id, "audio", "intro_01.ogg"), first.FullPath);
    }

    [Fact]
    public void Existing_default_files_are_not_reported()
    {
        using var tmp = new TempDir();
        var report = Run(tmp, null, "audio/intro_01.ogg", "audio/backstory_01.ogg");

        Assert.DoesNotContain(report, e => e.LineId == Id + "_intro_01");
        Assert.DoesNotContain(report, e => e.LineId == Id + "_backstory_01");
        Assert.Equal(2, report.Count);
    }

    [Fact]
    public void Explicit_audio_path_is_used()
    {
        using var tmp = new TempDir();
        var report = Run(tmp, d => d["lines"]![0]!["audio"] = "voice/custom.ogg");

        var entry = Assert.Single(report, e => e.LineId == Id + "_intro_01");
        Assert.Equal("voice/custom.ogg", entry.RelativePath);
    }

    [Fact]
    public void Explicit_audio_path_that_exists_is_not_reported()
    {
        using var tmp = new TempDir();
        var report = Run(tmp, d => d["lines"]![0]!["audio"] = "voice/custom.ogg", "voice/custom.ogg");

        Assert.DoesNotContain(report, e => e.LineId == Id + "_intro_01");
    }

    [Fact]
    public void Unvoiced_lines_are_never_reported()
    {
        using var tmp = new TempDir();
        var report = Run(tmp);

        Assert.DoesNotContain(report, e => e.LineId == Id + "_hire_decline_01");
    }

    [Fact]
    public void Lipsync_xml_alone_does_not_count_as_audio()
    {
        using var tmp = new TempDir();
        var report = Run(tmp, null, "audio/intro_01.xml");

        Assert.Contains(report, e => e.LineId == Id + "_intro_01");
    }

    [Fact]
    public void Multiple_companions_are_reported_in_package_order()
    {
        using var tmp = new TempDir();
        Fixtures.WriteValid(tmp.Path, "b_comp");
        Fixtures.WriteValid(tmp.Path, "a_comp");

        var report = MissingAudioReport.Find(CompanionLoader.LoadAll(tmp.Path).Packages);

        Assert.Equal(8, report.Count);
        Assert.Equal(new[] { "a_comp", "b_comp" }, report.Select(e => e.CompanionId).Distinct());
    }
}
