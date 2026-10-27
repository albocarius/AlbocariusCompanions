using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Triggers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace AlbocariusCompanions.Game;

/// <summary>
/// Game objects → trigger payload entities (M11 G2): the fixed properties of <see cref="EntityProperties"/> plus the
/// subject flags. A null object gives null: the field is left out of the payload (subjects and filters on it fail).
/// </summary>
internal static class TriggerPayloads
{
    /// <summary>A payload from (field, value) pairs; null values are skipped.</summary>
    public static TriggerPayload Of(params (string Field, PayloadValue? Value)[] fields) =>
        new TriggerPayload(fields.Where(f => f.Value != null).ToDictionary(f => f.Field, f => f.Value!));

    public static PayloadValue? Hero(Hero? hero)
    {
        if (hero == null)
            return null;
        var isPlayer = hero == TaleWorlds.CampaignSystem.Hero.MainHero;
        return PayloadValue.Entity(FieldKind.Hero, new Dictionary<string, object>
        {
            ["id"] = hero.StringId ?? "",
            ["name"] = hero.Name?.ToString() ?? "",
            ["culture"] = hero.Culture?.StringId ?? "",
            ["clan"] = hero.Clan?.StringId ?? "",
            ["kingdom"] = hero.Clan?.Kingdom?.StringId ?? "",
            ["occupation"] = hero.Occupation.ToString(),
            ["isCompanion"] = hero.IsPlayerCompanion,
            ["isAlive"] = hero.IsAlive,
            ["isPlayer"] = isPlayer,
            ["inPlayerParty"] = isPlayer || (hero.PartyBelongedTo != null && hero.PartyBelongedTo == MobileParty.MainParty),
            // Hero.Clan is CompanionOf ?? own clan, so hired companions count as the player's clan (GAME_MAP Q21).
            ["inPlayerClan"] = hero.Clan != null && hero.Clan == TaleWorlds.CampaignSystem.Clan.PlayerClan,
            ["companion"] = CompanionIdOf(hero),
            ["heroId"] = hero.StringId ?? "",
        });
    }

    /// <summary>A CharacterObject that is a hero (tournament winners, combat hits); troops give null.</summary>
    public static PayloadValue? Hero(CharacterObject? character) => Hero(character?.HeroObject);

    public static PayloadValue? Settlement(Settlement? settlement)
    {
        if (settlement == null)
            return null;
        var owner = settlement.OwnerClan;
        return PayloadValue.Entity(FieldKind.Settlement, new Dictionary<string, object>
        {
            ["id"] = settlement.StringId ?? "",
            ["name"] = settlement.Name?.ToString() ?? "",
            ["culture"] = settlement.Culture?.StringId ?? "",
            ["kind"] = settlement.IsTown ? "town" : settlement.IsCastle ? "castle" : settlement.IsVillage ? "village" : settlement.IsHideout ? "hideout" : "other",
            ["ownerClan"] = owner?.StringId ?? "",
            ["kingdom"] = owner?.Kingdom?.StringId ?? "",
            ["inPlayerClan"] = owner != null && owner == TaleWorlds.CampaignSystem.Clan.PlayerClan,
        });
    }

    public static PayloadValue? Settlement(Town? town) => Settlement(town?.Settlement);

    public static PayloadValue? Settlement(Village? village) => Settlement(village?.Settlement);

    public static PayloadValue? Clan(Clan? clan)
    {
        if (clan == null)
            return null;
        var isPlayer = clan == TaleWorlds.CampaignSystem.Clan.PlayerClan;
        return PayloadValue.Entity(FieldKind.Clan, new Dictionary<string, object>
        {
            ["id"] = clan.StringId ?? "",
            ["name"] = clan.Name?.ToString() ?? "",
            ["culture"] = clan.Culture?.StringId ?? "",
            ["kingdom"] = clan.Kingdom?.StringId ?? "",
            ["leader"] = clan.Leader?.StringId ?? "",
            ["isPlayer"] = isPlayer,
            ["inPlayerClan"] = isPlayer,
        });
    }

    public static PayloadValue? Kingdom(Kingdom? kingdom)
    {
        if (kingdom == null)
            return null;
        var isPlayer = kingdom == TaleWorlds.CampaignSystem.Clan.PlayerClan?.Kingdom;
        return PayloadValue.Entity(FieldKind.Kingdom, new Dictionary<string, object>
        {
            ["id"] = kingdom.StringId ?? "",
            ["name"] = kingdom.Name?.ToString() ?? "",
            ["culture"] = kingdom.Culture?.StringId ?? "",
            ["leader"] = kingdom.Leader?.StringId ?? "",
            ["isPlayer"] = isPlayer,
            ["inPlayerClan"] = isPlayer,
        });
    }

