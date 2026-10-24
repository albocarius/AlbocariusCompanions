using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AlbocariusCompanions.Core.Logging;
using AlbocariusCompanions.Core.Registry;
using AlbocariusCompanions.Core.Resolution;
using AlbocariusCompanions.Core.Templates;
using AlbocariusCompanions.Core.Validation;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace AlbocariusCompanions.Game;

/// <summary>
/// Registers one character template per registry companion (SPEC rule 8 amendment, GAME_MAP Q11):
/// resolve ids against the live game → generate the NPCCharacters document → MBObjectManager.LoadXml.
/// Run from MBSubModuleBase.AfterRegisterSubModuleObjects, for new and loaded campaigns alike.
/// </summary>
internal static class CompanionTemplates
{
    public const string DebugCopyFileName = "AlbocariusCompanions_NPCCharacters.xml";

    /// <summary>Companion id → registered template. Companions that failed resolution or loading are absent.</summary>
    public static IReadOnlyDictionary<string, CharacterObject> Register(CompanionRegistry registry, IGameObjectLookup game, ModLogger log)
    {
        var resolved = new List<ResolvedCompanion>();
        foreach (var package in registry.All)
        {
            var result = CompanionResolver.Resolve(package, game);
            foreach (var issue in result.Issues)
                LogIssue(log, issue);
            if (result.Companion == null)
                log.Error($"Companion '{package.Id}' skipped: required game data missing (see errors above).");
            else
                resolved.Add(result.Companion);
        }

        var templates = new Dictionary<string, CharacterObject>();
        if (resolved.Count == 0)
        {
            log.Warning("No companion templates to register.");
            return templates;
        }

        var document = NpcCharacterXml.Build(resolved);
        var copy = log.WriteSideFile(DebugCopyFileName, document.ToString());
        if (copy != null)
            log.Info($"Generated template XML written to {copy} (debug copy, never read back).");

        var xml = new XmlDocument();
        xml.LoadXml(document.ToString());
        MBObjectManager.Instance.LoadXml(xml);

        foreach (var companion in resolved)
        {
            var template = MBObjectManager.Instance.GetObject<CharacterObject>(companion.Id);
            if (template == null)
            {
                log.Error($"Template '{companion.Id}' was not registered by the game after LoadXml.");
                continue;
            }
            templates[companion.Id] = template;
            log.Info($"Template registered: {companion.Id} (culture {companion.CultureId}, voice {companion.Voice}).");
        }

        return templates;
    }

    internal static void LogIssue(ModLogger log, ValidationIssue issue)
    {
        var line = $"[{issue.Folder}] {issue.Code}: {issue.Message}";
        if (issue.IsError)
            log.Error(line);
        else if (issue.Code == IssueCodes.MissingTranslation)
            log.Info(line); // expected while translations are partial: file only, counted per language at load
        else
            log.Warning(line);
    }

    internal static bool HasErrors(IEnumerable<ValidationIssue> issues) => issues.Any(i => i.IsError);
}
