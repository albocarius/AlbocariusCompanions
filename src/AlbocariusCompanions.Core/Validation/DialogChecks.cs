using System;
using System.Collections.Generic;
using AlbocariusCompanions.Core.Models;

namespace AlbocariusCompanions.Core.Validation;

/// <summary>dialogs.json rules (SPEC §2.5). Returns issues, never throws.</summary>
public static class DialogChecks
{
    public static IReadOnlyList<ValidationIssue> Validate(IReadOnlyList<CompanionPackage> packages) =>
        throw new NotImplementedException("task 19-core-validation-dialogs");
}
