using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using AlbocariusCompanions.Core.Rules;

namespace AlbocariusCompanions.Core.Templates;

/// <summary>
/// Builds the NPCCharacters document for mod companion templates (SPEC rule 8 amendment, GAME_MAP Q11).
/// Generated at runtime, loaded with MBObjectManager.LoadXml; never committed or shipped.
/// </summary>
public static class NpcCharacterXml
{
    /// <summary>GAME_MAP Q11: the template must not be occupation="Wanderer" or it joins the vanilla spawn pool.</summary>
    public const string TemplateOccupation = "Special";

    /// <summary>BodyProperties key encoding version the game writes in vanilla character XML.</summary>
    public const string BodyPropertiesVersion = "4";

    /// <summary>One NPCCharacter per companion, in input order. Grammar per xmlschemas/NPCCharacters.xsd.</summary>
    public static XDocument Build(IEnumerable<ResolvedCompanion> companions)
    {
        if (companions == null) throw new ArgumentNullException(nameof(companions));

        var root = new XElement("NPCCharacters");
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var companion in companions)
        {
            if (companion == null) throw new ArgumentException("The companion list contains a null entry.", nameof(companions));
            if (!seenIds.Add(companion.Id))
                throw new ArgumentException($"Duplicate companion id '{companion.Id}'.", nameof(companions));

            root.Add(CharacterElement(companion));
        }

        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }

    static XElement CharacterElement(ResolvedCompanion companion)
    {
        var character = new XElement("NPCCharacter",
            new XAttribute("id", companion.Id),
            // "{=<key>}<fallback>" is the localization form the game resolves (GAME_MAP Q14).
            new XAttribute("name", $"{{={companion.Id}_name}}{companion.Name}"),
            new XAttribute("is_template", "true"),
            new XAttribute("occupation", TemplateOccupation),
            // culture is required by the schema with pattern Culture\..+ (GAME_MAP Q5).
            new XAttribute("culture", "Culture." + companion.CultureId),
            new XAttribute("voice", companion.Voice),
            new XAttribute("age", Invariant(companion.Age)),
            new XAttribute("is_female", companion.IsFemale ? "true" : "false"));

        if (companion.BodyPropertiesKey != null)
        {
            character.Add(new XElement("face",
                new XElement("BodyProperties",
                    new XAttribute("version", BodyPropertiesVersion),
                    new XAttribute("key", companion.BodyPropertiesKey))));
        }

        AddValueMap(character, "skills", "skill", companion.Skills);
        AddValueMap(character, "Traits", "Trait", companion.Traits);
        character.Add(EquipmentsElement(companion));

        return character;
    }

    static void AddValueMap(XElement parent, string wrapperName, string childName, IReadOnlyDictionary<string, int> values)
    {
        if (values.Count == 0) return;

        parent.Add(new XElement(wrapperName, values
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new XElement(childName,
                new XAttribute("id", entry.Key),
                new XAttribute("value", Invariant(entry.Value))))));
    }

    static XElement EquipmentsElement(ResolvedCompanion companion)
    {
        var equipments = new XElement("Equipments");

        var battleRoster = companion.BattleItems != null;
        var civilianRoster = companion.CivilianItems != null;

        // The schema's Equipments content is a sequence: every inline EquipmentRoster before every
        // EquipmentSet import. Entries stay battle-first, civilian-second except in the mixed case
        // (battle from a roster import, civilian from fixed items), where the schema wins.
        if (battleRoster) equipments.Add(RosterElement(companion.BattleItems!, civilian: false));
        if (civilianRoster) equipments.Add(RosterElement(companion.CivilianItems!, civilian: true));

        if (!battleRoster && !string.IsNullOrEmpty(companion.BattleEquipmentRosterId))
        {
            // Battle is the default equipment type, so vanilla writes no equipmentType attribute.
            equipments.Add(new XElement("EquipmentSet", new XAttribute("id", companion.BattleEquipmentRosterId)));
        }

        if (!civilianRoster && !string.IsNullOrEmpty(companion.CivilianEquipmentRosterId))
        {
            equipments.Add(new XElement("EquipmentSet",
                new XAttribute("id", companion.CivilianEquipmentRosterId),
                new XAttribute("equipmentType", "Civilian")));
        }

        return equipments;
    }

    /// <summary>Inline roster: slots in CompanionRules.EquipmentSlots order, ids with the "Item." reference prefix (GAME_MAP Q5).</summary>
    static XElement RosterElement(IReadOnlyDictionary<string, string> items, bool civilian)
    {
        var roster = new XElement("EquipmentRoster");
        // The game XSD allows only civilian="true" on an inline roster, not equipmentType.
        if (civilian) roster.Add(new XAttribute("civilian", "true"));

        foreach (var slot in CompanionRules.EquipmentSlots)
        {
            if (!items.TryGetValue(slot, out var itemId)) continue;
            roster.Add(new XElement("equipment",
                new XAttribute("slot", slot),
                new XAttribute("id", "Item." + itemId)));
        }

        return roster;
    }

    static string Invariant(int value) => value.ToString(CultureInfo.InvariantCulture);
}
