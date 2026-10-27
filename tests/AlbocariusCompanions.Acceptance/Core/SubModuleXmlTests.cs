using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AlbocariusCompanions.Acceptance;

// Task 23-game-submodule-xml (GAME_MAP Q16; SubModule.xml has no XSD, so the shape is checked structurally).
public class SubModuleXmlTests
{
    static readonly XElement Module = XDocument.Load(Path.Combine(RepoPaths.Root, "module", "SubModule.xml")).Root!;

    static string? Value(XElement parent, string name) => (string?)parent.Element(name)?.Attribute("value");

    [Fact]
    public void Root_and_identity()
    {
        Assert.Equal("Module", Module.Name.LocalName);
        Assert.Equal("AlbocariusCompanions", Value(Module, "Id"));
        Assert.False(string.IsNullOrWhiteSpace(Value(Module, "Name")));
        Assert.Equal("v0.3.1", Value(Module, "Version"));
    }

    [Fact]
    public void Version_matches_the_game_grammar() =>
        // ApplicationVersion.FromString: 3 or 4 dot-separated parts, first part prefixed with the type letter.
        Assert.Matches(new Regex(@"^[vaeb]\d+\.\d+\.\d+(\.\d+)?$"), Value(Module, "Version")!);

    [Fact]
    public void Single_player_community_module()
    {
        Assert.Equal("false", Value(Module, "DefaultModule"));
        Assert.Equal("Singleplayer", Value(Module, "ModuleCategory"));
        Assert.Equal("Community", Value(Module, "ModuleType"));
    }

    [Fact]
    public void Depends_on_harmony_and_the_sandbox_stack()
    {
        var ids = Module.Element("DependedModules")!.Elements("DependedModule").Select(e => (string?)e.Attribute("Id")).ToList();
        // Exact ids, case-sensitive: the SandBox module's id is "Sandbox" (GAME_MAP Q16). No StoryMode: sandbox must work alone.
        Assert.Equal(new[] { "Bannerlord.Harmony", "Native", "SandBoxCore", "Sandbox" }, ids);
        Assert.All(Module.Element("DependedModules")!.Elements("DependedModule"),
            e => Assert.NotEqual("true", (string?)e.Attribute("Optional")));
    }

    [Fact]
    public void One_submodule_pointing_at_the_game_dll()
    {
        var sub = Assert.Single(Module.Element("SubModules")!.Elements("SubModule"));
        Assert.False(string.IsNullOrWhiteSpace(Value(sub, "Name")));
        Assert.Equal("AlbocariusCompanions.Game.dll", Value(sub, "DLLName"));
        Assert.Equal("AlbocariusCompanions.Game.SubModule", Value(sub, "SubModuleClassType"));
        // Core is preloaded from the same bin folder before the main DLL (GAME_MAP Q16 "Assemblies").
        var assemblies = sub.Element("Assemblies")!.Elements("Assembly").Select(a => (string?)a.Attribute("value"));
        Assert.Equal(new[] { "AlbocariusCompanions.Core.dll" }, assemblies);
    }

    [Fact]
    public void No_xml_nodes_templates_are_generated_at_runtime() =>
        // SPEC rule 8 amendment: no XML file is registered; Xmls stays empty or absent.
        Assert.Empty(Module.Element("Xmls")?.Elements() ?? Enumerable.Empty<XElement>());

    [Fact]
    public void Dist_contains_the_same_file() =>
        Assert.Equal(File.ReadAllText(Path.Combine(RepoPaths.Root, "module", "SubModule.xml")),
            File.ReadAllText(Path.Combine(RepoPaths.Root, "dist", "AlbocariusCompanions", "SubModule.xml")));
}
