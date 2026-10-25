using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Registry;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 69 (M7, SPEC_BEHAVIOR_FOUNDATION I1/I2): declared variables, condition combinators, variable effects, the store.
public class VariableTests
{
    const string Id = "test_companion";

    /// <summary>The fixture conversation plus declarations; <paramref name="mutate"/> edits the conversation.</summary>
    static IReadOnlyList<ValidationIssue> Validate(Action<JObject>? mutate = null, JObject? variables = null)
    {
        var d = Fixtures.Dialogs(Id);
        var langs = Fixtures.Langs();
        var conversation = Fixtures.Conversation();
        mutate?.Invoke(conversation);
        Fixtures.AddConversation(d, langs, conversation);
        foreach (var pair in variables ?? new JObject())
            d["variables"]![pair.Key] = pair.Value;
        return DialogChecks.Validate(new[] { Fixtures.Package(Id, Fixtures.Companion(Id), d, langs: langs) });
    }

    static JObject Vars(params (string Name, string Type)[] vars)
    {
        var o = new JObject();
        foreach (var (name, type) in vars)
            o[name] = new JObject { ["type"] = type };
        return o;
    }

    static void When(JObject c, JObject when) => Fixtures.Node(c, "p_wound_skip")["when"] = when;
    static void Effects(JObject c, JObject effects) => Fixtures.Node(c, "p_wound_skip")["effects"] = effects;

    static void SingleError(IReadOnlyList<ValidationIssue> issues, string code)
    {
        var error = Assert.Single(issues.Errors());
        Assert.Equal(code, error.Code);
    }

    // ---- declarations ----

    [Fact]
    public void Valid_declarations_have_no_issues() => Assert.Empty(Validate(variables: JObject.Parse("""
        {
          "told": { "type": "bool", "scope": "shared" },
          "seen": { "type": "int", "scope": "local", "default": 3 },
          "angry": { "type": "bool", "default": true }
        }
        """)));

    [Theory]
    [InlineData("""{ "Bad-Name": { "type": "bool" } }""")]
    [InlineData("""{ "mood": { "type": "int" } }""")]
    [InlineData("""{ "x1": { "type": "string" } }""")]
    [InlineData("""{ "x1": { } }""")]
    [InlineData("""{ "x1": { "type": "bool", "scope": "global" } }""")]
    [InlineData("""{ "x1": { "type": "bool", "default": 1 } }""")]
    [InlineData("""{ "x1": { "type": "int", "default": true } }""")]
    [InlineData("""{ "x1": { "type": "int", "default": 1.5 } }""")]
    public void Invalid_declaration_is_an_error(string variables) =>
        SingleError(Validate(variables: JObject.Parse(variables)), IssueCodes.InvalidVariable);

    [Fact]
    public void Unknown_field_in_a_declaration_is_a_load_error()
    {
        using var tmp = new TempDir();
        var d = Fixtures.Dialogs(Id);
        d["variables"] = JObject.Parse("""{ "x1": { "type": "bool", "owner": "me" } }""");
        Fixtures.Write(tmp.Path, Id, Fixtures.Companion(Id), d, Fixtures.Langs());
        var result = CompanionCatalog.Load(tmp.Path);
        Assert.Contains(result.Issues, i => i.IsError && i.Folder == Id);
        Assert.Equal(0, result.Registry.Count);
    }

    // ---- uses of the companion's own variables ----

    [Theory]
    [InlineData("""{ "flags": ["nope"] }""")]
    [InlineData("""{ "notFlags": ["nope"] }""")]
    [InlineData("""{ "var": "nope", "min": 1 }""")]
    [InlineData("""{ "any": [ { "flags": ["nope"] }, { "hired": true } ] }""")]
    [InlineData("""{ "not": { "all": [ { "var": "nope", "eq": true } ] } }""")]
    public void Undeclared_variable_in_a_condition_is_an_error(string when) =>
        SingleError(Validate(c => When(c, JObject.Parse(when))), IssueCodes.UnknownVariable);

