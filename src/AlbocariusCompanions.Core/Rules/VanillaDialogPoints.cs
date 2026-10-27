using System;
using System.Collections.Generic;
using System.Linq;

namespace AlbocariusCompanions.Core.Rules;

/// <summary>
/// A vanilla dialog point where a wanderer or companion speaks (GAME_MAP Q18), a required companion slot (SPEC_CORE_GAPS
/// T4.0; required since 2026-10-26). The companion says one of its lines instead of the vanilla text; the vanilla condition,
/// consequence and tokens still run. The vanilla text only remains as a safety net (a slot without a usable text at runtime).
/// </summary>
public sealed class VanillaDialogPoint
{
    public VanillaDialogPoint(string slot, string englishText, params string[] sentenceIds)
    {
        Slot = slot;
        EnglishText = englishText;
        SentenceIds = sentenceIds;
    }

    public string Slot { get; }

    /// <summary>Vanilla ConversationSentence ids replaced by this slot.</summary>
    public IReadOnlyList<string> SentenceIds { get; }

    /// <summary>The vanilla English text (plain, no {=id}); the companion template's starting text for the slot.</summary>
    public string EnglishText { get; }
}

public static class VanillaDialogPoints
{
    public static readonly IReadOnlyList<VanillaDialogPoint> All = new[]
    {
        // Greetings and the conversation hub (LordConversationsCampaignBehavior).
        new VanillaDialogPoint("greet_recent", "{SALUTATION}...", "start_default_under_24_hours"),
        new VanillaDialogPoint("greet", "{STR_SALUTATION}. It's been a while.", "start_default"),
        new VanillaDialogPoint("main_hub", "So, then. What is it?", "hero_ask_4"),
        new VanillaDialogPoint("anything_else", "Is there anything else?", "hero_pretalk_2"),
        new VanillaDialogPoint("farewell", "Until next time, then.", "hero_leave"),
        new VanillaDialogPoint("job_status", "Do you have any orders for your alley, {?CONVERSATION_NPC.GENDER}madam{?}sir{\\?}", "wanderer_job_status_1"),

        // Roles, missions and rejoining (CompanionRolesCampaignBehavior).
        new VanillaDialogPoint("role_pretalk", "Yes?", "companion_pretalk"),
        new VanillaDialogPoint("role_ask", "What role do you have in mind?", "companion_assign_new_role"),
        new VanillaDialogPoint("role_ok", "Very well.", "companion_role_response", "companion_role_unassign_response"),
        new VanillaDialogPoint("role_honored", "I would be honored.", "companion_role_response_2"),
        new VanillaDialogPoint("role_too_many", "I already have quite a few duties. Perhaps you could relieve me of one of them, so that I can take on this new responsibility?", "companion_have_too_many_roles"),
        new VanillaDialogPoint("mission_offer", "I have a mission for you...", "companion_start_mission"),
        new VanillaDialogPoint("mission_ask", "What do you want me to do?", "companion_pretalk_2"),
        new VanillaDialogPoint("forage_ask", "As you wish. Will I take some troops with me?", "companion_forage_1"),
        new VanillaDialogPoint("rejoin_ok", "As you wish.", "companion_rejoin"),

        // Dismissal (CompanionRolesCampaignBehavior; the mod keeps the hero, GAME_MAP Q19).
        new VanillaDialogPoint("fire_reaction", "What? Why? Did I do something wrong?[ib:closed]", "companion_fire"),
        new VanillaDialogPoint("fire_farewell", "Well... I don't know what to say. Goodbye, then.[ib:nervous2]", "companion_fire_farewell"),
        new VanillaDialogPoint("fire_cancel_reply", "Well {PLAYER.NAME}, it is certainly good to see you still retain your sense of humor.[if:convo_nervous][ib:normal2]", "companion_fire_cancel2"),

        // Becoming a lord (CompanionRolesCampaignBehavior).
        new VanillaDialogPoint("lord_offer_busy", "I would be honored, my {?PLAYER.GENDER}lady{?}lord{\\?}. But I can't take on any new responsibilities while leading this caravan. If you wish to relieve me of my duties, we can discuss this further.", "turn_companion_to_lord_start_answer_2"),
        new VanillaDialogPoint("lord_offer_accept", "Thank you, my {?PLAYER.GENDER}lady{?}lord{\\?}. I have often thought about that. If I had a fief, with revenues, and perhaps a title to go with it, I could marry well and pass my wealth down to my heirs, and of course raise troops to help defend the realm.", "turn_companion_to_lord_start_answer"),
        new VanillaDialogPoint("lord_no_fief", "My {?PLAYER.GENDER}lady{?}lord{\\?}, as much as I appreciate the gesture, I am not sure that you have a suitable estate to grant me.", "turn_companion_to_lord_has_no_fief"),
        new VanillaDialogPoint("lord_no_fief_ok", "As you wish, my {?PLAYER.GENDER}lady{?}lord{\\?}.", "turn_companion_to_lord_has_no_fief_companion_answer"),
        new VanillaDialogPoint("lord_where", "Where exactly did you have in mind?[if:convo_happy]", "turn_companion_to_lord_has_fief_answer"),
        new VanillaDialogPoint("lord_fief_selected", "{SETTLEMENT_NAME}? This is a great honor, my {?PLAYER.GENDER}lady{?}lord{\\?}. I will protect it until the last drop of my blood.[ib:hip][if:convo_happy]", "turn_companion_to_lord_fief_selected"),
        new VanillaDialogPoint("lord_name_house", "My {?PLAYER.GENDER}lady{?}lord{\\?}, it would be an honor if you were to choose the name of my noble house.", "turn_companion_to_lord_fief_selected_confirm_box"),
        new VanillaDialogPoint("lord_thanks", "Thank you my {?PLAYER.GENDER}lady{?}lord{\\?}. I will always remember this grand gesture.[ib:hip][if:convo_happy]", "turn_companion_to_lord_done_answer_thanks"),
        new VanillaDialogPoint("lord_rejected", "It's only normal that you have second thoughts. I will be right by your side if you change your mind, my {?PLAYER.GENDER}lady{?}lord{\\?}.", "turn_companion_to_lord_done_answer_rejected"),

        // Grievances (CompanionGrievanceBehavior).
        new VanillaDialogPoint("grievance_start", "{TITLE}, there is something I wish to discuss with you.", "companion_start_grievance"),
        new VanillaDialogPoint("grievance_repeat", "{TITLE}... {GRIEVANCE_SHORT_DESCRIPTION}", "companion_repeat_grievance"),
        new VanillaDialogPoint("grievance_desert", "I don't like running away from a battle like that.", "companion_continue_grievance_desert_softspoken"),
        new VanillaDialogPoint("grievance_wage", "I should remind you that I expect to be paid as you had promised.", "companion_continue_grievance_wage_softspoken"),
        new VanillaDialogPoint("grievance_starve", "I hear we're running low on food. We should watch our stocks better.", "companion_continue_grievance_starve_softspoken"),
        new VanillaDialogPoint("grievance_raid", "Pillaging villages is not what I signed up for.", "companion_continue_grievance_raid_softspoken"),

        // Rescue and prison break (CompanionRolesCampaignBehavior, SandBox PrisonBreakCampaignBehavior).
        new VanillaDialogPoint("rescue_thanks", "{SALUTATION}... Thank you for freeing me.", "rescue_companion_start"),
        new VanillaDialogPoint("rescue_what_now", "What now?", "rescue_companion_preoptions"),
        new VanillaDialogPoint("rescue_join_reply", "All right. It's good to be back.", "rescue_companion_join_party_answer"),
        new VanillaDialogPoint("rescue_lead_reply", "Tell me who to command.", "rescue_companion_lead_party_answer"),
        new VanillaDialogPoint("rescue_alone_reply", "I will go off on my own, then. I can stay busy. But I'll remember - I owe you one!", "rescue_companion_do_nothing_answer"),
        new VanillaDialogPoint("rescue_party_ok", "As you wish.", "rescue_companion_lead_party_create_party_continue_0"),
        new VanillaDialogPoint("rescue_party_what", "So, what shall I do?", "rescue_companion_lead_party_create_party_continue_1"),
        new VanillaDialogPoint("rescue_party_go", "We'll make ready to set out at once.", "rescue_companion_lead_party_create_party_continue_2"),
        new VanillaDialogPoint("prison_break_thanks", "{SALUTATION}... You came for me!", "prison_break_start_1", "prison_break_start_2"),
        new VanillaDialogPoint("prison_break_rejoin", "I'm ready to rejoin you. I'm in your debt.", "prison_break_next_companion"),

        // Following the player in a settlement (SandBox ClanMemberRolesCampaignBehavior).
        new VanillaDialogPoint("follow_ok", "Lead the way.", "clan_member_follow_accept"),
        new VanillaDialogPoint("gather_ok", "I shall do that.", "clan_member_gather_clan_members_accept"),
        new VanillaDialogPoint("follow_stop_ok", "As you wish.", "clan_member_dont_follow_accept", "clan_members_dont_follow_accept"),
    };

