using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Core.Loading;

/// <summary>
/// Scans &lt;root&gt;/*/ for companion.json + dialogs.json + lang/*.json. The reserved _shared folder (player lines before
/// M6) is skipped with a warning. Never throws for bad data.
/// </summary>
public static class CompanionLoader
{
    public const string CompanionFileName = "companion.json";
    public const string DialogsFileName = "dialogs.json";
    public const string LangFolderName = "lang";

    /// <summary>Held the shared player lines until M6; now reserved and skipped, so an old install never loads it as a companion.</summary>
    public const string ObsoleteSharedFolderName = "_shared";

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

        if (Directory.Exists(Path.Combine(companionsRoot, ObsoleteSharedFolderName)))
            issues.Add(ValidationIssue.Warning(IssueCodes.ObsoleteSharedFolder, ObsoleteSharedFolderName,
                $"The '{ObsoleteSharedFolderName}' folder is no longer read: player lines now live in each companion's dialogs.json " +
                "(player_* slots). Delete the folder."));

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
            if (!TryReadLanguages(folder.Path, folder.Name, issues, out var languages))
                continue;

            dialogs!.Lines ??= new List<DialogLine>(); // "lines" missing -> empty list, not null
            packages.Add(new CompanionPackage(folder.Name, folder.Path, companion!, dialogs, languages));
        }

        return new LoadResult(packages, issues);
    }

    /// <summary>Direct subdirectories of the root except the obsolete _shared folder, ordered by name (ordinal).</summary>
    private static IEnumerable<(string Name, string Path)> EnumerateCompanionFolders(string root) =>
        Directory.GetDirectories(root)
            .Select(p => new DirectoryInfo(p))
            .Where(d => !string.Equals(d.Name, ObsoleteSharedFolderName, StringComparison.Ordinal))
            .Select(d => (Name: d.Name, Path: Path.Combine(root, d.Name)))
            .OrderBy(f => f.Name, StringComparer.Ordinal);

    /// <summary>
    /// lang/*.json of a folder, keyed by file name without extension (validation checks the code). No lang folder → no
    /// languages. Any broken file fails the whole folder, like a broken dialogs.json.
    /// </summary>
    private static bool TryReadLanguages(string folderPath, string folder, List<ValidationIssue> issues,
        out IReadOnlyDictionary<string, LangFile> languages)
    {
        var result = new Dictionary<string, LangFile>(StringComparer.Ordinal);
        languages = result;

        var langPath = Path.Combine(folderPath, LangFolderName);
        if (!Directory.Exists(langPath))
            return true;

        foreach (var file in Directory.GetFiles(langPath, "*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            var fileName = LangFolderName + "/" + Path.GetFileName(file);
            if (!TryReadJson<LangFile>(file, fileName, folder, issues, out var lang))
                return false;
            lang!.Lines ??= new Dictionary<string, string>(StringComparer.Ordinal);
            result[Path.GetFileNameWithoutExtension(file)] = lang;
        }
        return true;
    }

    /// <summary>Newtonsoft turns numbers and booleans into strings silently; a lang file's texts must be JSON strings.</summary>
    private static string? NonStringText(JObject document)
    {
        if (document["lines"] is not JObject lines)
            return null;
        foreach (var property in lines.Properties())
            if (property.Value.Type != JTokenType.String)
                return property.Name;
        return null;
    }

    private static bool TryReadJson<T>(string path, string fileName, string folder, List<ValidationIssue> issues, out T? value)
        where T : class
    {
        value = null;
        JObject document;
        try
        {
            document = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
        }
        catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException)
        {
            issues.Add(ValidationIssue.Error(IssueCodes.InvalidJson, folder,
                $"{folder}/{fileName} is not valid: {ex.Message}"));
            return false;
        }

        // The one version check (SchemaVersions). A present but wrong version skips the folder before the model is read;
        // a missing one is reported only after the model parsed, so a broken file surfaces its JSON error first.
        var versionIssue = SchemaVersions.Check(document, folder, fileName);
        if (versionIssue != null && versionIssue.Code != IssueCodes.MissingSchemaVersion)
        {
            issues.Add(versionIssue);
            return false;
        }

        if (typeof(T) == typeof(LangFile) && NonStringText(document) is { } badLine)
        {
            issues.Add(ValidationIssue.Error(IssueCodes.InvalidJson, folder,
                $"{folder}/{fileName} is not valid: the text of '{badLine}' must be a JSON string."));
            return false;
        }

        try
        {
            value = document.ToObject<T>(JsonSerializer.Create(StrictSettings))
                ?? throw new JsonSerializationException("document is null or empty");
        }
        catch (JsonException ex)
        {
            issues.Add(ValidationIssue.Error(IssueCodes.InvalidJson, folder,
                $"{folder}/{fileName} is not valid: {ex.Message}"));
            return false;
        }

        if (versionIssue != null)
        {
            issues.Add(versionIssue);
            return false;
        }
        return true;
    }
}
