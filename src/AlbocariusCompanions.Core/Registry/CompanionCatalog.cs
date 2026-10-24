using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Loading;
using AlbocariusCompanions.Core.Validation;

namespace AlbocariusCompanions.Core.Registry;

/// <summary>
/// Load + validate + build registry. Folders with any error are skipped; the rest still load. The _shared player lines
/// are kept only when they have no error (the game then registers no companion dialogs); companions never depend on them.
/// </summary>
public static class CompanionCatalog
{
    public static CatalogResult Load(string companionsRoot)
    {
        var loaded = CompanionLoader.LoadAll(companionsRoot);
        var sharedIssues = new List<ValidationIssue>();
        var shared = CompanionLoader.LoadShared(companionsRoot, sharedIssues);
        var validationIssues = CompanionValidator.Validate(loaded.Packages);
        if (shared != null)
        {
            sharedIssues.AddRange(SharedChecks.Validate(shared, loaded.Packages));
            if (sharedIssues.Any(i => i.IsError))
                shared = null;
        }

        // Issues are keyed by folder, so a broken folder can never take down the real owner of an id.
        var errorFolders = new HashSet<string>(
            validationIssues.Where(i => i.IsError).Select(i => i.Folder), StringComparer.Ordinal);

        var kept = loaded.Packages.Where(p => !errorFolders.Contains(p.FolderName)).ToList();

        var issues = loaded.Issues.Concat(validationIssues).Concat(sharedIssues).ToList();
        return new CatalogResult(new CompanionRegistry(kept, shared), issues);
    }
}

public sealed class CatalogResult
{
    public CatalogResult(CompanionRegistry registry, IReadOnlyList<Validation.ValidationIssue> issues)
    {
        Registry = registry;
        Issues = issues;
    }

    public CompanionRegistry Registry { get; }

    /// <summary>Loader issues followed by validation issues.</summary>
    public IReadOnlyList<Validation.ValidationIssue> Issues { get; }
}
