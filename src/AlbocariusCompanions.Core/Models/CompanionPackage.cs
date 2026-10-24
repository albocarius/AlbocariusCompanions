using System.Collections.Generic;
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
    }

    public string FolderName { get; }
    public string FolderPath { get; }
    public CompanionDefinition Companion { get; }
    public DialogFile Dialogs { get; }

    /// <summary>Language code (lang file name without .json) → file.</summary>
    public IReadOnlyDictionary<string, LangFile> Languages { get; }

    /// <summary>The companion id: the folder name.</summary>
    public string Id => FolderName;

    /// <summary>Text of a line (local id) in a language, falling back to English; null when there is none.</summary>
    public string? Text(string localId, string languageCode) => LangText.Find(Languages, localId, languageCode);
}
