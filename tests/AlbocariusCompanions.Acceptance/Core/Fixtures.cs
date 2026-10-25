using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Core;

/// <summary>Builders for valid companion data; tests mutate the JObjects to break exactly one rule.</summary>
public static class Fixtures
{
    /// <summary>companion.json; the companion id is the folder name, so <paramref name="id"/> only documents intent.</summary>
    public static JObject Companion(string id) => JObject.Parse("""
    {
      "schemaVersion": 1,
      "name": "Test Companion", "isFemale": false, "voice": "earnest", "age": 32,
      "culture": "empire", "bodyProperties": null,
      "attributes": { "vigor": 4, "control": 3, "endurance": 4, "cunning": 2, "social": 2, "intelligence": 2 },
      "skills": { "OneHanded": 100, "Athletics": 60 }, "traits": { "Valor": 1 },
      "equipment": { "templateId": "npc_wanderer_equipment_template_empire" },
      "hireCost": 500, "voiceProfile": { "reference": null, "style": null },
      "likes": [], "dislikes": [], "relations": []
    }
    """);

    /// <summary>dialogs.json with local line ids; full ids are &lt;folder&gt;_&lt;local id&gt;.</summary>
    public static JObject Dialogs(string id) => JObject.Parse("""
    {
      "schemaVersion": 1,
      "lines": [
        { "id": "intro_01", "slot": "intro", "voiced": true },
        { "id": "backstory_01", "slot": "backstory", "voiced": true },
        { "id": "hire_offer_01", "slot": "hire_offer", "voiced": true },
        { "id": "hire_accept_01", "slot": "hire_accept", "voiced": true },
        { "id": "hire_decline_01", "slot": "hire_decline", "voiced": false },
        { "id": "p_ask_about_01", "slot": "player_ask_about" },
        { "id": "p_ask_hire_01", "slot": "player_ask_hire" },
        { "id": "p_hire_pay_01", "slot": "player_hire_pay" },
        { "id": "p_hire_full_01", "slot": "player_hire_full" },
        { "id": "p_hire_later_01", "slot": "player_hire_later" }
      ]
    }
    """);

    /// <summary>lang/en.json for <see cref="Dialogs"/>: complete.</summary>
    public static JObject En() => JObject.Parse("""
    {
      "schemaVersion": 1,
      "lines": {
        "intro_01": "Greetings, Captain.",
        "backstory_01": "A placeholder past.",
        "hire_offer_01": "I could join you, Captain.",
        "hire_accept_01": "Lead on, Captain.",
        "hire_decline_01": "Another time, then.",
        "p_ask_about_01": "Tell me about yourself.",
        "p_ask_hire_01": "I could use someone like you in my company.",
        "p_hire_pay_01": "Very well. Here is {ALBOCARIUS_HIRE_COST} denars.",
        "p_hire_full_01": "I already have more companions than I can manage.",
        "p_hire_later_01": "Not now."
      }
    }
    """);

    /// <summary>lang/tr.json: one translated line, the rest fall back to English (warnings).</summary>
    public static JObject Tr() => JObject.Parse("""{ "schemaVersion": 1, "lines": { "intro_01": "Selam, Kaptan." } }""");

    /// <summary>Default language files of a fixture companion: complete en only (no warnings).</summary>
    public static Dictionary<string, JObject> Langs() => new() { ["en"] = En() };

    public static JObject Line(string id, string slot, bool voiced = true) =>
        new() { ["id"] = id, ["slot"] = slot, ["voiced"] = voiced };

    /// <summary>
    /// A small valid conversation: menu option → companion story → two player answers (one with effects) → back to the
    /// menu / end. Texts in <see cref="ConversationEn"/>.
    /// </summary>
    public static JObject Conversation() => JObject.Parse("""
    {
      "id": "old_wound",
      "entry": "hub",
      "once": true,
      "when": { "hired": true },
      "start": ["p_wound_ask"],
      "nodes": [
        { "id": "p_wound_ask", "speaker": "player", "next": ["wound_story"] },
        { "id": "wound_story", "speaker": "npc", "voiced": true, "next": ["p_wound_sorry", "p_wound_skip"] },
        { "id": "p_wound_sorry", "speaker": "player", "next": ["wound_thanks"], "effects": { "relation": 2, "setFlags": ["told_wound"] } },
        { "id": "p_wound_skip", "speaker": "player", "next": ["hub"] },
        { "id": "wound_thanks", "speaker": "npc", "next": ["end"] }
      ]
    }
    """);

    public static Dictionary<string, string> ConversationEn() => new()
    {
        ["p_wound_ask"] = "Where did that wound come from?",
        ["wound_story"] = "A border skirmish, Captain.",
        ["p_wound_sorry"] = "I'm sorry.",
        ["p_wound_skip"] = "Let's move on.",
        ["wound_thanks"] = "Thank you, Captain.",
    };

    /// <summary>Adds <paramref name="conversation"/> to dialogs.json and its texts to the English lang file.</summary>
    public static void AddConversation(JObject dialogs, Dictionary<string, JObject> langs, JObject? conversation = null,
        Dictionary<string, string>? texts = null)
    {
        if (dialogs["conversations"] is not JArray conversations)
            dialogs["conversations"] = conversations = new JArray();
        conversations.Add(conversation ?? Conversation());
        foreach (var (id, text) in texts ?? ConversationEn())
            LangLines(langs["en"])[id] = text;
    }

    public static JArray Nodes(JObject conversation) => (JArray)conversation["nodes"]!;

    public static JObject Node(JObject conversation, string id) =>
        (JObject)Nodes(conversation).First(n => (string?)n["id"] == id);

    public static JArray Lines(JObject dialogs) => (JArray)dialogs["lines"]!;

    public static JObject LangLines(JObject lang) => (JObject)lang["lines"]!;

    /// <summary>In-memory package (no disk) for validation tests.</summary>
    public static CompanionPackage Package(string folder, JObject companion, JObject dialogs, string? folderPath = null,
        Dictionary<string, JObject>? langs = null) =>
        new(folder, folderPath ?? Path.Combine(Path.GetTempPath(), "albocarius-none", folder),
            companion.ToObject<CompanionDefinition>()!, dialogs.ToObject<DialogFile>()!,
            (langs ?? Langs()).ToDictionary(kv => kv.Key, kv => kv.Value.ToObject<LangFile>()!));

    public static CompanionPackage Package(string id) => Package(id, Companion(id), Dialogs(id));

    public static void Write(string root, string folder, JObject? companion, JObject? dialogs, Dictionary<string, JObject>? langs = null)
    {
        var dir = Directory.CreateDirectory(Path.Combine(root, folder)).FullName;
        if (companion != null) File.WriteAllText(Path.Combine(dir, "companion.json"), companion.ToString(Formatting.Indented));
        if (dialogs != null) File.WriteAllText(Path.Combine(dir, "dialogs.json"), dialogs.ToString(Formatting.Indented));
        WriteLangs(dir, langs ?? Langs());
    }

    public static void WriteLangs(string dir, Dictionary<string, JObject> langs)
    {
        if (langs.Count == 0) return;
        var langDir = Directory.CreateDirectory(Path.Combine(dir, "lang")).FullName;
        foreach (var (code, lang) in langs)
            File.WriteAllText(Path.Combine(langDir, code + ".json"), lang.ToString(Formatting.Indented));
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
