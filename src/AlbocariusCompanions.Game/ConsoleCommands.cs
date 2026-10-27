using System.Collections.Generic;
using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace AlbocariusCompanions.Game;

/// <summary>
/// In-game console commands (M7 I7, GAME_MAP Q30): found by the game's attribute scan of loaded assemblies, typed as
/// "albocarius.&lt;name&gt; args". Text and parsing live in Core (DebugCommands); every command is wrapped in Safe.Run.
/// No reload command: dialogs are registered once per campaign start, so swapping the JSON under them is not safe.
/// </summary>
internal static class ConsoleCommands
{
    private const string Group = "albocarius";

    [CommandLineFunctionality.CommandLineArgumentFunction("list", Group)]
    public static string List(List<string> args) =>
        InCampaign("albocarius.list", () => DebugCommands.List(ModRuntime.Variables, Describe));

    [CommandLineFunctionality.CommandLineArgumentFunction("get", Group)]
    public static string Get(List<string> args) =>
        InCampaign("albocarius.get", () => DebugCommands.Get(ModRuntime.Variables, args));

    [CommandLineFunctionality.CommandLineArgumentFunction("set", Group)]
    public static string Set(List<string> args) =>
        InCampaign("albocarius.set", () =>
        {
            var result = DebugCommands.Set(ModRuntime.Variables, args);
            ModRuntime.Log.Info($"Console albocarius.set {string.Join(" ", args)} -> {result}");
            ModRuntime.Triggers?.VariablesChanged(); // varCrossed (spec checklist 6)
            return result;
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("reset", Group)]
    public static string Reset(List<string> args) =>
        InCampaign("albocarius.reset", () =>
        {
            var result = DebugCommands.Reset(ModRuntime.Variables, args);
            ModRuntime.Log.Info($"Console albocarius.reset {string.Join(" ", args)} -> {result}");
            return result;
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("relation", Group)]
    public static string Relation(List<string> args) =>
        InCampaign("albocarius.relation", () =>
        {
            var result = DebugCommands.Relation(args, ModRuntime.Registry.Contains,
                (a, b) => ModRuntime.HeroOf(a) is { } heroA && ModRuntime.HeroOf(b) is { } heroB ? CompanionRelations.Get(heroA, heroB) : (int?)null,
                (a, b, value) => CompanionRelations.Set(ModRuntime.HeroOf(a)!, ModRuntime.HeroOf(b)!, value));
            if (args.Count == 3)
                ModRuntime.Log.Info($"Console albocarius.relation {string.Join(" ", args)} -> {result}");
            return result;
        });

    /// <summary>M11 T7: fire a trigger now, e.g. <c>albocarius.fire heroKilled victim=lord_1_1 detail=Executed</c>.</summary>
    [CommandLineFunctionality.CommandLineArgumentFunction("fire", Group)]
    public static string Fire(List<string> args) =>
        InCampaign("albocarius.fire", () =>
        {
            var result = ModRuntime.Triggers?.FireFromConsole(args) ?? "Triggers are not running.";
            ModRuntime.Log.Info($"Console albocarius.fire {string.Join(" ", args)} -> {result}");
            return result;
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("queue", Group)]
    public static string Queue(List<string> args) =>
        InCampaign("albocarius.queue", () => ModRuntime.Triggers?.QueueText() ?? "Triggers are not running.");

    [CommandLineFunctionality.CommandLineArgumentFunction("clearcooldowns", Group)]
    public static string ClearCooldowns(List<string> args) =>
        InCampaign("albocarius.clearcooldowns", () =>
        {
            if (ModRuntime.Triggers == null)
                return "Triggers are not running.";
            ModRuntime.Triggers.ClearCooldowns();
            ModRuntime.Log.Info("Console albocarius.clearcooldowns");
            return "Trigger cooldowns and today's counters cleared.";
        });

    /// <summary>Variables exist per campaign, so every command needs a running one.</summary>
    private static string InCampaign(string context, System.Func<string> command) =>
        Safe.Run(ModRuntime.Log, "console " + context,
            () => Campaign.Current == null ? "No campaign is running." : command(),
            fallback: "Error: the command failed; see the AlbocariusCompanions log.");

    private static string Describe(string companionId)
    {
        var hero = ModRuntime.HeroOf(companionId);
        if (hero == null)
            return "no hero";
        if (!hero.IsAlive)
            return "dead";
        if (hero.PartyBelongedTo == MobileParty.MainParty)
            return "in your party";
        if (hero.IsPlayerCompanion)
            return "in your clan";
        return hero.CurrentSettlement != null ? "wanderer in " + hero.CurrentSettlement.Name : "wanderer, travelling";
    }
}
