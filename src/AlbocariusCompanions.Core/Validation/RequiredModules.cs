using System;
using System.Collections.Generic;
using AlbocariusCompanions.Core.Models;

namespace AlbocariusCompanions.Core.Validation;

/// <summary>
/// <c>companion.json requiredModules</c> (SPEC_CORE_GAPS T2.3): the companion is skipped when one of the listed
/// modules is not loaded. Core only filters the declared ids; the Game layer supplies the predicate
/// (ModuleHelper.IsModuleActive, GAME_MAP Q13).
/// </summary>
public static class RequiredModules
{
    /// <summary>
    /// The declared module ids for which <paramref name="isModuleActive"/> is false, in declared order.
    /// Empty when the companion requires no module; blank entries are skipped (they are an
    /// <see cref="IssueCodes.InvalidRequiredModule"/> validation error, never a module to look for).
    /// </summary>
    public static IReadOnlyList<string> Missing(CompanionPackage package, Func<string, bool> isModuleActive)
    {
        var declared = package?.Companion?.RequiredModules;
        var missing = new List<string>();
        if (declared == null || declared.Count == 0 || isModuleActive == null)
            return missing;

        foreach (var id in declared)
        {
            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (!isModuleActive(id))
                missing.Add(id);
        }

        return missing;
    }
}
