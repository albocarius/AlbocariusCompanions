using System;
using System.Collections.Generic;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Templates;
using AlbocariusCompanions.Core.Validation;

namespace AlbocariusCompanions.Core.Resolution;

/// <summary>SPEC rule 9: resolve every game id of a validated companion against the live game; fallbacks, warnings, never throws.</summary>
public static class CompanionResolver
{
    /// <summary>Suffix vanilla uses for the civilian twin of a wanderer roster (GAME_MAP Q4/Q5).</summary>
    public const string CivilianRosterSuffix = "_civilian";

    public static ResolutionResult Resolve(CompanionPackage package, IGameObjectLookup game) =>
        throw new NotImplementedException("task 25-core-resolver");
}

public sealed class ResolutionResult
{
    public ResolutionResult(ResolvedCompanion? companion, IReadOnlyList<ValidationIssue> issues)
    {
        Companion = companion;
        Issues = issues;
    }

    /// <summary>Null when a required value and its fallback are both missing: skip this companion only.</summary>
    public ResolvedCompanion? Companion { get; }
    public IReadOnlyList<ValidationIssue> Issues { get; }
}
