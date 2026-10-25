using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;

namespace AlbocariusCompanions.Core.Dialogs;

/// <summary>
/// Transfer effects (I3) and their actors: "player", "self" (the companion being talked to), "world" (source/sink: gold
/// or items appear / disappear) or another companion's id (a companion's own id names the same actor as "self").
/// Gold moves each hero's own gold; items move through the player's party inventory, so only "player" and "world" are
/// meaningful for items: companions in the party share that inventory and the others have none (GAME_MAP Q29).
/// </summary>
public static class Transfers
{
    public const string Player = "player";
    public const string Self = "self";
    public const string World = "world";

    /// <summary>Valid actor name: one of the fixed words or a companion id ([a-z0-9_]+), case-sensitive.</summary>
    public static bool IsValidActor(string? actor) =>
        actor == Player || actor == Self || actor == World || actor != null && CompanionRules.IdPattern.IsMatch(actor);

    /// <summary>A companion's own id names the same actor as "self"; every other name is returned unchanged.</summary>
    public static string? NormalizeActor(string? actor, string? ownId) =>
        actor != null && ownId != null && actor == ownId ? Self : actor;

    /// <summary>
    /// The transfer of a node's effects: the explicit one when set (the same instance), else the old "gold" key as
    /// sugar for a "player"/"world" gold transfer (negative = player → world, positive = world → player);
    /// null when the node transfers nothing.
    /// </summary>
    public static TransferEffect? Of(EffectSet? effects)
    {
        if (effects == null)
            return null;
        if (effects.Transfer != null)
            return effects.Transfer;
        if (effects.Gold is { } gold && gold != 0)
            return gold < 0
                ? new TransferEffect { Gold = -gold, From = Player, To = World }
                : new TransferEffect { Gold = gold, From = World, To = Player };
        return null;
    }

    /// <summary>Number of items transferred; 1 when "count" is not given.</summary>
    public static int CountOf(TransferEffect t) => t.Count ?? 1;
}
