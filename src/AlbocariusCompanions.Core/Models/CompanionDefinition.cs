using System.Collections.Generic;
using Newtonsoft.Json;

namespace AlbocariusCompanions.Core.Models;

/// <summary>companion.json (SPEC §4.1). Required values are nullable so validation can report them as missing.</summary>
public sealed class CompanionDefinition
{
    [JsonProperty("id")] public string? Id { get; set; }
    [JsonProperty("name")] public string? Name { get; set; }
    [JsonProperty("isFemale")] public bool? IsFemale { get; set; }
    [JsonProperty("voice")] public string? Voice { get; set; }
    [JsonProperty("age")] public int? Age { get; set; }
    [JsonProperty("culture")] public string? Culture { get; set; }
    [JsonProperty("cultureFallback")] public string? CultureFallback { get; set; }
    /// <summary>BodyProperties key; null = default per GAME_MAP.</summary>
    [JsonProperty("bodyProperties")] public string? BodyProperties { get; set; }
    /// <summary>Attribute name (vigor, control, ...) → value.</summary>
    [JsonProperty("attributes")] public Dictionary<string, int>? Attributes { get; set; }
    /// <summary>Game skill string id → value.</summary>
    [JsonProperty("skills")] public Dictionary<string, int>? Skills { get; set; }
    /// <summary>Game trait string id → level.</summary>
    [JsonProperty("traits")] public Dictionary<string, int>? Traits { get; set; }
    [JsonProperty("equipment")] public EquipmentDefinition? Equipment { get; set; }
    [JsonProperty("hireCost")] public int? HireCost { get; set; }
    [JsonProperty("voiceProfile")] public VoiceProfileDefinition? VoiceProfile { get; set; }
    [JsonProperty("likes")] public List<ReactionDefinition>? Likes { get; set; }
    [JsonProperty("dislikes")] public List<ReactionDefinition>? Dislikes { get; set; }
    [JsonProperty("relations")] public List<RelationDefinition>? Relations { get; set; }
}

public sealed class EquipmentDefinition
{
    [JsonProperty("templateId")] public string? TemplateId { get; set; }
    [JsonProperty("fallbackTemplateId")] public string? FallbackTemplateId { get; set; }
}

public sealed class VoiceProfileDefinition
{
    [JsonProperty("reference")] public string? Reference { get; set; }
    [JsonProperty("style")] public string? Style { get; set; }
}

/// <summary>Future likes/dislikes entry: { event, reaction, slot }. Shape-validated only.</summary>
public sealed class ReactionDefinition
{
    [JsonProperty("event")] public string? Event { get; set; }
    [JsonProperty("reaction")] public int? Reaction { get; set; }
    [JsonProperty("slot")] public string? Slot { get; set; }
}

/// <summary>Future relations entry: { companion, attitude }. Shape-validated only.</summary>
public sealed class RelationDefinition
{
    [JsonProperty("companion")] public string? Companion { get; set; }
    [JsonProperty("attitude")] public string? Attitude { get; set; }
}
