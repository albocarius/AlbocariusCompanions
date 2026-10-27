using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Core.Triggers;

/// <summary>
/// Save format of <see cref="TriggerEngine"/> (M11 G9): a <c>List&lt;string&gt;</c> (the Q20-verified save type), one JSON
/// object per entry: <c>{"t":"q",…}</c> queue item, <c>{"t":"d",…}</c> item cooldown, <c>{"t":"i",…}</c> companion
/// interruption, <c>{"t":"c",…}</c> today's counters. Times are campaign hours.
/// </summary>
internal static class TriggerEngineSave
{
    public static List<string> Encode(TriggerEngine engine)
    {
        var list = new List<string>();
        foreach (var q in engine.Queue)
            list.Add(Json(new JObject
            {
                ["t"] = "q", ["key"] = q.Key, ["companion"] = q.CompanionId, ["id"] = q.ContentId, ["line"] = q.IsLine,
                ["trigger"] = q.Trigger, ["delivery"] = q.Delivery, ["priority"] = q.Priority, ["fired"] = q.FiredAtHours,
                ["expires"] = q.ExpiresAtHours, ["inMission"] = q.InMission, ["payload"] = PayloadJson.ToJson(q.Payload),
            }));
        foreach (var p in engine.LastDelivered)
            list.Add(Json(new JObject { ["t"] = "d", ["key"] = p.Key, ["at"] = p.Value }));
        foreach (var p in engine.LastInterruption)
            list.Add(Json(new JObject { ["t"] = "i", ["companion"] = p.Key, ["at"] = p.Value }));
        list.Add(Json(new JObject { ["t"] = "c", ["day"] = engine.Day, ["interruptions"] = engine.InterruptionsToday, ["notifications"] = engine.NotificationsToday }));
        return list;
    }

    public static int Decode(TriggerEngine engine, IEnumerable<string>? saved, Action<string>? warn)
    {
        var queue = new List<QueuedItem>();
        var delivered = new Dictionary<string, double>(StringComparer.Ordinal);
        var interrupted = new Dictionary<string, double>(StringComparer.Ordinal);
        long day = long.MinValue;
        int interruptions = 0, notifications = 0, read = 0;

        foreach (var text in saved ?? Enumerable.Empty<string>())
        {
            try
            {
                var o = JObject.Parse(text);
                switch ((string?)o["t"])
                {
                    case "q":
                        queue.Add(new QueuedItem
                        {
                            Key = (string)o["key"]!, CompanionId = (string)o["companion"]!, ContentId = (string)o["id"]!,
                            IsLine = (bool)o["line"]!, Trigger = (string)o["trigger"]!, Delivery = (string)o["delivery"]!,
                            Priority = (int)o["priority"]!, FiredAtHours = (double)o["fired"]!, ExpiresAtHours = (double)o["expires"]!,
                            InMission = (bool)o["inMission"]!, Payload = PayloadJson.FromJson((JObject)o["payload"]!),
                        });
                        break;
                    case "d": delivered[(string)o["key"]!] = (double)o["at"]!; break;
                    case "i": interrupted[(string)o["companion"]!] = (double)o["at"]!; break;
                    case "c":
                        day = (long)o["day"]!;
                        interruptions = (int)o["interruptions"]!;
                        notifications = (int)o["notifications"]!;
                        break;
                    default: throw new FormatException("unknown entry type");
                }
                read++;
            }
            catch (Exception ex) when (ex is JsonException || ex is FormatException || ex is InvalidCastException || ex is ArgumentException || ex is NullReferenceException)
            {
                warn?.Invoke($"Trigger save entry skipped ({ex.Message}): {text}");
            }
        }

        engine.Restore(queue, delivered, interrupted, day, interruptions, notifications);
        return read;
    }

    private static string Json(JObject o) => o.ToString(Formatting.None);
}

/// <summary>Payload ↔ JSON (save data, simulator protocol): <c>{ field: { "k": kind, "v": value } | { "k": kind, "p": { … } } }</c>.</summary>
public static class PayloadJson
{
    public static JObject ToJson(TriggerPayload payload)
    {
        var o = new JObject();
        foreach (var pair in payload.Fields)
        {
            var v = pair.Value;
            o[pair.Key] = v.IsEntity
                ? new JObject { ["k"] = v.Kind.ToString(), ["p"] = JObject.FromObject(v.Properties) }
                : new JObject { ["k"] = v.Kind.ToString(), ["v"] = JToken.FromObject(v.Value!) };
        }
        return o;
    }

    public static TriggerPayload FromJson(JObject o)
    {
        var fields = new Dictionary<string, PayloadValue>(StringComparer.Ordinal);
        foreach (var pair in o)
        {
            var e = (JObject)pair.Value!;
            var kind = (FieldKind)Enum.Parse(typeof(FieldKind), (string)e["k"]!);
            if (EntityProperties.IsEntity(kind))
            {
                var props = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var p in (JObject)e["p"]!)
                    props[p.Key] = p.Value!.Type == JTokenType.Boolean ? (object)(bool)p.Value : (string)p.Value!;
                fields[pair.Key] = PayloadValue.Entity(kind, props);
            }
            else
            {
                var v = e["v"]!;
                fields[pair.Key] = kind switch
                {
                    FieldKind.Int => PayloadValue.Int((long)v),
                    FieldKind.Number => PayloadValue.Number((double)v),
                    FieldKind.Bool => PayloadValue.Bool((bool)v),
                    _ => PayloadValue.Text(Convert.ToString((object?)v, CultureInfo.InvariantCulture) ?? ""),
                };
            }
        }
        return new TriggerPayload(fields);
    }
}
