using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace AlbocariusCompanions.Acceptance;

// Tasks 28/30: the Game assembly exposes what SubModule.xml and Harmony expect (checked via metadata, no game needed).
public class GameAssemblyTests
{
    static readonly string Dll = Path.Combine(RepoPaths.Root, "dist", "AlbocariusCompanions", "bin", "Win64_Shipping_Client", "AlbocariusCompanions.Game.dll");

    static List<(string Namespace, string Name, bool IsPublic, string? Base)> Types()
    {
        using var pe = new PEReader(File.OpenRead(Dll));
        var md = pe.GetMetadataReader();
        var list = new List<(string, string, bool, string?)>();
        foreach (var h in md.TypeDefinitions)
        {
            var t = md.GetTypeDefinition(h);
            string? baseName = null;
            if (!t.BaseType.IsNil && t.BaseType.Kind == HandleKind.TypeReference)
                baseName = md.GetString(md.GetTypeReference((TypeReferenceHandle)t.BaseType).Name);
            var isPublic = (t.Attributes & System.Reflection.TypeAttributes.VisibilityMask) == System.Reflection.TypeAttributes.Public;
            list.Add((md.GetString(t.Namespace), md.GetString(t.Name), isPublic, baseName));
        }
        return list;
    }

    [Fact]
    public void Submodule_class_named_in_SubModule_xml_exists_and_is_public()
    {
        var t = Assert.Single(Types(), x => x.Namespace == "AlbocariusCompanions.Game" && x.Name == "SubModule");
        Assert.True(t.IsPublic);
        Assert.Equal("MBSubModuleBase", t.Base);
    }

    [Fact]
    public void Campaign_behavior_derives_from_the_game_base() =>
        Assert.Equal("CampaignBehaviorBase", Types().Single(x => x.Name == "AlbocariusCompanionsCampaignBehavior").Base);

    [Theory]
    [InlineData("ExcludeModCompanionsFromWandererCount")]
    [InlineData("HideVanillaHireOptionForModCompanions")]
    [InlineData("ReturnDismissedModCompanionToTavern")]
    [InlineData("KeepModCompanionsOnLowRelation")] // M8 V1
    [InlineData("NoGrievanceForModCompanions")] // M8 V2
    [InlineData("DropModCompanionGrievances")]
    [InlineData("SilenceGrievanceOpenersForModCompanions")]
    [InlineData("MarkOverLimitRemoval")] // M8 V3
    [InlineData("KeepModCompanionsOverTheLimit")]
    public void Harmony_patch_classes_exist(string name) => Assert.Contains(Types(), x => x.Name == name);

    [Fact]
    public void Assembly_version_is_0_2_2()
    {
        using var pe = new PEReader(File.OpenRead(Dll));
        Assert.Equal(new Version(0, 4, 0, 0), pe.GetMetadataReader().GetAssemblyDefinition().Version);
    }
}
