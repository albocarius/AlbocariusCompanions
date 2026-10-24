using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Core.Loading;

/// <summary>
/// SPEC_CORE_GAPS T2.4: every JSON document carries an integer "schemaVersion" at the top. This is the ONE place in
/// the codebase that checks versions; later loaders (player_lines.json, language files) call <see cref="Check"/> too.
/// </summary>
public static class SchemaVersions
{
    /// <summary>The only schema version this build of the mod understands.</summary>
    public const int Current = 1;

    /// <summary>Name of the field every JSON document carries the version in.</summary>
    public const string PropertyName = "schemaVersion";

    /// <summary>
    /// Checks the schemaVersion token of a parsed document for one folder + file. Returns the loader error when the
    /// field is absent/null (missing), not an integer (invalid) or an integer other than <see cref="Current"/>
    /// (unsupported); null when the version is supported. Never throws.
    /// </summary>
    public static ValidationIssue? Check(JObject document, string folder, string fileName)
    {
        var where = $"{folder}/{fileName}";
        var token = document[PropertyName];

        if (token == null || token.Type == JTokenType.Null)
            return ValidationIssue.Error(IssueCodes.MissingSchemaVersion, folder,
                $"{where} is missing the required {PropertyName} field (must be the integer {Current}).");

        if (token.Type != JTokenType.Integer)
            return ValidationIssue.Error(IssueCodes.InvalidSchemaVersion, folder,
                $"{where} has an invalid {PropertyName}: found {Describe(token)}, but {PropertyName} must be the integer {Current}.");

        // An integer token holds an int/long (BigInteger when it does not fit in long); only a value exactly Current passes.
        if (token is JValue { Value: System.IConvertible number })
        {
            try
            {
                if (System.Convert.ToInt64(number) == Current)
                    return null;
            }
            catch (System.OverflowException)
            {
                // a number too large for long is by definition not Current
            }
        }

        return ValidationIssue.Error(IssueCodes.UnsupportedSchemaVersion, folder,
            $"{where} has {PropertyName} {token}; this version of the mod supports {PropertyName} {Current}.");
    }

    private static string Describe(JToken token) => token.Type switch
    {
        JTokenType.String => $"the string \"{token}\"",
        JTokenType.Float => $"the number {token}",
        JTokenType.Boolean => "a boolean",
        JTokenType.Array => "an array",
        JTokenType.Object => "an object",
        _ => token.Type.ToString().ToLowerInvariant(),
    };
}