    [Theory]
    [InlineData("""{ "setFlags": ["nope"] }""")]
    [InlineData("""{ "clearFlags": ["nope"] }""")]
    [InlineData("""{ "set": { "nope": 2 } }""")]
    [InlineData("""{ "add": { "nope": 1 } }""")]
    public void Undeclared_variable_in_an_effect_is_an_error(string effects) =>
        SingleError(Validate(c => Effects(c, JObject.Parse(effects))), IssueCodes.UnknownVariable);

    [Fact]
    public void Built_in_mood_needs_no_declaration() => Assert.Empty(Validate(c =>
    {
        When(c, JObject.Parse("""{ "var": "mood", "min": -5, "max": 5 }"""));
        Effects(c, JObject.Parse("""{ "add": { "mood": -1 } }"""));
    }));

    [Theory]
    [InlineData("""{ "flags": ["seen"] }""")]
    [InlineData("""{ "var": "told", "min": 1 }""")]
    [InlineData("""{ "var": "seen", "eq": true }""")]
    [InlineData("""{ "var": "told", "eq": 1 }""")]
    [InlineData("""{ "var": "told", "eq": "yes" }""")]
    public void Wrong_type_in_a_condition_is_an_error(string when) =>
        SingleError(Validate(c => When(c, JObject.Parse(when)), Vars(("told", "bool"), ("seen", "int"))), IssueCodes.VariableTypeMismatch);

    [Theory]
    [InlineData("""{ "setFlags": ["seen"] }""")]
    [InlineData("""{ "set": { "told": 3 } }""")]
    [InlineData("""{ "set": { "seen": false } }""")]
    [InlineData("""{ "add": { "told": 1 } }""")]
    [InlineData("""{ "set": { "seen": 1.5 } }""")]
    public void Wrong_type_in_an_effect_is_an_error(string effects) =>
        SingleError(Validate(c => Effects(c, JObject.Parse(effects)), Vars(("told", "bool"), ("seen", "int"))), IssueCodes.VariableTypeMismatch);

    [Fact]
    public void Every_condition_and_effect_form_together_is_valid() => Assert.Empty(Validate(c =>
    {
        When(c, JObject.Parse("""
            {
              "hired": true,
              "flags": ["told"],
              "all": [ { "var": "seen", "min": 1 }, { "var": "seen", "max": 9 } ],
              "any": [ { "var": "told", "eq": true }, { "var": "seen", "eq": 4 } ],
              "not": { "notFlags": ["told"] }
            }
            """));
        Effects(c, JObject.Parse("""{ "setFlags": ["told"], "set": { "seen": 0 }, "add": { "mood": 2 } }"""));
    }, Vars(("told", "bool"), ("seen", "int"))));

    // ---- condition shape ----

    [Theory]
    [InlineData("""{ "var": "seen" }""")]
    [InlineData("""{ "min": 1 }""")]
    [InlineData("""{ "eq": 1 }""")]
    [InlineData("""{ "var": "seen", "eq": 1, "min": 0 }""")]
    [InlineData("""{ "var": "seen", "min": 5, "max": 4 }""")]
    [InlineData("""{ "any": [] }""")]
    [InlineData("""{ "all": [] }""")]
    [InlineData("""{ "all": [ null ] }""")]
    [InlineData("""{ "flags": ["a.b.c"] }""")]
    [InlineData("""{ "not": { "minRelation": 500 } }""")]
    public void Malformed_condition_is_an_error(string when) =>
        SingleError(Validate(c => When(c, JObject.Parse(when)), Vars(("seen", "int"))), IssueCodes.InvalidCondition);

