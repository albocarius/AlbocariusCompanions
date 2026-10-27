using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AlbocariusCompanions.Core.Dialogs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Core.Triggers;

/// <summary>
/// The polling triggers (SPEC_TRIGGERS_DELIVERY T4, M11 G10): <c>nearSettlement</c> (two-radius latch, GAME_MAP Q43),
/// <c>daily</c>, <c>varCrossed</c> and <c>roleStarted</c>/<c>roleEnded</c>. Pure: the Game supplies distances, roles,
/// variable values and entities on its internal ticks; each fired item goes through <see cref="TriggerEngine.FireContent"/>.
/// The latch / last-value state is saved with the campaign.
/// </summary>
public sealed class TriggerPolling
{
    private readonly TriggerEngine _engine;
    /// <summary>nearSettlement content key → armed (true: may fire on the next approach).</summary>
    private readonly Dictionary<string, bool> _armed = new Dictionary<string, bool>(StringComparer.Ordinal);
    /// <summary>varCrossed content key → value seen last.</summary>
    private readonly Dictionary<string, int> _lastValue = new Dictionary<string, int>(StringComparer.Ordinal);
    /// <summary>Companion id → role seen last ("" = none).</summary>
    private readonly Dictionary<string, string> _lastRole = new Dictionary<string, string>(StringComparer.Ordinal);
    private long _lastDailyDay = long.MinValue;

    public TriggerPolling(TriggerEngine engine) => _engine = engine ?? throw new ArgumentNullException(nameof(engine));

    /// <summary>
    /// nearSettlement: for each item, the distance from the player's party to its settlement (null = unknown settlement,
    /// skipped). Inside <c>radius</c> while armed → fire and disarm; beyond <c>rearmRadius</c> → arm again. The first look
    /// only arms when outside, so loading a save next to the town does not fire.
    /// </summary>
    public List<FireCandidate> NearSettlements(double nowHours, Func<string, double?> distanceTo, Func<string, PayloadValue?> settlementEntity,
        Func<string, TriggerPayload, IConversationContext?> contextFor)
    {
        var fired = new List<FireCandidate>();
        foreach (var content in _engine.ContentFor("nearSettlement"))
        {
            var p = content.Spec.Params;
            var settlement = Text(p, "settlement");
            var radius = Number(p, "radius");
            var rearm = Number(p, "rearmRadius");
            if (settlement == null || radius == null || rearm == null)
                continue;
            var distance = distanceTo(settlement);
            if (distance == null)
                continue;

            if (!_armed.TryGetValue(content.Key, out var armed))
            {
                _armed[content.Key] = distance > radius;
                continue;
            }
            if (armed && distance <= radius)
            {
                _armed[content.Key] = false;
                var entity = settlementEntity(settlement);
                var payload = new TriggerPayload(entity == null
                    ? new Dictionary<string, PayloadValue>()
                    : new Dictionary<string, PayloadValue> { ["settlement"] = entity });
                fired.Add(_engine.FireContent(content, payload, nowHours, contextFor));
            }
            else if (!armed && distance > rearm)
                _armed[content.Key] = true;
        }
        return fired;
    }

    /// <summary>daily: fires once per in-game day (day = floor(hours / 24)); a second call on the same day does nothing.</summary>
    public IReadOnlyList<FireCandidate> Daily(double nowHours, Func<string, TriggerPayload, IConversationContext?> contextFor)
    {
        var day = (long)Math.Floor(nowHours / 24);
        if (day == _lastDailyDay)
            return Array.Empty<FireCandidate>();
        _lastDailyDay = day;
        return _engine.Fire("daily", TriggerPayload.None, nowHours, contextFor);
    }

    /// <summary>
    /// varCrossed: each item watches its own companion's variable (<c>params.var</c>). <c>up</c> fires when the value moves
    /// from below <c>value</c> to at-or-above it, <c>down</c> the other way. The first look only records the value. Called
    /// after effects and on the daily tick.
    /// </summary>
    public List<FireCandidate> Variables(double nowHours, Func<string, string, int?> variable,
        Func<string, TriggerPayload, IConversationContext?> contextFor)
    {
        var fired = new List<FireCandidate>();
        foreach (var content in _engine.ContentFor("varCrossed"))
        {
            var p = content.Spec.Params;
            var name = Text(p, "var");
            var threshold = Number(p, "value");
            var direction = Text(p, "direction");
            if (name == null || threshold == null || direction == null)
                continue;
            var value = variable(content.CompanionId, name);
            if (value == null)
                continue;

            var had = _lastValue.TryGetValue(content.Key, out var last);
            _lastValue[content.Key] = value.Value;
            if (!had)
                continue;
            var crossed = direction == "up"
                ? last < threshold && value >= threshold
                : last >= threshold && value < threshold;
            if (!crossed)
                continue;
            var payload = new TriggerPayload(new Dictionary<string, PayloadValue>
            {
                ["var"] = PayloadValue.Text(name),
                ["value"] = PayloadValue.Int(value.Value),
            });
            fired.Add(_engine.FireContent(content, payload, nowHours, contextFor));
        }
        return fired;
    }

