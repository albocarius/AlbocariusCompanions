using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AlbocariusCompanions.Core.Logging;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Registry;
using AlbocariusCompanions.Core.Validation;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;

namespace AlbocariusCompanions.Game;

/// <summary>
/// Module entry point (SubModule.xml SubModuleClassType). Every override is wrapped in Safe.Run: the game catches no
/// exception from mod code (GAME_MAP Q15). Lifecycle per GAME_MAP Q16.
/// </summary>
public sealed class SubModule : MBSubModuleBase
{
    public const string HarmonyId = "albocarius.companions";
    public const string LogFolderName = "AlbocariusCompanions";
    public const string LogFileName = "AlbocariusCompanions.log";

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();

        // Documents\Mount and Blade II Bannerlord\AlbocariusCompanions\ (GAME_MAP Q15).
        var logPath = Safe.Run(ModRuntime.Log, "log path", () => FileHelper.GetFileFullPath(
            new PlatformFilePath(new PlatformDirectoryPath(PlatformFileType.User, LogFolderName), LogFileName)), string.Empty);
        ModRuntime.Log = new ModLogger(logPath) { Notify = InGameNotifications.OnLogged };
        var log = ModRuntime.Log;
        log.StartSession($"{ModRuntime.ModuleId} {Assembly.GetExecutingAssembly().GetName().Version} starting");

        Safe.Run(log, "load companions", () =>
        {
            var root = ModuleHelper.GetModuleFullPath(ModRuntime.ModuleId) + "ModuleData/Companions";
            var catalog = CompanionCatalog.Load(root, ModuleHelper.IsModuleActive);
            foreach (var issue in catalog.Issues)
                CompanionTemplates.LogIssue(log, issue);
            foreach (var package in catalog.Registry.All)
                log.Info($"Companion loaded: {package.Id} ({package.Dialogs.Lines?.Count ?? 0} dialog lines, " +
                    $"{package.Conversations.Count} conversation(s)).");
            foreach (var language in MissingTranslations.CountByLanguage(catalog.Issues))
                log.Info($"Missing translations in '{language.Key}': {language.Value} line(s) fall back to English.");
            var skipped = catalog.Issues.Where(i => i.IsError)
                .Select(i => i.Folder).Distinct().ToList();
            if (skipped.Count > 0)
                log.Warning($"Companions skipped because of errors: {string.Join(", ", skipped)}.");

            // requiredModules (SPEC_CORE_GAPS T2.3): a companion whose module is not loaded is skipped, the rest stay.
            foreach (var skip in catalog.ModuleSkips)
                log.Warning($"Companion '{skip.Key}' skipped: required module(s) not loaded: {string.Join(", ", skip.Value)}.");

            log.Info($"{catalog.Registry.Count} companion(s) ready from {root}.");
            ModRuntime.SkippedAtLoad = skipped.Count + catalog.ModuleSkips.Count;
            ModRuntime.Registry = catalog.Registry;
        });

        Safe.Run(log, "write voiced lines", () => VoicedLinesFile.Write(ModRuntime.Registry, log));

        Safe.Run(log, "apply Harmony patches", () =>
        {
            new Harmony(HarmonyId).PatchAll(Assembly.GetExecutingAssembly());
            log.Info("Harmony patches applied.");
        });
    }

    protected override void InitializeGameStarter(TaleWorlds.Core.Game game, IGameStarter starterObject)
    {
        base.InitializeGameStarter(game, starterObject);
        Safe.Run(ModRuntime.Log, "InitializeGameStarter", () =>
        {
            if (game.GameType is Campaign && starterObject is CampaignGameStarter starter)
            {
                starter.AddBehavior(new AlbocariusCompanionsCampaignBehavior());
                ModRuntime.Log.Info("Campaign behavior registered.");
            }
        });
    }

    public override void AfterRegisterSubModuleObjects(bool isSavedCampaign)
    {
        base.AfterRegisterSubModuleObjects(isSavedCampaign);
        Safe.Run(ModRuntime.Log, "register companion templates", () =>
        {
            ModRuntime.Templates = CompanionTemplates.Register(ModRuntime.Registry, new GameObjectLookup(), ModRuntime.Log);
            ModRuntime.Log.Info($"{ModRuntime.Templates.Count} template(s) registered (saved campaign: {isSavedCampaign}).");
        });
    }
}
