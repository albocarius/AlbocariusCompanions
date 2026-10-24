using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Logging;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace AlbocariusCompanions.Game;

/// <summary>Generic dialog registration from dialogs.json for every registry companion (GAME_MAP Q6, Q7).</summary>
internal static class CompanionDialogs
{
    private const string HeroMainOptions = "hero_main_options";
    private const string CloseWindow = "close_window";
    private const string HireCostVariable = "ALBOCARIUS_HIRE_COST";

    // Beats vanilla start_wanderer_unmet (110): NPC lines are first-passing-match by priority (Q7).
    private const int IntroPriority = 120;

    private const string PlayerBackstory = "{=albocarius_player_backstory}Tell me about yourself.";
    private const string PlayerHire = "{=albocarius_player_hire}I could use someone like you in my company.";
    private const string PlayerHirePay = "{=albocarius_player_hire_pay}Very well. Here is {ALBOCARIUS_HIRE_COST} denars.";
    private const string PlayerHireFull = "{=albocarius_player_hire_full}I already have more companions than I can manage.";
    private const string PlayerHireLater = "{=albocarius_player_hire_later}Not now.";

    public static void Register(CampaignGameStarter starter)
    {
        foreach (var id in ModRuntime.Templates.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var companionId = id;
            Safe.Run(ModRuntime.Log, $"register dialogs for '{companionId}'", () => RegisterCompanion(starter, companionId));
        }
    }

    private static void RegisterCompanion(CampaignGameStarter starter, string id)
    {
        if (!ModRuntime.Registry.TryGet(id, out var package) || package == null)
        {
            ModRuntime.Log.Warning($"Dialogs: '{id}' has a template but no registry entry; skipped.");
            return;
        }

        var texts = FirstLineTexts(package);
        foreach (var slot in DialogSlots.Required)
            if (!texts.ContainsKey(slot))
            {
                ModRuntime.Log.Error($"Dialogs: '{id}' has no usable '{slot}' line; no dialogs registered for it.");
                return;
            }

        var hireCost = package.Companion.HireCost ?? 0;
        var prefix = "albocarius_" + id + "_";
        var backstoryToken = prefix + "backstory";
        var hireOfferToken = prefix + "hire_offer";
        var hireResponseToken = prefix + "hire_response";
        var hireAcceptToken = prefix + "hire_accept";
        var hireDeclineToken = prefix + "hire_decline";
        var registered = 0;

        bool IsThisCompanion()
        {
            var hero = Hero.OneToOneConversationHero;
            return ModRuntime.IsModHero(hero) && hero.Template.StringId == id;
        }

        ConversationSentence.OnConditionDelegate Condition(string context, Func<bool> condition) =>
            () => Safe.Run(ModRuntime.Log, $"dialog condition {prefix}{context}", condition, fallback: false);

        ConversationSentence.OnConsequenceDelegate Consequence(string context, Action consequence) =>
            () => Safe.Run(ModRuntime.Log, $"dialog consequence {prefix}{context}", consequence);

        // 1. First meeting.
        starter.AddDialogLine(prefix + "intro", "start", HeroMainOptions, texts[DialogSlots.Intro],
            Condition("intro", () => IsThisCompanion() && !Hero.OneToOneConversationHero.HasMet),
            Consequence("intro", () => Hero.OneToOneConversationHero.SetHasMet()),
            IntroPriority);
        registered++;

        // 2. Backstory.
        starter.AddPlayerLine(prefix + "ask_backstory", HeroMainOptions, backstoryToken, PlayerBackstory,
            Condition("ask_backstory", IsThisCompanion), null);
        starter.AddDialogLine(prefix + "backstory", backstoryToken, HeroMainOptions, texts[DialogSlots.Backstory], null, null);
        registered += 2;

        // 3. Hire offer.
        starter.AddPlayerLine(prefix + "ask_hire", HeroMainOptions, hireOfferToken, PlayerHire,
            Condition("ask_hire", () =>
            {
                if (!IsThisCompanion())
                    return false;
                var hero = Hero.OneToOneConversationHero;
                return !hero.IsPlayerCompanion && hero.PartyBelongedTo == null;
            }),
            null);
        starter.AddDialogLine(prefix + "hire_offer", hireOfferToken, hireResponseToken, texts[DialogSlots.HireOffer],
            Condition("hire_offer", () =>
            {
                MBTextManager.SetTextVariable(HireCostVariable, hireCost);
                return true;
            }),
            null);
        registered += 2;

        // 4. Player answers (all passing options are listed, Q7).
        starter.AddPlayerLine(prefix + "hire_pay", hireResponseToken, hireAcceptToken, PlayerHirePay,
            Condition("hire_pay", () => Hero.MainHero.Gold >= hireCost && !TooManyCompanions()),
            Consequence("hire_pay", () => Hire(id, hireCost)));
        starter.AddPlayerLine(prefix + "hire_full", hireResponseToken, hireDeclineToken, PlayerHireFull,
            Condition("hire_full", TooManyCompanions), null);
        starter.AddPlayerLine(prefix + "hire_later", hireResponseToken, hireDeclineToken, PlayerHireLater,
            Condition("hire_later", () => !TooManyCompanions()), null);
        registered += 3;

        // 5. Companion's reply.
        starter.AddDialogLine(prefix + "hire_accept", hireAcceptToken, CloseWindow, texts[DialogSlots.HireAccept], null, null);
        starter.AddDialogLine(prefix + "hire_decline", hireDeclineToken, HeroMainOptions, texts[DialogSlots.HireDecline], null, null);
        registered += 2;

        ModRuntime.Log.Info($"Dialogs: registered {registered} lines for '{id}' (hire cost {hireCost}).");
    }

    /// <summary>Vanilla limit check (Q6): the hire option is refused once the clan is at its companion limit.</summary>
    private static bool TooManyCompanions() =>
        Clan.PlayerClan.Companions.Count >= Clan.PlayerClan.CompanionLimit;

    /// <summary>Vanilla hire order (Q6): pay, join the clan, join the main party.</summary>
    private static void Hire(string id, int hireCost)
    {
        var hero = Hero.OneToOneConversationHero;
        GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, hero, hireCost);
        AddCompanionAction.Apply(Clan.PlayerClan, hero);
        AddHeroToPartyAction.Apply(hero, MobileParty.MainParty);
        ModRuntime.Log.Info($"Hired '{id}' (hero {hero.StringId}) for {hireCost} denars.");
    }

    /// <summary>Slot → "{=lineId}english text" of the first line per slot; lines without an id or English text are skipped.</summary>
    private static Dictionary<string, string> FirstLineTexts(CompanionPackage package)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in package.Dialogs?.Lines ?? new List<DialogLine>())
        {
            if (line?.Slot == null || line.Id == null || texts.ContainsKey(line.Slot!))
                continue;
            if (line.Text == null || !line.Text.TryGetValue(CompanionRules.EnglishCode, out var english) || string.IsNullOrWhiteSpace(english))
                continue;
            texts[line.Slot!] = "{=" + line.Id + "}" + english;
        }
        return texts;
    }
}
