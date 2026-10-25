using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Loading;
using AlbocariusCompanions.Core.Validation;

namespace AlbocariusCompanions.Core.Registry;

/// <summary>Load + validate + build registry. Folders with any error are skipped; the rest still load.</summary>
public static class CompanionCatalog
{
    /// <param name="isModuleActive">
    /// When given, a valid companion with a required module that is not active is left out too (listed in
    /// <see cref="CatalogResult.ModuleSkips"/>), before other companions' references to it are checked.
    /// </param>
    public static CatalogResult Load(string companionsRoot, Func<string, bool>? isModuleActive = null)
    {
        var loaded = CompanionLoader.LoadAll(companionsRoot);
        var validationIssues = CompanionValidator.Validate(loaded.Packages);

        // Issues are keyed by folder, so a broken folder can never take down the real owner of an id.
        var errorFolders = new HashSet<string>(
            validationIssues.Where(i => i.IsError).Select(i => i.Folder), StringComparer.Ordinal);
        var kept = loaded.Packages.Where(p => !errorFolders.Contains(p.FolderName)).ToList();

        var moduleSkips = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        if (isModuleActive != null)
            foreach (var package in kept)
            {
                var missing = RequiredModules.Missing(package, isModuleActive);
                if (missing.Count > 0)
                    moduleSkips[package.Id] = missing;
            }
        kept = kept.Where(p => !moduleSkips.ContainsKey(p.Id)).ToList();

        // References to other companions' variables depend on which companions are kept. Skipping one can only turn
        // errors about it into "not loaded" warnings, so repeat until no new folder is skipped (M7 D2).
        List<ValidationIssue> crossIssues;
        while (true)
        {
            crossIssues = VariableChecks.CrossCompanion(kept);
            var skipped = new HashSet<string>(crossIssues.Where(i => i.IsError).Select(i => i.Folder), StringComparer.Ordinal);
            if (skipped.Count == 0)
                break;
            kept = kept.Where(p => !skipped.Contains(p.FolderName)).ToList();
            validationIssues = validationIssues.Concat(crossIssues.Where(i => i.IsError)).ToList();
        }

        var issues = loaded.Issues.Concat(validationIssues).Concat(crossIssues).ToList();
        return new CatalogResult(new CompanionRegistry(kept), issues, moduleSkips);
    }
}

public sealed class CatalogResult
{
    public CatalogResult(CompanionRegistry registry, IReadOnlyList<Validation.ValidationIssue> issues,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? moduleSkips = null)
    {
        Registry = registry;
        Issues = issues;
        ModuleSkips = moduleSkips ?? new Dictionary<string, IReadOnlyList<string>>();
    }

    public CompanionRegistry Registry { get; }

    /// <summary>Loader issues followed by validation issues.</summary>
    public IReadOnlyList<Validation.ValidationIssue> Issues { get; }

    /// <summary>Valid companions left out because a required module is not active: id → missing module ids.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ModuleSkips { get; }
}
