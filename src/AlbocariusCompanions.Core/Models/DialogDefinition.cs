using System.Collections.Generic;
using Newtonsoft.Json;

namespace AlbocariusCompanions.Core.Models;

/// <summary>dialogs.json (SPEC §4.2).</summary>
public sealed class DialogFile
{
    [JsonProperty("lines")] public List<DialogLine>? Lines { get; set; }
}

public sealed class DialogLine
{
    /// <summary>Also the localization key and voice key: &lt;companionId&gt;_&lt;slot&gt;_&lt;nn&gt;.</summary>
    [JsonProperty("id")] public string? Id { get; set; }
    [JsonProperty("slot")] public string? Slot { get; set; }
    /// <summary>Language code → text; "en" is required and is the fallback.</summary>
    [JsonProperty("text")] public Dictionary<string, string>? Text { get; set; }
    [JsonProperty("voiced")] public bool Voiced { get; set; }
    /// <summary>Path relative to the companion folder; default audio/&lt;line id&gt;.ogg.</summary>
    [JsonProperty("audio")] public string? Audio { get; set; }
}
