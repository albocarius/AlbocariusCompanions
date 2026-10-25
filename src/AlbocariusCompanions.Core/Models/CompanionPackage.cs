using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Rules;

namespace AlbocariusCompanions.Core.Models;

/// <summary>One parsed companion folder: ModuleData/Companions/&lt;FolderName&gt;/. The folder name is the companion id.</summary>
public sealed class CompanionPackage
{
    public CompanionPackage(string folderName, string folderPath, CompanionDefinition companion, DialogFile dialogs,
        IReadOnlyDictionary<string, LangFile>? languages = null)
    {
        FolderName = folderName;
        FolderPath = folderPath;
        Companion = companion;
        Dialogs = dialogs;
        Languages = languages ?? LangText.Empty;

        foreach (var line in dialogs?.Lines ?? new List<DialogLine>())
            if (line != null)
                line.FullId = string.IsNullOrEmpty(line.Id) ? null : LineIds.Full(folderName, line.Id!);
        foreach (var node in Conversations.SelectMany(c => c.Nodes ?? new List<ConversationNode>()))
            if (node != null)
                node.FullId = string.IsNullOrEmpty(node.Id) ? null : LineIds.Full(folderName, node.Id!);
    }

    public string FolderName { get; }
    public string FolderPath { get; }
    public CompanionDefinition Companion { get; }
    public DialogFile Dialogs { get; }

    /// <summary>Language code (lang file name without .json) → file.</summary>
    public IReadOnlyDictionary<string, LangFile> Languages { get; }

    /// <summary>The authored conversations (empty when dialogs.json has none); null entries skipped.</summary>
    public IReadOnlyList<ConversationDefinition> Conversations =>
        (Dialogs?.Conversations ?? new List<ConversationDefinition>()).Where(c => c != null).ToList();

    /// <summary>Every text-bearing entry: slot lines, then conversation nodes in file order (null entries skipped).</summary>
    public IEnumerable<ISpokenLine> SpokenLines =>
        (Dialogs?.Lines ?? new List<DialogLine>()).Where(l => l != null).Cast<ISpokenLine>()
            .Concat(Conversations.SelectMany(c => c.Nodes ?? new List<ConversationNode>()).Where(n => n != null));

    /// <summary>The companion id: the folder name.</summary>
    public string Id => FolderName;

    /// <summary>Text of a line (local id) in a language, falling back to English; null when there is none.</summary>
    public string? Text(string localId, string languageCode) => LangText.Find(Languages, localId, languageCode);
}
