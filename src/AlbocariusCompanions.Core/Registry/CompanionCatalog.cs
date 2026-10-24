using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Loading;
using AlbocariusCompanions.Core.Validation;

namespace AlbocariusCompanions.Core.Registry;

/// <summary>Load + validate + build registry. Folders with any error are skipped; the rest still load.</summary>
public static class CompanionCatalog
{
    public static CatalogResult Load(string companionsRoot)
    {
        var loaded = CompanionLoader.LoadAll(companionsRoot);
        var validationIssues = CompanionValidator.Validate(loaded.Packages);

        // Issues are keyed by folder, so a broken folder can never take down the real owner of an id.
        var errorFolders = new HashSet<string>(
            validationIssues.Where(i => i.IsError).Select(i => i.Folder), StringComparer.Ordinal);

        var kept = loaded.Packages.Where(p => !errorFolders.Contains(p.FolderName)).ToList();

        var issues = loaded.Issues.Concat(validationIssues).ToList();
        return new CatalogResult(new CompanionRegistry(kept), issues);
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
