using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;
using AlbocariusCompanions.Core.Triggers;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Core.Validation;

/// <summary>
/// <c>trigger</c> on conversations and lines (SPEC_TRIGGERS_DELIVERY T3, M11 G5/G6): known trigger, subject valid for it,
/// <c>where</c> on registry fields with a value of the right type, chance and times in range, polling params, and content
/// that fits the delivery. The entry/slot exceptions live in ConversationChecks / DialogChecks.
/// </summary>
public static class TriggerChecks
{
    public static void Validate(CompanionPackage package, List<ValidationIssue> issues)
    {
        var folder = package.FolderName;
        foreach (var conversation in package.Conversations.Where(c => c.Trigger != null))
        {
            var where = $"Conversation '{conversation.Id ?? "(no id)"}'";
            ValidateSpec(conversation.Trigger!, where, package, folder, issues);
            ValidateConversationDelivery(conversation, where, folder, issues);
        }

        foreach (var line in package.Dialogs?.Lines ?? new List<DialogLine>())
        {
            if (line?.Trigger == null) continue;
            var where = $"Line '{line.Id ?? "(no id)"}'";
            ValidateSpec(line.Trigger, where, package, folder, issues);
            if (line.Trigger.Delivery != null && line.Trigger.Delivery != DeliveryRules.Notification)
                issues.Add(ValidationIssue.Error(IssueCodes.InvalidTrigger, folder,
                    $"{where} is a triggered line with delivery '{line.Trigger.Delivery}'; a line can only be a '{DeliveryRules.Notification}'."));
        }
    }

    /// <summary>The delivery a trigger spec uses: its own, or the content's default.</summary>
    public static string DeliveryOf(TriggerSpec spec, bool isLine) =>
        spec.Delivery ?? (isLine ? DeliveryRules.Notification : DeliveryRules.Conversation);

    private static void ValidateSpec(TriggerSpec spec, string where, CompanionPackage package, string folder,
        List<ValidationIssue> issues)
    {
        void Error(string message) => issues.Add(ValidationIssue.Error(IssueCodes.InvalidTrigger, folder, $"{where}: trigger {message}"));

        if (string.IsNullOrEmpty(spec.On))
        {
            Error("needs 'on' (a trigger name from docs/TRIGGERS.md).");
            return;
        }
        if (!TriggerRegistry.TryGet(spec.On, out var trigger))
        {
            issues.Add(ValidationIssue.Error(IssueCodes.UnknownTrigger, folder,
                $"{where}: trigger '{spec.On}' does not exist (see docs/TRIGGERS.md)."));
            return;
        }

        if (spec.Subject != null && !Subjects.IsValidFor(trigger, spec.Subject))
            Error($"'{spec.On}' has subject '{spec.Subject}'; valid: {string.Join(", ", trigger.SubjectKinds().Select(k => Subjects.WithId.Contains(k) ? k + ":<id>" : k))}.");

        foreach (var pair in spec.Where ?? new Dictionary<string, JToken>())
        {
            var kind = trigger.KindOfPath(pair.Key);
            if (kind == null)
                Error($"'{spec.On}' has no field '{pair.Key}' (where); fields: {string.Join(", ", trigger.Fields.Select(f => f.Name))}.");
            else if (!ValueFits(kind.Value, pair.Value))
                Error($"'{spec.On}' where '{pair.Key}' needs a {KindName(kind.Value)} value.");
        }

        if (spec.Chance != null && (spec.Chance < 0 || spec.Chance > 1))
            Error($"has chance {Num(spec.Chance.Value)}; use 0..1.");
        if (spec.Delivery != null && !DeliveryRules.Deliveries.Contains(spec.Delivery))
            Error($"has delivery '{spec.Delivery}'; use one of: {string.Join(", ", DeliveryRules.Deliveries)}.");
        if (spec.ExpiresAfterHours != null && spec.ExpiresAfterHours < 0)
            Error("has a negative 'expiresAfterHours'.");
        if (spec.CooldownDays != null && spec.CooldownDays < 0)
            Error("has a negative 'cooldownDays'.");

        ValidateParams(spec, trigger, package, Error);
    }

