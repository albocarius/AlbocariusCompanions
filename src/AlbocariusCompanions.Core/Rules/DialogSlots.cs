using System;
using System.Collections.Generic;
using System.Linq;

namespace AlbocariusCompanions.Core.Rules;

/// <summary>The single global list of dialog slots (SPEC §4.3).</summary>
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
