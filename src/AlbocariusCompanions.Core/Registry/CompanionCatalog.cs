using System;
using System.Collections.Generic;

namespace AlbocariusCompanions.Core.Registry;

/// <summary>Load + validate + build registry. Folders with any error are skipped; the rest still load.</summary>
public static class CompanionCatalog
{
    public static CatalogResult Load(string companionsRoot) => throw new NotImplementedException("task 17-core-registry");
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
