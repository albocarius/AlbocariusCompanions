using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;

namespace AlbocariusCompanions.Core.Audio;

/// <summary>
/// The voiced-lines document the game reads for voice-over (GAME_MAP Q12): one VoiceOver per voiced line whose .ogg
/// exists, id = line id (the {=id} key of the dialog text). Rewritten at every start from the companion folders, so
/// adding a companion needs no build step. The game appends ".ogg" itself, so paths carry no extension.
/// </summary>
public static class VoicedLinesXml
{
    /// <summary>Folder of the voiced-lines file, relative to the module's ModuleData/Languages.</summary>
    public const string FileRelativePath = "VoicedLines/albocarius_voiced_lines_en.xml";

    /// <summary>Voice paths are stored relative to ModuleData/Languages; companion folders sit next to it.</summary>
    public const string CompanionsFromLanguages = "../Companions/";

    private const string OggExtension = ".ogg";

    public static XDocument Build(IEnumerable<CompanionPackage> packages, Func<string, bool> fileExists)
    {
        if (packages == null) throw new ArgumentNullException(nameof(packages));
        if (fileExists == null) throw new ArgumentNullException(nameof(fileExists));

        var voiceOvers = new XElement("VoiceOvers");
        foreach (var package in packages)
        {
            foreach (var line in package.Dialogs?.Lines ?? new List<DialogLine>())
            {
                if (line == null || !line.Voiced || string.IsNullOrEmpty(line.Id))
                    continue;

                var relative = AudioRelativePath(line);
                if (!relative.EndsWith(OggExtension, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!fileExists(Path.Combine(package.FolderPath, relative.Replace('/', Path.DirectorySeparatorChar))))
                    continue;

                var withoutExtension = relative.Substring(0, relative.Length - OggExtension.Length);
                voiceOvers.Add(new XElement("VoiceOver", new XAttribute("id", line.Id),
                    new XElement("Voice", new XAttribute("path", CompanionsFromLanguages + package.FolderName + "/" + withoutExtension))));
            }
        }

        // Root name is not checked by the game; vanilla uses <Base>. An empty <VoiceOvers/> must still be present.
        return new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement("Base", voiceOvers));
    }

    /// <summary>The line's audio path, or the default audio/&lt;line id&gt;.ogg, with '/' separators.</summary>
    public static string AudioRelativePath(DialogLine line) =>
        (string.IsNullOrEmpty(line.Audio) ? LineIds.DefaultAudioPath(line.Id!) : line.Audio!).Replace('\\', '/');

    /// <summary>
    /// Maps a voice path the game built from this document (&lt;module&gt;/ModuleData/Languages/../Companions/...)
    /// to the playable file path; null for any other path. Used by the voice-over Harmony postfix.
    /// </summary>
    public static string? ResolveSoundPath(string voicePath, string moduleId)
    {
        if (string.IsNullOrEmpty(voicePath))
            return null;

        var normalized = voicePath.Replace('\\', '/');
        var marker = "/" + moduleId + "/ModuleData/Languages/" + CompanionsFromLanguages;
        var index = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return null;

        var rest = normalized.Substring(index + marker.Length);
        return normalized.Substring(0, index) + "/" + moduleId + "/ModuleData/Companions/" + rest + OggExtension;
    }
}
