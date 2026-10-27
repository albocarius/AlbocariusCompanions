using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;
using AlbocariusCompanions.Core.Validation;

namespace AlbocariusCompanions.Core.Triggers;

/// <summary>One conversation or line with a <c>trigger</c>, as the engine indexes it.</summary>
public sealed class TriggeredContent
{
    internal TriggeredContent(string companionId, string id, bool isLine, TriggerSpec spec, TriggerDefinition trigger,
        ConditionSet? when, int? priority, ConversationDefinition? conversation)
    {
        CompanionId = companionId;
        Id = id;
        IsLine = isLine;
        Spec = spec;
        Trigger = trigger;
        When = when;
        Priority = priority;
        Conversation = conversation;
        Delivery = TriggerChecks.DeliveryOf(spec, isLine);
        Subject = spec.Subject ?? trigger.DefaultSubject;
    }

    public string CompanionId { get; }
    /// <summary>The conversation id or the line's local id.</summary>
    public string Id { get; }
    public bool IsLine { get; }
    public TriggerSpec Spec { get; }
    public TriggerDefinition Trigger { get; }
    public ConditionSet? When { get; }
    public int? Priority { get; }
    /// <summary>The conversation (null for a line).</summary>
    public ConversationDefinition? Conversation { get; }
    public string Delivery { get; }
    public string Subject { get; }
    /// <summary>"&lt;companion&gt;/conversation/&lt;id&gt;" or "&lt;companion&gt;/line/&lt;id&gt;": cooldown and queue key.</summary>
    public string Key => $"{CompanionId}/{(IsLine ? "line" : "conversation")}/{Id}";
    public bool IsInterruption => Delivery != DeliveryRules.Notification;
}

/// <summary>What happened to one candidate when a trigger fired (logs, simulator, console).</summary>
public sealed class FireCandidate
{
    public FireCandidate(TriggeredContent content) => Content = content;

    public TriggeredContent Content { get; }
    public bool SubjectMatches { get; internal set; }
    public bool WhereMatches { get; internal set; }
    /// <summary>Null when the step was not reached.</summary>
    public bool? Present { get; internal set; }
    public bool? OffCooldown { get; internal set; }
    public bool? AlreadyQueued { get; internal set; }
    public bool? ChanceHit { get; internal set; }
    public bool? WhenHolds { get; internal set; }
    public bool Queued { get; internal set; }
    /// <summary>The first failed step, or "queued".</summary>
    public string Outcome { get; internal set; } = "";
}

/// <summary>A fired item waiting for delivery (saved with the campaign, G9).</summary>
public sealed class QueuedItem
{
    public string Key { get; set; } = "";
    public string CompanionId { get; set; } = "";
    public string ContentId { get; set; } = "";
    public bool IsLine { get; set; }
    public string Trigger { get; set; } = "";
    public string Delivery { get; set; } = "";
    public int Priority { get; set; }
    public double FiredAtHours { get; set; }
    public double ExpiresAtHours { get; set; }
    public bool InMission { get; set; }
    public TriggerPayload Payload { get; set; } = TriggerPayload.None;
    public bool IsInterruption => Delivery != DeliveryRules.Notification;
}

/// <summary>Where the player is when the engine is asked for a delivery (the Game fills it from GAME_MAP Q25).</summary>
public sealed class DeliveryEnvironment
{
    /// <summary>The campaign map is the active state (no mission/scene).</summary>
    public bool OnMap { get; set; }
    /// <summary>A conversation or pop-up may open now: on the map, not at sea, no map event, no encounter (Q25).</summary>
    public bool CanInterrupt { get; set; }
}

/// <summary>
/// Trigger → queue → delivery (SPEC_TRIGGERS_DELIVERY T3/T5, M11 G7–G9). Pure: the Game feeds fired payloads, the clock and
/// a context per companion, and delivers what <see cref="NextDelivery"/> returns. Limits: <see cref="DeliveryRules"/>.
/// </summary>
public sealed class TriggerEngine
{
    private readonly Dictionary<string, List<TriggeredContent>> _byTrigger = new Dictionary<string, List<TriggeredContent>>(StringComparer.Ordinal);
    private readonly Dictionary<string, TriggeredContent> _byKey = new Dictionary<string, TriggeredContent>(StringComparer.Ordinal);
    private readonly IRandomSource _random;

    private readonly List<QueuedItem> _queue = new List<QueuedItem>();
    /// <summary>Content key → hour it was last delivered (cooldownDays).</summary>
    private readonly Dictionary<string, double> _lastDelivered = new Dictionary<string, double>(StringComparer.Ordinal);
    /// <summary>Companion → hour of its last interruption (InterruptionCooldownDays).</summary>
    private readonly Dictionary<string, double> _lastInterruption = new Dictionary<string, double>(StringComparer.Ordinal);
    private long _day = long.MinValue;
    private int _interruptionsToday;
    private int _notificationsToday;