    /// <summary>
    /// roleStarted / roleEnded: the current role of each companion (null = none). A change fires roleEnded for the old role
    /// and roleStarted for the new one, for the items whose <c>params.role</c> matches. The first look only records.
    /// </summary>
    public List<FireCandidate> Roles(double nowHours, IEnumerable<string> companions, Func<string, string?> roleOf,
        Func<string, PayloadValue?> heroEntity, Func<string, TriggerPayload, IConversationContext?> contextFor)
    {
        var fired = new List<FireCandidate>();
        foreach (var companion in companions)
        {
            var role = roleOf(companion) ?? "";
            var had = _lastRole.TryGetValue(companion, out var last);
            _lastRole[companion] = role;
            if (!had || last == role)
                continue;
            var hero = heroEntity(companion);
            if (hero == null)
                continue;
            if (last!.Length > 0)
                fired.AddRange(FireRole("roleEnded", last, hero, nowHours, contextFor));
            if (role.Length > 0)
                fired.AddRange(FireRole("roleStarted", role, hero, nowHours, contextFor));
        }
        return fired;
    }

    private IEnumerable<FireCandidate> FireRole(string trigger, string role, PayloadValue hero, double nowHours,
        Func<string, TriggerPayload, IConversationContext?> contextFor)
    {
        var payload = new TriggerPayload(new Dictionary<string, PayloadValue>
        {
            ["companion"] = hero,
            ["role"] = PayloadValue.Text(role),
        });
        return _engine.ContentFor(trigger).Where(c => Text(c.Spec.Params, "role") == role)
            .Select(c => _engine.FireContent(c, payload, nowHours, contextFor)).ToList();
    }

    private static string? Text(Dictionary<string, JToken>? p, string name) =>
        p != null && p.TryGetValue(name, out var v) && v.Type == JTokenType.String ? (string)v! : null;

    private static double? Number(Dictionary<string, JToken>? p, string name) =>
        p != null && p.TryGetValue(name, out var v) && (v.Type == JTokenType.Integer || v.Type == JTokenType.Float) ? (double)v : (double?)null;

    /// <summary>Save data: one JSON string per latch / last value / last role, plus the last daily day.</summary>
    public List<string> Save()
    {
        string J(JObject o) => o.ToString(Formatting.None);
        var list = new List<string>();
        foreach (var p in _armed) list.Add(J(new JObject { ["t"] = "near", ["key"] = p.Key, ["armed"] = p.Value }));
        foreach (var p in _lastValue) list.Add(J(new JObject { ["t"] = "var", ["key"] = p.Key, ["value"] = p.Value }));
        foreach (var p in _lastRole) list.Add(J(new JObject { ["t"] = "role", ["companion"] = p.Key, ["role"] = p.Value }));
        list.Add(J(new JObject { ["t"] = "daily", ["day"] = _lastDailyDay }));
        return list;
    }

    public int Load(IEnumerable<string>? saved, Action<string>? warn = null)
    {
        _armed.Clear();
        _lastValue.Clear();
        _lastRole.Clear();
        _lastDailyDay = long.MinValue;
        var read = 0;
        foreach (var text in saved ?? Enumerable.Empty<string>())
        {
            try
            {
                var o = JObject.Parse(text);
                switch ((string?)o["t"])
                {
                    case "near": _armed[(string)o["key"]!] = (bool)o["armed"]!; break;
                    case "var": _lastValue[(string)o["key"]!] = (int)o["value"]!; break;
                    case "role": _lastRole[(string)o["companion"]!] = (string)o["role"]!; break;
                    case "daily": _lastDailyDay = (long)o["day"]!; break;
                    default: throw new FormatException("unknown entry type");
                }
                read++;
            }
            catch (Exception ex) when (ex is JsonException || ex is FormatException || ex is InvalidCastException || ex is ArgumentException || ex is NullReferenceException)
            {
                warn?.Invoke(string.Format(CultureInfo.InvariantCulture, "Polling save entry skipped ({0}): {1}", ex.Message, text));
            }
        }
        return read;
    }
}
