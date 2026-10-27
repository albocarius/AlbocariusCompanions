using System.Collections.Generic;
using Newtonsoft.Json;

namespace AlbocariusCompanions.Core.Models;

/// <summary>dialogs.json (SPEC §4.2). Texts live in the lang files (<see cref="LangFile"/>).</summary>
public sealed class DialogFile
{
    [JsonProperty("schemaVersion")] public int? SchemaVersion { get; set; }
    /// <summary>Slot lines: the companion's and the player's (player_* slots) side of the mod's fixed dialogs.</summary>
    [JsonProperty("lines")] public List<DialogLine>? Lines { get; set; }
    /// <summary>Authored companion ↔ player conversations (optional).</summary>
    [JsonProperty("conversations")] public List<ConversationDefinition>? Conversations { get; set; }
    /// <summary>Declared conversation variables, by name (M7). The built-in int "mood" is never declared.</summary>
    [JsonProperty("variables")] public Dictionary<string, VariableDeclaration>? Variables { get; set; }
}

/// <summary>Anything with a text, an id and maybe a recorded voice: a slot line or a conversation node.</summary>
public interface ISpokenLine
{
    /// <summary>Local id, unique within the companion; key of the text in the lang files.</summary>
    string? Id { get; }
    /// <summary>&lt;companionId&gt;_&lt;local id&gt;, derived when the package is built.</summary>
    string? FullId { get; }
    bool Voiced { get; }
    /// <summary>Path relative to the companion folder; default audio/&lt;local id&gt;.ogg.</summary>
    string? Audio { get; }
}

public sealed class DialogLine : ISpokenLine
{
    /// <summary>Local id, unique within the companion (e.g. intro_01); key of the line in the lang files.</summary>
    [JsonProperty("id")] public string? Id { get; set; }
    /// <summary>The fixed dialog the line belongs to; absent for a triggered line (<see cref="Trigger"/>).</summary>
    [JsonProperty("slot")] public string? Slot { get; set; }
    /// <summary>A line shown when a game event fires (M11): no slot, delivered as a notification.</summary>
    [JsonProperty("trigger")] public TriggerSpec? Trigger { get; set; }
    /// <summary>The line is a candidate only while this holds (same conditions as conversations).</summary>
    [JsonProperty("when")] public ConditionSet? When { get; set; }
    /// <summary>Among the slot's passing lines the highest priority wins, ties at random. Absent = 0.</summary>
    [JsonProperty("priority")] public int? Priority { get; set; }
    [JsonProperty("voiced")] public bool Voiced { get; set; }
    /// <summary>Path relative to the companion folder; default audio/&lt;local id&gt;.ogg.</summary>
    [JsonProperty("audio")] public string? Audio { get; set; }

    /// <summary>
    /// &lt;companionId&gt;_&lt;local id&gt;: the localization key, voice-over id and log name of the line. Derived when the
    /// package is built, never read from JSON.
    /// </summary>
    [JsonIgnore] public string? FullId { get; set; }
}

/// <summary>lang/&lt;code&gt;.json: local line id → text in one language.</summary>
public sealed class LangFile
{
    [JsonProperty("schemaVersion")] public int? SchemaVersion { get; set; }
    [JsonProperty("lines")] public Dictionary<string, string>? Lines { get; set; }
}
