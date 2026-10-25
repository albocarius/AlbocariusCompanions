using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;

namespace AlbocariusCompanions.Core.Validation;

/// <summary>
/// dialogs.json "variables" (M7) and every variable use in conditions and effects. References to the companion's own
/// variables are checked per folder (<see cref="Validate"/>); references to another companion's ("companion.name") need
/// the whole loaded set and are checked by <see cref="CrossCompanion"/>.
/// </summary>
public static class VariableChecks
{
    public static void Validate(CompanionPackage package, List<ValidationIssue> issues)
    {
        var folder = package.FolderName;
        foreach (var pair in package.Dialogs?.Variables ?? new Dictionary<string, VariableDeclaration>())
            ValidateDeclaration(folder, pair.Key, pair.Value, issues);

        foreach (var (use, where) in Uses(package))
        {
            if (!VariableRules.TryParseReference(package.Id, use.Reference, out var reference))
            {
                issues.Add(ValidationIssue.Error(use.IsWrite ? IssueCodes.InvalidEffect : IssueCodes.InvalidCondition, folder,
                    $"{where}: '{use.Reference}' in '{use.Key}' is not a variable; use 'name' or 'companion.name' ({VariableRules.NamePattern})."));
                continue;
            }
            if (use.Type == null)
            {
                issues.Add(ValidationIssue.Error(IssueCodes.VariableTypeMismatch, folder,
                    $"{where}: the value for '{use.Reference}' in '{use.Key}' must be true/false (bool) or a whole number (int)."));
                continue;
            }
            if (reference.Owner != package.Id)
                continue;
            var type = OwnType(package, reference.Name);
            if (type == null)
                issues.Add(ValidationIssue.Error(IssueCodes.UnknownVariable, folder,
                    $"{where}: variable '{reference.Name}' is not declared; add it to \"variables\" in dialogs.json."));
            else if (type != use.Type)
                issues.Add(Mismatch(folder, where, use, type.Value));
        }
    }

    /// <summary>
    /// References to other companions' variables, over the companions that are loaded. Owner loaded but the variable is
    /// undeclared, local or of another type → error; owner not loaded → warning (at runtime the condition is false and the
    /// write is skipped).
    /// </summary>
    public static List<ValidationIssue> CrossCompanion(IReadOnlyList<CompanionPackage> loaded)
    {
        var issues = new List<ValidationIssue>();
        var byId = loaded.GroupBy(p => p.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (var package in loaded)
            foreach (var (use, where) in Uses(package))
            {
                if (use.Type == null || !VariableRules.TryParseReference(package.Id, use.Reference, out var reference) || reference.Owner == package.Id)
                    continue;
                if (!byId.TryGetValue(reference.Owner, out var owner))
                {
                    issues.Add(ValidationIssue.Warning(IssueCodes.MissingCompanionReference, package.FolderName,
                        $"{where}: '{use.Reference}' belongs to companion '{reference.Owner}', which is not loaded; " +
                        (use.IsWrite ? "this write is skipped." : "this condition is false.")));
                    continue;
                }
                var declaration = reference.Name == VariableRules.Mood ? null : Declaration(owner, reference.Name);
                var type = OwnType(owner, reference.Name);
                if (type == null)
                    issues.Add(ValidationIssue.Error(IssueCodes.UnknownVariable, package.FolderName,
                        $"{where}: companion '{reference.Owner}' declares no variable '{reference.Name}'."));
                else if (declaration?.Scope != VariableRules.ScopeShared)
                    issues.Add(ValidationIssue.Error(IssueCodes.LocalVariable, package.FolderName,
                        $"{where}: '{use.Reference}' is local to '{reference.Owner}'; only \"scope\": \"shared\" variables can be used by other companions."));
                else if (type != use.Type)
                    issues.Add(Mismatch(package.FolderName, where, use, type.Value));
            }
        return issues;
    }

    private static void ValidateDeclaration(string folder, string name, VariableDeclaration? declaration, List<ValidationIssue> issues)
    {
        void Bad(string message) => issues.Add(ValidationIssue.Error(IssueCodes.InvalidVariable, folder, $"Variable '{name}' {message}"));

        if (!VariableRules.NamePattern.IsMatch(name))
            Bad($"must match {VariableRules.NamePattern}.");
        if (name == VariableRules.Mood)
            Bad("is built in (every companion has the int 'mood'); do not declare it.");
        if (declaration == null)
        {
            Bad("has no declaration object.");
            return;
        }
        if (!VariableRules.TryParseType(declaration.Type, out var type))
        {
            Bad($"has type '{declaration.Type}'; use one of: {string.Join(", ", VariableRules.Types)}.");
            return;
        }
        if (declaration.Scope != null && !VariableRules.Scopes.Contains(declaration.Scope, StringComparer.Ordinal))
            Bad($"has scope '{declaration.Scope}'; use one of: {string.Join(", ", VariableRules.Scopes)}.");
        if (declaration.Default != null && VariableUses.TypeOf(declaration.Default) != type)
            Bad($"is a {declaration.Type}, so its default must be {(type == VariableType.Bool ? "true or false" : "a whole number")}.");
    }

    /// <summary>The type of a variable of <paramref name="package"/> (declared or the built-in mood); null when unknown or invalid.</summary>
    private static VariableType? OwnType(CompanionPackage package, string name)
    {
        if (name == VariableRules.Mood)
            return VariableType.Int;
        var declaration = Declaration(package, name);
        return declaration != null && VariableRules.TryParseType(declaration.Type, out var type) ? type : (VariableType?)null;
    }

    private static VariableDeclaration? Declaration(CompanionPackage package, string name) =>
        package.Dialogs?.Variables != null && package.Dialogs.Variables.TryGetValue(name, out var declaration) ? declaration : null;

    private static ValidationIssue Mismatch(string folder, string where, VariableUse use, VariableType declared) =>
        ValidationIssue.Error(IssueCodes.VariableTypeMismatch, folder,
            $"{where}: '{use.Reference}' is a {VariableRules.Name(declared)}, but '{use.Key}' needs a {VariableRules.Name(use.Type!.Value)}.");

    /// <summary>Every use in the companion's conversations, with a location for messages.</summary>
    private static IEnumerable<(VariableUse Use, string Where)> Uses(CompanionPackage package)
    {
        foreach (var conversation in package.Conversations)
        {
            var name = $"Conversation '{conversation.Id ?? "(no id)"}'";
            foreach (var use in VariableUses.InCondition(conversation.When))
                yield return (use, name);
            foreach (var node in (conversation.Nodes ?? new List<ConversationNode>()).Where(n => n != null))
                foreach (var use in VariableUses.InCondition(node.When).Concat(VariableUses.InEffects(node.Effects)))
                    yield return (use, $"{name} node '{node.Id}'");
        }
    }
}
