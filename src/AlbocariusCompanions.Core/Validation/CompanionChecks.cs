using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;

namespace AlbocariusCompanions.Core.Validation;

/// <summary>companion.json rules (SPEC §2.5, §4.1). Returns issues, never throws.</summary>
public static class CompanionChecks
{
    public static IReadOnlyList<ValidationIssue> Validate(IReadOnlyList<CompanionPackage> packages)
    {
        var issues = new List<ValidationIssue>();
        if (packages == null)
            return issues;

        // Global context: ids are unique across folders, and relation targets resolve against loaded ids.
        var idCounts = packages.GroupBy(p => p.Id)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var knownIds = new HashSet<string>(idCounts.Keys, StringComparer.Ordinal);

        foreach (var package in packages)
            ValidatePackage(package, idCounts, knownIds, issues);

        return issues;
    }

    private static void ValidatePackage(
        CompanionPackage package, Dictionary<string, int> idCounts, HashSet<string> knownIds, List<ValidationIssue> issues)
    {
        var folder = package.FolderName;
        var c = package.Companion;

        ValidateId(package, idCounts, issues);

        RequireString(c.Name, "name", folder, issues);

        if (c.IsFemale == null)
            Missing(issues, folder, "isFemale");

        if (c.Voice == null)
            Missing(issues, folder, "voice");
        else if (!Contains(CompanionRules.VoiceTypes, c.Voice))
            issues.Add(ValidationIssue.Error(IssueCodes.InvalidVoice, folder,
                $"voice '{c.Voice}' is not a vanilla voice type (expected one of: {string.Join(", ", CompanionRules.VoiceTypes)})."));

        if (c.Age == null)
            Missing(issues, folder, "age");
        else if (c.Age < CompanionRules.AgeMin || c.Age > CompanionRules.AgeMax)
            OutOfRange(issues, folder, "age", c.Age.Value, CompanionRules.AgeMin, CompanionRules.AgeMax);

        RequireString(c.Culture, "culture", folder, issues);

        ValidateAttributes(c.Attributes, folder, issues);

        if (c.Skills == null)
        {
            Missing(issues, folder, "skills");
        }
        else
        {
            foreach (var skill in c.Skills)
                if (skill.Value < CompanionRules.SkillMin || skill.Value > CompanionRules.SkillMax)
                    OutOfRange(issues, folder, $"skills['{skill.Key}']", skill.Value, CompanionRules.SkillMin, CompanionRules.SkillMax);
        }

        if (c.Equipment == null)
        {
            Missing(issues, folder, "equipment");
        }
        else if (string.IsNullOrWhiteSpace(c.Equipment.TemplateId))
        {
            Missing(issues, folder, "equipment.templateId");
        }
        else if (!string.IsNullOrWhiteSpace(c.Equipment.FallbackTemplateId) &&
                 string.Equals(c.Equipment.FallbackTemplateId, c.Equipment.TemplateId, StringComparison.Ordinal))
        {
            issues.Add(ValidationIssue.Error(IssueCodes.SameAsFallback, folder,
                $"'equipment.fallbackTemplateId' equals 'templateId' ('{c.Equipment.TemplateId}'); a fallback must differ from the primary id."));
        }

        if (c.HireCost == null)
            Missing(issues, folder, "hireCost");
        else if (c.HireCost < 0)
            issues.Add(ValidationIssue.Error(IssueCodes.OutOfRange, folder,
                $"hireCost value {c.HireCost.Value} is out of range (must be >= 0)."));

        if (!string.IsNullOrWhiteSpace(c.CultureFallback) && c.Culture != null &&
            string.Equals(c.CultureFallback, c.Culture, StringComparison.Ordinal))
        {
            issues.Add(ValidationIssue.Error(IssueCodes.SameAsFallback, folder,
                $"'cultureFallback' equals 'culture' ('{c.Culture}'); a fallback must differ from the primary id."));
        }

        ValidateReactions(c.Likes, "likes", folder, issues);
        ValidateReactions(c.Dislikes, "dislikes", folder, issues);
        ValidateRelations(c.Relations, package.Id, folder, knownIds, issues);
    }

    private static void ValidateId(CompanionPackage package, Dictionary<string, int> idCounts, List<ValidationIssue> issues)
    {
        var folder = package.FolderName;
        var id = package.Companion.Id;

        if (string.IsNullOrWhiteSpace(id))
            Missing(issues, folder, "id");
        else if (!CompanionRules.IdPattern.IsMatch(id))
            issues.Add(ValidationIssue.Error(IssueCodes.InvalidId, folder,
                $"id '{id}' does not match the pattern {CompanionRules.IdPattern} (lowercase [a-z0-9_]+)."));
        else if (!string.Equals(id, folder, StringComparison.Ordinal))
            issues.Add(ValidationIssue.Error(IssueCodes.IdFolderMismatch, folder,
                $"id '{id}' does not equal the folder name '{folder}'."));

        if (idCounts.TryGetValue(package.Id, out var count) && count > 1)
            issues.Add(ValidationIssue.Error(IssueCodes.DuplicateId, folder,
                $"id '{package.Id}' is used by {count} companion folders."));
    }

