namespace AlbocariusCompanions.Core.Validation;

/// <summary>Stable issue codes. Tests and logs rely on these strings.</summary>
public static class IssueCodes
{
    // Loader
    public const string MissingCompanionFile = "missing_companion_file";
    public const string MissingDialogsFile = "missing_dialogs_file";
    public const string InvalidJson = "invalid_json";
    public const string MissingSchemaVersion = "missing_schema_version";
    public const string InvalidSchemaVersion = "invalid_schema_version";
    public const string UnsupportedSchemaVersion = "unsupported_schema_version";
    public const string RootNotFound = "root_not_found";
    public const string ObsoleteSharedFolder = "obsolete_shared_folder";

    // companion.json
    public const string InvalidId = "invalid_id";
    public const string MissingField = "missing_field";
    public const string OutOfRange = "out_of_range";
    public const string UnknownAttribute = "unknown_attribute";
    public const string InvalidVoice = "invalid_voice";
    public const string InvalidRequiredModule = "invalid_required_module";
    public const string InvalidReaction = "invalid_reaction";
    public const string InvalidRelation = "invalid_relation";
    public const string UnknownRelationTarget = "unknown_relation_target";
    public const string UnknownEquipmentSlot = "unknown_equipment_slot";
    public const string InvalidEquipmentItem = "invalid_equipment_item";

    // dialogs.json, lang files
    public const string InvalidLineId = "invalid_line_id";
    public const string DuplicateLineId = "duplicate_line_id";
    public const string UnknownSlot = "unknown_slot";
    public const string MissingRequiredSlot = "missing_required_slot";
    public const string MissingEnglishText = "missing_en_text";
    public const string MissingTranslation = "missing_translation";
    public const string UnknownTranslationId = "unknown_translation_id";
    public const string InvalidLanguageCode = "invalid_language_code";
    public const string PlayerTokenInVoicedLine = "player_token_in_voiced_line";
    public const string InvalidAudioPath = "invalid_audio_path";
    public const string InvalidAudioExtension = "invalid_audio_extension";
    public const string VoicedPlayerLine = "voiced_player_line";

    // dialogs.json conversations
    public const string InvalidConversation = "invalid_conversation";
    public const string UnknownNode = "unknown_node";
    public const string InvalidCondition = "invalid_condition";
    public const string InvalidEffect = "invalid_effect";
    public const string UnreachableNode = "unreachable_node";
    public const string ConversationMayStall = "conversation_may_stall";
    public const string ConversationNoExit = "conversation_no_exit";
    /// <summary>Warning: a non-"once" conversation path can reward the player every time (I5).</summary>
    public const string RepeatableReward = "repeatable_reward";
    /// <summary>Warning: a transfer whose source is not "world" and no condition on its path checks that source for (I5).</summary>
    public const string TransferNotChecked = "transfer_not_checked";

    // dialogs.json variables (M7)
    public const string InvalidVariable = "invalid_variable";
    public const string UnknownVariable = "unknown_variable";
    public const string LocalVariable = "local_variable";
    public const string VariableTypeMismatch = "variable_type_mismatch";
    /// <summary>Warning: a reference to a variable of a companion that is not loaded (the condition is false, the write skipped).</summary>
    public const string MissingCompanionReference = "missing_companion_reference";
    /// <summary>Warning: a declared variable nothing writes and whose default is the type default, so it never changes (I5).</summary>
    public const string VariableNeverChanges = "variable_never_changes";

    // Runtime resolution (SPEC rule 9)
    public const string CultureMissing = "culture_missing";
    public const string EquipmentMissing = "equipment_missing";
    public const string SkillMissing = "skill_missing";
    public const string TraitMissing = "trait_missing";
    public const string ItemMissing = "item_missing";
    public const string ItemDoesNotFitSlot = "item_does_not_fit_slot";
    public const string FixedSetEmpty = "fixed_set_empty";
}
