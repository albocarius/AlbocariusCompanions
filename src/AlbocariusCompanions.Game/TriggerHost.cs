using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Logging;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;
using AlbocariusCompanions.Core.Triggers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace AlbocariusCompanions.Game;

/// <summary>
/// Runs the trigger engine in the campaign (M11 T4/T5): event adapters feed <see cref="Fire"/>, the internal hourly and
/// daily ticks poll and deliver, and delivery opens a map conversation (GAME_MAP Q25), shows a message (Q17) or a pop-up
/// (Q26). Listeners and ticks are attached only for triggers loaded content uses (G11). Every entry point is guarded.
/// </summary>
internal sealed class TriggerHost
{
    private readonly TriggerEngine _engine;
    private readonly TriggerPolling _polling;
    private readonly HashSet<string> _used;

    public TriggerHost(IEnumerable<CompanionPackage> packages)
    {
        _engine = new TriggerEngine(packages, DialogText.Random);
        _polling = new TriggerPolling(_engine);
        _used = new HashSet<string>(_engine.UsedTriggers, StringComparer.Ordinal);
    }

    public TriggerEngine Engine => _engine;

    private static double Now => CampaignTime.Now.ToHours;

    /// <summary>Attaches the adapters of the used triggers and, when anything is used, the internal ticks (G11).</summary>
    public void RegisterEvents(CampaignBehaviorBase owner)
    {
        var subscribed = TriggerAdapters.Subscribe(owner, _used, (trigger, payload) => Fire(trigger, payload));
        if (_used.Count > 0)
        {
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(owner, () => Safe.Run(ModRuntime.Log, "trigger hourly tick", Hourly));
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(owner, () => Safe.Run(ModRuntime.Log, "trigger daily tick", Daily));
        }
        ModRuntime.Log.Info(_used.Count == 0
            ? "Triggers: no loaded content uses a trigger; no listeners attached."
            : $"Triggers: {_used.Count} used ({string.Join(", ", _used.OrderBy(t => t, StringComparer.Ordinal))}); " +
              $"{subscribed.Count} event listener(s) attached.");
    }

    /// <summary>An event trigger fired (adapter) or the console fired one.</summary>
    public IReadOnlyList<FireCandidate> Fire(string trigger, TriggerPayload payload)
    {
        var results = _engine.Fire(trigger, payload, Now, ContextFor);
        LogQueued(results);
        TryDeliver();
        return results;
    }

    private void Hourly()
    {
        if (_used.Contains("nearSettlement"))
            LogQueued(_polling.NearSettlements(Now, DistanceTo, id => TriggerPayloads.Settlement(Settlement.Find(id)), ContextFor));
        if (_used.Contains("roleStarted") || _used.Contains("roleEnded"))
            LogQueued(_polling.Roles(Now, ModRuntime.Registry.Ids, id => CompanionRoles.Of(ModRuntime.HeroOf(id)),
                id => TriggerPayloads.Hero(ModRuntime.HeroOf(id)), ContextFor));
        TryDeliver();
    }

    private void Daily()
    {
        if (_used.Contains("daily"))
            LogQueued(_polling.Daily(Now, ContextFor));
        VariablesChanged();
    }

    /// <summary>After effects and console writes: varCrossed compares the variables with their last values (G10).</summary>
    public void VariablesChanged()
    {
        if (!_used.Contains("varCrossed"))
            return;
        LogQueued(_polling.Variables(Now, (companion, name) => ModRuntime.Variables.Get(companion, name), ContextFor));
        TryDeliver();
    }

    /// <summary>Map distance from the player's party to a settlement (GAME_MAP Q43); null for an unknown id.</summary>
    private static double? DistanceTo(string settlementId)
    {
        var settlement = Settlement.Find(settlementId);
        var party = MobileParty.MainParty;
        if (settlement == null || party == null)
            return null;
        return party.Position.Distance(settlement.GatePosition);
    }

    private static IConversationContext? ContextFor(string companionId, TriggerPayload payload)
    {
        var hero = ModRuntime.HeroOf(companionId);
        return hero == null || !hero.IsAlive ? null : new HeroConversationContext(hero, companionId, payload);
    }

