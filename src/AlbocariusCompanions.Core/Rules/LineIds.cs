using System.Globalization;
using System.Text.RegularExpressions;

namespace AlbocariusCompanions.Core.Rules;

/// <summary>Line id rule: &lt;companionId&gt;_&lt;slot&gt;_&lt;nn&gt;, nn = two digits (e.g. test_companion_intro_01).</summary>
public static class LineIds
{
    public static string Format(string companionId, string slot, int number) =>
        companionId + "_" + slot + "_" + number.ToString("00", CultureInfo.InvariantCulture);

    public static bool IsValid(string? lineId, string companionId, string slot) =>
        lineId != null && Regex.IsMatch(lineId, "^" + Regex.Escape(companionId + "_" + slot + "_") + "[0-9]{2}$");

    /// <summary>Default audio path relative to the companion folder.</summary>
    public static string DefaultAudioPath(string lineId) => "audio/" + lineId + ".ogg";
}
