namespace AlbocariusCompanions.Core.Models;

/// <summary>One parsed companion folder: ModuleData/Companions/&lt;FolderName&gt;/.</summary>
public sealed class CompanionPackage
{
    public CompanionPackage(string folderName, string folderPath, CompanionDefinition companion, DialogFile dialogs)
    {
        FolderName = folderName;
        FolderPath = folderPath;
        Companion = companion;
        Dialogs = dialogs;
    }

    public string FolderName { get; }
    public string FolderPath { get; }
    public CompanionDefinition Companion { get; }
    public DialogFile Dialogs { get; }

    /// <summary>The companion id, falling back to the folder name when the id is missing.</summary>
    public string Id => string.IsNullOrEmpty(Companion.Id) ? FolderName : Companion.Id!;
}
