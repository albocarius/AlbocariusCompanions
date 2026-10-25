using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Rules;

namespace AlbocariusCompanions.Core.Dialogs;

/// <summary>
/// Text side of the in-game console commands (M7 I7): argument parsing and output. The game layer only registers the
/// commands and passes the live store; everything here is testable without the game.
/// </summary>
public static class DebugCommands
{
    public const string UsageGet = "Usage: albocarius.get <companion>.<variable>";
    public const string UsageSet = "Usage: albocarius.set <companion>.<variable> <true|false|number>";
    public const string UsageReset = "Usage: albocarius.reset <companion>";

    /// <summary>One line per loaded companion: its state (from <paramref name="describe"/>) and every variable's value.</summary>
    public static string List(VariableStore store, Func<string, string> describe)
    {
        var lines = store.Companions
            .Select(id => $"{id}: {describe(id)}; " +
                string.Join(", ", store.Definitions(id).Select(d => d.Name + "=" + VariableStore.Format(d.Type, store.Value(d)))))
            .ToList();
        return lines.Count == 0 ? "No companions loaded." : string.Join("\n", lines);
    }

    public static string Get(VariableStore store, IReadOnlyList<string> args)
    {
        if (args.Count != 1)
            return UsageGet;
        if (!TryFind(store, args[0], out var definition, out var error))
            return error;
        return $"{args[0]} = {VariableStore.Format(definition!.Type, store.Value(definition))} " +
            $"({VariableRules.Name(definition.Type)}, {(definition.Shared ? VariableRules.ScopeShared : VariableRules.ScopeLocal)}, " +
            $"default {VariableStore.Format(definition.Type, definition.Default)})";
    }

    public static string Set(VariableStore store, IReadOnlyList<string> args)
    {
        if (args.Count != 2)
            return UsageSet;
        if (!TryFind(store, args[0], out var definition, out var error))
            return error;
        if (!VariableStore.TryParse(definition!.Type, args[1], out var value))
            return $"Error: {args[0]} is a {VariableRules.Name(definition.Type)}; '{args[1]}' is not a valid value.";
        store.Set(definition.Owner, definition.Name, value);
        return $"{args[0]} = {VariableStore.Format(definition.Type, store.Value(definition))}";
    }

    public static string Reset(VariableStore store, IReadOnlyList<string> args)
    {
        if (args.Count != 1)
            return UsageReset;
        if (!store.IsLoaded(args[0]))
            return $"Error: no loaded companion '{args[0]}'.";
        return $"{args[0]}: {store.Reset(args[0])} written value(s) reset; every variable is at its default.";
    }

    private static bool TryFind(VariableStore store, string reference, out VariableDefinition? definition, out string error)
    {
        definition = null;
        error = "";
        var dot = reference.IndexOf('.');
        if (dot <= 0 || !VariableRules.TryParseReference("", reference, out var r))
        {
            error = $"Error: '{reference}' is not <companion>.<variable>.";
            return false;
        }
        if (!store.IsLoaded(r.Owner))
        {
            error = $"Error: no loaded companion '{r.Owner}'.";
            return false;
        }
        definition = store.Find(r.Owner, reference);
        if (definition == null)
            error = $"Error: companion '{r.Owner}' has no variable '{r.Name}'.";
        return definition != null;
    }
}
