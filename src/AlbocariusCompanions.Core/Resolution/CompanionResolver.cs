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

    public static ResolutionResult Resolve(CompanionPackage package, IGameObjectLookup game)
    {
        var issues = new List<ValidationIssue>();
        var folder = package.FolderName;
        var definition = package.Companion;

        var cultureId = ResolveCulture(definition.Culture, definition.CultureFallback, game, folder, issues);
        if (cultureId == null)
            return new ResolutionResult(null, issues);

        var rosterId = ResolveEquipmentRoster(definition.Equipment, game, folder, issues);
        if (rosterId == null)
            return new ResolutionResult(null, issues);

        var skills = ResolveLevels(definition.Skills, game.SkillExists, IssueCodes.SkillMissing, "skills", folder, issues);
        var traits = ResolveLevels(definition.Traits, game.TraitExists, IssueCodes.TraitMissing, "traits", folder, issues);

        var battleItems = ResolveFixedSet(definition.Equipment?.Battle, "equipment.battle", game, folder, issues);
        var civilianItems = ResolveFixedSet(definition.Equipment?.Civilian, "equipment.civilian", game, folder, issues);

        var civilianTwin = rosterId + CivilianRosterSuffix;
        var companion = new ResolvedCompanion(
            package.Id, definition.Name ?? package.Id, definition.IsFemale ?? false, definition.Voice ?? string.Empty,
            definition.Age ?? 0, cultureId, skills, traits, rosterId,
            game.EquipmentRosterExists(civilianTwin) ? civilianTwin : null,
            definition.BodyProperties, battleItems, civilianItems);

        return new ResolutionResult(companion, issues);
    }

    /// <summary>Missing culture → cultureFallback (warning); both missing → error, null skips the companion.</summary>
    private static string? ResolveCulture(string? culture, string? fallback, IGameObjectLookup game, string folder, List<ValidationIssue> issues)
    {
        if (culture != null && game.CultureExists(culture))
            return culture;

        var missing = culture ?? "(missing)";
        if (!string.IsNullOrEmpty(fallback) && game.CultureExists(fallback!))
        {
            issues.Add(ValidationIssue.Warning(IssueCodes.CultureFallbackUsed, folder,
                $"culture '{missing}' is not available in the running game; using cultureFallback '{fallback}'."));
            return fallback;
        }

        issues.Add(ValidationIssue.Error(IssueCodes.CultureMissing, folder,
            $"culture '{missing}' is not available in the running game" +
            (string.IsNullOrEmpty(fallback) ? " and no cultureFallback is set; companion skipped." : $" and cultureFallback '{fallback}' is missing too; companion skipped.")));
        return null;
    }

    /// <summary>Missing equipment template → fallbackTemplateId (warning); both missing → error, null skips the companion.</summary>
    private static string? ResolveEquipmentRoster(EquipmentDefinition? equipment, IGameObjectLookup game, string folder, List<ValidationIssue> issues)
    {
        var templateId = equipment?.TemplateId;
        if (templateId != null && game.EquipmentRosterExists(templateId))
            return templateId;

        var missing = templateId ?? "(missing)";
        var fallback = equipment?.FallbackTemplateId;
        if (!string.IsNullOrEmpty(fallback) && game.EquipmentRosterExists(fallback!))
        {
            issues.Add(ValidationIssue.Warning(IssueCodes.EquipmentFallbackUsed, folder,
                $"equipment template '{missing}' is not available in the running game; using fallbackTemplateId '{fallback}'."));
            return fallback;
        }

        issues.Add(ValidationIssue.Error(IssueCodes.EquipmentMissing, folder,
            $"equipment template '{missing}' is not available in the running game" +
            (string.IsNullOrEmpty(fallback) ? " and no equipment.fallbackTemplateId is set; companion skipped." : $" and fallbackTemplateId '{fallback}' is missing too; companion skipped.")));
        return null;
    }

    /// <summary>Keeps game-known ids, drops the rest with one warning each (SPEC rule 9: missing skill/trait is never fatal).</summary>
    private static IReadOnlyDictionary<string, int> ResolveLevels(
        Dictionary<string, int>? source, Func<string, bool> exists, string issueCode, string field, string folder, List<ValidationIssue> issues)
    {
        var kept = new Dictionary<string, int>();
        if (source == null)
            return kept;

        foreach (var entry in source)
        {
            if (exists(entry.Key))
                kept[entry.Key] = entry.Value;
            else
                issues.Add(ValidationIssue.Warning(issueCode, folder,
                    $"{field} entry '{entry.Key}' is not available in the running game; entry dropped."));
        }

        return kept;
    }

    /// <summary>One fixed equipment set: missing or misfitting items are dropped with warnings; nothing left → null so the roster import is used.</summary>
    private static IReadOnlyDictionary<string, string>? ResolveFixedSet(
        Dictionary<string, string>? set, string field, IGameObjectLookup game, string folder, List<ValidationIssue> issues)
    {
        if (set == null)
            return null;

        var kept = new Dictionary<string, string>();
        foreach (var entry in set)
        {
            var itemId = entry.Value;
            if (itemId == null || !game.ItemExists(itemId))
                issues.Add(ValidationIssue.Warning(IssueCodes.ItemMissing, folder,
                    $"{field} item '{itemId}' does not exist in the running game; dropped from slot '{entry.Key}'."));
            else if (!game.ItemFitsSlot(entry.Key, itemId))
                issues.Add(ValidationIssue.Warning(IssueCodes.ItemDoesNotFitSlot, folder,
                    $"{field} item '{itemId}' does not fit slot '{entry.Key}'; dropped."));
            else
                kept[entry.Key] = itemId;
        }

        if (kept.Count == 0)
        {
            issues.Add(ValidationIssue.Warning(IssueCodes.FixedSetEmpty, folder,
                $"'{field}' has no usable item left in the running game; falling back to the equipment roster import."));
            return null;
        }

        return kept;
    }
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
