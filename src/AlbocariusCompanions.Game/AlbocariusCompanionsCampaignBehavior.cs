using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace AlbocariusCompanions.Game;

/// <summary>
/// Spawns registry companions on new games and on loads (existing saves), persists companion id → hero, and registers
/// the companion dialogs. Distinctive type name and explicit StringId so save data is never confused (GAME_MAP Q9).
/// </summary>
internal sealed class AlbocariusCompanionsCampaignBehavior : CampaignBehaviorBase
{
    public const string BehaviorId = "AlbocariusCompanionsCampaignBehavior";

    /// <summary>
    /// Companion id → hero. Saved with the campaign as a registered saveable container (GAME_MAP Q8); the map is only a
    /// cache — a hero whose entry is lost is found again by its template id, and an entry is never dropped once set.
    /// </summary>
    private Dictionary<string, Hero> _companionHeroes = new Dictionary<string, Hero>();

    public AlbocariusCompanionsCampaignBehavior() : base(BehaviorId)
    {
    }

    public override void RegisterEvents()
    {
        // Listeners are per session and re-registered every launch, so they are never saved (GAME_MAP Q8).
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
        CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, OnGameLoadFinished);
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
    }

    public override void SyncData(IDataStore dataStore)
    {
        Safe.Run(ModRuntime.Log, "SyncData", () =>
        {
            dataStore.SyncData("_companionHeroes", ref _companionHeroes);

            // A record written without this key, or holding null, leaves the field untouched (GAME_MAP Q9): never
            // dereference a null map afterwards. The old-save case where SyncData is never called at all is covered by
            // the field initializer.
            if (_companionHeroes == null)
            {
                _companionHeroes = new Dictionary<string, Hero>();
                ModRuntime.Log.Warning("Save data carried no companion map; continuing with an empty one.");
            }
        });
    }

    private void OnNewGameCreated(CampaignGameStarter starter)
    {
        Safe.Run(ModRuntime.Log, "OnNewGameCreated", () => SpawnMissing("new game"));
    }

    private void OnGameLoadFinished()
    {
        // Restored SyncData is already in the fields when this fires (GAME_MAP Q8/Q9).
        Safe.Run(ModRuntime.Log, "OnGameLoadFinished", () => SpawnMissing("game load"));
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        Safe.Run(ModRuntime.Log, "OnSessionLaunched", () => CompanionDialogs.Register(starter));
        Safe.Run(ModRuntime.Log, "in-game notifications", () =>
        {
            // Templates that failed to resolve count as skipped too (CompanionTemplates logs why).
            var loaded = ModRuntime.Templates.Count;
            var skipped = ModRuntime.SkippedAtLoad + (ModRuntime.Registry.Count - loaded);
            InGameNotifications.OnCampaignReady(loaded, skipped);
        });
    }

    /// <summary>
    /// Gives every registered template a hero: an already mapped one is left alone, an unmapped hero with the template
    /// id is re-linked (the save lost the map, or the mod data was removed and re-added), anything else is created and
    /// placed in a town (GAME_MAP Q1, Q3, Q9). Each companion runs in its own Safe.Run: one failure never stops the rest.
    /// </summary>
    private void SpawnMissing(string reason)
    {
        var log = ModRuntime.Log;
        var spawned = 0;
        var present = 0;
        var relinked = 0;

        foreach (var pair in ModRuntime.Templates.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var id = pair.Key;
            var template = pair.Value;

            Safe.Run(log, $"spawn companion '{id}'", () =>
            {
                if (_companionHeroes.TryGetValue(id, out var mapped) && mapped != null)
                {
                    // Alive or dead: the companion exists, so never respawn and never duplicate (Q9).
                    present++;
                    CompanionHeroFactory.ApplyName(mapped, template);
                    log.Info($"Companion '{id}' already present as hero '{mapped.StringId}' (template '{mapped.Template?.StringId}'), " +
                        $"now in {mapped.CurrentSettlement?.Name?.ToString() ?? "no settlement"}.");
                    return;
                }

                var existing = FindHeroByTemplateId(id);
                if (existing != null)
                {
                    _companionHeroes[id] = existing;
                    relinked++;
                    CompanionHeroFactory.ApplyName(existing, template);
                    log.Info($"Companion '{id}' re-linked to existing hero '{existing.StringId}' (template '{existing.Template?.StringId}').");
                    return;
                }

                if (!ModRuntime.Registry.TryGet(id, out var package) || package == null)
                {
                    log.Error($"Companion '{id}' has a registered template but no JSON package in the registry; cannot spawn it.");
                    return;
                }

                var town = PickSpawnTown(template);
                if (town == null)
                {
                    log.Error($"No town available to spawn companion '{id}' (template culture '{template.Culture?.StringId}'); skipped.");
                    return;
                }

                var settlement = town.Settlement;
                var hero = CompanionHeroFactory.Create(template, package.Companion, settlement);

                // The only write of Hero.StayingInSettlement: places the partyless hero in the town (Q3).
                EnterSettlementAction.ApplyForCharacterOnly(hero, settlement);

                _companionHeroes[id] = hero;
                spawned++;
                log.Info($"Companion spawned: '{id}' → hero '{hero.StringId}' (template '{template.StringId}') in {settlement.Name} ('{settlement.StringId}').");
            });
        }

        log.Info($"Companion spawn pass ({reason}): {spawned} spawned, {present} already present, {relinked} re-linked, " +
            $"{ModRuntime.Templates.Count} template(s) considered.");
    }

    /// <summary>
    /// Finds a hero already created from the companion template, scanning the two rosters vanilla itself scans on load
    /// (GAME_MAP Q1/Q2/Q9). Alive heroes come first, so a living companion is preferred over a dead one.
    /// </summary>
    private static Hero? FindHeroByTemplateId(string id)
    {
        var alive = Hero.AllAliveHeroes.FirstOrDefault(hero => hero.Template?.StringId == id);
        return alive ?? Hero.DeadOrDisabledHeroes.FirstOrDefault(hero => hero.Template?.StringId == id);
    }

    /// <summary>
    /// Town choice like vanilla (GAME_MAP Q1/Q3): every town except the one the player is standing in, preferring a town
    /// of the template's culture, then any candidate; one of the pool at random. Null when no town is available.
    /// </summary>
    private static Town? PickSpawnTown(CharacterObject template)
    {
        var playerSettlement = Hero.MainHero?.CurrentSettlement;
        var candidates = Town.AllTowns
            .Where(town => town.Settlement != null && town.Settlement != playerSettlement)
            .ToList();

        var cultureMatched = template.Culture == null
            ? new List<Town>()
            : candidates.Where(town => town.Culture == template.Culture).ToList();

        var pool = cultureMatched.Count > 0 ? cultureMatched : candidates;
        if (pool.Count == 0)
            return null;

        return pool[MBRandom.RandomInt(pool.Count)];
    }
}
