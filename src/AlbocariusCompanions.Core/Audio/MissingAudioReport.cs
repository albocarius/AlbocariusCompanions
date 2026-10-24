using System;
using System.Collections.Generic;
using AlbocariusCompanions.Core.Models;

namespace AlbocariusCompanions.Core.Audio;

/// <summary>Voiced lines whose audio file does not exist (used later by the voice pipeline).</summary>
public static class MissingAudioReport
{
    public static IReadOnlyList<MissingAudioEntry> Find(IEnumerable<CompanionPackage> packages) =>
        throw new NotImplementedException("task 20-core-missing-audio");
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
