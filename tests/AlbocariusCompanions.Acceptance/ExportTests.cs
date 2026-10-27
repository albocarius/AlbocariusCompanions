using System.Text.RegularExpressions;
using AlbocariusCompanions.Cli;
using AlbocariusCompanions.Core.Triggers;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance;

// SPEC_EDITOR §3: machine-readable exports for the Qt editor (export schema, export triggers, validate --json).
public class ExportTests
{
    static string Companions => Path.Combine(RepoPaths.Root, "module", "ModuleData", "Companions");

    static (int Code, string Out, string Err) Run(params string[] args)
    {
        var stdout = new StringWriter { NewLine = "\n" };
        var stderr = new StringWriter { NewLine = "\n" };
        var code = CliProgram.Run(args, stdout, stderr);
        return (code, stdout.ToString(), stderr.ToString());
    }

    // ---- export schema ----

    [Fact]
    public void Every_model_property_has_schema_metadata_and_back()
    {
        foreach (var (name, type) in SchemaExport.Objects)
        {
            var model = SchemaExport.ModelProperties(type).Select(p => p.Name).OrderBy(n => n).ToList();
            var meta = SchemaExport.Fields[name].Keys.OrderBy(n => n).ToList();
            Assert.True(model.SequenceEqual(meta),
                $"{name}: model has [{string.Join(", ", model.Except(meta))}] without metadata, metadata has [{string.Join(", ", meta.Except(model))}] without a model property");
        }
    }

    [Fact]
    public void Every_exported_field_is_documented_in_schema_md()
    {
        var doc = File.ReadAllText(Path.Combine(RepoPaths.Root, "docs", "SCHEMA.md"));
        var schema = SchemaExport.Build();
        foreach (var obj in ((JObject)schema["objects"]!).Properties())
            foreach (var field in (JArray)obj.Value["fields"]!)
            {
                var n = (string)field["name"]!;
                Assert.True(doc.Contains($"`{n}`") || doc.Contains($"\"{n}\"") || doc.Contains($".{n}`") || doc.Contains($"`{n}."),
                    $"{obj.Name}.{n} is exported but docs/SCHEMA.md does not mention it");
            }
    }

    [Fact]
    public void Condition_and_effect_catalogues_cover_their_objects_exactly()
    {
        void Check(IReadOnlyDictionary<string, IReadOnlyList<string>> catalogue, Type type)
        {
            var keys = catalogue.Values.SelectMany(k => k).ToList();
            Assert.Equal(keys.Count, keys.Distinct().Count());
            Assert.Equal(SchemaExport.ModelProperties(type).Select(p => p.Name).OrderBy(n => n), keys.OrderBy(n => n));
        }

        Check(SchemaExport.ConditionKeys, SchemaExport.Objects["conditions"]);
        Check(SchemaExport.EffectKeys, SchemaExport.Objects["effects"]);
    }

    [Fact]
    public void Export_schema_writes_the_document()
    {
        var (code, output, _) = Run("export", "schema");
        Assert.Equal(0, code);
        var schema = JObject.Parse(output);
        Assert.Equal(1, (int)schema["exportVersion"]!);
        Assert.Equal("dialogs", (string)schema["files"]!["dialogs.json"]!);
        var node = ((JArray)schema["objects"]!["node"]!["fields"]!).ToDictionary(f => (string)f["name"]!);
        Assert.Equal(new[] { "npc", "player" }, node["speaker"]["allowed"]!.Values<string>());
        Assert.Equal("effects", (string)node["effects"]["type"]!);
        Assert.Equal("string[]", (string)node["next"]["type"]!);
        var conditions = ((JArray)schema["conditions"]!).Select(c => (string)c["name"]!).ToList();
        Assert.Contains("hasGold", conditions);
        Assert.Contains("var", conditions);
        Assert.Contains("main_hub", schema["slots"]!["required"]!.Values<string>());
    }

    // ---- export triggers ----