    private static readonly Dictionary<string, VanillaDialogPoint> BySentence =
        All.SelectMany(p => p.SentenceIds.Select(id => (id, p))).ToDictionary(x => x.id, x => x.p, StringComparer.Ordinal);

    public static IReadOnlyList<string> Slots { get; } = All.Select(p => p.Slot).ToArray();

    /// <summary>Sentence ids the mod registers itself start with this ("albocarius_…" fixed lines, "albocarius:…" conversations).</summary>
    public const string OwnSentencePrefix = "albocarius";

    /// <summary>
    /// What an NPC sentence said by a mod companion is: one of the mod's own, a vanilla point covered by a slot, or a vanilla
    /// (or other mod's) line no slot covers — the last one shows text the mod did not write and must be reported.
    /// </summary>
    public static SentenceKind Classify(string? sentenceId) =>
        sentenceId != null && sentenceId.StartsWith(OwnSentencePrefix, StringComparison.Ordinal) ? SentenceKind.Own
        : ForSentence(sentenceId) != null ? SentenceKind.Slot
        : SentenceKind.Unmapped;

    /// <summary>
    /// What a vanilla-text warning is reported once per session for (SPEC_SELECTION_AND_EVENTS I5): the slot of a covered
    /// point (several sentences can share a slot), else the sentence itself.
    /// </summary>
    public static string WarningKey(string? sentenceId) =>
        ForSentence(sentenceId) is { } point ? "slot:" + point.Slot : "sentence:" + sentenceId;

    /// <summary>The point that replaces a vanilla sentence id, or null.</summary>
    public static VanillaDialogPoint? ForSentence(string? sentenceId) =>
        sentenceId != null && BySentence.TryGetValue(sentenceId, out var point) ? point : null;
}

public enum SentenceKind
{
    Own,
    Slot,
    Unmapped,
}
