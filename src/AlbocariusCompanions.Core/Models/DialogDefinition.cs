using System.Collections.Generic;
using Newtonsoft.Json;

namespace AlbocariusCompanions.Core.Models;

/// <summary>dialogs.json (SPEC §4.2). Texts live in the lang files (<see cref="LangFile"/>).</summary>
public sealed class DialogFile
{
    [JsonProperty("schemaVersion")] public int? SchemaVersion { get; set; }
    [JsonProperty("lines")] public List<DialogLine>? Lines { get; set; }
}

public sealed class DialogLine
{
    /// <summary>Local id, unique within the companion (e.g. intro_01); key of the line in the lang files.</summary>
    [JsonProperty("id")] public string? Id { get; set; }
    [JsonProperty("slot")] public string? Slot { get; set; }
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

/// <summary>_shared/player_lines.json: the player's side of the companion dialogs.</summary>
public sealed class PlayerLinesFile
{
    [JsonProperty("schemaVersion")] public int? SchemaVersion { get; set; }
    [JsonProperty("lines")] public List<PlayerLine>? Lines { get; set; }
}

public sealed class PlayerLine
{
    /// <summary>Used as-is (no companion prefix) as localization key; must not collide with a companion line's full id.</summary>
    [JsonProperty("id")] public string? Id { get; set; }
    [JsonProperty("slot")] public string? Slot { get; set; }
}
