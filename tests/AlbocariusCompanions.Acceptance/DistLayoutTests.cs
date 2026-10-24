using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace AlbocariusCompanions.Acceptance;

// Task 22-game-build. TEST_CMD builds first, so dist/ reflects the current sources.
public class DistLayoutTests
{
    static readonly string Root = RepoPaths.Root;
    static readonly string Dist = Path.Combine(Root, "dist", "AlbocariusCompanions");
    static readonly string BinDir = Path.Combine(Dist, "bin", "Win64_Shipping_Client");

    [Theory]
    [InlineData("AlbocariusCompanions.Game.dll")]
    [InlineData("AlbocariusCompanions.Core.dll")]
    public void Mod_assemblies_are_in_the_module_bin_folder(string dll) =>
        Assert.True(File.Exists(Path.Combine(BinDir, dll)), $"missing {dll} in {BinDir}");

    [Fact]
    public void Only_mod_assemblies_are_shipped()
    {
        var dlls = Directory.GetFiles(Dist, "*.dll", SearchOption.AllDirectories).Select(Path.GetFileName).OrderBy(x => x);
        // No Newtonsoft (game ships it), no 0Harmony (Bannerlord.Harmony module), no TaleWorlds/DLC assemblies.
        Assert.Equal(new[] { "AlbocariusCompanions.Core.dll", "AlbocariusCompanions.Game.dll" }, dlls);
    }

    [Fact]
    public void Every_module_source_file_is_mirrored_into_dist()
    {
        var src = Path.Combine(Root, "module");
        foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories).Where(f => Path.GetFileName(f) != ".gitkeep"))
        {
            var rel = Path.GetRelativePath(src, file);
            var copy = Path.Combine(Dist, rel);
            Assert.True(File.Exists(copy), $"module/{rel} not copied to dist");
            Assert.Equal(File.ReadAllBytes(file), File.ReadAllBytes(copy));
        }
    }

    [Fact]
    public void Core_does_not_reference_TaleWorlds() =>
        Assert.DoesNotContain(References("AlbocariusCompanions.Core.dll"), r => r.StartsWith("TaleWorlds", StringComparison.Ordinal));

    [Fact]
    public void Game_does_not_reference_DLC_assemblies() =>
        Assert.DoesNotContain(References("AlbocariusCompanions.Game.dll"), r => r.StartsWith("NavalDLC", StringComparison.Ordinal));

    [Fact]
    public void Game_uses_the_game_newtonsoft_version()
    {
        var newtonsoft = ReferenceVersions("AlbocariusCompanions.Game.dll").Where(r => r.Name == "Newtonsoft.Json").ToList();
        Assert.All(newtonsoft, r => Assert.Equal(new Version(13, 0, 0, 0), r.Version));
    }

    static IEnumerable<string> References(string dll) => ReferenceVersions(dll).Select(r => r.Name);

    static List<(string Name, Version Version)> ReferenceVersions(string dll)
    {
        using var pe = new PEReader(File.OpenRead(Path.Combine(BinDir, dll)));
        var md = pe.GetMetadataReader();
        return md.AssemblyReferences.Select(h => md.GetAssemblyReference(h))
            .Select(r => (md.GetString(r.Name), r.Version)).ToList();
    }
}

static class RepoPaths
{
    public static readonly string Root = Find();

    static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AlbocariusCompanions.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
