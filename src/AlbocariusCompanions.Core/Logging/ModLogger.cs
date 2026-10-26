using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace AlbocariusCompanions.Core.Logging;

public enum LogLevel
{
    Info,
    Warning,
    Error,
}

/// <summary>
/// Own log file of the mod (GAME_MAP Q15: Documents\Mount and Blade II Bannerlord\AlbocariusCompanions\).
/// Plain System.IO so it is testable without the game. Never throws: logging must not crash the game.
/// </summary>
public sealed class ModLogger
{
    /// <summary>Fixed-width invariant format, so lines sort chronologically in any locale: 2026-10-24 17:05:09.123 [INFO] text</summary>
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";

    /// <summary>UTF-8 without a BOM: the file is truncated on session start and read back line by line.</summary>
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Serializes every write (the game calls mod code from the main thread, but Harmony patches and
    /// background work can log too); held for the whole write, never released into user code.</summary>
    private readonly object _gate = new object();

    private readonly Func<DateTime> _clock;

    /// <param name="logFilePath">Absolute path of the log file; its folder is created when missing.</param>
    /// <param name="clock">Time source for timestamps (tests); defaults to local now.</param>
    public ModLogger(string logFilePath, Func<DateTime>? clock = null)
    {
        LogFilePath = logFilePath;
        _clock = clock ?? (static () => DateTime.Now);
    }

    public string LogFilePath { get; }

    /// <summary>Optional listener for warnings and errors (in-game notifications); its exceptions are swallowed.</summary>
    public Action<LogLevel, string>? Notify { get; set; }

    /// <summary>Starts a new session: truncates the file and writes a header line. Never throws.</summary>
    public void StartSession(string header)
    {
        lock (_gate)
        {
            try
            {
                EnsureLogFolder();
                // WriteAllText overwrites, i.e. truncates: only the new header remains from this call.
                File.WriteAllText(LogFilePath, Line(LogLevel.Info, header) + Environment.NewLine, Utf8NoBom);
            }
            catch (Exception) { /* unusable path, permission, full disk: nothing is written */ }
        }
    }

    public void Info(string message) => Append(LogLevel.Info, message);
    public void Warning(string message) => Append(LogLevel.Warning, message);
    public void Error(string message) => Append(LogLevel.Error, message);

    /// <summary>Writes to the log file only: <see cref="Notify"/> is not raised (details that a summary line announces).</summary>
    public void FileOnly(LogLevel level, string message) => Append(level, message, notify: false);

    public void Error(string message, Exception exception)
    {
        RaiseNotify(LogLevel.Error, message);
        lock (_gate)
        {
            try
            {
                EnsureLogFolder();
                // exception.ToString() carries type + message + stack, on its own following lines.
                var text = Line(LogLevel.Error, message) + Environment.NewLine
                    + (exception?.ToString() ?? "<null exception>") + Environment.NewLine;
                File.AppendAllText(LogFilePath, text, Utf8NoBom);
            }
            catch (Exception) { /* never throw out of a log call */ }
        }
    }

    /// <summary>Writes (overwrites) a file next to the log, e.g. the generated NPCCharacters copy. Returns the full path, or null on failure.</summary>
    public string? WriteSideFile(string fileName, string content)
    {
        lock (_gate)
        {
            try
            {
                var folder = FolderOfLog();
                if (folder == null || string.IsNullOrEmpty(fileName)) return null;

                Directory.CreateDirectory(folder);
                var fullPath = Path.Combine(folder, fileName);
                File.WriteAllText(fullPath, content ?? string.Empty, Utf8NoBom);
                return fullPath;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    private void Append(LogLevel level, string message, bool notify = true)
    {
        if (notify && level != LogLevel.Info)
            RaiseNotify(level, message);
        lock (_gate)
        {
            try
            {
                EnsureLogFolder();
                // AppendAllText creates the file when it is missing (no session was started).
                File.AppendAllText(LogFilePath, Line(level, message) + Environment.NewLine, Utf8NoBom);
            }
            catch (Exception) { /* never throw out of a log call */ }
        }
    }

    private void RaiseNotify(LogLevel level, string message)
    {
        try { Notify?.Invoke(level, message); }
        catch (Exception) { /* a broken listener must not break logging */ }
    }

    /// <summary>Folder of the log file, or null when the path has none (root file, empty path).</summary>
    private string? FolderOfLog()
    {
        if (string.IsNullOrEmpty(LogFilePath)) return null;
        var folder = Path.GetDirectoryName(LogFilePath);
        return string.IsNullOrEmpty(folder) ? null : folder;
    }

    private void EnsureLogFolder()
    {
        var folder = FolderOfLog();
        if (folder != null) Directory.CreateDirectory(folder);
    }

    private string Line(LogLevel level, string message)
    {
        var timestamp = Now();
        return timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture)
            + " [" + Tag(level) + "] "
            + message;
    }

    private static string Tag(LogLevel level) => level switch
    {
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERROR",
        _ => "INFO",
    };

    /// <summary>A throwing time source must not take the log down with it.</summary>
    private DateTime Now()
    {
        try { return _clock(); }
        catch (Exception) { return default; }
    }
}
