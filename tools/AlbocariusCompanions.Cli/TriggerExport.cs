using System.Linq;
using AlbocariusCompanions.Core.Rules;
using AlbocariusCompanions.Core.Triggers;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Cli;

/// <summary>
/// <c>export triggers</c> (SPEC_EDITOR §3): the trigger registry as JSON, read from the same <see cref="TriggerRegistry"/>
/// that <see cref="TriggerDocs"/> renders into docs/TRIGGERS.md.
/// </summary>
public static class TriggerExport
{
    public const int ExportVersion = 1;

    private static readonly FieldKind[] EntityKinds =
        { FieldKind.Hero, FieldKind.Party, FieldKind.Settlement, FieldKind.Clan, FieldKind.Kingdom, FieldKind.Faction, FieldKind.Item };

    public static JObject Build() => new JObject
    {
        ["exportVersion"] = ExportVersion,
        ["defaults"] = new JObject
        {
            ["chance"] = DeliveryRules.DefaultChance,
            ["expiresAfterHours"] = DeliveryRules.DefaultExpiresAfterHours,
            ["cooldownDays"] = DeliveryRules.DefaultCooldownDays,
            ["lineDelivery"] = DeliveryRules.Notification,
            ["conversationDelivery"] = DeliveryRules.Conversation,
        },
        ["limits"] = new JObject
        {
            ["interruptionCooldownDays"] = DeliveryRules.InterruptionCooldownDays,
            ["interruptionsPerDay"] = DeliveryRules.InterruptionsPerDay,
            ["notificationsPerDay"] = DeliveryRules.NotificationsPerDay,
            ["standardInquiryMaxOptions"] = DeliveryRules.StandardInquiryMaxOptions,
        },
        ["deliveries"] = new JArray(DeliveryRules.Deliveries),
        ["subjectsWithId"] = new JArray(Subjects.WithId.OrderBy(s => s, System.StringComparer.Ordinal)),
        ["entityProperties"] = new JObject(EntityKinds.Select(k => new JProperty(k.ToString(), new JObject
        {
            ["properties"] = new JArray(EntityProperties.Of(k)),
            ["boolProperties"] = new JArray(EntityProperties.Of(k).Where(p => EntityProperties.BoolProperties.Contains(p))),
        }))),
        ["roles"] = new JArray(TriggerRegistry.Roles),
        ["triggers"] = new JArray(TriggerRegistry.All.Select(Trigger)),
    };

    private static JObject Trigger(TriggerDefinition t) => new JObject
    {
        ["name"] = t.Name,
        ["category"] = t.Category,
        ["common"] = t.Common,
        ["description"] = t.Description,
        ["polling"] = t.IsPolling,
        ["inMission"] = t.InMission,
        ["subjects"] = new JArray(t.SubjectKinds()),
        ["defaultSubject"] = t.DefaultSubject,
        ["subjectFields"] = new JArray(t.SubjectFields),
        ["fields"] = new JArray(t.Fields.Select(f => new JObject
        {
            ["name"] = f.Name,
            ["kind"] = f.Kind.ToString(),
            ["entity"] = EntityProperties.IsEntity(f.Kind),
            ["description"] = f.Description,
        })),
        ["params"] = new JArray(t.Parameters.Select(p => new JObject
        {
            ["name"] = p.Name,
            ["kind"] = p.Kind.ToString(),
            ["required"] = p.Required,
            ["description"] = p.Description,
        })),
        // Lines are always notifications; any delivery is open to a conversation (SCHEMA "Triggers").
        ["deliveries"] = new JArray(DeliveryRules.Deliveries),
    };
}
