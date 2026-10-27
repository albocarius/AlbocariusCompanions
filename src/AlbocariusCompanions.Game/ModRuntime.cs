using System;
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

    /// <summary>Selection picks of the current talk (M9 F2); cleared when a conversation ends and at session launch.</summary>
    public static SelectionMemory Selections { get; } = new SelectionMemory();

    /// <summary>Companion id → its hero in the running campaign (null when it has none); set by the campaign behavior.</summary>
    public static Func<string, Hero?> HeroOf { get; set; } = _ => null;

    /// <summary>Companion id → registered template for the current campaign (AfterRegisterSubModuleObjects).</summary>
    public static IReadOnlyDictionary<string, CharacterObject> Templates { get; set; } = new Dictionary<string, CharacterObject>();

    /// <summary>The triggered conversation delivered on the map right now (M11 T5); cleared when the conversation ends.</summary>
    public static ActiveTrigger? ActiveTrigger { get; set; }

    /// <summary>Trigger engine host of the running campaign (M11); null before the campaign behavior is created.</summary>
    public static TriggerHost? Triggers { get; set; }

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

/// <summary>A triggered conversation being shown: whose, which, and the payload its conditions read.</summary>
internal sealed class ActiveTrigger
{
    public ActiveTrigger(string companionId, string conversationId, Core.Triggers.TriggerPayload payload)
    {
        CompanionId = companionId;
        ConversationId = conversationId;
        Payload = payload;
    }

    public string CompanionId { get; }
    public string ConversationId { get; }
    public Core.Triggers.TriggerPayload Payload { get; }
}
