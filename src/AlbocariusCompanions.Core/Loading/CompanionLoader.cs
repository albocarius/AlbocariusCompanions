using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json;

namespace AlbocariusCompanions.Core.Loading;

/// <summary>Scans &lt;root&gt;/*/ for companion.json + dialogs.json. Never throws for bad data.</summary>
public static class CompanionLoader
{
    public const string CompanionFileName = "companion.json";
    public const string DialogsFileName = "dialogs.json";

    /// <summary>Strict parsing: unknown properties are errors (catches typos); type mismatches throw too.</summary>
    private static JsonSerializerSettings StrictSettings { get; } = new JsonSerializerSettings
    {
        MissingMemberHandling = MissingMemberHandling.Error,
    };

    public static LoadResult LoadAll(string companionsRoot)
    {
        var packages = new List<CompanionPackage>();
        var issues = new List<ValidationIssue>();

        if (string.IsNullOrEmpty(companionsRoot) || !Directory.Exists(companionsRoot))
        {
            issues.Add(ValidationIssue.Error(IssueCodes.RootNotFound, "",
                $"Companions root folder not found: {companionsRoot}"));
            return new LoadResult(packages, issues);
        }

        foreach (var folder in EnumerateCompanionFolders(companionsRoot))
        {
            var companionPath = Path.Combine(folder.Path, CompanionFileName);
            var dialogsPath = Path.Combine(folder.Path, DialogsFileName);

            var hasCompanion = File.Exists(companionPath);
            var hasDialogs = File.Exists(dialogsPath);
            if (!hasCompanion)
                issues.Add(ValidationIssue.Error(IssueCodes.MissingCompanionFile, folder.Name,
                    $"Missing {CompanionFileName} in companion folder '{folder.Name}'."));
            if (!hasDialogs)
                issues.Add(ValidationIssue.Error(IssueCodes.MissingDialogsFile, folder.Name,
                    $"Missing {DialogsFileName} in companion folder '{folder.Name}'."));
            if (!hasCompanion || !hasDialogs)
                continue;

            if (!TryReadJson<CompanionDefinition>(companionPath, CompanionFileName, folder.Name, issues, out var companion))
                continue;
            if (!TryReadJson<DialogFile>(dialogsPath, DialogsFileName, folder.Name, issues, out var dialogs))
                continue;

            dialogs!.Lines ??= new List<DialogLine>(); // "lines" missing -> empty list, not null
            packages.Add(new CompanionPackage(folder.Name, folder.Path, companion!, dialogs));
        }

        return new LoadResult(packages, issues);
    }

    /// <summary>Direct subdirectories of the root (files in the root are ignored), ordered by name (ordinal).</summary>
    private static IEnumerable<(string Name, string Path)> EnumerateCompanionFolders(string root) =>
        Directory.GetDirectories(root)
            .Select(p => new DirectoryInfo(p))
            .Select(d => (Name: d.Name, Path: Path.Combine(root, d.Name)))
            .OrderBy(f => f.Name, StringComparer.Ordinal);

    private static bool TryReadJson<T>(string path, string fileName, string folder, List<ValidationIssue> issues, out T? value)
        where T : class
    {
        value = null;
        try
        {
            value = JsonConvert.DeserializeObject<T>(File.ReadAllText(path, Encoding.UTF8), StrictSettings)
                ?? throw new JsonSerializationException("document is null or empty");
        }
        catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException)
        {
            issues.Add(ValidationIssue.Error(IssueCodes.InvalidJson, folder,
                $"{folder}/{fileName} is not valid: {ex.Message}"));
            return false;
        }
        return true;
    }
}
