using System.IO;
using System.Linq;
using AlbocariusCompanions.Core.Audio;
using AlbocariusCompanions.Core.Logging;
using AlbocariusCompanions.Core.Registry;
using TaleWorlds.ModuleManager;

namespace AlbocariusCompanions.Game;

/// <summary>
/// Rewrites the module's voiced-lines file from the companion folders (GAME_MAP Q12). Runs in OnSubModuleLoad: the game
/// registers the file path (language_data.xml) before submodules load, but parses its content only afterwards, when the
/// voice language is applied — so the fresh content is what the game reads.
/// </summary>
internal static class VoicedLinesFile
{
    public static void Write(CompanionRegistry registry, ModLogger log)
    {
        foreach (var missing in MissingAudioReport.Find(registry.All))
            log.Warning($"Voiced line '{missing.LineId}' of '{missing.CompanionId}' has no audio file at {missing.RelativePath}; it plays silent.");

        var document = VoicedLinesXml.Build(registry.All, File.Exists);
        var count = document.Root!.Element("VoiceOvers")!.Elements().Count();

        var path = ModuleHelper.GetModuleFullPath(ModRuntime.ModuleId) + "ModuleData/Languages/" + VoicedLinesXml.FileRelativePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Never leave a half-written file: the game crashes on a voiced-lines file without <VoiceOvers> (Q12).
        var temp = path + ".tmp";
        document.Save(temp);
        if (File.Exists(path))
            File.Delete(path);
        File.Move(temp, path);

        log.Info($"Voiced lines written: {count} line(s) with audio → {path}.");
    }
}
