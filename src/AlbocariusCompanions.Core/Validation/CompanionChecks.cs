using System;
using System.Collections.Generic;
using AlbocariusCompanions.Core.Models;

namespace AlbocariusCompanions.Core.Validation;

/// <summary>companion.json rules (SPEC §2.5). Returns issues, never throws.</summary>
public static class CompanionChecks
{
    public static IReadOnlyList<ValidationIssue> Validate(IReadOnlyList<CompanionPackage> packages) =>
        throw new NotImplementedException("task 18-core-validation-companion");
}