    [Fact]
    public void Export_triggers_lists_the_registry_documented_in_triggers_md()
    {
        var (code, output, _) = Run("export", "triggers");
        Assert.Equal(0, code);
        var triggers = (JArray)JObject.Parse(output)["triggers"]!;
        Assert.Equal(TriggerRegistry.All.Count, triggers.Count);

        var doc = File.ReadAllText(Path.Combine(RepoPaths.Root, "docs", "TRIGGERS.md"));
        var documented = Regex.Matches(doc, @"^### (\S+)$", RegexOptions.Multiline).Select(m => m.Groups[1].Value).OrderBy(n => n);
        Assert.Equal(documented, triggers.Select(t => (string)t["name"]!).OrderBy(n => n));

        var near = triggers.Single(t => (string)t["name"]! == "nearSettlement");
        Assert.True((bool)near["polling"]!);
        Assert.Equal(new[] { "settlement", "radius", "rearmRadius" }, near["params"]!.Select(p => (string)p["name"]!));
    }

    [Fact]
    public void Export_to_a_file_and_bad_usage()
    {
        var file = Path.Combine(Path.GetTempPath(), "albocarius_export_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var (code, _, _) = Run("export", "triggers", file);
            Assert.Equal(0, code);
            Assert.NotNull(JObject.Parse(File.ReadAllText(file))["triggers"]);
        }
        finally
        {
            File.Delete(file);
        }
        Assert.Equal(2, Run("export").Code);
        Assert.Equal(2, Run("export", "nothing").Code);
    }

    // ---- validate --json ----

    [Fact]
    public void Validate_json_clean_sample()
    {
        var (code, output, _) = Run("validate", "--json", Path.Combine(Companions, "test_companion"));
        Assert.Equal(0, code);
        var result = JObject.Parse(output);
        Assert.Equal(0, (int)result["errors"]!);
        Assert.Empty((JArray)result["diagnostics"]!);
    }

    [Fact]
    public void Validate_json_locates_file_conversation_and_node()
    {
        var root = Path.Combine(Path.GetTempPath(), "albocarius_validate_" + Guid.NewGuid().ToString("N"));
        var folder = Path.Combine(root, "test_companion");
        try
        {
            CopyDirectory(Path.Combine(Companions, "test_companion"), folder);
            var dialogs = JObject.Parse(File.ReadAllText(Path.Combine(folder, "dialogs.json")));
            var scar = (JObject)dialogs["conversations"]![0]!;
            scar["nodes"]![1]!["next"] = new JArray("p_scar_sorry", "nowhere");
            File.WriteAllText(Path.Combine(folder, "dialogs.json"), dialogs.ToString());
            var companion = JObject.Parse(File.ReadAllText(Path.Combine(folder, "companion.json")));
            companion["age"] = 5;
            File.WriteAllText(Path.Combine(folder, "companion.json"), companion.ToString());

            var (code, output, _) = Run("validate", "--json", folder);
            Assert.Equal(1, code);
            var diagnostics = ((JArray)JObject.Parse(output)["diagnostics"]!).Cast<JObject>().ToList();

            var unknown = diagnostics.Single(d => (string)d["code"]! == "unknown_node");
            Assert.Equal("error", (string)unknown["severity"]!);
            Assert.Equal("dialogs.json", (string)unknown["file"]!);
            Assert.Equal("the_scar", (string)unknown["conversation"]!);
            Assert.Equal("scar_story", (string)unknown["node"]!);
            Assert.Equal("test_companion", (string)unknown["folder"]!);

            var age = diagnostics.Single(d => (string)d["code"]! == "out_of_range");
            Assert.Equal("companion.json", (string)age["file"]!);
            Assert.Equal(JTokenType.Null, age["node"]!.Type);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Validate_text_output_is_unchanged_by_the_json_option()
    {
        var (code, output, _) = Run("validate", Path.Combine(Companions, "test_companion"));
        Assert.Equal(0, code);
        Assert.Equal("0 error(s), 0 warning(s).\n", output);
        Assert.Equal(2, Run("validate", "--xml", Companions).Code);
    }

    static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from))
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        foreach (var dir in Directory.GetDirectories(from))
            CopyDirectory(dir, Path.Combine(to, Path.GetFileName(dir)));
    }
}
