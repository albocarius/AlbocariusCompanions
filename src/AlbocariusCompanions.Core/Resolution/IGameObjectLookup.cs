namespace AlbocariusCompanions.Core.Resolution;

/// <summary>
/// The only view the resolution rules have of the running game. The Game layer implements it with
/// MBObjectManager.GetObject&lt;T&gt;(id) != null and Equipment.IsItemFitsToSlot (GAME_MAP Q5, Q13).
/// </summary>
public interface IGameObjectLookup
{
    bool CultureExists(string id);
    bool SkillExists(string id);
    bool TraitExists(string id);
    bool EquipmentRosterExists(string id);
    bool ItemExists(string id);
    /// <summary>True when the item exists and fits the slot (Item0..Item3, Head, Body, Leg, Gloves, Cape, Horse, HorseHarness).</summary>
    bool ItemFitsSlot(string slot, string itemId);
}
