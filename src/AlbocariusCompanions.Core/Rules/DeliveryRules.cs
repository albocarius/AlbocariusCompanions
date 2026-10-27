using System.Collections.Generic;

namespace AlbocariusCompanions.Core.Rules;

/// <summary>
/// Trigger delivery (SPEC_TRIGGERS_DELIVERY T3/T5) and its mod-wide limits. Values set by the user at the M11 plan gate
/// (2026-10-27); documented in docs/TRIGGERS.md and docs/SCHEMA.md.
/// </summary>
public static class DeliveryRules
{
    public const string Conversation = "conversation";
    public const string Notification = "notification";
    public const string Inquiry = "inquiry";

    public static readonly IReadOnlyList<string> Deliveries = new[] { Conversation, Notification, Inquiry };

    public const double DefaultChance = 1;
    public const double DefaultExpiresAfterHours = 24;
    public const double DefaultCooldownDays = 0;

    /// <summary>In-game days between two interruptions (conversation or inquiry) by the same companion.</summary>
    public const double InterruptionCooldownDays = 3;
    /// <summary>Interruptions (conversations + inquiries, all companions) per in-game day.</summary>
    public const int InterruptionsPerDay = 1;
    /// <summary>Notifications (all companions) per in-game day; counted apart from interruptions.</summary>
    public const int NotificationsPerDay = 3;

    /// <summary>Inquiries with at most this many options use the standard pop-up; more use the multi-choice one (user, gate).</summary>
    public const int StandardInquiryMaxOptions = 2;

    /// <summary>partyMorale effect range (Recent Events morale, GAME_MAP Q22).</summary>
    public const int PartyMoraleMin = -20;
    public const int PartyMoraleMax = 20;
}
