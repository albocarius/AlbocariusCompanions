using System;
using System.Collections.Generic;
using System.Linq;

namespace AlbocariusCompanions.Core.Dialogs;

/// <summary>
/// Conversation flags and "once" markers of one campaign, scoped per companion. Saved as a sorted list of keys
/// ("&lt;companion&gt;:&lt;flag&gt;", "&lt;companion&gt;#started:&lt;conversation&gt;"); ids never contain ':' or '#', so
/// keys cannot collide.
/// </summary>
public sealed class ConversationFlags
{
    private readonly HashSet<string> _keys = new HashSet<string>(StringComparer.Ordinal);

    public bool Has(string companionId, string flag) => _keys.Contains(FlagKey(companionId, flag));

    public void Set(string companionId, string flag) => _keys.Add(FlagKey(companionId, flag));

    public void Clear(string companionId, string flag) => _keys.Remove(FlagKey(companionId, flag));

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

    public int Count => _keys.Count;

    private static string FlagKey(string companionId, string flag) => companionId + ":" + flag;

    private static string StartedKey(string companionId, string conversationId) => companionId + "#started:" + conversationId;
}
