using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using AlbocariusCompanions.Core.Templates;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 20c-core-npc-xml. GAME_MAP Q5/Q11, SPEC rule 8 amendment.
public class NpcCharacterXmlTests
{
    static ResolvedCompanion Sample(string id = "test_companion", string? civilian = "npc_wanderer_equipment_template_empire_civilian",
        string? body = null, string name = "Test Companion") =>
        new(id, name, isFemale: false, voice: "earnest", age: 32, cultureId: "empire",
            skills: new Dictionary<string, int> { ["OneHanded"] = 100, ["Athletics"] = 60 },
            traits: new Dictionary<string, int> { ["Valor"] = 1, ["Mercy"] = -1 },
            battleEquipmentRosterId: "npc_wanderer_equipment_template_empire",
            civilianEquipmentRosterId: civilian, bodyPropertiesKey: body);

    static XElement Single(XDocument doc) => Assert.Single(doc.Root!.Elements("NPCCharacter"));

    static void AssertValidAgainstXsd(XDocument doc)
    {
        var xsd = Path.Combine(RepoPaths.Root, "xmlschemas", "NPCCharacters.xsd");
        Assert.True(File.Exists(xsd), $"game schema not found: {xsd} (xmlschemas/ symlink missing?)");
        var set = new XmlSchemaSet();
        set.Add(null, xsd);
        var errors = new List<string>();
        doc.Validate(set, (_, e) => errors.Add($"{e.Severity}: {e.Message}"));
        Assert.Empty(errors);
    }

    [Fact]
    public void Full_companion_validates_against_the_game_schema() =>
        AssertValidAgainstXsd(NpcCharacterXml.Build(new[] { Sample(body: "0x0000000000000000000000000000000000000000000000000000000000000000") }));

    [Fact]
    public void Minimal_companion_validates_against_the_game_schema() =>
        AssertValidAgainstXsd(NpcCharacterXml.Build(new[]
        {
            new ResolvedCompanion("bare", "Bare", true, "curt", 18, "vlandia",
                new Dictionary<string, int>(), new Dictionary<string, int>(), "roster_x", null, null),
        }));

    [Fact]
    public void Several_companions_validate_and_keep_input_order()
    {
        var doc = NpcCharacterXml.Build(new[] { Sample("b_comp"), Sample("a_comp") });
        AssertValidAgainstXsd(doc);
        Assert.Equal("NPCCharacters", doc.Root!.Name.LocalName);
        Assert.Equal(new[] { "b_comp", "a_comp" }, doc.Root.Elements("NPCCharacter").Select(e => (string?)e.Attribute("id")));
    }

    [Fact]
    public void Empty_input_gives_an_empty_valid_document()
    {
        var doc = NpcCharacterXml.Build(Array.Empty<ResolvedCompanion>());
        AssertValidAgainstXsd(doc);
        Assert.Empty(doc.Root!.Elements());
    }

    [Fact]
    public void Template_attributes()
    {
        var c = Single(NpcCharacterXml.Build(new[] { Sample() }));

        Assert.Equal("test_companion", (string?)c.Attribute("id"));
        Assert.Equal("true", (string?)c.Attribute("is_template"));
        // Not "Wanderer": a Wanderer template would enter the vanilla spawn pool (GAME_MAP Q11).
        Assert.Equal("Special", (string?)c.Attribute("occupation"));
        Assert.Equal("Culture.empire", (string?)c.Attribute("culture"));
        Assert.Equal("earnest", (string?)c.Attribute("voice"));
        Assert.Equal("32", (string?)c.Attribute("age"));
        Assert.Equal("false", (string?)c.Attribute("is_female"));
    }

    [Fact]
    public void Name_is_localization_ready()
    {
        var c = Single(NpcCharacterXml.Build(new[] { Sample() }));
        Assert.Equal("{=test_companion_name}Test Companion", (string?)c.Attribute("name"));
    }

    [Fact]
    public void Female_flag()
    {
        var c = Single(NpcCharacterXml.Build(new[]
        {
            new ResolvedCompanion("f", "F", true, "softspoken", 40, "aserai",
                new Dictionary<string, int>(), new Dictionary<string, int>(), "r", null, null),
        }));
        Assert.Equal("true", (string?)c.Attribute("is_female"));
    }

    [Fact]
    public void Skills_and_traits_are_written_sorted_by_id()
    {
        var c = Single(NpcCharacterXml.Build(new[] { Sample() }));

        var skills = c.Element("skills")!.Elements("skill").Select(s => ((string?)s.Attribute("id"), (string?)s.Attribute("value")));
        Assert.Equal(new[] { ("Athletics", "60"), ("OneHanded", "100") }, skills);
        var traits = c.Element("Traits")!.Elements("Trait").Select(s => ((string?)s.Attribute("id"), (string?)s.Attribute("value")));
        Assert.Equal(new[] { ("Mercy", "-1"), ("Valor", "1") }, traits);
    }

    [Fact]
    public void Empty_skills_and_traits_are_omitted()
    {
        var c = Single(NpcCharacterXml.Build(new[]
        {
            new ResolvedCompanion("e", "E", false, "curt", 30, "empire",
                new Dictionary<string, int>(), new Dictionary<string, int>(), "r", null, null),
        }));
        Assert.Null(c.Element("skills"));
        Assert.Null(c.Element("Traits"));
    }

