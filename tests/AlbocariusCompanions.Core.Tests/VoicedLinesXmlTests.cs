using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Audio;
using AlbocariusCompanions.Core.Models;
using Xunit;

namespace AlbocariusCompanions.Core.Tests;

public class VoicedLinesXmlTests
{
    private static CompanionPackage Package(params DialogLine[] lines) => new CompanionPackage(
        "jeremus", "/mods/AlbocariusCompanions/ModuleData/Companions/jeremus",
        new CompanionDefinition { Id = "jeremus" }, new DialogFile { Lines = lines.ToList() });

    private static DialogLine Line(string id, bool voiced = true, string? audio = null) =>
        new DialogLine { Id = id, Slot = "intro", Voiced = voiced, Audio = audio, Text = new Dictionary<string, string> { ["en"] = "x" } };

    private static List<(string? Id, string? Path)> Entries(System.Xml.Linq.XDocument doc) =>
        doc.Root!.Element("VoiceOvers")!.Elements("VoiceOver")
            .Select(v => (v.Attribute("id")?.Value, v.Element("Voice")?.Attribute("path")?.Value)).ToList();

    [Fact]
    public void Voiced_lines_with_audio_get_an_extensionless_path_relative_to_Languages()
    {
        var doc = VoicedLinesXml.Build(new[] { Package(Line("jeremus_intro_01")) }, _ => true);
        Assert.Equal(new List<(string?, string?)> { ("jeremus_intro_01", "../Companions/jeremus/audio/jeremus_intro_01") }, Entries(doc));
    }

    [Fact]
    public void Unvoiced_missing_audio_and_non_ogg_lines_are_left_out()
    {
        var package = Package(Line("jeremus_intro_01", voiced: false), Line("jeremus_intro_02"), Line("jeremus_intro_03", audio: "voice/a.wav"));
        var doc = VoicedLinesXml.Build(new[] { package }, path => !path.Contains("intro_02"));
        Assert.Empty(Entries(doc));
    }

    [Fact]
    public void Custom_audio_path_uses_forward_slashes()
    {
        var doc = VoicedLinesXml.Build(new[] { Package(Line("jeremus_intro_01", audio: "voice\\Custom.OGG")) }, _ => true);
        Assert.Equal("../Companions/jeremus/voice/Custom", Entries(doc).Single().Path);
    }

    [Fact]
    public void Empty_document_still_has_VoiceOvers()
    {
        var doc = VoicedLinesXml.Build(new CompanionPackage[0], _ => true);
        Assert.NotNull(doc.Root!.Element("VoiceOvers"));
    }

    [Theory]
    [InlineData("../../Modules/AlbocariusCompanions/ModuleData/Languages/../Companions/jeremus/audio/jeremus_intro_01",
                "../../Modules/AlbocariusCompanions/ModuleData/Companions/jeremus/audio/jeremus_intro_01.ogg")]
    [InlineData("C:\\Game\\Modules\\AlbocariusCompanions\\ModuleData\\Languages/../Companions/j/audio/x",
                "C:/Game/Modules/AlbocariusCompanions/ModuleData/Companions/j/audio/x.ogg")]
    public void Mod_voice_paths_resolve_to_the_companion_folder(string voicePath, string expected) =>
        Assert.Equal(expected, VoicedLinesXml.ResolveSoundPath(voicePath, "AlbocariusCompanions"));

    [Theory]
    [InlineData("../../Modules/SandBox/ModuleData/Languages/VoicedLines/EN/$PLATFORM/looters_01_004")]
    [InlineData("../../Modules/OtherMod/ModuleData/Languages/../Companions/x/audio/y")]
    [InlineData("")]
    public void Other_paths_are_not_touched(string voicePath) =>
        Assert.Null(VoicedLinesXml.ResolveSoundPath(voicePath, "AlbocariusCompanions"));
}