    private static void LogQueued(IEnumerable<FireCandidate> results)
    {
        foreach (var c in results.Where(c => c.Queued))
            ModRuntime.Log.Info($"Trigger {c.Content.Trigger.Name}: queued {c.Content.Key} ({c.Content.Delivery}).");
    }

    /// <summary>Where the player is (GAME_MAP Q25): on the map, and free for a conversation or pop-up.</summary>
    private static DeliveryEnvironment Environment()
    {
        var onMap = GameStateManager.Current?.ActiveState is MapState;
        var party = MobileParty.MainParty;
        var canInterrupt = onMap && party != null && party.MapEvent == null && PlayerEncounter.Current == null && !party.IsCurrentlyAtSea
            && Campaign.Current?.ConversationManager?.IsConversationInProgress != true && !InformationManager.IsAnyInquiryActive();
        return new DeliveryEnvironment { OnMap = onMap, CanInterrupt = canInterrupt };
    }

    /// <summary>Delivers at most one item per call (the limits spread the rest over later ticks).</summary>
    public void TryDeliver()
    {
        var item = _engine.NextDelivery(Now, Environment(), ContextFor,
            (q, why) => ModRuntime.Log.Info($"Trigger item {q.Key} dropped: {why}."));
        if (item != null)
            Safe.Run(ModRuntime.Log, "deliver " + item.Key, () => Deliver(item));
    }

    private void Deliver(QueuedItem item)
    {
        var hero = ModRuntime.HeroOf(item.CompanionId);
        if (hero == null || !ModRuntime.Registry.TryGet(item.CompanionId, out var package) || package == null
            || !_engine.TryGetContent(item.Key, out var content))
            return;

        if (content.IsLine)
        {
            var line = (package.Dialogs?.Lines ?? new List<DialogLine>()).FirstOrDefault(l => l?.Id == item.ContentId);
            if (line != null)
                Notify(hero, DialogText.TextOf(package, line)?.ToString());
            ModRuntime.Log.Info($"Trigger delivered: {item.Key} (notification).");
            return;
        }

        var conversation = content.Conversation!;
        var context = new HeroConversationContext(hero, item.CompanionId, item.Payload);
        var nodes = conversation.Nodes ?? new List<ConversationNode>();
        // The start node by the selection rule among those that hold (null: none holds any more).
        ConversationNode? Start() => Selection.Pick(
            (conversation.Start ?? new List<string>()).Select(id => nodes.FirstOrDefault(n => n?.Id == id))
                .Where(n => n != null && ConversationConditions.NodeHolds(n, context)).ToList()!,
            n => n.Priority, DialogText.Random);

        switch (item.Delivery)
        {
            case DeliveryRules.Notification:
            {
                var start = Start();
                if (start == null) return;
                Notify(hero, DialogText.TextOf(package, start)?.ToString());
                CompanionDialogs.ApplyEffects(item.CompanionId, start, hero);
                break;
            }
            case DeliveryRules.Inquiry:
            {
                var start = Start();
                if (start == null) return;
                var options = (start.Next ?? new List<string>()).Select(id => nodes.FirstOrDefault(n => n?.Id == id))
                    .Where(n => n != null && ConversationConditions.NodeHolds(n, context)).ToList();
                if (options.Count == 0)
                {
                    ModRuntime.Log.Info($"Trigger item {item.Key} dropped: no inquiry option holds.");
                    return;
                }
                CompanionDialogs.ApplyEffects(item.CompanionId, start, hero);
                ShowInquiry(item.CompanionId, package, hero, start, options!);
                break;
            }
            default:
                ModRuntime.ActiveTrigger = new ActiveTrigger(item.CompanionId, conversation.Id!, item.Payload);
                try
                {
                    // The companion's map conversation (Q25 A); the registered start sentence of this conversation wins
                    // the "start" token while ActiveTrigger names it (CompanionDialogs).
                    CampaignMapConversation.OpenConversation(
                        new ConversationCharacterData(Hero.MainHero.CharacterObject, PartyBase.MainParty),
                        new ConversationCharacterData(hero.CharacterObject));
                }
                catch
                {
                    ModRuntime.ActiveTrigger = null;
                    throw;
                }
                break;
        }
        ModRuntime.Log.Info($"Trigger delivered: {item.Key} ({item.Delivery}).");
    }

