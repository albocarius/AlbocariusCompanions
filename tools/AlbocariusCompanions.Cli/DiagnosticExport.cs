using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Cli;

/// <summary>
/// <c>validate --json</c> (SPEC_EDITOR §3): validation issues as JSON with the file, conversation and node they are about.
/// Core issues carry only a folder and a message, so the location is read from the message's fixed wording
/// ("Conversation 'x'", "node 'y'", "Line 'z'", "lang/tr.json") and from the issue code; a part that cannot be told is null.
/// </summary>
public static class DiagnosticExport
{
    public const int ExportVersion = 1;

    private static readonly Regex ConversationPattern = new Regex(@"Conversation '([^']*)'");
    private static readonly Regex NodePattern = new Regex(@"\bnode '([^']*)'");
    private static readonly Regex LinePattern = new Regex(@"\bLine (?:id )?'([^']*)'|has a text for '([^']*)'");
    private static readonly Regex FilePattern = new Regex(@"(companion\.json|dialogs\.json|lang/[a-z]{2}\.json)");
    private static readonly Regex LangFilePattern = new Regex(@"Lang file '([^']*\.json)'");

    private static readonly HashSet<string> CompanionCodes = new HashSet<string>
    {
        IssueCodes.MissingCompanionFile, IssueCodes.InvalidId, IssueCodes.MissingField, IssueCodes.OutOfRange,
        IssueCodes.UnknownAttribute, IssueCodes.InvalidVoice, IssueCodes.InvalidRequiredModule, IssueCodes.InvalidReaction,
        IssueCodes.InvalidRelation, IssueCodes.UnknownRelationTarget, IssueCodes.UnknownEquipmentSlot, IssueCodes.InvalidEquipmentItem,
    };

    public static JObject Build(IReadOnlyList<ValidationIssue> issues)
    {
        var errors = issues.Count(i => i.IsError);
        return new JObject
        {
            ["exportVersion"] = ExportVersion,
            ["errors"] = errors,
            ["warnings"] = issues.Count - errors,
            ["diagnostics"] = new JArray(issues.Select(Diagnostic)),
        };
    }

    public static JObject Diagnostic(ValidationIssue issue)
    {
        var message = issue.Message;
        var conversation = Group(ConversationPattern, message);
        var node = Group(NodePattern, message) ?? Group(LinePattern, message);
        // A line named by its full id (<companion>_<local id>) points at the local id the files use.
        if (node != null && issue.Folder.Length > 0 && node.StartsWith(issue.Folder + "_", System.StringComparison.Ordinal))
            node = node.Substring(issue.Folder.Length + 1);

        return new JObject
        {
            ["severity"] = issue.IsError ? "error" : "warning",
            ["code"] = issue.Code,
            ["folder"] = issue.Folder,
            ["file"] = File(issue, conversation, node),
            ["conversation"] = conversation,
            ["node"] = node,
            ["message"] = message,
        };
    }

    private static string? File(ValidationIssue issue, string? conversation, string? node)
    {
        var lang = Group(LangFilePattern, issue.Message);
        if (lang != null)
            return "lang/" + lang;
        var named = Group(FilePattern, issue.Message);
        if (named != null)
            return named;
        if (CompanionCodes.Contains(issue.Code))
            return "companion.json";
        if (issue.Code == IssueCodes.MissingDialogsFile || conversation != null || node != null)
            return "dialogs.json";
        return null;
    }

    private static string? Group(Regex pattern, string text)
    {
        var match = pattern.Match(text);
        if (!match.Success)
            return null;
        // The first group that took part in the match (a pattern may have alternatives).
        return match.Groups.Cast<Group>().Skip(1).First(g => g.Success).Value;
    }
}
