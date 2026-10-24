using AlbocariusCompanions.Core.Resolution;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace AlbocariusCompanions.Game;

/// <summary>
/// Live-game side of <see cref="IGameObjectLookup"/>: string-id lookups that return null for unknown ids (GAME_MAP Q13),
/// slot checks as the XML loader does them (GAME_MAP Q5). Valid once the game registered its objects (Q11).
/// </summary>
internal sealed class GameObjectLookup : IGameObjectLookup
{
    private static MBObjectManager Objects => MBObjectManager.Instance;

    public bool CultureExists(string id) => Objects.GetObject<CultureObject>(id) != null;

    public bool SkillExists(string id) => Objects.GetObject<SkillObject>(id) != null;

    public bool TraitExists(string id) => Objects.GetObject<TraitObject>(id) != null;

    public bool EquipmentRosterExists(string id) => Objects.GetObject<MBEquipmentRoster>(id) != null;

    public bool ItemExists(string id) => Objects.GetObject<ItemObject>(id) != null;

    public bool ItemFitsSlot(string slot, string itemId)
    {
        var item = Objects.GetObject<ItemObject>(itemId);
        if (item == null)
            return false;
        var index = Equipment.GetEquipmentIndexFromOldEquipmentIndexName(slot);
        return index != EquipmentIndex.None && Equipment.IsItemFitsToSlot(index, item);
    }
}