    [Theory]
    [InlineData("""{ "add": { "seen": 0 } }""")]
    [InlineData("""{ "set": { "seen": 1 }, "add": { "seen": 1 } }""")]
    [InlineData("""{ "setFlags": ["told"], "set": { "told": false } }""")]
    [InlineData("""{ "setFlags": ["Bad-Name"] }""")]
    public void Malformed_effect_is_an_error(string effects) =>
        SingleError(Validate(c => Effects(c, JObject.Parse(effects)), Vars(("seen", "int"), ("told", "bool"))), IssueCodes.InvalidEffect);

    // ---- other companions' variables (catalog, from disk) ----

    /// <summary>Writes a companion whose fixture conversation has <paramref name="when"/> on the conversation itself.</summary>
    static void WriteCompanion(string root, string id, JObject? when = null, JObject? variables = null, JObject? effects = null,
        Action<JObject>? companion = null)
    {
        var d = Fixtures.Dialogs(id);
        var langs = Fixtures.Langs();
        var conversation = Fixtures.Conversation();
        if (when != null)
            conversation["when"] = when;
        if (effects != null)
            Fixtures.Node(conversation, "p_wound_skip")["effects"] = effects;
        Fixtures.AddConversation(d, langs, conversation);
        foreach (var pair in variables ?? new JObject())
            d["variables"]![pair.Key] = pair.Value;
        var c = Fixtures.Companion(id);
        companion?.Invoke(c);
        Fixtures.Write(root, id, c, d, langs);
    }

    static readonly JObject SharedTold = JObject.Parse("""{ "told_scar": { "type": "bool", "scope": "shared" } }""");
    static JObject NeedsJeremus => JObject.Parse("""{ "flags": ["jeremus.told_scar"] }""");

    [Fact]
    public void Shared_variable_of_a_loaded_companion_can_be_read_and_written()
    {
        using var tmp = new TempDir();
        WriteCompanion(tmp.Path, "jeremus", variables: SharedTold);
        WriteCompanion(tmp.Path, "ahmet", when: NeedsJeremus, effects: JObject.Parse("""{ "clearFlags": ["jeremus.told_scar"] }"""));

        var result = CompanionCatalog.Load(tmp.Path);

        Assert.Empty(result.Issues);
        Assert.Equal(2, result.Registry.Count);
    }

    [Fact]
    public void Missing_companion_is_a_warning_the_referencing_companion_loads_and_the_conversation_is_not_offered()
    {
        using var tmp = new TempDir();
        WriteCompanion(tmp.Path, "ahmet", when: NeedsJeremus);

        var result = CompanionCatalog.Load(tmp.Path);

        var warning = Assert.Single(result.Issues);
        Assert.Equal(IssueCodes.MissingCompanionReference, warning.Code);
        Assert.False(warning.IsError);
        Assert.Equal("ahmet", warning.Folder);
        Assert.True(result.Registry.TryGet("ahmet", out var ahmet));

        var store = new VariableStore(result.Registry.All);
        var conversation = ahmet!.Conversations.Single();
        var start = ConversationGraph.Build("ahmet", conversation).Single(e => e.IsStart);
        Assert.False(ConversationConditions.CanSay(conversation, start, new StoreContext(store, "ahmet"), alreadyStarted: false));
    }

    [Fact]
    public void Write_to_a_missing_companion_is_a_warning_and_skipped_at_runtime()
    {
        using var tmp = new TempDir();
        WriteCompanion(tmp.Path, "ahmet", effects: JObject.Parse("""{ "setFlags": ["jeremus.told_scar"] }"""));

        var result = CompanionCatalog.Load(tmp.Path);
        Assert.Equal(IssueCodes.MissingCompanionReference, Assert.Single(result.Issues).Code);

        var store = new VariableStore(result.Registry.All);
        var skipped = ConversationEffects.ApplyVariables("ahmet", new EffectSet { SetFlags = new List<string> { "jeremus.told_scar" } }, store);
        Assert.Equal(new[] { "jeremus.told_scar" }, skipped);
        Assert.Empty(store.ToList());
    }

