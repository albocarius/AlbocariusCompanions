using System.Collections.Generic;
using AlbocariusCompanions.Core.Dialogs;
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

    /// <summary>Companion folders skipped at load because of errors (for the in-game summary).</summary>
    public static int SkippedAtLoad { get; set; }

    /// <summary>
    /// Conversation "once" markers of the running campaign. Replaced by each new campaign behavior (new game or load) and
    /// filled from the save in its SyncData (GAME_MAP Q20).
    /// </summary>
    public static ConversationFlags Flags { get; set; } = new ConversationFlags();

    /// <summary>Conversation variables of the running campaign (M7); replaced and restored like <see cref="Flags"/>.</summary>
    public static VariableStore Variables { get; set; } = new VariableStore();

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
