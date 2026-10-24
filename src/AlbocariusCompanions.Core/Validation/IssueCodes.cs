namespace AlbocariusCompanions.Core.Validation;

/// <summary>Stable issue codes. Tests and logs rely on these strings.</summary>
public static class IssueCodes
{
    // Loader
    public const string MissingCompanionFile = "missing_companion_file";
    public const string MissingDialogsFile = "missing_dialogs_file";
    public const string InvalidJson = "invalid_json";
    public const string RootNotFound = "root_not_found";

    // companion.json
    public const string InvalidId = "invalid_id";
    public const string DuplicateId = "duplicate_id";
    public const string IdFolderMismatch = "id_folder_mismatch";
    public const string MissingField = "missing_field";
    public const string OutOfRange = "out_of_range";
    public const string UnknownAttribute = "unknown_attribute";
    public const string InvalidVoice = "invalid_voice";
    public const string SameAsFallback = "same_as_fallback";
    public const string InvalidReaction = "invalid_reaction";
    public const string InvalidRelation = "invalid_relation";
    public const string UnknownRelationTarget = "unknown_relation_target";

    // dialogs.json
    public const string InvalidLineId = "invalid_line_id";
    public const string DuplicateLineId = "duplicate_line_id";
    public const string UnknownSlot = "unknown_slot";
    public const string MissingRequiredSlot = "missing_required_slot";
    public const string MissingEnglishText = "missing_en_text";
    public const string InvalidLanguageCode = "invalid_language_code";
    public const string PlayerTokenInVoicedLine = "player_token_in_voiced_line";
    public const string InvalidAudioPath = "invalid_audio_path";
}
