using System.Collections.Generic;

namespace AlbocariusCompanions.Core.Triggers;

public static partial class TriggerRegistry
{
    /// <summary>GAME_MAP Q35 triggers (orchestra/triggers.tsv rows with q = Q35).</summary>
    private static IEnumerable<TriggerDefinition> Q35() => new TriggerDefinition[]
    {
        new TriggerDefinition("playerBattleEnded", "battle",
            "A battle the player fought in ended (winner known; casualties and loot not yet counted).",
            "OnPlayerBattleEndEvent",
            fields: MapEventFields("mapEvent"),
            // Always the player's battle: "any" is the natural default and matches every time.
            defaultSubject: Subjects.Any, common: true),
        new TriggerDefinition("heroKilled", "heroes", "A hero died (battle, execution, old age, …).", "HeroKilledEvent",
            fields: new[]
            {
                F("victim", FieldKind.Hero, "the hero who died", "victim"),
                F("killer", FieldKind.Hero, "who killed them; empty for natural deaths", "killer"),
                F("detail", FieldKind.Text, "the game's KillCharacterActionDetail: DiedInBattle, Executed, DiedOfOldAge, …", "detail"),
            },
            subjectFields: S("victim"), defaultSubject: Subjects.PlayerClan, common: true),
        new TriggerDefinition("villageLooted", "raids", "A village raid finished and the village is now looted.", "VillageLooted",
            fields: new[] { F("village", FieldKind.Settlement, "the looted village", "village.Settlement") },
            subjectFields: S("village"), defaultSubject: Subjects.PlayerClan, common: true),
    };
}
