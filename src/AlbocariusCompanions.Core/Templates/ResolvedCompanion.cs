using System.Collections.Generic;

namespace AlbocariusCompanions.Core.Templates;

/// <summary>
/// Companion data after the Game layer resolved every game id against the live object registry
/// (fallbacks applied, unknown skills/traits dropped). Input of <see cref="NpcCharacterXml"/>.
/// </summary>
public sealed class ResolvedCompanion
{
    public ResolvedCompanion(string id, string name, bool isFemale, string voice, int age, string cultureId,
        IReadOnlyDictionary<string, int> skills, IReadOnlyDictionary<string, int> traits,
        string battleEquipmentRosterId, string? civilianEquipmentRosterId, string? bodyPropertiesKey)
    {
        Id = id;
        Name = name;
        IsFemale = isFemale;
        Voice = voice;
        Age = age;
        CultureId = cultureId;
        Skills = skills;
        Traits = traits;
        BattleEquipmentRosterId = battleEquipmentRosterId;
        CivilianEquipmentRosterId = civilianEquipmentRosterId;
        BodyPropertiesKey = bodyPropertiesKey;
    }

    /// <summary>Companion id; becomes the template id (GAME_MAP Q10/Q11: hero.Template.StringId == companion id).</summary>
    public string Id { get; }
    public string Name { get; }
    public bool IsFemale { get; }
    /// <summary>Persona trait id: curt, earnest, ironic, softspoken (GAME_MAP Q12).</summary>
    public string Voice { get; }
    public int Age { get; }
    /// <summary>Bare culture id (e.g. "empire"); written as "Culture.empire".</summary>
    public string CultureId { get; }
    public IReadOnlyDictionary<string, int> Skills { get; }
    public IReadOnlyDictionary<string, int> Traits { get; }
    public string BattleEquipmentRosterId { get; }
    public string? CivilianEquipmentRosterId { get; }
    public string? BodyPropertiesKey { get; }
}
