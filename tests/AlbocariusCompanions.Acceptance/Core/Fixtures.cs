using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Core;

/// <summary>Builders for valid companion data; tests mutate the JObjects to break exactly one rule.</summary>
public static class Fixtures
{
    public static JObject Companion(string id) => JObject.Parse($$"""
    {
      "schemaVersion": 1,
      "id": "{{id}}", "name": "Test Companion", "isFemale": false, "voice": "earnest", "age": 32,
      "culture": "empire", "bodyProperties": null,
      "attributes": { "vigor": 4, "control": 3, "endurance": 4, "cunning": 2, "social": 2, "intelligence": 2 },
      "skills": { "OneHanded": 100, "Athletics": 60 }, "traits": { "Valor": 1 },
      "equipment": { "templateId": "npc_wanderer_equipment_template_empire" },
      "hireCost": 500, "voiceProfile": { "reference": null, "style": null },
      "likes": [], "dislikes": [], "relations": []
    }
    """);

    public static JObject Dialogs(string id) => JObject.Parse($$"""
    {
      "schemaVersion": 1,
      "lines": [
        { "id": "{{id}}_intro_01", "slot": "intro", "text": { "en": "Greetings, Captain.", "tr": "Selam, Kaptan." }, "voiced": true },
        { "id": "{{id}}_backstory_01", "slot": "backstory", "text": { "en": "A placeholder past." }, "voiced": true },
        { "id": "{{id}}_hire_offer_01", "slot": "hire_offer", "text": { "en": "I could join you, Captain." }, "voiced": true },
        { "id": "{{id}}_hire_accept_01", "slot": "hire_accept", "text": { "en": "Lead on, Captain." }, "voiced": true },
        { "id": "{{id}}_hire_decline_01", "slot": "hire_decline", "text": { "en": "Another time, then." }, "voiced": false }
      ]
    }
    """);

    public static JObject Line(string id, string slot, string en, bool voiced = true) =>
        new() { ["id"] = id, ["slot"] = slot, ["text"] = new JObject { ["en"] = en }, ["voiced"] = voiced };

    public static JArray Lines(JObject dialogs) => (JArray)dialogs["lines"]!;

    /// <summary>In-memory package (no disk) for validation tests.</summary>
    public static CompanionPackage Package(string folder, JObject companion, JObject dialogs, string? folderPath = null) =>
        new(folder, folderPath ?? Path.Combine(Path.GetTempPath(), "albocarius-none", folder),
            companion.ToObject<CompanionDefinition>()!, dialogs.ToObject<DialogFile>()!);

    public static CompanionPackage Package(string id) => Package(id, Companion(id), Dialogs(id));

    public static void Write(string root, string folder, JObject? companion, JObject? dialogs)
    {
        var dir = Directory.CreateDirectory(Path.Combine(root, folder)).FullName;
        if (companion != null) File.WriteAllText(Path.Combine(dir, "companion.json"), companion.ToString(Formatting.Indented));
        if (dialogs != null) File.WriteAllText(Path.Combine(dir, "dialogs.json"), dialogs.ToString(Formatting.Indented));
    }

    public static void WriteValid(string root, string id) => Write(root, id, Companion(id), Dialogs(id));

    public static IEnumerable<ValidationIssue> Errors(this IEnumerable<ValidationIssue> issues) => issues.Where(i => i.IsError);
}

public sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("albocarius-test-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
    }
}
