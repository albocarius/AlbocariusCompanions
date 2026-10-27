using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using static AlbocariusCompanions.Game.TriggerPayloads;

namespace AlbocariusCompanions.Game;

internal static partial class TriggerAdapters
{
    /// <summary>GAME_MAP Q35 triggers (TriggerRegistry.Q35.cs); field names and sources as in the registry.</summary>
    private static void Q35(Dictionary<string, TriggerAdapter> d)
    {
        d["playerBattleEnded"] = (owner, fire) => CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(owner,
            mapEvent => Emit(fire, "playerBattleEnded", () => Of(MapEvent(mapEvent))));
        d["heroKilled"] = (owner, fire) => CampaignEvents.HeroKilledEvent.AddNonSerializedListener(owner,
            (victim, killer, detail, _) => Emit(fire, "heroKilled", () => Of(("victim", Hero(victim)), ("killer", Hero(killer)), ("detail", Text(detail)))));
        d["villageLooted"] = (owner, fire) => CampaignEvents.VillageLooted.AddNonSerializedListener(owner,
            village => Emit(fire, "villageLooted", () => Of(("village", Settlement(village)))));
    }
}