    /// <summary>An IFaction: a kingdom or a clan (wars, peace, crime).</summary>
    public static PayloadValue? Faction(IFaction? faction)
    {
        if (faction == null)
            return null;
        var kingdom = faction as Kingdom;
        var clan = faction as Clan;
        var playerClan = TaleWorlds.CampaignSystem.Clan.PlayerClan;
        var isPlayer = kingdom != null ? kingdom == playerClan?.Kingdom : clan != null && clan == playerClan;
        return PayloadValue.Entity(FieldKind.Faction, new Dictionary<string, object>
        {
            ["id"] = faction.StringId ?? "",
            ["name"] = faction.Name?.ToString() ?? "",
            ["culture"] = faction.Culture?.StringId ?? "",
            ["leader"] = faction.Leader?.StringId ?? "",
            ["kingdom"] = kingdom?.StringId ?? clan?.Kingdom?.StringId ?? "",
            ["isKingdom"] = faction.IsKingdomFaction,
            ["isPlayer"] = isPlayer,
            ["inPlayerClan"] = isPlayer,
        });
    }

    public static PayloadValue? Party(MobileParty? party)
    {
        if (party == null)
            return null;
        var leader = party.LeaderHero;
        var clan = party.ActualClan;
        var isMain = party.IsMainParty;
        return PayloadValue.Entity(FieldKind.Party, new Dictionary<string, object>
        {
            ["id"] = party.StringId ?? "",
            ["name"] = party.Name?.ToString() ?? "",
            ["leader"] = leader?.StringId ?? "",
            ["clan"] = clan?.StringId ?? "",
            ["kingdom"] = clan?.Kingdom?.StringId ?? "",
            ["isMainParty"] = isMain,
            ["isPlayer"] = isMain,
            ["inPlayerParty"] = isMain,
            ["inPlayerClan"] = clan != null && clan == TaleWorlds.CampaignSystem.Clan.PlayerClan,
            ["companion"] = CompanionIdOf(leader),
            ["heroId"] = leader?.StringId ?? "",
        });
    }

    /// <summary>A PartyBase: its mobile party; a settlement's own party (garrison side) gives null.</summary>
    public static PayloadValue? Party(PartyBase? party) => Party(party?.MobileParty);

    public static PayloadValue? Item(ItemObject? item) =>
        item == null
            ? null
            : PayloadValue.Entity(FieldKind.Item, new Dictionary<string, object>
            {
                ["id"] = item.StringId ?? "",
                ["name"] = item.Name?.ToString() ?? "",
                ["type"] = item.ItemType.ToString(),
            });

    /// <summary>
    /// The standard MapEvent fields (TriggerRegistry.MapEventFields, GAME_MAP Q23): settlement, both side leaders, winner,
    /// whether the player fought and won, the event type.
    /// </summary>
    public static (string, PayloadValue?)[] MapEvent(MapEvent? mapEvent)
    {
        if (mapEvent == null)
            return System.Array.Empty<(string, PayloadValue?)>();
        var winning = mapEvent.WinningSide;
        var playerSide = PartyBase.MainParty?.Side ?? BattleSideEnum.None;
        return new (string, PayloadValue?)[]
        {
            ("settlement", Settlement(mapEvent.MapEventSettlement)),
            ("attacker", Party(mapEvent.AttackerSide?.LeaderParty)),
            ("defender", Party(mapEvent.DefenderSide?.LeaderParty)),
            ("winner", Text(winning == BattleSideEnum.Attacker ? "attacker" : winning == BattleSideEnum.Defender ? "defender" : "none")),
            ("playerInvolved", Bool(mapEvent.IsPlayerMapEvent)),
            ("playerWon", Bool(winning != BattleSideEnum.None && winning == playerSide)),
            ("battleType", Text(mapEvent.EventType)),
        };
    }

    public static PayloadValue Text(object? value) => PayloadValue.Text(value?.ToString() ?? "");
    public static PayloadValue Int(long value) => PayloadValue.Int(value);
    public static PayloadValue Number(double value) => PayloadValue.Number(value);
    public static PayloadValue Bool(bool value) => PayloadValue.Bool(value);

    /// <summary>The mod companion id of a hero created from a registry template, else "".</summary>
    public static string CompanionIdOf(Hero? hero) => ModRuntime.IsModHero(hero) ? hero!.Template.StringId : "";
}
