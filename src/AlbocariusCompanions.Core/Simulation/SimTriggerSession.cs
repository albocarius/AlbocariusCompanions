using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Registry;
using AlbocariusCompanions.Core.Rules;
using AlbocariusCompanions.Core.Triggers;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Core.Simulation;

/// <summary>
/// The trigger side of the simulator (M11 T7, docs/SIMULATOR_PROTOCOL.md "Triggers"): one <see cref="TriggerEngine"/>
/// over every loaded companion, a state as in <see cref="SimSession"/> and a clock in campaign hours. Fire a trigger with a
/// payload, see each candidate's checks, the queue, and what a delivery would show. Effects are not applied here.
/// </summary>
public sealed class SimTriggerSession
{
    private readonly CompanionRegistry _registry;
    private readonly TriggerEngine _engine;
    private readonly IRandomSource _random;
    private SimState _state;
    private VariableStore _variables;

    public SimTriggerSession(CompanionRegistry registry, SimState? state, IRandomSource random)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _engine = new TriggerEngine(registry.All, random);
        _state = (state ?? new SimState()).Clone();
        _variables = SimSession.BuildVariables(registry, _state);
    }

    /// <summary>Campaign time in hours; requests may move it forward (never back).</summary>
    public double Hours { get; private set; }

    public TriggerEngine Engine => _engine;

    private void Advance(double? hours)
    {
        if (hours == null) return;
        if (hours < Hours)
            throw new SimulationException($"'hours' {hours} is before the session time {Hours}");
        Hours = hours.Value;
    }

    public JObject Fire(string trigger, JObject? payloadJson, double? hours)
    {
        if (!TriggerRegistry.TryGet(trigger, out var definition))
            throw new SimulationException($"unknown trigger '{trigger}'");
        Advance(hours);
        var payload = ParsePayload(definition, payloadJson);
        var candidates = _engine.Fire(trigger, payload, Hours, ContextFor);
        return new JObject
        {
            ["hours"] = Hours,
            ["candidates"] = new JArray(candidates.Select(c => Candidate(c, payload))),
            ["queue"] = QueueJson(),
        };
    }

    public JObject Deliver(double? hours, bool onMap, bool canInterrupt)
    {
        Advance(hours);
        var dropped = new JArray();
        var item = _engine.NextDelivery(Hours, new DeliveryEnvironment { OnMap = onMap, CanInterrupt = canInterrupt && onMap }, ContextFor,
            (q, why) => dropped.Add(new JObject { ["key"] = q.Key, ["reason"] = why }));
        return new JObject
        {
            ["hours"] = Hours,
            ["delivered"] = item == null ? JValue.CreateNull() : Delivered(item),
            ["dropped"] = dropped,
            ["queue"] = QueueJson(),
        };
    }

    public JObject QueueJson() => new JObject
    {
        ["items"] = new JArray(_engine.Queue.OrderByDescending(q => q.Priority).ThenBy(q => q.FiredAtHours).Select(q => new JObject
        {
            ["key"] = q.Key, ["companion"] = q.CompanionId, ["content"] = q.ContentId, ["trigger"] = q.Trigger,
            ["delivery"] = q.Delivery, ["priority"] = q.Priority, ["firedAt"] = q.FiredAtHours, ["expiresAt"] = q.ExpiresAtHours,
            ["inMission"] = q.InMission,
        })),
        ["itemCooldowns"] = JObject.FromObject(_engine.LastDelivered),
        ["companionInterruptions"] = JObject.FromObject(_engine.LastInterruption),
        ["interruptionsToday"] = _engine.InterruptionsToday,
        ["notificationsToday"] = _engine.NotificationsToday,
    };

    public void ClearCooldowns() => _engine.ClearCooldowns();

    private JObject Candidate(FireCandidate c, TriggerPayload payload)
    {
        var o = new JObject
        {
            ["companion"] = c.Content.CompanionId,
            ["content"] = c.Content.Id,
            ["kind"] = c.Content.IsLine ? "line" : "conversation",
            ["delivery"] = c.Content.Delivery,
            ["subject"] = c.Content.Subject,
            ["outcome"] = c.Outcome,
            ["subjectMatches"] = c.SubjectMatches,
            ["whereMatches"] = c.WhereMatches,
            ["present"] = Nullable(c.Present),
            ["offCooldown"] = Nullable(c.OffCooldown),
            ["alreadyQueued"] = Nullable(c.AlreadyQueued),
            ["chanceHit"] = Nullable(c.ChanceHit),
            ["whenHolds"] = Nullable(c.WhenHolds),
        };
        if (c.WhenHolds != null && ContextFor(c.Content.CompanionId, payload) is { } context)
            o["conditions"] = JToken.FromObject(ConditionExplain.When(c.Content.When, context));
        return o;
    }

    private JObject Delivered(QueuedItem item)
    {
        var o = new JObject
        {
            ["key"] = item.Key, ["companion"] = item.CompanionId, ["content"] = item.ContentId, ["trigger"] = item.Trigger,
            ["delivery"] = item.Delivery,
        };
        if (!_registry.TryGet(item.CompanionId, out var package) || package == null || !_engine.TryGetContent(item.Key, out var content))
            return o;
        var context = ContextFor(item.CompanionId, item.Payload)!;
        if (content.IsLine)
        {
            o["text"] = package.Text(item.ContentId, "en");
            return o;
        }

        var conversation = content.Conversation!;
        var nodes = (conversation.Nodes ?? new List<ConversationNode>()).Where(n => n?.Id != null).ToList();
        var start = (conversation.Start ?? new List<string>()).Select(id => nodes.FirstOrDefault(n => n.Id == id)).Where(n => n != null)
            .Where(n => ConversationConditions.NodeHolds(n!, context)).ToList();
        var said = Selection.Pick(start, n => n!.Priority, _random);
        if (said == null)
            return o;
        o["node"] = said.Id;
        o["text"] = package.Text(said.Id!, "en");
        if (item.Delivery == DeliveryRules.Inquiry)
            o["options"] = new JArray((said.Next ?? new List<string>()).Select(id => nodes.FirstOrDefault(n => n.Id == id)).Where(n => n != null)
                .Select(n => new JObject
                {
                    ["node"] = n!.Id,
                    ["text"] = package.Text(n.Id!, "en"),
                    ["shown"] = ConversationConditions.NodeHolds(n, context),
                }));
        if (item.Delivery == DeliveryRules.Inquiry)
            o["popup"] = ((JArray)o["options"]!).Count <= DeliveryRules.StandardInquiryMaxOptions ? "standard" : "multiSelection";
        return o;
    }

    private static JToken Nullable(bool? value) => value == null ? JValue.CreateNull() : (JToken)value.Value;

    private IConversationContext? ContextFor(string companionId, TriggerPayload payload) =>
        _registry.TryGet(companionId, out var package) && package != null ? new Context(this, companionId, payload) : null;

    /// <summary>
    /// Request payload: field → value; an entity field takes an object of its properties (missing ones read as ""/false),
    /// a primitive field a JSON value of its kind. Unknown fields or wrong kinds are an error.
    /// </summary>
    public static TriggerPayload ParsePayload(TriggerDefinition trigger, JObject? json)
    {
        var fields = new Dictionary<string, PayloadValue>(StringComparer.Ordinal);
        foreach (var pair in json ?? new JObject())
        {
            var field = trigger.Field(pair.Key) ?? throw new SimulationException(
                $"'{trigger.Name}' has no field '{pair.Key}'; fields: {string.Join(", ", trigger.Fields.Select(f => f.Name))}");
            var value = pair.Value!;
            if (EntityProperties.IsEntity(field.Kind))
            {
                if (value.Type != JTokenType.Object)
                    throw new SimulationException($"field '{pair.Key}' ({field.Kind}) takes an object of properties");
                var props = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var p in (JObject)value)
                {
                    if (!EntityProperties.Of(field.Kind).Contains(p.Key))
                        throw new SimulationException($"field '{pair.Key}' ({field.Kind}) has no property '{p.Key}'");
                    var isBool = EntityProperties.BoolProperties.Contains(p.Key);
                    if (isBool ? p.Value!.Type != JTokenType.Boolean : p.Value!.Type != JTokenType.String)
                        throw new SimulationException($"property '{pair.Key}.{p.Key}' takes a {(isBool ? "true/false" : "text")} value");
                    props[p.Key] = isBool ? (object)(bool)p.Value : (string)p.Value!;
                }
                fields[pair.Key] = PayloadValue.Entity(field.Kind, props);
            }
            else
            {
                if (!Validation.TriggerChecks.ValueFits(field.Kind, value))
                    throw new SimulationException($"field '{pair.Key}' takes a {Validation.TriggerChecks.KindName(field.Kind)} value");
                fields[pair.Key] = field.Kind switch
                {
                    FieldKind.Int => PayloadValue.Int((long)value),
                    FieldKind.Number => PayloadValue.Number((double)value),
                    FieldKind.Bool => PayloadValue.Bool((bool)value),
                    _ => PayloadValue.Text((string)value!),
                };
            }
        }
        return new TriggerPayload(fields);
    }

    private sealed class Context : IConversationContext, ITriggerContext
    {
        private readonly SimTriggerSession _s;
        private readonly string _id;
        private readonly SimCompanion _self;

        public Context(SimTriggerSession session, string id, TriggerPayload payload)
        {
            _s = session;
            _id = id;
            _self = session._state.Companions.TryGetValue(id, out var c) ? c : new SimCompanion();
            Event = payload;
        }

        public TriggerPayload? Event { get; }
        public string? Role => _self.Role;
        public bool HasMet => _self.Met;
        public bool IsHired => _self.Hired;
        public bool IsInPlayerParty => _self.InParty;
        public int Relation => _self.Relation;
        public int PlayerGold => _s._state.Player.Gold;
        public bool ClanIsFull => _s._state.ClanIsFull;
        public int? GetVariable(string reference) => _s._variables.Get(_id, reference);
        public int? GoldOf(string actor) => actor switch
        {
            Transfers.Player => _s._state.Player.Gold,
            Transfers.Self => _self.Gold,
            _ => _s._registry.TryGet(actor, out _) ? (_s._state.Companions.TryGetValue(actor, out var other) ? other.Gold : 0) : (int?)null,
        };
        public int PlayerItemCount(string itemId) => _s._state.Player.Items.TryGetValue(itemId, out var n) ? n : 0;
    }
}
