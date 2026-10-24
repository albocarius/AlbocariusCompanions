using TaleWorlds.CampaignSystem;

namespace AlbocariusCompanions.Game;

/// <summary>
/// Spawns registry companions on new games and on loads (existing saves), persists companion id → hero, and registers
/// the companion dialogs. Distinctive type name and explicit StringId so save data is never confused (GAME_MAP Q9).
/// </summary>
internal sealed class AlbocariusCompanionsCampaignBehavior : CampaignBehaviorBase
{
    public const string BehaviorId = "AlbocariusCompanionsCampaignBehavior";

    public AlbocariusCompanionsCampaignBehavior() : base(BehaviorId)
    {
    }

    public override void RegisterEvents()
    {
        // task 27-game-behavior-spawn
    }

    public override void SyncData(IDataStore dataStore)
    {
        // task 27-game-behavior-spawn
    }
}