    public TriggerEngine(IEnumerable<CompanionPackage> packages, IRandomSource random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        foreach (var package in packages)
            Index(package);
    }

    private void Index(CompanionPackage package)
    {
        foreach (var conversation in package.Conversations)
            if (conversation.Trigger != null && conversation.Id != null && TriggerRegistry.TryGet(conversation.Trigger.On, out var t))
                Add(new TriggeredContent(package.Id, conversation.Id, false, conversation.Trigger, t, conversation.When, conversation.Priority, conversation));
        foreach (var line in package.Dialogs?.Lines ?? new List<DialogLine>())
            if (line?.Trigger != null && line.Id != null && TriggerRegistry.TryGet(line.Trigger.On, out var t))
                Add(new TriggeredContent(package.Id, line.Id, true, line.Trigger, t, line.When, line.Priority, null));
    }

    private void Add(TriggeredContent content)
    {
        if (!_byTrigger.TryGetValue(content.Trigger.Name, out var list))
            _byTrigger[content.Trigger.Name] = list = new List<TriggeredContent>();
        list.Add(content);
        _byKey[content.Key] = content;
    }

    /// <summary>The triggers some loaded content uses: the Game subscribes listeners and polls only these (G11).</summary>
    public IReadOnlyCollection<string> UsedTriggers => _byTrigger.Keys.ToList();

    public IReadOnlyList<TriggeredContent> ContentFor(string trigger) =>
        _byTrigger.TryGetValue(trigger, out var list) ? list : (IReadOnlyList<TriggeredContent>)Array.Empty<TriggeredContent>();

    public bool TryGetContent(string key, out TriggeredContent content) => _byKey.TryGetValue(key, out content!);

    public IReadOnlyList<QueuedItem> Queue => _queue;
    public IReadOnlyDictionary<string, double> LastDelivered => _lastDelivered;
    public IReadOnlyDictionary<string, double> LastInterruption => _lastInterruption;
    public int InterruptionsToday => _interruptionsToday;
    public int NotificationsToday => _notificationsToday;

    /// <summary>
    /// A trigger fired: every content item using it is checked in order — subject, where, companion present (unless
    /// allowAbsent), cooldown, not already queued, chance, when — and queued when all pass. <paramref name="contextFor"/>
    /// gives a context for a companion with the payload as its event; null = the companion has no hero (skipped).
    /// </summary>
    public IReadOnlyList<FireCandidate> Fire(string trigger, TriggerPayload payload, double nowHours,
        Func<string, TriggerPayload, IConversationContext?> contextFor)
    {
        var results = new List<FireCandidate>();
        if (!TriggerRegistry.TryGet(trigger, out var definition))
            return results;

        foreach (var content in ContentFor(trigger))
        {
            var c = new FireCandidate(content);
            results.Add(c);
            c.SubjectMatches = Subjects.Matches(definition, content.Subject, payload, content.CompanionId);
            if (!c.SubjectMatches) { c.Outcome = "subject"; continue; }
            c.WhereMatches = Subjects.WhereMatches(content.Spec.Where, payload);
            if (!c.WhereMatches) { c.Outcome = "where"; continue; }

            var context = contextFor(content.CompanionId, payload);
            if (context == null) { c.Present = false; c.Outcome = "no hero"; continue; }
            c.Present = content.Spec.AllowAbsent || context.IsInPlayerParty;
            if (c.Present == false) { c.Outcome = "not in party"; continue; }
            c.OffCooldown = IsOffCooldown(content, nowHours);
            if (c.OffCooldown == false) { c.Outcome = "cooldown"; continue; }
            c.AlreadyQueued = _queue.Any(q => q.Key == content.Key);
            if (c.AlreadyQueued == true) { c.Outcome = "already queued"; continue; }
            c.ChanceHit = Roll(content.Spec.Chance ?? DeliveryRules.DefaultChance);
            if (c.ChanceHit == false) { c.Outcome = "chance"; continue; }
            c.WhenHolds = ConversationConditions.Holds(content.When, context);
            if (c.WhenHolds == false) { c.Outcome = "when"; continue; }

            _queue.Add(new QueuedItem
            {
                Key = content.Key,
                CompanionId = content.CompanionId,
                ContentId = content.Id,
                IsLine = content.IsLine,
                Trigger = trigger,
                Delivery = content.Delivery,
                Priority = Selection.PriorityOf(content.Priority),
                FiredAtHours = nowHours,
                ExpiresAtHours = nowHours + (content.Spec.ExpiresAfterHours ?? DeliveryRules.DefaultExpiresAfterHours),
                InMission = definition.InMission,
                Payload = payload,
            });
            c.Queued = true;
            c.Outcome = "queued";
        }
        return results;
    }

    private bool IsOffCooldown(TriggeredContent content, double nowHours) =>
        !_lastDelivered.TryGetValue(content.Key, out var last)
        || nowHours >= last + (content.Spec.CooldownDays ?? DeliveryRules.DefaultCooldownDays) * 24;

