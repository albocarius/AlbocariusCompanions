using System.Collections.Generic;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Validation;

namespace AlbocariusCompanions.Core.Loading;

public sealed class LoadResult
{
    public LoadResult(IReadOnlyList<CompanionPackage> packages, IReadOnlyList<ValidationIssue> issues)
    {
        Packages = packages;
        Issues = issues;
    }

    /// <summary>Folders that parsed successfully, ordered by folder name (ordinal).</summary>
    public IReadOnlyList<CompanionPackage> Packages { get; }

    /// <summary>Loader errors (missing files, invalid JSON, missing root). Validation is separate.</summary>
    public IReadOnlyList<ValidationIssue> Issues { get; }
}