    [Theory]
    [InlineData("""{ "told_scar": { "type": "bool" } }""", IssueCodes.LocalVariable)]
    [InlineData("""{ "told_scar": { "type": "bool", "scope": "local" } }""", IssueCodes.LocalVariable)]
    [InlineData("""{ "other": { "type": "bool", "scope": "shared" } }""", IssueCodes.UnknownVariable)]
    [InlineData("""{ "told_scar": { "type": "int", "scope": "shared" } }""", IssueCodes.VariableTypeMismatch)]
    public void Bad_reference_to_a_loaded_companion_skips_the_referencing_companion(string jeremusVariables, string code)
    {
        using var tmp = new TempDir();
        WriteCompanion(tmp.Path, "jeremus", variables: JObject.Parse(jeremusVariables));
        WriteCompanion(tmp.Path, "ahmet", when: NeedsJeremus);

        var result = CompanionCatalog.Load(tmp.Path);

        var error = Assert.Single(result.Issues.Errors());
        Assert.Equal(code, error.Code);
        Assert.Equal("ahmet", error.Folder);
        Assert.False(result.Registry.Contains("ahmet"));
        Assert.True(result.Registry.Contains("jeremus"));
    }

    [Fact]
    public void Another_companions_mood_is_local()
    {
        using var tmp = new TempDir();
        WriteCompanion(tmp.Path, "jeremus");
        WriteCompanion(tmp.Path, "ahmet", when: JObject.Parse("""{ "var": "jeremus.mood", "min": 1 }"""));

        var result = CompanionCatalog.Load(tmp.Path);

        Assert.Equal(IssueCodes.LocalVariable, Assert.Single(result.Issues.Errors()).Code);
    }

    [Fact]
    public void Companion_skipped_for_its_own_errors_counts_as_missing()
    {
        using var tmp = new TempDir();
        WriteCompanion(tmp.Path, "jeremus", variables: SharedTold, companion: c => c["age"] = 5);
        WriteCompanion(tmp.Path, "ahmet", when: NeedsJeremus);

        var result = CompanionCatalog.Load(tmp.Path);

        Assert.All(result.Issues.Errors(), i => Assert.Equal("jeremus", i.Folder));
        Assert.Contains(result.Issues, i => i.Code == IssueCodes.MissingCompanionReference && i.Folder == "ahmet");
        Assert.True(result.Registry.Contains("ahmet"));
        Assert.False(result.Registry.Contains("jeremus"));
    }

    [Fact]
    public void Companion_skipped_for_a_missing_module_counts_as_missing()
    {
        using var tmp = new TempDir();
        WriteCompanion(tmp.Path, "jeremus", variables: SharedTold, companion: c => c["requiredModules"] = new JArray("EOE_Armory"));
        WriteCompanion(tmp.Path, "ahmet", when: NeedsJeremus);

        var result = CompanionCatalog.Load(tmp.Path, module => module != "EOE_Armory");

        Assert.Equal(new[] { "EOE_Armory" }, result.ModuleSkips["jeremus"]);
        Assert.Equal(IssueCodes.MissingCompanionReference, Assert.Single(result.Issues).Code);
        Assert.Equal(new[] { "ahmet" }, result.Registry.Ids);
    }

    [Fact]
    public void Skipping_a_companion_turns_references_to_it_into_warnings()
    {
        using var tmp = new TempDir();
        WriteCompanion(tmp.Path, "jeremus", variables: JObject.Parse("""{ "told_scar": { "type": "bool" } }"""));
        WriteCompanion(tmp.Path, "ahmet", when: NeedsJeremus, variables: JObject.Parse("""{ "met_jeremus": { "type": "bool", "scope": "shared" } }"""));
        WriteCompanion(tmp.Path, "bora", when: JObject.Parse("""{ "flags": ["ahmet.met_jeremus"] }"""));

        var result = CompanionCatalog.Load(tmp.Path);

        Assert.Equal(IssueCodes.LocalVariable, Assert.Single(result.Issues.Errors()).Code);
        Assert.Contains(result.Issues, i => i.Code == IssueCodes.MissingCompanionReference && i.Folder == "bora");
        Assert.True(result.Registry.Contains("bora"));
        Assert.False(result.Registry.Contains("ahmet"));
    }

