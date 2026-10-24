using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using AlbocariusCompanions.Core.Templates;

namespace AlbocariusCompanions.Core.Tests;

/// <summary>Unit tests for NpcCharacterXml (complement the acceptance tests for task 20c-core-npc-xml).</summary>
public class NpcCharacterXmlGeneratorTests
{
    private static ResolvedCompanion Companion(string id = "c1",
        string? body = "0xABC",
        IReadOnlyDictionary<string, int>? skills = null,
        IReadOnlyDictionary<string, int>? traits = null,
        string? civilianRoster = "roster_civilian",
        IReadOnlyDictionary<string, string>? battleItems = null,
        IReadOnlyDictionary<string, string>? civilianItems = null) =>
        new(id, "Name", false, "earnest", 30, "empire",
            skills ?? new Dictionary<string, int>(),
            traits ?? new Dictionary<string, int>(),
            "roster_battle", civilianRoster, body, battleItems, civilianItems);

    private static XElement Character(XDocument doc) => doc.Root!.Element("NPCCharacter")!;

    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AlbocariusCompanions.sln"))) dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
        }
    }

    private static void AssertValid(XDocument doc)
    {
        var set = new XmlSchemaSet();
        set.Add(null, Path.Combine(RepoRoot, "xmlschemas", "NPCCharacters.xsd"));
        var errors = new List<string>();
        doc.Validate(set, (_, e) => errors.Add($"{e.Severity}: {e.Message}"));
        Assert.Empty(errors);
    }

    [Fact]
    public void Child_elements_follow_face_skills_Traits_Equipments()
    {
        var character = Character(NpcCharacterXml.Build(new[]
        {
            Companion(skills: new Dictionary<string, int> { ["Athletics"] = 1 },
                traits: new Dictionary<string, int> { ["Valor"] = 1 }),
        }));

        Assert.Equal(new[] { "face", "skills", "Traits", "Equipments" },
            character.Elements().Select(e => e.Name.LocalName));
    }

    [Fact]
    public void No_face_element_when_body_properties_key_is_null() =>
        Assert.DoesNotContain("face",
            Character(NpcCharacterXml.Build(new[] { Companion(body: null) })).Elements().Select(e => e.Name.LocalName));

    [Fact]
    public void Skills_are_sorted_ordinal_not_culturally()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
            var skills = Character(NpcCharacterXml.Build(new[]
            {
                Companion(skills: new Dictionary<string, int> { ["a_skill"] = 1, ["B_skill"] = 2, ["OneHanded"] = 3 }),
            })).Element("skills")!.Elements("skill").Select(s => (string?)s.Attribute("id"));

            Assert.Equal(new[] { "B_skill", "OneHanded", "a_skill" }, skills);
        }
        finally { Thread.CurrentThread.CurrentCulture = previous; }
    }

    [Fact]
    public void Roster_slots_follow_the_canonical_order_and_unknown_slots_are_ignored()
    {
        var items = new Dictionary<string, string>
        {
            ["HorseHarness"] = "caparison",
            ["Body"] = "monk_robe",
            ["Item2"] = "bo_staff",
            ["Item0"] = "sword",
            ["Horse"] = "horse_c",
            ["Weapon0"] = "alias_that_core_validation_rejects",
        };

        var equipment = Character(NpcCharacterXml.Build(new[] { Companion(civilianRoster: null, battleItems: items) }))
            .Element("Equipments")!.Element("EquipmentRoster")!
            .Elements("equipment").Select(e => ((string?)e.Attribute("slot"), (string?)e.Attribute("id")));

        Assert.Equal(new (string?, string?)[]
        {
            ("Item0", "Item.sword"), ("Item2", "Item.bo_staff"), ("Body", "Item.monk_robe"),
            ("Horse", "Item.horse_c"), ("HorseHarness", "Item.caparison"),
        }, equipment);
    }

    [Fact]
    public void Two_inline_rosters_are_written_battle_first_and_stay_valid()
    {
        var doc = NpcCharacterXml.Build(new[]
        {
            Companion(battleItems: new Dictionary<string, string> { ["Body"] = "monk_robe" },
                civilianItems: new Dictionary<string, string> { ["Body"] = "wool_clothing" }),
        });
        AssertValid(doc);

        var equipments = Character(doc).Element("Equipments")!;
        Assert.Equal(new[] { "EquipmentRoster", "EquipmentRoster" },
            equipments.Elements().Select(e => e.Name.LocalName));
        Assert.Equal(new[] { "Item.monk_robe", "Item.wool_clothing" },
            equipments.Elements().Select(e => (string?)e.Element("equipment")!.Attribute("id")));
        Assert.Equal(new[] { (string?)null, "true" },
            equipments.Elements().Select(e => (string?)e.Attribute("civilian")));
    }

    [Fact]
    public void Battle_import_plus_civilian_fixed_items_still_validates_against_the_schema()
    {
        // The schema's Equipments content is a sequence (inline rosters, then imports), so a fixed
        // civilian set precedes the battle import even though the battle entry comes first elsewhere.
        var doc = NpcCharacterXml.Build(new[]
        {
            Companion(civilianItems: new Dictionary<string, string> { ["Body"] = "monk_robe" }),
        });
        AssertValid(doc);

        var equipments = Character(doc).Element("Equipments")!;
        Assert.Equal(new[] { "EquipmentRoster", "EquipmentSet" },
            equipments.Elements().Select(e => e.Name.LocalName));
        Assert.Equal("roster_battle", (string?)equipments.Element("EquipmentSet")!.Attribute("id"));
    }

    [Fact]
    public void Battle_fixed_items_plus_civilian_import_order_and_validate()
    {
        var doc = NpcCharacterXml.Build(new[]
        {
            Companion(battleItems: new Dictionary<string, string> { ["Head"] = "pilgrim_hood" }),
        });
        AssertValid(doc);

        var equipments = Character(doc).Element("Equipments")!;
        Assert.Equal(new[] { "EquipmentRoster", "EquipmentSet" },
            equipments.Elements().Select(e => e.Name.LocalName));

        var set = Assert.Single(equipments.Elements("EquipmentSet"));
        Assert.Equal("roster_civilian", (string?)set.Attribute("id"));
        Assert.Equal("Civilian", (string?)set.Attribute("equipmentType"));
        Assert.Null(equipments.Element("EquipmentRoster")!.Attribute("civilian"));
    }

    [Fact]
    public void Empty_fixed_item_set_emits_an_empty_roster_and_still_validates()
    {
        var doc = NpcCharacterXml.Build(new[] { Companion(civilianRoster: null, battleItems: new Dictionary<string, string>()) });

        var equipments = Character(doc).Element("Equipments")!;
        var roster = Assert.Single(equipments.Elements());
        Assert.Equal("EquipmentRoster", roster.Name.LocalName);
        Assert.Empty(roster.Elements());
        AssertValid(doc);
    }

    [Fact]
    public void Duplicate_id_error_names_the_offending_id()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            NpcCharacterXml.Build(new[] { Companion("dup"), Companion("other"), Companion("dup") }));
        Assert.Contains("dup", ex.Message);
    }

    [Fact]
    public void Document_has_a_declaration_and_loads_as_an_XmlDocument()
    {
        var doc = NpcCharacterXml.Build(new[] { Companion() });

        Assert.Equal("1.0", doc.Declaration?.Version);
        Assert.Equal("NPCCharacters", doc.Root!.Name.LocalName);
        Assert.Single(XDocument.Parse(doc.ToString()).Elements());
        var xml = new XmlDocument();
        xml.LoadXml(doc.ToString());
        Assert.Equal("NPCCharacters", xml.DocumentElement!.Name);
    }
}
