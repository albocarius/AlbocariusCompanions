using System.Text.RegularExpressions;
using AlbocariusCompanions.Core.Triggers;

namespace AlbocariusCompanions.Acceptance.Triggers;

// Tasks 122/123 (M11 T5, G11 — user: must be tested): game adapters attach listeners only for used triggers.
public class TriggerGameWiringTests
{
    static readonly string Game = Path.Combine(RepoPaths.Root, "src", "AlbocariusCompanions.Game");

    static string Read(string file) => File.ReadAllText(Path.Combine(Game, file));

    static IEnumerable<string> AdapterFiles() =>
        Directory.GetFiles(Game, "TriggerAdapters.Q*.cs").Select(Path.GetFileName)!;

    static readonly Regex Entry = new(@"d\[""(?<name>[a-zA-Z]+)""\]\s*=\s*\(owner, fire\)\s*=>\s*CampaignEvents\.(?<source>\w+)\.AddNonSerializedListener\(owner,", RegexOptions.Compiled);

    internal static List<(string File, string Name, string Source)> Entries() =>
        AdapterFiles().SelectMany(f => Entry.Matches(Read(f)).Select(m => (f, m.Groups["name"].Value, m.Groups["source"].Value))).ToList();

    [Fact]
    public void Host_subscribes_only_the_used_triggers_through_the_gate()
    {
        var host = Read("TriggerHost.cs");
        Assert.Contains("_used = new HashSet<string>(_engine.UsedTriggers", host);
        Assert.Contains("TriggerAdapters.Subscribe(owner, _used,", host);
        // Ticks only when something is used.
        Assert.Matches(new Regex(@"if \(_used\.Count > 0\)\s*\{\s*CampaignEvents\.HourlyTickEvent"), host);
        // Each poll only when its trigger is used.
        Assert.Contains("if (_used.Contains(\"nearSettlement\"))", host);
        Assert.Contains("if (_used.Contains(\"daily\"))", host);
        Assert.Contains("if (!_used.Contains(\"varCrossed\"))", host);
        Assert.Contains("if (_used.Contains(\"roleStarted\") || _used.Contains(\"roleEnded\"))", host);

        var gate = Read("TriggerAdapters.cs");
        Assert.Contains("foreach (var name in usedTriggers)", gate);
        Assert.Contains("if (Adapters.Value.TryGetValue(name, out var adapter))", gate);
        // The adapter table is private: Subscribe is the only way to attach.
        Assert.Contains("private static readonly Lazy<Dictionary<string, TriggerAdapter>> Adapters", gate);
    }

    [Fact]
    public void Behavior_hands_its_registration_to_the_host()
    {
        var behavior = Read("AlbocariusCompanionsCampaignBehavior.cs");
        Assert.Contains("ModRuntime.Triggers?.RegisterEvents(this)", behavior);
        Assert.Contains("ModRuntime.Triggers = new TriggerHost(ModRuntime.Registry.All)", behavior);
    }

    [Fact]
    public void Every_listener_in_the_adapter_files_is_one_table_entry()
    {
        foreach (var file in AdapterFiles())
        {
            var text = Read(file);
            var listeners = Regex.Matches(text, @"\.AddNonSerializedListener\(").Count;
            var entries = Entry.Matches(text).Count;
            Assert.True(listeners == entries, $"{file}: {listeners} listener(s) but {entries} table entries");
        }
        Assert.DoesNotContain("AddNonSerializedListener", Read("TriggerAdapters.cs"));
    }

    [Fact]
    public void Adapter_entries_match_registry_names_and_sources()
    {
        var entries = Entries();
        Assert.Equal(entries.Count, entries.Select(e => e.Name).Distinct().Count());
        foreach (var (file, name, source) in entries)
        {
            Assert.True(TriggerRegistry.TryGet(name, out var t), $"{file}: '{name}' is not a registry trigger");
            Assert.False(t.IsPolling, $"{file}: '{name}' is a polling trigger");
            Assert.True(t.Source == source, $"{file}: '{name}' listens on {source}, registry says {t.Source}");
            Assert.True(file == $"TriggerAdapters.{QOf(name)}.cs", $"'{name}' belongs in TriggerAdapters.{QOf(name)}.cs, found in {file}");
        }
    }

    internal static string QOf(string trigger) =>
        File.ReadAllLines(Path.Combine(RepoPaths.Root, "orchestra", "triggers.tsv")).Select(l => l.Split('\t'))
            .First(c => c.Length > 4 && c[4] == trigger)[1];

    [Fact]
    public void Triggered_conversations_open_only_through_their_delivery()
    {
        var dialogs = Read("CompanionDialogs.cs");
        Assert.Contains("active.CompanionId == id && active.ConversationId == conversation.Id", dialogs);
        Assert.Contains("current.IsStart ? (triggered ? TriggeredPriority : GreetingPriority) : 100", dialogs);
        var behavior = Read("AlbocariusCompanionsCampaignBehavior.cs");
        Assert.Contains("ModRuntime.ActiveTrigger = null;", behavior);
    }

    [Fact]
    public void Trigger_state_is_saved_with_the_campaign()
    {
        var behavior = Read("AlbocariusCompanionsCampaignBehavior.cs");
        Assert.Contains("dataStore.SyncData(\"_triggerQueue\", ref _triggerQueue);", behavior);
        Assert.Contains("dataStore.SyncData(\"_triggerPolling\", ref _triggerPolling);", behavior);
        Assert.Contains("ModRuntime.Triggers?.Load(_triggerQueue, _triggerPolling);", behavior);
    }

    [Fact]
    public void Console_has_fire_queue_and_clearcooldowns()
    {
        var console = Read("ConsoleCommands.cs");
        foreach (var name in new[] { "fire", "queue", "clearcooldowns" })
            Assert.Contains($"[CommandLineFunctionality.CommandLineArgumentFunction(\"{name}\", Group)]", console);
    }
}