    // ---- runtime: conditions over the store ----

    static VariableStore Store(params (string Id, string Variables)[] companions) =>
        new(companions.Select(c =>
        {
            var d = Fixtures.Dialogs(c.Id);
            d["variables"] = JObject.Parse(c.Variables);
            return Fixtures.Package(c.Id, Fixtures.Companion(c.Id), d);
        }));

    static VariableStore Jeremus() => Store(("jeremus", """
        {
          "told": { "type": "bool", "scope": "shared" },
          "seen": { "type": "int", "default": 2 },
          "angry": { "type": "bool", "default": true }
        }
        """));

    static bool Holds(VariableStore store, string when) =>
        ConversationConditions.Holds(JObject.Parse(when).ToObject<ConditionSet>(), new StoreContext(store, "jeremus"));

    [Fact]
    public void Defaults_apply_until_written()
    {
        var store = Jeremus();
        Assert.Equal(0, store.Get("jeremus", "told"));
        Assert.Equal(2, store.Get("jeremus", "seen"));
        Assert.Equal(1, store.Get("jeremus", "angry"));
        Assert.Equal(0, store.Get("jeremus", "mood"));
        Assert.Equal(2, store.Get("ahmet", "jeremus.seen"));
        Assert.Null(store.Get("jeremus", "nope"));
        Assert.Null(store.Get("ahmet", "bora.x"));
    }

    [Theory]
    [InlineData("""{ "var": "seen", "min": 2 }""", true)]
    [InlineData("""{ "var": "seen", "min": 3 }""", false)]
    [InlineData("""{ "var": "seen", "max": 2 }""", true)]
    [InlineData("""{ "var": "seen", "max": 1 }""", false)]
    [InlineData("""{ "var": "seen", "eq": 2 }""", true)]
    [InlineData("""{ "var": "angry", "eq": true }""", true)]
    [InlineData("""{ "var": "told", "eq": true }""", false)]
    [InlineData("""{ "flags": ["angry"], "notFlags": ["told"] }""", true)]
    [InlineData("""{ "any": [ { "flags": ["told"] }, { "var": "seen", "eq": 2 } ] }""", true)]
    [InlineData("""{ "any": [ { "flags": ["told"] }, { "var": "seen", "eq": 3 } ] }""", false)]
    [InlineData("""{ "all": [ { "flags": ["angry"] }, { "var": "mood", "eq": 0 } ] }""", true)]
    [InlineData("""{ "all": [ { "flags": ["angry"] }, { "flags": ["told"] } ] }""", false)]
    [InlineData("""{ "not": { "flags": ["told"] } }""", true)]
    [InlineData("""{ "not": { "any": [ { "flags": ["angry"] } ] } }""", false)]
    [InlineData("""{ "flags": ["jeremus.angry"] }""", true)]
    public void Conditions_read_the_store(string when, bool expected) => Assert.Equal(expected, Holds(Jeremus(), when));

    [Theory]
    [InlineData("""{ "flags": ["bora.x"] }""")]
    [InlineData("""{ "not": { "flags": ["bora.x"] } }""")]
    [InlineData("""{ "any": [ { "hired": false }, { "flags": ["bora.x"] } ] }""")]
    public void Condition_referencing_a_missing_companion_is_false_as_a_whole(string when) => Assert.False(Holds(Jeremus(), when));

    // ---- runtime: effects, save, load ----

