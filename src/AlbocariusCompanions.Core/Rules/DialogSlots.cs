using System;
using System.Collections.Generic;
using System.Linq;

namespace AlbocariusCompanions.Core.Rules;

/// <summary>Dialog slots of dialogs.json lines (SPEC §4.3): the companion's slots and the player's (<see cref="PlayerSlots"/>).</summary>
public static class DialogSlots
{
    public const string Intro = "intro";
    public const string Backstory = "backstory";
    public const string HireOffer = "hire_offer";
    public const string HireAccept = "hire_accept";
    public const string HireDecline = "hire_decline";
    public const string LeaveThreat = "leave_threat";
    public const string RelationComplaint = "relation_complaint";
    public const string LikeReaction = "like_reaction";
    public const string DislikeReaction = "dislike_reaction";
    public const string Idle = "idle";

    /// <summary>The companion's slots of the mod's meeting/hire flow.</summary>
    public static readonly IReadOnlyList<string> RequiredCompanion = new[] { Intro, Backstory, HireOffer, HireAccept, HireDecline };

    /// <summary>Slots every companion must have at least one line for: its own and the player's side of the flow.</summary>
    public static readonly IReadOnlyList<string> Required = RequiredCompanion.Concat(PlayerSlots.All).ToArray();

    /// <summary>
    /// Optional slots: reserved ones without effect yet, then every vanilla dialog point (<see cref="VanillaDialogPoints"/>,
    /// GAME_MAP Q18) — a point with lines replaces the vanilla text for this companion, an empty one keeps vanilla.
    /// </summary>
    public static readonly IReadOnlyList<string> Optional =
        new[] { LeaveThreat, RelationComplaint, LikeReaction, DislikeReaction, Idle }.Concat(VanillaDialogPoints.Slots).ToArray();

    public static readonly IReadOnlyList<string> All = Required.Concat(Optional).ToArray();

    public static bool IsKnown(string? slot) => slot != null && All.Contains(slot, StringComparer.Ordinal);

    public static bool IsRequired(string? slot) => slot != null && Required.Contains(slot, StringComparer.Ordinal);
}

/// <summary>
/// The player's side of the mod's meeting/hire flow, per companion (M6): lines in these slots are said by the player, are
/// never voiced, and every companion needs at least one line in each.
/// </summary>
public static class PlayerSlots
{
    public const string AskAbout = "player_ask_about";
    public const string AskHire = "player_ask_hire";
    public const string HirePay = "player_hire_pay";
    public const string HireFull = "player_hire_full";
    public const string HireLater = "player_hire_later";

    public static readonly IReadOnlyList<string> All = new[] { AskAbout, AskHire, HirePay, HireFull, HireLater };

    public static bool IsKnown(string? slot) => slot != null && All.Contains(slot, StringComparer.Ordinal);
}
