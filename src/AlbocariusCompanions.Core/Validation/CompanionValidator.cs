using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Models;

namespace AlbocariusCompanions.Core.Validation;

/// <summary>All validation rules over the full set of loaded packages (uniqueness is global).</summary>
public static class CompanionValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(IReadOnlyList<CompanionPackage> packages) =>
        CompanionChecks.Validate(packages).Concat(DialogChecks.Validate(packages)).ToList();
}