    [Fact]
    public void Effects_write_the_store()
    {
        var store = Jeremus();
        var skipped = ConversationEffects.ApplyVariables("jeremus", JObject.Parse("""
            { "setFlags": ["told"], "clearFlags": ["angry"], "set": { "seen": 7 }, "add": { "mood": -3 } }
            """).ToObject<EffectSet>(), store);

        Assert.Empty(skipped);
        Assert.Equal(1, store.Get("jeremus", "told"));
        Assert.Equal(0, store.Get("jeremus", "angry"));
        Assert.Equal(7, store.Get("jeremus", "seen"));
        Assert.Equal(-3, store.Get("jeremus", "mood"));

        ConversationEffects.ApplyVariables("jeremus", new EffectSet { Add = new Dictionary<string, int> { ["seen"] = 2 } }, store);
        Assert.Equal(9, store.Get("jeremus", "seen"));
    }

    [Fact]
    public void Save_list_round_trips_only_written_values()
    {
        var store = Jeremus();
        store.Set("jeremus", "told", 1);
        store.Add("jeremus", "mood", -2);

        Assert.Equal(new[] { "jeremus.mood=-2", "jeremus.told=true" }, store.ToList());

        var restored = Jeremus();
        Assert.Equal(2, restored.Load(store.ToList(), _ => { }));
        Assert.Equal(store.ToList(), restored.ToList());
        Assert.Equal(1, restored.Get("jeremus", "told"));
        Assert.Equal(2, restored.Get("jeremus", "seen"));
    }

    [Fact]
    public void Load_ignores_unknown_names_and_uses_the_default_for_a_wrong_type()
    {
        var log = new List<string>();
        var store = Jeremus();

        var restored = store.Load(new[] { "jeremus.gone=true", "bora.x=1", "jeremus.seen=true", "jeremus.told=yes", "garbage", "jeremus.mood=4" }, log.Add);

        Assert.Equal(1, restored);
        Assert.Equal(4, store.Get("jeremus", "mood"));
        Assert.Equal(2, store.Get("jeremus", "seen"));
        Assert.Equal(0, store.Get("jeremus", "told"));
        Assert.Equal(5, log.Count);
    }

    [Fact]
    public void Newly_declared_variable_starts_at_its_default_after_load()
    {
        var old = Store(("jeremus", """{ "told": { "type": "bool" } }"""));
        old.Set("jeremus", "told", 1);

        var current = Jeremus();
        current.Load(old.ToList(), _ => { });

        Assert.Equal(1, current.Get("jeremus", "told"));
        Assert.Equal(2, current.Get("jeremus", "seen"));
    }

    [Fact]
    public void Old_flags_become_declared_bool_variables()
    {
        var log = new List<string>();
        var store = Jeremus();

        var imported = store.ImportLegacyFlags(new[] { "jeremus:told", "jeremus:seen", "jeremus:gone", "bora:x" }, log.Add);

        Assert.Equal(1, imported);
        Assert.Equal(1, store.Get("jeremus", "told"));
        Assert.Equal(2, store.Get("jeremus", "seen"));
        Assert.Equal(3, log.Count);
    }

    [Fact]
    public void Reset_returns_a_companion_to_its_defaults()
    {
        var store = Store(("jeremus", """{ "told": { "type": "bool" } }"""), ("ahmet", """{ "told": { "type": "bool" } }"""));
        store.Set("jeremus", "told", 1);
        store.Set("ahmet", "told", 1);

        Assert.Equal(1, store.Reset("jeremus"));
        Assert.Equal(0, store.Get("jeremus", "told"));
        Assert.Equal(1, store.Get("ahmet", "told"));
    }

    /// <summary>A conversation context over a store, like the game's, with everything else neutral.</summary>
    sealed class StoreContext : IConversationContext
    {
        readonly VariableStore _store;
        readonly string _companion;

        public StoreContext(VariableStore store, string companion)
        {
            _store = store;
            _companion = companion;
        }

        public bool HasMet => true;
        public bool IsHired => true;
        public bool IsInPlayerParty => true;
        public int Relation => 0;
        public int PlayerGold => 0;
        public bool ClanIsFull => false;
        public int? GetVariable(string reference) => _store.Get(_companion, reference);
    }
}