    private static void ValidateAttributes(Dictionary<string, int>? attributes, string folder, List<ValidationIssue> issues)
    {
        if (attributes == null)
        {
            Missing(issues, folder, "attributes");
            return;
        }

        foreach (var name in CompanionRules.AttributeNames)
            if (!attributes.ContainsKey(name))
                Missing(issues, folder, $"attributes.{name}");

        foreach (var attribute in attributes)
        {
            if (!Contains(CompanionRules.AttributeNames, attribute.Key))
                issues.Add(ValidationIssue.Error(IssueCodes.UnknownAttribute, folder,
                    $"Unknown attribute '{attribute.Key}' in 'attributes' (known: {string.Join(", ", CompanionRules.AttributeNames)})."));
            else if (attribute.Value < CompanionRules.AttributeMin || attribute.Value > CompanionRules.AttributeMax)
                OutOfRange(issues, folder, $"attributes['{attribute.Key}']", attribute.Value, CompanionRules.AttributeMin, CompanionRules.AttributeMax);
        }
    }

    private static void ValidateReactions(List<ReactionDefinition>? reactions, string field, string folder, List<ValidationIssue> issues)
    {
        if (reactions == null)
            return;

        foreach (var reaction in reactions)
        {
            if (reaction == null)
            {
                issues.Add(ValidationIssue.Error(IssueCodes.InvalidReaction, folder, $"{field} contains a null entry."));
                continue;
            }

            var problems = new List<string>();
            if (string.IsNullOrWhiteSpace(reaction.Event))
                problems.Add($"missing 'event' in '{field}' entry");
            if (reaction.Reaction == null)
                problems.Add($"missing 'reaction' in '{field}' entry");
            if (!DialogSlots.IsKnown(reaction.Slot))
                problems.Add($"'slot' {(reaction.Slot == null ? "is missing" : $"'{reaction.Slot}' is not a known dialog slot")} in '{field}' entry");

            if (problems.Count > 0)
                issues.Add(ValidationIssue.Error(IssueCodes.InvalidReaction, folder, string.Join(" ", problems) + "."));
        }
    }

    private static void ValidateRelations(
        List<RelationDefinition>? relations, string ownId, string folder, HashSet<string> knownIds, List<ValidationIssue> issues)
    {
        if (relations == null)
            return;

        foreach (var relation in relations)
        {
            if (relation == null)
            {
                issues.Add(ValidationIssue.Error(IssueCodes.InvalidRelation, folder, "relations contains a null entry."));
                continue;
            }

            var target = relation.Companion;
            if (string.IsNullOrWhiteSpace(target))
            {
                issues.Add(ValidationIssue.Error(IssueCodes.InvalidRelation, folder, "relations entry is missing 'companion'."));
                continue;
            }

            if (relation.Attitude == null || !Contains(CompanionRules.RelationAttitudes, relation.Attitude))
            {
                issues.Add(ValidationIssue.Error(IssueCodes.InvalidRelation, folder,
                    $"relations entry for '{target}' has attitude {(relation.Attitude == null ? "missing" : $"'{relation.Attitude}'")}; expected one of: {string.Join(", ", CompanionRules.RelationAttitudes)}."));
                continue;
            }

            if (string.Equals(target, ownId, StringComparison.Ordinal))
            {
                issues.Add(ValidationIssue.Error(IssueCodes.InvalidRelation, folder,
                    $"relations entry targets the companion itself ('{target}')."));
                continue;
            }

            if (!knownIds.Contains(target!))
                issues.Add(ValidationIssue.Warning(IssueCodes.UnknownRelationTarget, folder,
                    $"relations entry targets unknown companion '{target}' (warning: the companion may be added later)."));
        }
    }

    private static void RequireString(string? value, string field, string folder, List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
            Missing(issues, folder, field);
    }

    private static void Missing(List<ValidationIssue> issues, string folder, string field) =>
        issues.Add(ValidationIssue.Error(IssueCodes.MissingField, folder, $"companion.json is missing required field '{field}'."));

    private static void OutOfRange(List<ValidationIssue> issues, string folder, string field, int value, int min, int max) =>
        issues.Add(ValidationIssue.Error(IssueCodes.OutOfRange, folder,
            $"{field} value {value} is out of range ({min}..{max})."));

    private static bool Contains(IReadOnlyList<string> values, string value) => values.Contains(value, StringComparer.Ordinal);
}
