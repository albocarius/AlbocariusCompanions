using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using static AlbocariusCompanions.Game.TriggerPayloads;

namespace AlbocariusCompanions.Game;

internal static partial class TriggerAdapters
{
    /// <summary>GAME_MAP Q39 triggers (TriggerRegistry.Q39.cs); field names and sources as in the registry.</summary>
    private static void Q39(Dictionary<string, TriggerAdapter> d)
    {
        // ---- hero lifecycle (creation, state, leveling, parties) ----

        d["heroCreated"] = (owner, fire) => CampaignEvents.HeroCreated.AddNonSerializedListener(owner,
            (hero, isBornNaturally) => Emit(fire, "heroCreated", () => Of(("hero", Hero(hero)), ("bornNaturally", Bool(isBornNaturally)))));
        d["heroActivated"] = (owner, fire) => CampaignEvents.OnHeroActivatedEvent.AddNonSerializedListener(owner,
            (hero, previousState) => Emit(fire, "heroActivated", () => Of(("hero", Hero(hero)), ("previousState", Text(previousState)))));
        d["heroOccupationChanged"] = (owner, fire) => CampaignEvents.HeroOccupationChangedEvent.AddNonSerializedListener(owner,
            (hero, oldOccupation) => Emit(fire, "heroOccupationChanged", () => Of(("hero", Hero(hero)), ("oldOccupation", Text(oldOccupation)))));
        d["heroLeveledUp"] = (owner, fire) => CampaignEvents.HeroLevelledUp.AddNonSerializedListener(owner,
            (hero, _) => Emit(fire, "heroLeveledUp", () => Of(("hero", Hero(hero)))));
        d["heroGainedSkill"] = (owner, fire) => CampaignEvents.HeroGainedSkill.AddNonSerializedListener(owner,
            (hero, skill, changeAmount, _) => Emit(fire, "heroGainedSkill", () => Of(("hero", Hero(hero)), ("skill", Text(skill?.StringId)), ("change", Int(changeAmount)))));
        d["heroJoinedParty"] = (owner, fire) => CampaignEvents.OnHeroJoinedPartyEvent.AddNonSerializedListener(owner,
            (hero, newParty) => Emit(fire, "heroJoinedParty", () => Of(("hero", Hero(hero)), ("party", Party(newParty)))));
        d["companionAdded"] = (owner, fire) => CampaignEvents.NewCompanionAdded.AddNonSerializedListener(owner,
            hero => Emit(fire, "companionAdded", () => Of(("hero", Hero(hero)))));
        d["playerMetHero"] = (owner, fire) => CampaignEvents.OnPlayerMetHeroEvent.AddNonSerializedListener(owner,
            hero => Emit(fire, "playerMetHero", () => Of(("hero", Hero(hero)))));

        // ---- player development (traits, perks): the event has no entity argument, newLevel is read live off the player ----

        d["playerTraitChanged"] = (owner, fire) => CampaignEvents.PlayerTraitChangedEvent.AddNonSerializedListener(owner,
            (trait, previousLevel) => Emit(fire, "playerTraitChanged", () => Of(("trait", Text(trait?.StringId)), ("previousLevel", Int(previousLevel)), ("newLevel", Int(TaleWorlds.CampaignSystem.Hero.MainHero.GetTraitLevel(trait))))));
        d["perkUnlocked"] = (owner, fire) => CampaignEvents.PerkOpenedEvent.AddNonSerializedListener(owner,
            (hero, perk) => Emit(fire, "perkUnlocked", () => Of(("hero", Hero(hero)), ("perk", Text(perk?.StringId)))));

        // ---- clan (relations, tier, renown, creation, kingdom membership) ----

        d["relationChanged"] = (owner, fire) => CampaignEvents.HeroRelationChanged.AddNonSerializedListener(owner,
            (effectiveHero, effectiveHeroGainedRelationWith, relationChange, _, detail, originalHero, originalGainedRelationWith) => Emit(fire, "relationChanged", () => Of(("hero", Hero(effectiveHero)), ("other", Hero(effectiveHeroGainedRelationWith)), ("change", Int(relationChange)), ("detail", Text(detail)), ("actor", Hero(originalHero)), ("target", Hero(originalGainedRelationWith)))));
        d["clanTierIncreased"] = (owner, fire) => CampaignEvents.ClanTierIncrease.AddNonSerializedListener(owner,
            (clan, _) => Emit(fire, "clanTierIncreased", () => Of(("clan", Clan(clan)))));
        d["renownGained"] = (owner, fire) => CampaignEvents.RenownGained.AddNonSerializedListener(owner,
            (hero, gainedRenown, _) => Emit(fire, "renownGained", () => Of(("hero", Hero(hero)), ("amount", Int(gainedRenown)))));
        d["clanCreated"] = (owner, fire) => CampaignEvents.OnClanCreatedEvent.AddNonSerializedListener(owner,
            (clan, isCompanion) => Emit(fire, "clanCreated", () => Of(("clan", Clan(clan)), ("isCompanion", Bool(isCompanion)))));
        d["clanChangedKingdom"] = (owner, fire) => CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(owner,
            (clan, oldKingdom, newKingdom, detail, _) => Emit(fire, "clanChangedKingdom", () => Of(("clan", Clan(clan)), ("oldKingdom", Kingdom(oldKingdom)), ("newKingdom", Kingdom(newKingdom)), ("detail", Text(detail)))));
        d["clanDefected"] = (owner, fire) => CampaignEvents.OnClanDefectedEvent.AddNonSerializedListener(owner,
            (clan, oldKingdom, newKingdom) => Emit(fire, "clanDefected", () => Of(("clan", Clan(clan)), ("oldKingdom", Kingdom(oldKingdom)), ("newKingdom", Kingdom(newKingdom)))));

        // ---- kingdom (council decisions, wars, ruler, destruction): the decision's kingdom is read off the decision ----

        d["kingdomDecisionProposed"] = (owner, fire) => CampaignEvents.KingdomDecisionAdded.AddNonSerializedListener(owner,
            (decision, isPlayerInvolved) => Emit(fire, "kingdomDecisionProposed", () => Of(("kingdom", Kingdom(decision.Kingdom)), ("playerInvolved", Bool(isPlayerInvolved)))));
        d["kingdomDecisionCancelled"] = (owner, fire) => CampaignEvents.KingdomDecisionCancelled.AddNonSerializedListener(owner,
            (decision, isPlayerInvolved) => Emit(fire, "kingdomDecisionCancelled", () => Of(("kingdom", Kingdom(decision.Kingdom)), ("playerInvolved", Bool(isPlayerInvolved)))));
        d["kingdomDecisionConcluded"] = (owner, fire) => CampaignEvents.KingdomDecisionConcluded.AddNonSerializedListener(owner,
            (decision, _, isPlayerInvolved) => Emit(fire, "kingdomDecisionConcluded", () => Of(("kingdom", Kingdom(decision.Kingdom)), ("playerInvolved", Bool(isPlayerInvolved)))));
        d["warDeclared"] = (owner, fire) => CampaignEvents.WarDeclared.AddNonSerializedListener(owner,
            (attacker, defender, detail) => Emit(fire, "warDeclared", () => Of(("attacker", Faction(attacker)), ("defender", Faction(defender)), ("detail", Text(detail)))));
        d["peaceMade"] = (owner, fire) => CampaignEvents.MakePeace.AddNonSerializedListener(owner,
            (first, second, detail) => Emit(fire, "peaceMade", () => Of(("firstFaction", Faction(first)), ("secondFaction", Faction(second)), ("detail", Text(detail)))));
        d["rulingClanChanged"] = (owner, fire) => CampaignEvents.RulingClanChanged.AddNonSerializedListener(owner,
            (kingdom, oldRulingClan) => Emit(fire, "rulingClanChanged", () => Of(("kingdom", Kingdom(kingdom)), ("oldRuler", Clan(oldRulingClan)), ("newRuler", Clan(kingdom.RulingClan)))));
        d["kingdomDestroyed"] = (owner, fire) => CampaignEvents.KingdomDestroyedEvent.AddNonSerializedListener(owner,
            kingdom => Emit(fire, "kingdomDestroyed", () => Of(("kingdom", Kingdom(kingdom)))));

        // ---- family (romance, marriage, children growing up) ----

        d["romanticStateChanged"] = (owner, fire) => CampaignEvents.RomanticStateChanged.AddNonSerializedListener(owner,
            (hero1, hero2, level) => Emit(fire, "romanticStateChanged", () => Of(("firstHero", Hero(hero1)), ("secondHero", Hero(hero2)), ("level", Text(level)))));
        d["heroesAboutToMarry"] = (owner, fire) => CampaignEvents.BeforeHeroesMarried.AddNonSerializedListener(owner,
            (firstHero, secondHero, _) => Emit(fire, "heroesAboutToMarry", () => Of(("firstHero", Hero(firstHero)), ("secondHero", Hero(secondHero)))));
        d["childEducationCompleted"] = (owner, fire) => CampaignEvents.ChildEducationCompletedEvent.AddNonSerializedListener(owner,
            (child, age) => Emit(fire, "childEducationCompleted", () => Of(("child", Hero(child)), ("age", Int(age)))));
        d["heroCameOfAge"] = (owner, fire) => CampaignEvents.HeroComesOfAgeEvent.AddNonSerializedListener(owner,
            hero => Emit(fire, "heroCameOfAge", () => Of(("hero", Hero(hero)))));
        d["heroGrewOutOfInfancy"] = (owner, fire) => CampaignEvents.HeroGrowsOutOfInfancyEvent.AddNonSerializedListener(owner,
            hero => Emit(fire, "heroGrewOutOfInfancy", () => Of(("hero", Hero(hero)))));
        d["heroBecameTeenager"] = (owner, fire) => CampaignEvents.HeroReachesTeenAgeEvent.AddNonSerializedListener(owner,
            hero => Emit(fire, "heroBecameTeenager", () => Of(("hero", Hero(hero)))));
    }
}
