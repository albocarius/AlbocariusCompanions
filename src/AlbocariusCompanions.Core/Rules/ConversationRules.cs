using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AlbocariusCompanions.Core.Rules;

/// <summary>Fixed words and limits of dialogs.json "conversations" (M6).</summary>
public static class ConversationRules
{
    /// <summary>Start player node(s) are options in the companion's conversation menu.</summary>
    public const string EntryHub = "hub";
    /// <summary>A start NPC node opens the conversation when the player talks to the (already met) companion.</summary>
    public const string EntryGreeting = "greeting";
    public static readonly IReadOnlyList<string> Entries = new[] { EntryHub, EntryGreeting };

    public const string SpeakerNpc = "npc";
    public const string SpeakerPlayer = "player";
    public static readonly IReadOnlyList<string> Speakers = new[] { SpeakerNpc, SpeakerPlayer };

    /// <summary>"next" target: back to the companion's conversation menu.</summary>
    public const string NextHub = "hub";
    /// <summary>"next" target: the conversation closes.</summary>
    public const string NextEnd = "end";
    public static bool IsReservedNext(string? id) => id == NextHub || id == NextEnd;

    /// <summary>Flag names: [a-z0-9_]+, scoped to the companion.</summary>
    public static readonly Regex FlagPattern = new Regex("^[a-z0-9_]+$");

    /// <summary>The game's relation range (CharacterRelationManager clamps to it).</summary>
    public const int MinRelation = -100;
    public const int MaxRelation = 100;
}
