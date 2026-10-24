using System.Collections.Generic;
using AlbocariusCompanions.Core.Logging;
using AlbocariusCompanions.Core.Registry;
using TaleWorlds.CampaignSystem;

namespace AlbocariusCompanions.Game;

/// <summary>Process-wide state shared by the submodule, the campaign behavior and the Harmony patches.</summary>
internal static class ModRuntime
{
    public const string ModuleId = "AlbocariusCompanions";

    /// <summary>Set in OnSubModuleLoad; never null afterwards (a logger with an unusable path just writes nothing).</summary>
    public static ModLogger Log { get; set; } = new ModLogger(string.Empty);

    /// <summary>Valid companions loaded from ModuleData/Companions; empty until OnSubModuleLoad finished.</summary>
    public static CompanionRegistry Registry { get; set; } = new CompanionRegistry(new List<Core.Models.CompanionPackage>());

    /// <summary>Companion id → registered template for the current campaign (AfterRegisterSubModuleObjects).</summary>
    public static IReadOnlyDictionary<string, CharacterObject> Templates { get; set; } = new Dictionary<string, CharacterObject>();

    /// <summary>
    /// True for heroes created from a mod template: the template id equals the companion id (GAME_MAP Q2/Q10/Q11).
    /// Null-safe: Hero.Template is null for heroes whose character is not derived from a template.
    /// </summary>
    public static bool IsModHero(Hero? hero)
    {
        var templateId = hero?.Template?.StringId;
        return templateId != null && Registry.Contains(templateId);
    }
}
