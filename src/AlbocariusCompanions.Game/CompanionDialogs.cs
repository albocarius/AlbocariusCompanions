using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Logging;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace AlbocariusCompanions.Game;

/// <summary>
/// Generic dialog registration for every registry companion (GAME_MAP Q6, Q7). Each line is registered once as
/// <c>{=!}{VAR}</c>; its condition picks one of the slot's lines at random (SPEC_CORE_GAPS T5) in the active game language
/// (T6) and sets VAR to <c>{=&lt;full id&gt;}&lt;text&gt;</c>, so voice-over and lipsync still find the line (Q14 note 2).
/// Player texts come from _shared/player_lines.json; no player-facing text lives in code.
/// </summary>
internal static class CompanionDialogs
{
    private const string HeroMainOptions = "hero_main_options";
    private const string CloseWindow = "close_window";
    private const string HireCostVariable = "ALBOCARIUS_HIRE_COST";

    // Beats vanilla start_wanderer_unmet (110): NPC lines are first-passing-match by priority (Q7).
    private const int IntroPriority = 120;

    private static readonly IRandomSource Random = new MBRandomSource();

    public static void Register(CampaignGameStarter starter)
    {
        var playerLines = ModRuntime.Registry.PlayerLines;
        if (playerLines == null)
        {
            ModRuntime.Log.Error("Dialogs: the player lines (_shared/player_lines.json) are missing or invalid; no companion dialogs registered.");
            return;
        }

        foreach (var id in ModRuntime.Templates.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var companionId = id;
            Safe.Run(ModRuntime.Log, $"register dialogs for '{companionId}'", () => RegisterCompanion(starter, companionId, playerLines));
        }
    }

