using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Core.Models;

/// <summary>One entry of dialogs.json "variables" (M7): a permanent per-campaign value owned by this companion.</summary>
public sealed class VariableDeclaration
{
    /// <summary>"bool" or "int".</summary>
    [JsonProperty("type")] public string? Type { get; set; }
    /// <summary>"local" (default: only this companion) or "shared" (others use it as &lt;companionId&gt;.&lt;name&gt;).</summary>
    [JsonProperty("scope")] public string? Scope { get; set; }
    /// <summary>Initial value: a JSON bool for bool (default false), an integer for int (default 0).</summary>
    [JsonProperty("default")] public JToken? Default { get; set; }
}
