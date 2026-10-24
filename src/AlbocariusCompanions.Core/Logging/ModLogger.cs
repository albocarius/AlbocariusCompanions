using System;

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
    /// <param name="logFilePath">Absolute path of the log file; its folder is created when missing.</param>
    /// <param name="clock">Time source for timestamps (tests); defaults to local now.</param>
    public ModLogger(string logFilePath, Func<DateTime>? clock = null) => throw new NotImplementedException("task 24-core-logging");

    public string LogFilePath => throw new NotImplementedException();

    /// <summary>Starts a new session: truncates the file and writes a header line. Never throws.</summary>
    public void StartSession(string header) => throw new NotImplementedException();

    public void Info(string message) => throw new NotImplementedException();
    public void Warning(string message) => throw new NotImplementedException();
    public void Error(string message) => throw new NotImplementedException();
    public void Error(string message, Exception exception) => throw new NotImplementedException();

    /// <summary>Writes (overwrites) a file next to the log, e.g. the generated NPCCharacters copy. Returns the full path, or null on failure.</summary>
    public string? WriteSideFile(string fileName, string content) => throw new NotImplementedException();
}
