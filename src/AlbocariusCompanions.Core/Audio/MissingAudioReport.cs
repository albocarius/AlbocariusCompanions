using System.Collections.Generic;
using System.IO;
using AlbocariusCompanions.Core.Models;

namespace AlbocariusCompanions.Core.Audio;

/// <summary>Voiced lines whose audio file does not exist (used later by the voice pipeline).</summary>
public static class MissingAudioReport
{
    /// <summary>Walks packages in order, then lines in order; one entry per voiced line with no audio file on disk.</summary>
    public static IReadOnlyList<MissingAudioEntry> Find(IEnumerable<CompanionPackage> packages)
    {
        var entries = new List<MissingAudioEntry>();

        foreach (var package in packages)
        {
            var lines = package.Dialogs.Lines;
            if (lines == null) continue;

            foreach (var line in lines)
            {
                if (line == null || !line.Voiced) continue;
                if (string.IsNullOrEmpty(line.Id)) continue;

                var relativePath = VoicedLinesXml.AudioRelativePath(line);
                var fullPath = Path.Combine(package.FolderPath,
                    relativePath.Replace('/', Path.DirectorySeparatorChar));

                if (!File.Exists(fullPath))
                    entries.Add(new MissingAudioEntry(package.Id, line.Id!, relativePath, fullPath));
            }
        }

        return entries;
    }
}

public sealed class MissingAudioEntry
{
    public MissingAudioEntry(string companionId, string lineId, string relativePath, string fullPath)
    {
        CompanionId = companionId;
        LineId = lineId;
        RelativePath = relativePath;
        FullPath = fullPath;
    }

    public string CompanionId { get; }
    public string LineId { get; }
    /// <summary>The line's audio path, or the default audio/&lt;line id&gt;.ogg, with '/' separators.</summary>
    public string RelativePath { get; }
    public string FullPath { get; }
}