    /// <summary>1–2 options → the standard pop-up, more → the multi-choice pop-up (Q26; user, M11 gate).</summary>
    private static void ShowInquiry(string companionId, CompanionPackage package, Hero hero, ConversationNode start, List<ConversationNode> options)
    {
        var title = hero.Name?.ToString() ?? companionId;
        var body = DialogText.TextOf(package, start)?.ToString() ?? "";
        string Label(ConversationNode n) => DialogText.TextOf(package, n)?.ToString() ?? n.Id!;
        void Choose(ConversationNode n) => Safe.Run(ModRuntime.Log, $"inquiry option {companionId}/{n.Id}", () =>
        {
            CompanionDialogs.ApplyEffects(companionId, n, hero);
            ModRuntime.Log.Info($"Trigger inquiry of '{companionId}': chose '{n.Id}'.");
        });

        if (options.Count <= DeliveryRules.StandardInquiryMaxOptions)
        {
            var first = options[0];
            var second = options.Count > 1 ? options[1] : null;
            InformationManager.ShowInquiry(new InquiryData(title, body, true, second != null, Label(first), second != null ? Label(second) : "",
                () => Choose(first), second != null ? () => Choose(second) : (Action?)null), pauseGameActiveState: true);
            return;
        }

        var elements = options.Select(n => new InquiryElement(n.Id, Label(n), null)).ToList();
        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(title, body, elements, false, 1, 1,
            "OK", "",
            chosen => { if (chosen?.FirstOrDefault()?.Identifier is string id && options.FirstOrDefault(o => o.Id == id) is { } n) Choose(n); },
            null), pauseGameActiveState: true);
    }

    private static void Notify(Hero hero, string? text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        InformationManager.DisplayMessage(new InformationMessage($"{hero.Name}: {text}"));
    }

    public List<string> SaveQueue() => _engine.Save();
    public List<string> SavePolling() => _polling.Save();

    public void Load(List<string>? queue, List<string>? polling)
    {
        var q = _engine.Load(queue, ModRuntime.Log.Warning);
        var p = _polling.Load(polling, ModRuntime.Log.Warning);
        ModRuntime.Log.Info($"Triggers loaded: {_engine.Queue.Count} queued item(s), {q} queue/cooldown entries, {p} polling entries.");
    }

    public string FireFromConsole(IReadOnlyList<string> args)
    {
        var payload = TriggerCommands.ParseFire(args, ResolveEntity, out var trigger, out var error);
        if (payload == null)
            return error;
        var results = trigger!.IsPolling
            ? _engine.ContentFor(trigger.Name).Select(c => _engine.FireContent(c, payload, Now, ContextFor)).ToList()
            : (IReadOnlyList<FireCandidate>)_engine.Fire(trigger.Name, payload, Now, ContextFor);
        LogQueued(results);
        TryDeliver();
        return TriggerCommands.FormatFire(trigger.Name, results);
    }

    public string QueueText() => TriggerCommands.FormatQueue(_engine, Now);

    public void ClearCooldowns() => _engine.ClearCooldowns();

    /// <summary>Console ids → entities: heroes, settlements, clans, kingdoms by string id (GAME_MAP Q13/Q30), parties by id.</summary>
    private static PayloadValue? ResolveEntity(FieldKind kind, string id)
    {
        var objects = TaleWorlds.ObjectSystem.MBObjectManager.Instance;
        switch (kind)
        {
            case FieldKind.Hero: return TriggerPayloads.Hero(objects?.GetObject<Hero>(id));
            case FieldKind.Settlement: return TriggerPayloads.Settlement(Settlement.Find(id));
            case FieldKind.Clan: return TriggerPayloads.Clan(objects?.GetObject<Clan>(id));
            case FieldKind.Kingdom: return TriggerPayloads.Kingdom(objects?.GetObject<Kingdom>(id));
            case FieldKind.Faction:
                return TriggerPayloads.Faction((IFaction?)objects?.GetObject<Kingdom>(id) ?? objects?.GetObject<Clan>(id));
            case FieldKind.Party: return TriggerPayloads.Party(MobileParty.All.FirstOrDefault(p => p.StringId == id));
            case FieldKind.Item: return TriggerPayloads.Item(objects?.GetObject<ItemObject>(id));
            default: return null;
        }
    }
}
