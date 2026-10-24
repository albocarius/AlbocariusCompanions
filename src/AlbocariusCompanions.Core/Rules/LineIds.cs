using System.Text.RegularExpressions;

namespace AlbocariusCompanions.Core.Rules;

/// <summary>
/// Line ids: dialogs.json uses a local id ([a-z0-9_]+, e.g. intro_01); everything outside the JSON uses the full id
/// &lt;companionId&gt;_&lt;local id&gt; (e.g. test_companion_intro_01). Player lines use their id as-is.
/// </summary>
public static class LineIds
{
    public static readonly Regex LocalIdPattern = new Regex("^[a-z0-9_]+$");

    public static bool IsValidLocal(string? lineId) => lineId != null && LocalIdPattern.IsMatch(lineId);

    public static string Full(string companionId, string localId) => companionId + "_" + localId;

    /// <summary>Default audio path relative to the companion folder: audio/&lt;local id&gt;.ogg.</summary>
    public static string DefaultAudioPath(string localId) => "audio/" + localId + ".ogg";
}
