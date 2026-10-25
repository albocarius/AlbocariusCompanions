using System;
using System.Collections.Generic;
using System.Linq;

namespace AlbocariusCompanions.Core.Dialogs;

/// <summary>
/// "Once" markers of one campaign, scoped per companion, saved as a sorted list of keys
/// ("&lt;companion&gt;#started:&lt;conversation&gt;"). M6 also kept flags here ("&lt;companion&gt;:&lt;flag&gt;"); since M7
/// flags are bool variables (<see cref="VariableStore"/>) and old keys are moved there with <see cref="TakeLegacyFlags"/>.
/// </summary>
public sealed class ConversationFlags
{
    private readonly HashSet<string> _keys = new HashSet<string>(StringComparer.Ordinal);

    public bool HasStarted(string companionId, string conversationId) => _keys.Contains(StartedKey(companionId, conversationId));

    public void MarkStarted(string companionId, string conversationId) => _keys.Add(StartedKey(companionId, conversationId));

    /// <summary>The keys for the save, sorted (ordinal).</summary>
    public List<string> ToList() => _keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

    /// <summary>Replaces the content with saved keys; null (no saved data) empties it.</summary>
    public void Load(IEnumerable<string>? keys)
    {
        _keys.Clear();
        foreach (var key in keys ?? Enumerable.Empty<string>())
            if (!string.IsNullOrEmpty(key))
                _keys.Add(key);
    }

    /// <summary>Removes and returns the M6 flag keys ("&lt;companion&gt;:&lt;flag&gt;") found in a loaded save.</summary>
    public List<string> TakeLegacyFlags()
    {
        var legacy = _keys.Where(k => !k.Contains("#")).OrderBy(k => k, StringComparer.Ordinal).ToList();
        foreach (var key in legacy)
            _keys.Remove(key);
        return legacy;
    }

    public int Count => _keys.Count;

    private static string StartedKey(string companionId, string conversationId) => companionId + "#started:" + conversationId;
}