    private static void RegisterCompanion(CampaignGameStarter starter, string id, SharedPackage playerLines)
    {
        if (!ModRuntime.Registry.TryGet(id, out var package) || package == null)
        {
            ModRuntime.Log.Warning($"Dialogs: '{id}' has a template but no registry entry; skipped.");
            return;
        }

        var bySlot = LinesBySlot(package);
        foreach (var slot in DialogSlots.Required)
            if (!bySlot.ContainsKey(slot))
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

        // Every registered line has its own variable: all player options' conditions run before the options are shown.
        string Variable(string context) => ("ALBOCARIUS_" + id + "_" + context).ToUpperInvariant();

        // NPC line text: a random line of the slot in the active language, keyed by its full id for voice-over.
        bool SetNpcText(string variable, string slot)
        {
            var line = LinePicker.Pick(bySlot[slot], Random)!;
            var text = package.Text(line.Id!, LanguageCode(package.Languages.Keys));
            MBTextManager.SetTextVariable(variable, new TextObject("{=" + line.FullId + "}" + text));
            return true;
        }

        // Player line text: same pick from _shared; the player is never voiced.
        bool SetPlayerText(string variable, string slot)
        {
            var lines = playerLines.Lines.Where(l => l.Slot == slot).ToList();
            var line = LinePicker.Pick(lines, Random);
            if (line == null)
                return false;
            var text = playerLines.Text(line.Id!, LanguageCode(playerLines.Languages.Keys));
            MBTextManager.SetTextVariable(variable, new TextObject("{=" + line.Id + "}" + text));
            return true;
        }

        string Text(string variable) => "{=!}{" + variable + "}";

        // 1. First meeting.
        starter.AddDialogLine(prefix + "intro", "start", HeroMainOptions, Text(Variable("intro")),
            Condition("intro", () => IsThisCompanion() && !Hero.OneToOneConversationHero.HasMet
                && SetNpcText(Variable("intro"), DialogSlots.Intro)),
            Consequence("intro", () => Hero.OneToOneConversationHero.SetHasMet()),
            IntroPriority);
        registered++;

        // 2. Backstory.
        starter.AddPlayerLine(prefix + "ask_backstory", HeroMainOptions, backstoryToken, Text(Variable("ask_backstory")),
            Condition("ask_backstory", () => IsThisCompanion() && SetPlayerText(Variable("ask_backstory"), PlayerSlots.AskAbout)), null);
        starter.AddDialogLine(prefix + "backstory", backstoryToken, HeroMainOptions, Text(Variable("backstory")),
            Condition("backstory", () => SetNpcText(Variable("backstory"), DialogSlots.Backstory)), null);
        registered += 2;

        // 3. Hire offer.
        starter.AddPlayerLine(prefix + "ask_hire", HeroMainOptions, hireOfferToken, Text(Variable("ask_hire")),
            Condition("ask_hire", () =>
            {
                if (!IsThisCompanion())
                    return false;
                var hero = Hero.OneToOneConversationHero;
                return !hero.IsPlayerCompanion && hero.PartyBelongedTo == null
                    && SetPlayerText(Variable("ask_hire"), PlayerSlots.AskHire);
            }),
            null);
        starter.AddDialogLine(prefix + "hire_offer", hireOfferToken, hireResponseToken, Text(Variable("hire_offer")),
            Condition("hire_offer", () =>
            {
                MBTextManager.SetTextVariable(HireCostVariable, hireCost);
                return SetNpcText(Variable("hire_offer"), DialogSlots.HireOffer);
            }),
            null);
        registered += 2;

        // 4. Player answers (all passing options are listed, Q7).
        starter.AddPlayerLine(prefix + "hire_pay", hireResponseToken, hireAcceptToken, Text(Variable("hire_pay")),
            Condition("hire_pay", () => Hero.MainHero.Gold >= hireCost && !TooManyCompanions()
                && SetPlayerText(Variable("hire_pay"), PlayerSlots.HirePay)),
            Consequence("hire_pay", () => Hire(id, hireCost)));
        starter.AddPlayerLine(prefix + "hire_full", hireResponseToken, hireDeclineToken, Text(Variable("hire_full")),
            Condition("hire_full", () => TooManyCompanions() && SetPlayerText(Variable("hire_full"), PlayerSlots.HireFull)), null);
        starter.AddPlayerLine(prefix + "hire_later", hireResponseToken, hireDeclineToken, Text(Variable("hire_later")),
            Condition("hire_later", () => !TooManyCompanions() && SetPlayerText(Variable("hire_later"), PlayerSlots.HireLater)), null);
        registered += 3;

        // 5. Companion's reply.
        starter.AddDialogLine(prefix + "hire_accept", hireAcceptToken, CloseWindow, Text(Variable("hire_accept")),
            Condition("hire_accept", () => SetNpcText(Variable("hire_accept"), DialogSlots.HireAccept)), null);
        starter.AddDialogLine(prefix + "hire_decline", hireDeclineToken, HeroMainOptions, Text(Variable("hire_decline")),
            Condition("hire_decline", () => SetNpcText(Variable("hire_decline"), DialogSlots.HireDecline)), null);
        registered += 2;

        ModRuntime.Log.Info($"Dialogs: registered {registered} lines for '{id}' (hire cost {hireCost}, " +
            $"languages {string.Join(", ", package.Languages.Keys.OrderBy(k => k, StringComparer.Ordinal))}).");
    }

    /// <summary>The lang file code for the game's active language (Q14 note 2), else "en".</summary>
    private static string LanguageCode(IEnumerable<string> available) =>
        LanguagePick.Choose(available, MBTextManager.ActiveTextLanguage, GameLanguageOf);

    /// <summary>ISO code → game language id, cached: the game asserts on unknown codes, so each is asked once per session.</summary>
    private static string? GameLanguageOf(string isoCode)
    {
        if (!GameLanguageByIso.TryGetValue(isoCode, out var language))
        {
            language = LocalizedTextManager.GetLocalizationCodeOfISOLanguageCode(isoCode);
            GameLanguageByIso[isoCode] = language;
        }
        return language;
    }

    private static readonly Dictionary<string, string?> GameLanguageByIso = new Dictionary<string, string?>(StringComparer.Ordinal);

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

    /// <summary>Slot → the slot's lines that have a full id and an English text (validation guarantees both).</summary>
    private static Dictionary<string, List<DialogLine>> LinesBySlot(CompanionPackage package) =>
        (package.Dialogs?.Lines ?? new List<DialogLine>())
            .Where(l => l?.Slot != null && l.FullId != null && package.Text(l.Id!, CompanionRules.EnglishCode) != null)
            .GroupBy(l => l.Slot!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

    /// <summary>Slot variation through the game's random generator (GAME_MAP Q1 MBRandom).</summary>
    private sealed class MBRandomSource : IRandomSource
    {
        public int Next(int maxExclusive) => MBRandom.RandomInt(maxExclusive);
    }
}
