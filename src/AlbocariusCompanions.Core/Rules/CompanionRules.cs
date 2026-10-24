using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AlbocariusCompanions.Core.Rules;

/// <summary>Constants for validation, grounded in docs/GAME_MAP.md (Q4 ranges, Q4/Q12 voice types).</summary>
public static class CompanionRules
{
    public static readonly Regex IdPattern = new Regex("^[a-z0-9_]+$");

    /// <summary>ISO 639-1 language code = lang file name (en, tr, de); mapped to game languages via supported_iso (GAME_MAP Q14).</summary>
    public static readonly Regex LanguageCodePattern = new Regex("^[a-z]{2}$");

    /// <summary>Player-name tokens such as {PLAYER_NAME}, {PLAYER.NAME}, {PLAYER}. Case-insensitive.</summary>
    public static readonly Regex PlayerTokenPattern = new Regex(@"\{\s*PLAYER[^}]*\}", RegexOptions.IgnoreCase);

    public const string EnglishCode = "en";

    // GAME_MAP Q4: SPEC default 0..330; game does not clamp skills, vanilla data <= 260.
    public const int SkillMin = 0;
    public const int SkillMax = 330;
    // GAME_MAP Q4: MaxAttribute = 10, 0 is bumped to 1 at creation.
    public const int AttributeMin = 1;
    public const int AttributeMax = 10;
    // GAME_MAP Q4: HeroComesOfAge = 18.
    public const int AgeMin = 18;
    public const int AgeMax = 80;

    /// <summary>The six attributes (GAME_MAP Q4, DefaultCharacterAttributes), JSON keys in lowercase.</summary>
    public static readonly IReadOnlyList<string> AttributeNames = new[] { "vigor", "control", "endurance", "cunning", "social", "intelligence" };

    /// <summary>Vanilla voice types = persona trait ids (GAME_MAP Q4 DefaultTraits; confirmed by Q12).</summary>
    public static readonly IReadOnlyList<string> VoiceTypes = new[] { "curt", "earnest", "ironic", "softspoken" };

    public static readonly IReadOnlyList<string> RelationAttitudes = new[] { "friend", "rival" };

    /// <summary>Equipment slot names as written in vanilla XML (GAME_MAP Q5, Equipment.GetEquipmentIndexFromOldEquipmentIndexName), in slot order.</summary>
    public static readonly IReadOnlyList<string> EquipmentSlots = new[]
        { "Item0", "Item1", "Item2", "Item3", "Head", "Body", "Leg", "Gloves", "Cape", "Horse", "HorseHarness" };
}
