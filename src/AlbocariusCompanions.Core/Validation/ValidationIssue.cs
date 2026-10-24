namespace AlbocariusCompanions.Core.Validation;

public enum IssueSeverity
{
    Warning,
    Error,
}

/// <summary>One loader/validation finding. Errors skip the companion; warnings are only logged.</summary>
public sealed class ValidationIssue
{
    public ValidationIssue(IssueSeverity severity, string code, string folder, string message)
    {
        Severity = severity;
        Code = code;
        Folder = folder;
        Message = message;
    }

    public IssueSeverity Severity { get; }
    /// <summary>One of <see cref="IssueCodes"/>.</summary>
    public string Code { get; }
    /// <summary>
    /// Companion folder name (equals the companion id for valid companions). Issues are keyed by folder, not by id,
    /// so a broken folder can never cause another companion to be skipped. Empty for issues not tied to a folder.
    /// </summary>
    public string Folder { get; }
    public string Message { get; }

    public bool IsError => Severity == IssueSeverity.Error;

    public static ValidationIssue Error(string code, string folder, string message) =>
        new ValidationIssue(IssueSeverity.Error, code, folder, message);

    public static ValidationIssue Warning(string code, string folder, string message) =>
        new ValidationIssue(IssueSeverity.Warning, code, folder, message);

    public override string ToString() => $"{Severity} {Code} [{Folder}] {Message}";
}