    private bool Roll(double chance)
    {
        if (chance >= 1) return true;
        if (chance <= 0) return false;
        const int scale = 1_000_000;
        return _random.Next(scale) < chance * scale;
    }

    /// <summary>
    /// Expired items are dropped (reported through <paramref name="dropped"/>). Then the deliverable items — environment and
    /// limits allow them, their content's conditions still hold — are compared by the selection rule; the winner is removed
    /// from the queue, counted, and returned. Items whose conditions fail now are dropped. Null when nothing is deliverable.
    /// </summary>
    public QueuedItem? NextDelivery(double nowHours, DeliveryEnvironment environment,
        Func<string, TriggerPayload, IConversationContext?> contextFor, Action<QueuedItem, string>? dropped = null)
    {
        RollDay(nowHours);
        foreach (var item in _queue.Where(q => nowHours > q.ExpiresAtHours).ToList())
        {
            _queue.Remove(item);
            dropped?.Invoke(item, "expired");
        }

        var deliverable = new List<QueuedItem>();
        foreach (var item in _queue.ToList())
        {
            if (!Allowed(item, nowHours, environment))
                continue;
            if (!_byKey.TryGetValue(item.Key, out var content))
            {
                _queue.Remove(item);
                dropped?.Invoke(item, "content no longer loaded");
                continue;
            }
            var context = contextFor(item.CompanionId, item.Payload);
            if (context == null || !StillHolds(content, context))
            {
                _queue.Remove(item);
                dropped?.Invoke(item, context == null ? "no hero" : "conditions no longer hold");
                continue;
            }
            deliverable.Add(item);
        }

        var winner = Selection.Pick(deliverable, i => i.Priority, _random);
        if (winner == null)
            return null;

        _queue.Remove(winner);
        _lastDelivered[winner.Key] = nowHours;
        if (winner.IsInterruption)
        {
            _interruptionsToday++;
            _lastInterruption[winner.CompanionId] = nowHours;
        }
        else
            _notificationsToday++;
        return winner;
    }

    private bool Allowed(QueuedItem item, double nowHours, DeliveryEnvironment environment)
    {
        if (item.InMission && !environment.OnMap)
            return false;
        if (!item.IsInterruption)
            return _notificationsToday < DeliveryRules.NotificationsPerDay;
        if (!environment.CanInterrupt || _interruptionsToday >= DeliveryRules.InterruptionsPerDay)
            return false;
        return !_lastInterruption.TryGetValue(item.CompanionId, out var last)
            || nowHours >= last + DeliveryRules.InterruptionCooldownDays * 24;
    }

    /// <summary>At delivery: the content's own <c>when</c>, its presence rule, and for a conversation one start node that can be said.</summary>
    private static bool StillHolds(TriggeredContent content, IConversationContext context)
    {
        if (!content.Spec.AllowAbsent && !context.IsInPlayerParty)
            return false;
        if (!ConversationConditions.Holds(content.When, context))
            return false;
        if (content.Conversation == null)
            return true;
        var nodes = content.Conversation.Nodes ?? new List<ConversationNode>();
        return (content.Conversation.Start ?? new List<string>())
            .Select(id => nodes.FirstOrDefault(n => n?.Id == id))
            .Any(n => n != null && ConversationConditions.NodeHolds(n, context));
    }

    private void RollDay(double nowHours)
    {
        var day = (long)Math.Floor(nowHours / 24);
        if (day == _day) return;
        _day = day;
        _interruptionsToday = 0;
        _notificationsToday = 0;
    }

    /// <summary>Console <c>albocarius.clearcooldowns</c>: per-item and per-companion cooldowns and today's counters.</summary>
    public void ClearCooldowns()
    {
        _lastDelivered.Clear();
        _lastInterruption.Clear();
        _interruptionsToday = 0;
        _notificationsToday = 0;
    }

    /// <summary>Save data (G9): one JSON string per queue item / cooldown / counter.</summary>
    public List<string> Save() => TriggerEngineSave.Encode(this);

    /// <summary>Restores <see cref="Save"/> output; entries that cannot be read are skipped and reported.</summary>
    public int Load(IEnumerable<string>? saved, Action<string>? warn = null) => TriggerEngineSave.Decode(this, saved, warn);

    internal void Restore(IEnumerable<QueuedItem> queue, IDictionary<string, double> lastDelivered, IDictionary<string, double> lastInterruption,
        long day, int interruptions, int notifications)
    {
        _queue.Clear();
        _queue.AddRange(queue);
        _lastDelivered.Clear();
        foreach (var p in lastDelivered) _lastDelivered[p.Key] = p.Value;
        _lastInterruption.Clear();
        foreach (var p in lastInterruption) _lastInterruption[p.Key] = p.Value;
        _day = day;
        _interruptionsToday = interruptions;
        _notificationsToday = notifications;
    }

    internal long Day => _day;
}