    [Fact]
    public void Equipment_sets_follow_vanilla_shape()
    {
        var sets = Single(NpcCharacterXml.Build(new[] { Sample() })).Element("Equipments")!.Elements("EquipmentSet").ToList();

        Assert.Equal(2, sets.Count);
        Assert.Equal("npc_wanderer_equipment_template_empire", (string?)sets[0].Attribute("id"));
        Assert.Null(sets[0].Attribute("equipmentType"));          // battle set: vanilla omits the attribute
        Assert.Equal("npc_wanderer_equipment_template_empire_civilian", (string?)sets[1].Attribute("id"));
        Assert.Equal("Civilian", (string?)sets[1].Attribute("equipmentType"));
    }

    [Fact]
    public void Civilian_set_is_omitted_when_not_resolved()
    {
        var sets = Single(NpcCharacterXml.Build(new[] { Sample(civilian: null) })).Element("Equipments")!.Elements("EquipmentSet");
        Assert.Single(sets);
    }

    static ResolvedCompanion WithItems(IReadOnlyDictionary<string, string>? battle, IReadOnlyDictionary<string, string>? civilian) =>
        new("jeremus", "Jeremus", false, "softspoken", 50, "empire",
            new Dictionary<string, int>(), new Dictionary<string, int>(),
            "npc_wanderer_equipment_template_empire", "npc_wanderer_equipment_template_empire_civilian", null,
            battle, civilian);

    [Fact]
    public void Fixed_items_validate_against_the_game_schema() =>
        AssertValidAgainstXsd(NpcCharacterXml.Build(new[]
        {
            WithItems(new Dictionary<string, string> { ["Item0"] = "bo_staff", ["Body"] = "monk_robe", ["Head"] = "pilgrim_hood" },
                      new Dictionary<string, string> { ["Body"] = "monk_robe" }),
        }));

    [Fact]
    public void Fixed_battle_items_replace_the_battle_roster_import()
    {
        var eq = Single(NpcCharacterXml.Build(new[]
        {
            WithItems(new Dictionary<string, string> { ["Body"] = "monk_robe", ["Item0"] = "bo_staff", ["Head"] = "pilgrim_hood" }, null),
        })).Element("Equipments")!;

        var roster = Assert.Single(eq.Elements("EquipmentRoster"));
        Assert.Null(roster.Attribute("civilian"));
        // Slot order follows CompanionRules.EquipmentSlots; ids use the "Item." reference prefix (GAME_MAP Q5).
        Assert.Equal(new[] { ("Item0", "Item.bo_staff"), ("Head", "Item.pilgrim_hood"), ("Body", "Item.monk_robe") },
            roster.Elements("equipment").Select(e => ((string?)e.Attribute("slot"), (string?)e.Attribute("id"))));
        // Civilian still comes from the roster import.
        var set = Assert.Single(eq.Elements("EquipmentSet"));
        Assert.Equal("Civilian", (string?)set.Attribute("equipmentType"));
    }

    [Fact]
    public void Fixed_civilian_items_replace_the_civilian_roster_import()
    {
        var eq = Single(NpcCharacterXml.Build(new[]
        {
            WithItems(null, new Dictionary<string, string> { ["Body"] = "monk_robe" }),
        })).Element("Equipments")!;

        var set = Assert.Single(eq.Elements("EquipmentSet"));
        Assert.Null(set.Attribute("equipmentType"));             // battle import kept
        var roster = Assert.Single(eq.Elements("EquipmentRoster"));
        Assert.Equal("true", (string?)roster.Attribute("civilian"));
        Assert.Equal("Item.monk_robe", (string?)roster.Element("equipment")!.Attribute("id"));
    }

    [Fact]
    public void Body_properties_key_goes_into_face()
    {
        var c = Single(NpcCharacterXml.Build(new[] { Sample(body: "0xABC") }));
        var bp = c.Element("face")!.Element("BodyProperties")!;
        Assert.Equal("4", (string?)bp.Attribute("version"));
        Assert.Equal("0xABC", (string?)bp.Attribute("key"));
    }

    [Fact]
    public void No_face_element_without_body_properties() =>
        Assert.Null(Single(NpcCharacterXml.Build(new[] { Sample(body: null) })).Element("face"));

    [Fact]
    public void Special_characters_in_names_survive_a_round_trip()
    {
        var doc = NpcCharacterXml.Build(new[] { Sample(name: "Rhys \"the <Bold>\" & Co") });
        var reparsed = XDocument.Parse(doc.ToString());
        Assert.Equal("{=test_companion_name}Rhys \"the <Bold>\" & Co", (string?)reparsed.Root!.Element("NPCCharacter")!.Attribute("name"));
    }

    [Fact]
    public void Numbers_use_invariant_culture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
            var c = Single(NpcCharacterXml.Build(new[] { Sample() }));
            Assert.Equal("32", (string?)c.Attribute("age"));
            Assert.Equal("-1", (string?)c.Element("Traits")!.Elements("Trait").First().Attribute("value"));
        }
        finally { Thread.CurrentThread.CurrentCulture = previous; }
    }

    [Fact]
    public void Duplicate_ids_are_rejected() =>
        Assert.Throws<ArgumentException>(() => NpcCharacterXml.Build(new[] { Sample("x"), Sample("x") }));

    [Fact]
    public void Output_loads_as_an_XmlDocument()
    {
        // The Game layer hands an XmlDocument to MBObjectManager.LoadXml.
        var xml = new XmlDocument();
        xml.LoadXml(NpcCharacterXml.Build(new[] { Sample() }).ToString());
        Assert.Equal("NPCCharacters", xml.DocumentElement!.Name);
    }
}