    private static void ValidateParams(TriggerSpec spec, TriggerDefinition trigger, CompanionPackage package, Action<string> error)
    {
        var given = spec.Params ?? new Dictionary<string, JToken>();
        if (!trigger.IsPolling)
        {
            if (given.Count > 0)
                error($"'{trigger.Name}' takes no 'params'.");
            return;
        }

        foreach (var name in given.Keys.Where(k => trigger.Parameters.All(p => p.Name != k)))
            error($"'{trigger.Name}' has no parameter '{name}'; parameters: {string.Join(", ", trigger.Parameters.Select(p => p.Name))}.");
        foreach (var p in trigger.Parameters)
        {
            if (!given.TryGetValue(p.Name, out var value) || value == null || value.Type == JTokenType.Null)
            {
                if (p.Required) error($"'{trigger.Name}' needs the parameter '{p.Name}' ({p.Description}).");
                continue;
            }
            if (!ValueFits(p.Kind, value))
                error($"'{trigger.Name}' parameter '{p.Name}' needs a {KindName(p.Kind)} value.");
        }

        switch (trigger.Name)
        {
            case "nearSettlement":
                var radius = NumberOf(given, "radius");
                var rearm = NumberOf(given, "rearmRadius");
                if (radius != null && radius <= 0)
                    error("'nearSettlement' needs a radius > 0.");
                if (radius != null && rearm != null && rearm <= radius)
                    error("'nearSettlement' needs rearmRadius > radius (hysteresis, GAME_MAP Q43).");
                if (given.TryGetValue("settlement", out var settlement) && settlement.Type == JTokenType.String && ((string)settlement!).Length == 0)
                    error("'nearSettlement' needs a settlement id.");
                break;
            case "varCrossed":
                if (given.TryGetValue("direction", out var direction) && direction.Type == JTokenType.String
                    && (string)direction! != "up" && (string)direction! != "down")
                    error("'varCrossed' direction must be 'up' or 'down'.");
                if (given.TryGetValue("var", out var variable) && variable.Type == JTokenType.String && !IsOwnIntVariable(package, (string)variable!))
                    error($"'varCrossed' var '{(string)variable!}' is not an int variable of this companion (declare it, or use mood).");
                break;
            case "roleStarted":
            case "roleEnded":
                if (given.TryGetValue("role", out var role) && role.Type == JTokenType.String && !TriggerRegistry.Roles.Contains((string)role!))
                    error($"'{trigger.Name}' role '{(string)role!}' is unknown; use one of: {string.Join(", ", TriggerRegistry.Roles)}.");
                break;
        }
    }

    private static void ValidateConversationDelivery(ConversationDefinition conversation, string where, string folder, List<ValidationIssue> issues)
    {
        void Error(string message) => issues.Add(ValidationIssue.Error(IssueCodes.InvalidTrigger, folder, $"{where}: {message}"));

        var nodes = (conversation.Nodes ?? new List<ConversationNode>()).Where(n => n?.Id != null)
            .GroupBy(n => n.Id!, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var start = (conversation.Start ?? new List<string>()).Where(id => id != null && nodes.ContainsKey(id)).Select(id => nodes[id]).ToList();
        var delivery = DeliveryOf(conversation.Trigger!, isLine: false);

        if (delivery == DeliveryRules.Notification)
        {
            // Only the selected start line is shown; anything after it would never be reached.
            foreach (var node in start.Where(n => !IsEnd(n)))
                Error($"is delivered as a notification, so its start node '{node.Id}' must end the conversation (next: [\"end\"]).");
        }
        else if (delivery == DeliveryRules.Inquiry)
        {
            if (start.Count != 1)
            {
                Error("is delivered as an inquiry, so it needs exactly one start (npc) node: the pop-up text.");
                return;
            }
            var options = (start[0].Next ?? new List<string>()).Where(id => id != null && nodes.ContainsKey(id)).Select(id => nodes[id]).ToList();
            if (options.Count == 0 || options.Count != (start[0].Next?.Count ?? 0)
                || options.Any(o => o.Speaker != ConversationRules.SpeakerPlayer))
            {
                Error($"is delivered as an inquiry, so its start node '{start[0].Id}' must continue with 1 or more player nodes (the buttons).");
                return;
            }
            foreach (var option in options.Where(o => !IsEnd(o)))
                Error($"is delivered as an inquiry, so its option '{option.Id}' must end the conversation (next: [\"end\"]).");
        }
    }

    private static bool IsEnd(ConversationNode node) =>
        node.Next != null && node.Next.Count == 1 && node.Next[0] == ConversationRules.NextEnd;

    private static bool IsOwnIntVariable(CompanionPackage package, string name)
    {
        if (name == VariableRules.Mood) return true;
        var declared = package.Dialogs?.Variables;
        return declared != null && declared.TryGetValue(name, out var declaration) && declaration?.Type == VariableRules.TypeInt;
    }

    private static double? NumberOf(Dictionary<string, JToken> given, string name) =>
        given.TryGetValue(name, out var v) && (v.Type == JTokenType.Integer || v.Type == JTokenType.Float) ? (double)v : (double?)null;

    /// <summary>A JSON value fits a field kind: Text → string, Bool → bool, Int → integer, Number → any number.</summary>
    public static bool ValueFits(FieldKind kind, JToken? value)
    {
        if (value == null) return false;
        switch (kind)
        {
            case FieldKind.Bool: return value.Type == JTokenType.Boolean;
            case FieldKind.Int: return value.Type == JTokenType.Integer;
            case FieldKind.Number: return value.Type == JTokenType.Integer || value.Type == JTokenType.Float;
            default: return value.Type == JTokenType.String;
        }
    }

    public static string KindName(FieldKind kind) => kind switch
    {
        FieldKind.Bool => "true/false",
        FieldKind.Int => "whole-number",
        FieldKind.Number => "number",
        _ => "text",
    };

    private static string Num(double value) => value.ToString(CultureInfo.InvariantCulture);
}
