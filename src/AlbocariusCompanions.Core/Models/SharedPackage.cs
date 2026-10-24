using System.Collections.Generic;

namespace AlbocariusCompanions.Core.Models;

/// <summary>The reserved ModuleData/Companions/_shared/ folder: player lines and their lang files.</summary>
public sealed class SharedPackage
{
    public const string FolderName = "_shared";
    public const string PlayerLinesFileName = "player_lines.json";

    public SharedPackage(string folderPath, PlayerLinesFile playerLines, IReadOnlyDictionary<string, LangFile>? languages = null)
    {
        FolderPath = folderPath;
        PlayerLinesFile = playerLines;
        Languages = languages ?? LangText.Empty;
    }

    public string FolderPath { get; }
    public PlayerLinesFile PlayerLinesFile { get; }
    public IReadOnlyDictionary<string, LangFile> Languages { get; }

    public IReadOnlyList<PlayerLine> Lines => (IReadOnlyList<PlayerLine>?)PlayerLinesFile.Lines ?? new List<PlayerLine>();

    /// <summary>Text of a player line in a language, falling back to English; null when there is none.</summary>
    public string? Text(string lineId, string languageCode) => LangText.Find(Languages, lineId, languageCode);
}
