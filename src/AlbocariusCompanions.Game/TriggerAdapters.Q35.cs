using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
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

        // ---- rest of the Q35 batch, in registry order ----

        d["heroWounded"] = (owner, fire) => CampaignEvents.HeroWounded.AddNonSerializedListener(owner,
            hero => Emit(fire, "heroWounded", () => Of(("hero", Hero(hero)))));
        d["battleStarted"] = (owner, fire) => CampaignEvents.BattleStarted.AddNonSerializedListener(owner,
            (attacker, defender, subject, _) => Emit(fire, "battleStarted", () => Of(("attacker", Party(attacker)), ("defender", Party(defender)), ("settlement", Settlement(subject as Settlement)))));
        d["mapEventStarted"] = (owner, fire) => CampaignEvents.MapEventStarted.AddNonSerializedListener(owner,
            (mapEvent, _, _) => Emit(fire, "mapEventStarted", () => Of(MapEvent(mapEvent))));
        d["mapEventEnded"] = (owner, fire) => CampaignEvents.MapEventEnded.AddNonSerializedListener(owner,
            mapEvent => Emit(fire, "mapEventEnded", () => Of(MapEvent(mapEvent))));
        d["playerDesertedBattle"] = (owner, fire) => CampaignEvents.PlayerDesertedBattleEvent.AddNonSerializedListener(owner,
            sacrificedMenCount => Emit(fire, "playerDesertedBattle", () => Of(("menLeft", Int(sacrificedMenCount)))));
        d["heroHitInCombat"] = (owner, fire) => CampaignEvents.OnHeroCombatHitEvent.AddNonSerializedListener(owner,
            (attacker, victim, attackerParty, _, isFatal, xp) => Emit(fire, "heroHitInCombat", () => Of(("attacker", Hero(attacker)), ("victim", Hero(victim)), ("attackerParty", Party(attackerParty)), ("fatal", Bool(isFatal)), ("xp", Int(xp)))));
        d["siegeStarted"] = (owner, fire) => CampaignEvents.OnSiegeEventStartedEvent.AddNonSerializedListener(owner,
            siegeEvent => Emit(fire, "siegeStarted", () => Of(("settlement", Settlement(siegeEvent.BesiegedSettlement)), ("attacker", Party(siegeEvent.BesiegerCamp.LeaderParty)))));
        d["playerJoinedSiege"] = (owner, fire) => CampaignEvents.OnPlayerSiegeStartedEvent.AddNonSerializedListener(owner,
            () => Emit(fire, "playerJoinedSiege", () => Of()));
        d["crimeRatingChanged"] = (owner, fire) => CampaignEvents.CrimeRatingChanged.AddNonSerializedListener(owner,
            (faction, deltaCrimeAmount) => Emit(fire, "crimeRatingChanged", () => Of(("faction", Faction(faction)), ("delta", Number(deltaCrimeAmount)))));
        d["heroBecameFugitive"] = (owner, fire) => CampaignEvents.CharacterBecameFugitiveEvent.AddNonSerializedListener(owner,
            (fugitive, _) => Emit(fire, "heroBecameFugitive", () => Of(("hero", Hero(fugitive)))));
        d["banditPartyRecruited"] = (owner, fire) => CampaignEvents.BanditPartyRecruited.AddNonSerializedListener(owner,
            banditParty => Emit(fire, "banditPartyRecruited", () => Of(("party", Party(banditParty)))));
        d["trackSpotted"] = (owner, fire) => CampaignEvents.TrackDetectedEvent.AddNonSerializedListener(owner,
            track => Emit(fire, "trackSpotted", () => Of(("partyName", Text(track.PartyName)), ("partyType", Text(track.PartyType)), ("men", Int(track.NumberOfAllMembers)), ("isEnemy", Bool(track.IsEnemy)))));
        d["trackLost"] = (owner, fire) => CampaignEvents.TrackLostEvent.AddNonSerializedListener(owner,
            track => Emit(fire, "trackLost", () => Of(("partyName", Text(track.PartyName)), ("partyType", Text(track.PartyType)), ("men", Int(track.NumberOfAllMembers)), ("isEnemy", Bool(track.IsEnemy)))));
        d["heroCaptured"] = (owner, fire) => CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(owner,
            (capturerParty, prisoner) => Emit(fire, "heroCaptured", () => Of(("prisoner", Hero(prisoner)), ("captor", Party(capturerParty)))));
        d["heroReleased"] = (owner, fire) => CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(owner,
            (prisoner, formerCaptorParty, capturerFaction, detail, _) => Emit(fire, "heroReleased", () => Of(("prisoner", Hero(prisoner)), ("formerCaptor", Party(formerCaptorParty)), ("formerCaptorFaction", Faction(capturerFaction)), ("detail", Text(detail)))));
        d["prisonersChangedInSettlement"] = (owner, fire) => CampaignEvents.PrisonersChangeInSettlement.AddNonSerializedListener(owner,
            (settlement, _, prisonerHero, takenFromDungeon) => Emit(fire, "prisonersChangedInSettlement", () => Of(("settlement", Settlement(settlement)), ("prisoner", Hero(prisonerHero)), ("takenFromDungeon", Bool(takenFromDungeon)))));
        d["ransomOfferCancelled"] = (owner, fire) => CampaignEvents.OnRansomOfferCancelledEvent.AddNonSerializedListener(owner,
            captiveHero => Emit(fire, "ransomOfferCancelled", () => Of(("captive", Hero(captiveHero)))));
        d["villageRaidStarted"] = (owner, fire) => CampaignEvents.VillageBeingRaided.AddNonSerializedListener(owner,
            village => Emit(fire, "villageRaidStarted", () => Of(("village", Settlement(village)))));
        d["itemsLooted"] = (owner, fire) => CampaignEvents.ItemsLooted.AddNonSerializedListener(owner,
            (lootingParty, _) => Emit(fire, "itemsLooted", () => Of(("looter", Party(lootingParty)))));
        d["townRebellionStateChanged"] = (owner, fire) => CampaignEvents.TownRebelliosStateChanged.AddNonSerializedListener(owner,
            (town, inRebelliousState) => Emit(fire, "townRebellionStateChanged", () => Of(("town", Settlement(town)), ("rebellious", Bool(inRebelliousState)))));
        d["rebellionSucceeded"] = (owner, fire) => CampaignEvents.RebellionFinished.AddNonSerializedListener(owner,
            (settlement, oldOwnerClan) => Emit(fire, "rebellionSucceeded", () => Of(("settlement", Settlement(settlement)), ("oldOwner", Clan(oldOwnerClan)))));
        d["heroAboutToBeKilled"] = (owner, fire) => CampaignEvents.BeforeHeroKilledEvent.AddNonSerializedListener(owner,
            (victim, killer, detail, _) => Emit(fire, "heroAboutToBeKilled", () => Of(("victim", Hero(victim)), ("killer", Hero(killer)), ("detail", Text(detail)))));
    }
}
