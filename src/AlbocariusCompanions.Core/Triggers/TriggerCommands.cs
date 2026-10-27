using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AlbocariusCompanions.Core.Rules;

namespace AlbocariusCompanions.Core.Triggers;

/// <summary>
/// Text side of the trigger console commands (M11 T7): <c>albocarius.fire &lt;trigger&gt; [field=value …]</c>,
/// <c>albocarius.queue</c>, <c>albocarius.clearcooldowns</c>. The game layer resolves entity ids to game objects.
/// </summary>
public static class TriggerCommands
{
    public const string UsageFire = "Usage: albocarius.fire <trigger> [field=value ...]  (entity fields take a string id, e.g. victim=lord_1_1)";

    /// <summary>
    /// Parses the arguments into a payload. Entity fields go through <paramref name="resolveEntity"/> (kind, string id →
    /// entity, null = not found); primitives are parsed by kind. Returns null and an error text when anything is wrong.
    /// Polling triggers fire every item that uses them; their params are not checked here.
    /// </summary>
    public static TriggerPayload? ParseFire(IReadOnlyList<string> args, Func<FieldKind, string, PayloadValue?> resolveEntity,
        out TriggerDefinition? trigger, out string error)
    {
        trigger = null;
        error = "";
        if (args.Count == 0)
        {
            error = UsageFire;
            return null;
        }
        if (!TriggerRegistry.TryGet(args[0], out var definition))
        {
            error = $"Error: unknown trigger '{args[0]}' (see docs/TRIGGERS.md).";
            return null;
        }
        trigger = definition;

        var fields = new Dictionary<string, PayloadValue>(StringComparer.Ordinal);
        foreach (var arg in args.Skip(1))
        {
            var eq = arg.IndexOf('=');
            if (eq <= 0)
            {
                error = $"Error: '{arg}' is not field=value. {UsageFire}";
                return null;
            }
            var name = arg.Substring(0, eq);
            var text = arg.Substring(eq + 1);
            var field = definition.Field(name);
            if (field == null)
            {
                error = $"Error: '{definition.Name}' has no field '{name}'; fields: {string.Join(", ", definition.Fields.Select(f => f.Name))}.";
                return null;
            }
            PayloadValue? value;
            if (EntityProperties.IsEntity(field.Kind))
            {
                value = resolveEntity(field.Kind, text);
                if (value == null)
                {
                    error = $"Error: no {field.Kind.ToString().ToLowerInvariant()} with id '{text}'.";
                    return null;
                }
            }
            else if (!Subjects.TryParsePrimitive(field.Kind, text, out value))
            {
                error = $"Error: '{name}' takes a {Validation.TriggerChecks.KindName(field.Kind)} value, not '{text}'.";
                return null;
            }
            fields[name] = value!;
        }
        return new TriggerPayload(fields);
    }

    /// <summary>One line per candidate: its key and the outcome.</summary>
    public static string FormatFire(string trigger, IReadOnlyList<FireCandidate> candidates)
    {
        if (candidates.Count == 0)
            return $"{trigger}: no loaded content uses this trigger.";
        return $"{trigger}: {candidates.Count(c => c.Queued)} of {candidates.Count} queued\n" +
            string.Join("\n", candidates.Select(c => $"  {c.Content.Key} ({c.Content.Delivery}): " +
                (c.Queued ? "queued" : "stopped at " + c.Outcome)));
    }

    /// <summary>The queue in delivery order, cooldowns and today's counters.</summary>
    public static string FormatQueue(TriggerEngine engine, double nowHours)
    {
        string H(double hours) => hours.ToString("0.#", CultureInfo.InvariantCulture);
        var lines = new List<string> { $"Now: hour {H(nowHours)}. Today: {engine.InterruptionsToday}/{DeliveryRules.InterruptionsPerDay} interruption(s), " +
            $"{engine.NotificationsToday}/{DeliveryRules.NotificationsPerDay} notification(s)." };
        lines.Add(engine.Queue.Count == 0 ? "Queue: empty." : $"Queue ({engine.Queue.Count}):");
        foreach (var q in engine.Queue.OrderByDescending(q => q.Priority).ThenBy(q => q.FiredAtHours))
            lines.Add($"  {q.Key} [{q.Delivery}, priority {q.Priority}] from {q.Trigger} at hour {H(q.FiredAtHours)}, expires at {H(q.ExpiresAtHours)}" +
                (q.InMission ? ", waits for the map" : ""));
        foreach (var p in engine.LastInterruption.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var free = p.Value + DeliveryRules.InterruptionCooldownDays * 24;
            lines.Add($"  {p.Key}: last interruption at hour {H(p.Value)}" + (free > nowHours ? $", may interrupt again at hour {H(free)}" : ""));
        }
        foreach (var p in engine.LastDelivered.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var cooldown = engine.TryGetContent(p.Key, out var content) ? (content.Spec.CooldownDays ?? DeliveryRules.DefaultCooldownDays) : 0;
            var free = p.Value + cooldown * 24;
            lines.Add($"  {p.Key}: delivered at hour {H(p.Value)}" + (free > nowHours ? $", cooldown until hour {H(free)}" : ""));
        }
        return string.Join("\n", lines);
    }
}
