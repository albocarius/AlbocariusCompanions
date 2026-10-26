using System;
using AlbocariusCompanions.Core.Dialogs;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace AlbocariusCompanions.Game;

/// <summary>The live values conversation conditions read, over the hero being talked to.</summary>
internal sealed class HeroConversationContext : IConversationContext
{
    private readonly Hero _hero;
    private readonly string _companionId;

    public HeroConversationContext(Hero hero, string companionId)
    {
        _hero = hero;
        _companionId = companionId;
    }

    public bool HasMet => _hero.HasMet;
    public bool IsHired => _hero.IsPlayerCompanion;
    public bool IsInPlayerParty => _hero.PartyBelongedTo == MobileParty.MainParty;
    public int Relation => (int)Math.Round(_hero.GetRelationWithPlayer());
    public int PlayerGold => Hero.MainHero.Gold;
    public bool ClanIsFull => CompanionDialogs.TooManyCompanions();
    public int? GetVariable(string reference) => ModRuntime.Variables.Get(_companionId, reference);

    public int? GoldOf(string actor) => actor switch
    {
        "player" => Hero.MainHero.Gold,
        "self" => _hero.Gold,
        _ => ModRuntime.Registry.Contains(actor) ? ModRuntime.HeroOf(actor)?.Gold : null,
    };

    public int PlayerItemCount(string itemId)
    {
        var item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
        return item == null ? 0 : MobileParty.MainParty.ItemRoster.GetItemNumber(item);
    }
}
