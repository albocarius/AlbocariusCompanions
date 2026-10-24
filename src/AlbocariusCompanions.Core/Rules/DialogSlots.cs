using System;
using System.Collections.Generic;
using System.Linq;

namespace AlbocariusCompanions.Core.Rules;

/// <summary>Companion dialog slots (SPEC §4.3); with <see cref="PlayerSlots"/> the global slot list.</summary>
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

    /// <summary>Slots every companion must have at least one line for in this run.</summary>
    public static readonly IReadOnlyList<string> Required = new[] { Intro, Backstory, HireOffer, HireAccept, HireDecline };

    public static readonly IReadOnlyList<string> Optional = new[] { LeaveThreat, RelationComplaint, LikeReaction, DislikeReaction, Idle };

    public static readonly IReadOnlyList<string> All = Required.Concat(Optional).ToArray();

    public static bool IsKnown(string? slot) => slot != null && All.Contains(slot, StringComparer.Ordinal);

    public static bool IsRequired(string? slot) => slot != null && Required.Contains(slot, StringComparer.Ordinal);
}

/// <summary>Slots of the player's lines (_shared/player_lines.json); every one needs at least one line.</summary>
public static class PlayerSlots
{
    public const string AskAbout = "ask_about";
    public const string AskHire = "ask_hire";
    public const string HirePay = "hire_pay";
    public const string HireFull = "hire_full";
    public const string HireLater = "hire_later";

    public static readonly IReadOnlyList<string> All = new[] { AskAbout, AskHire, HirePay, HireFull, HireLater };

    public static bool IsKnown(string? slot) => slot != null && All.Contains(slot, StringComparer.Ordinal);
}
