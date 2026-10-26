using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Registry;
using AlbocariusCompanions.Core.Rules;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Core.Simulation;

/// <summary>
/// Simulator (M9 I3): talks with one companion over a <see cref="SimState"/>, step by step, with the code the game
/// uses for conditions (<see cref="ConversationConditions"/>), selection (<see cref="ConversationSelection"/>) and
/// variable effects (<see cref="ConversationEffects"/>). Relation, transfers and joining are applied to the state the
/// way the game applies them, except that relation changes are applied raw (the game scales positive ones, GAME_MAP Q21).
/// </summary>
public sealed class SimSession
{
    /// <summary>NPC lines said in a row without a player choice; a longer chain is reported as a loop.</summary>
    public const int MaxChain = 50;

    private readonly CompanionRegistry _registry;
    private readonly CompanionPackage _package;
    private readonly IRandomSource _random;
    private readonly SelectionMemory _memory = new SelectionMemory();
    private VariableStore _variables = new VariableStore();
    private SimState _state = new SimState();
    private SimStep? _last;

    public SimSession(CompanionRegistry registry, string companionId, SimState? state, IRandomSource random)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        if (!registry.TryGet(companionId, out var package) || package == null)
            throw new SimulationException($"companion '{companionId}' is not loaded");
        _package = package;
        _random = random ?? throw new ArgumentNullException(nameof(random));
        SetState(state ?? new SimState());
    }

    public string CompanionId => _package.Id;

    /// <summary>Replaces the state; unknown variables are an error, nothing is changed then.</summary>
    public void SetState(SimState state)
    {
        var store = new VariableStore(_registry.All);
        foreach (var pair in state.Variables ?? new Dictionary<string, JToken>())
        {
            var dot = pair.Key.IndexOf('.');
            var definition = dot > 0 ? store.Find(pair.Key.Substring(0, dot), pair.Key.Substring(dot + 1)) : null;
            if (definition == null)
                throw new SimulationException($"unknown variable '{pair.Key}' (use '<companionId>.<name>' of a declared variable)");
            if (VariableUses.TypeOf(pair.Value) != definition.Type)
                throw new SimulationException($"variable '{pair.Key}' is a {VariableRules.Name(definition.Type)}");
            store.Set(definition.Owner, definition.Name, VariableUses.ValueOf(pair.Value));
        }
        var copy = state.Clone();
        copy.Variables = new Dictionary<string, JToken>();
        _state = copy;
        _variables = store;
    }

    /// <summary>The current state, every declared variable included (with its value or default).</summary>
    public SimState State
    {
        get
        {
            var copy = _state.Clone();
            foreach (var id in _variables.Companions)
                foreach (var definition in _variables.Definitions(id))
                {
                    var value = _variables.Value(definition);
                    copy.Variables[id + "." + definition.Name] = definition.Type == VariableType.Bool ? (JToken)(value != 0) : value;
                }
            return copy;
        }
    }

    /// <summary>A new talk: the kept selection picks are dropped (the game does this when a conversation ends).</summary>
    public void EndTalk()
    {
        _memory.Clear();
        _last = null;
    }

    /// <summary>
    /// The vanilla-style hire of the mod's fixed dialog (GAME_MAP Q6): pays hireCost to the companion, joins clan and party.
    /// Null when hired, or the reason it is refused.
    /// </summary>
    public string? Hire()
    {
        var self = Companion(_package.Id);
        var cost = _package.Companion.HireCost ?? 0;
        if (self.Hired)
            return "already hired";
        if (_state.ClanIsFull)
            return "the clan is at its companion limit";
        if (_state.Player.Gold < cost)
            return $"the player has {_state.Player.Gold} gold, hiring costs {cost}";
        _state.Player.Gold -= cost;
        self.Gold += cost;
        self.Met = self.Hired = self.InParty = true;
        return null;
    }

    /// <summary>The talk opens: "greeting" (the companion may open with a greeting conversation) or "hub" (the menu).</summary>
    public SimStep Start(string entry)
    {
        EndTalk();
        var step = new SimStep();
        if (entry == ConversationRules.EntryHub)
            return Finish(step, HubOptions());
        if (entry != ConversationRules.EntryGreeting)
            throw new SimulationException($"entry must be '{ConversationRules.EntryHub}' or '{ConversationRules.EntryGreeting}'");

        var context = Context();
        var greetings = _package.Conversations.Where(c => c.Entry == ConversationRules.EntryGreeting && c.Id != null).ToList();
        var offered = ConversationSelection.OfferedGreetings(_package, context, HasStarted);
        var conversation = ConversationSelection.Greeting(_package, context, HasStarted, _memory, _random);
        var greetingSet = Trace("greeting", greetings, offered, conversation, c => c.Id!, c => c.Priority,
            c => ConditionExplain.Offered(c, context, HasStarted(c.Id!))
                .Concat(new[] { ConditionResult.Of("startNodeHolds", ConversationSelection.NpcCandidates(c, c.Start, context).Count > 0, true, null) })
                .ToList());
        step.Selections.Add(greetingSet);
        if (conversation == null)
            return Finish(step, HubOptions());

        MarkStarted(conversation);
        return Continue(step, conversation, null, Nodes(conversation, conversation.Start));
    }

    /// <summary>Back to the menu in the same talk.</summary>
    public SimStep Hub() => Finish(new SimStep(), HubOptions());

    /// <summary>The player picks one of the shown options of the last step.</summary>
    public SimStep Choose(string conversationId, string nodeId)
    {
        var option = _last?.Options.FirstOrDefault(o => o.Conversation == conversationId && o.Node == nodeId);
        if (option == null)
            throw new SimulationException($"'{conversationId}/{nodeId}' is not an option of the last step");
        if (!option.Shown)
            throw new SimulationException($"'{conversationId}/{nodeId}' is not shown (its conditions do not hold)");

        var conversation = _package.Conversations.First(c => c.Id == conversationId);
        var node = conversation.Nodes!.First(n => n?.Id == nodeId);
        var step = new SimStep();
        if (option.At == SimStep.AtHub)
            MarkStarted(conversation);
        step.Said.Add(Say(conversation, node, selection: null));
        return After(step, conversation, node);
    }

    // ---- flow ----

    /// <summary>What follows a said node: the end, the menu, the player's options or the companion's next line.</summary>
    private SimStep After(SimStep step, ConversationDefinition conversation, ConversationNode node)
    {
        var next = node.Next ?? new List<string>();
        if (next.Count == 1 && next[0] == ConversationRules.NextEnd)
        {
            step.Kind = SimStep.KindEnd;
            EndTalk();
            step.State = State;
            return step;
        }
        if (next.Count == 1 && next[0] == ConversationRules.NextHub)
            return Finish(step, HubOptions());
        return Continue(step, conversation, node.Id, Nodes(conversation, next));
    }

    private SimStep Continue(SimStep step, ConversationDefinition conversation, string? parentId, List<ConversationNode> next)
    {
        if (step.Said.Count > MaxChain)
            throw new SimulationException($"more than {MaxChain} companion lines without a player choice (a loop?)");

        if (next.Any(n => n.Speaker == ConversationRules.SpeakerPlayer))
        {
            var context = Context();
            var options = next.Where(n => n.Speaker == ConversationRules.SpeakerPlayer)
                .Select(n => Option(conversation, n, ConversationConditions.NodeHolds(n, context), NodeConditions(n, context), parentId))
                .ToList();
            return Finish(step, options, parentId == null ? conversation.Id! : conversation.Id + "/" + parentId);
        }

        var ctx = Context();
        var winner = ConversationSelection.NpcReply(_package.Id, conversation, parentId, ctx, _memory, _random);
        var trace = Trace("npc", next, ConversationSelection.NpcCandidates(conversation, next.Select(n => n.Id!), ctx), winner,
            n => n.Id!, n => n.Priority, n => NodeConditions(n, ctx));
        if (winner == null)
        {
            step.Selections.Add(trace);
            step.Kind = SimStep.KindStuck;
            step.State = State;
            return step;
        }
        step.Said.Add(Say(conversation, winner, trace));
        return After(step, conversation, winner);
    }

    private SimSaid Say(ConversationDefinition conversation, ConversationNode node, SimSelection? selection)
    {
        var said = new SimSaid
        {
            Conversation = conversation.Id!,
            Node = node.Id!,
            Speaker = node.Speaker!,
            Text = _package.Text(node.Id!, CompanionRules.EnglishCode),
            Selection = selection,
        };
        said.Effects.AddRange(ApplyEffects(node));
        return said;
    }

    private SimStep Finish(SimStep step, List<SimOption> options, string at = SimStep.AtHub)
    {
        step.Kind = SimStep.KindOptions;
        step.At = at;
        foreach (var option in options)
            option.At = at;
        step.Options = options;
        step.State = State;
        _last = step;
        return step;
    }

    /// <summary>Hub conversations' start options, in menu order, each with whether it is shown and why.</summary>
    private List<SimOption> HubOptions()
    {
        var context = Context();
        var options = new List<SimOption>();
        foreach (var conversation in ConversationSelection.HubOrder(_package))
        {
            var started = HasStarted(conversation.Id!);
            var offered = ConversationConditions.IsOffered(conversation, context, started);
            foreach (var node in Nodes(conversation, conversation.Start))
            {
                var conditions = ConditionExplain.Offered(conversation, context, started).Concat(NodeConditions(node, context)).ToList();
                options.Add(Option(conversation, node, offered && ConversationConditions.NodeHolds(node, context), conditions, null));
            }
        }
        return options;
    }

    private static SimOption Option(ConversationDefinition conversation, ConversationNode node, bool shown, List<ConditionResult> conditions,
        string? parentId) =>
        new SimOption
        {
            Conversation = conversation.Id!,
            Node = node.Id!,
            Priority = parentId == null ? Selection.PriorityOf(conversation.Priority) : Selection.PriorityOf(node.Priority),
            Shown = shown,
            Conditions = conditions,
        };

    private static List<ConditionResult> NodeConditions(ConversationNode node, IConversationContext context) =>
        ConditionExplain.When(node.When, context).Concat(ConditionExplain.Implied(node.Effects, context)).ToList();

    private static SimSelection Trace<T>(string set, IEnumerable<T> all, IReadOnlyList<T> passing, T? winner, Func<T, string> id,
        Func<T, int?> priority, Func<T, List<ConditionResult>> conditions) where T : class
    {
        var group = Selection.TopGroup(passing, priority);
        return new SimSelection
        {
            Set = set,
            Candidates = all.Select(c => new SimCandidate
            {
                Id = id(c),
                Priority = Selection.PriorityOf(priority(c)),
                Passes = passing.Contains(c),
                Conditions = conditions(c),
            }).ToList(),
            TopPriority = group.Count == 0 ? (int?)null : Selection.PriorityOf(priority(group[0])),
            Group = group.Select(id).ToList(),
            Winner = winner == null ? null : id(winner),
        };
    }

    // ---- effects (order as in the game: variables, relation, transfer, join) ----

    private List<string> ApplyEffects(ConversationNode node)
    {
        var applied = new List<string>();
        var effects = node.Effects;
        if (effects == null)
            return applied;

        foreach (var use in VariableUses.InEffects(effects))
            applied.Add($"variable {use.Reference}");
        foreach (var skipped in ConversationEffects.ApplyVariables(_package.Id, effects, _variables))
            applied.Add($"skipped {skipped} (companion not loaded)");

        var self = Companion(_package.Id);
        if (effects.Relation is { } relation)
        {
            self.Relation = Math.Max(ConversationRules.MinRelation, Math.Min(ConversationRules.MaxRelation, self.Relation + relation));
            applied.Add($"relation {relation:+0;-0} (raw) -> {self.Relation}");
        }

        if (Transfers.Of(effects) is { } transfer)
            applied.Add(ApplyTransfer(transfer));

        if (effects.Join)
        {
            self.Hired = self.InParty = true;
            applied.Add("join");
        }
        return applied;
    }

    private string ApplyTransfer(TransferEffect transfer)
    {
        if (transfer.Gold is { } gold)
        {
            var amount = transfer.From == Transfers.World ? gold : Math.Min(gold, Math.Max(0, GoldOf(transfer.From) ?? 0));
            if (transfer.From != Transfers.World && GoldOf(transfer.From) == null || transfer.To != Transfers.World && GoldOf(transfer.To) == null)
                return $"skipped transfer {transfer.From} -> {transfer.To} (companion not loaded)";
            AddGold(transfer.From!, -amount);
            AddGold(transfer.To!, amount);
            return $"gold {amount} {transfer.From} -> {transfer.To}";
        }

        var items = _state.Player.Items;
        var count = Transfers.CountOf(transfer);
        items.TryGetValue(transfer.Item!, out var have);
        if (transfer.From == Transfers.Player)
            count = Math.Min(count, have);
        items[transfer.Item!] = have + (transfer.From == Transfers.Player ? -count : count);
        return $"item {count} x {transfer.Item} {transfer.From} -> {transfer.To}";
    }

    private void AddGold(string actor, int amount)
    {
        if (actor == Transfers.World)
            return;
        if (actor == Transfers.Player)
            _state.Player.Gold += amount;
        else
            Companion(actor == Transfers.Self ? _package.Id : actor).Gold += amount;
    }

    private int? GoldOf(string? actor) => actor switch
    {
        Transfers.Player => _state.Player.Gold,
        Transfers.Self => Companion(_package.Id).Gold,
        null => null,
        _ when _registry.Contains(actor) => Companion(actor).Gold,
        _ => null,
    };

    // ---- state access ----

    private SimCompanion Companion(string id)
    {
        if (!_state.Companions.TryGetValue(id, out var companion))
            _state.Companions[id] = companion = new SimCompanion();
        return companion;
    }

    private bool HasStarted(string conversationId) => _state.Started.Contains(StartedKey(conversationId));

    private void MarkStarted(ConversationDefinition conversation)
    {
        if (!HasStarted(conversation.Id!))
            _state.Started.Add(StartedKey(conversation.Id!));
    }

    private string StartedKey(string conversationId) => _package.Id + ":" + conversationId;

    private static List<ConversationNode> Nodes(ConversationDefinition conversation, IEnumerable<string>? ids)
    {
        var nodes = (conversation.Nodes ?? new List<ConversationNode>()).Where(n => n?.Id != null).ToList();
        return (ids ?? Enumerable.Empty<string>()).Select(id => nodes.FirstOrDefault(n => n.Id == id)).Where(n => n != null).ToList()!;
    }

    private IConversationContext Context() => new SimContext(this);

    private sealed class SimContext : IConversationContext
    {
        private readonly SimSession _session;
        private readonly SimCompanion _self;

        public SimContext(SimSession session)
        {
            _session = session;
            _self = session.Companion(session._package.Id);
        }

        public bool HasMet => _self.Met;
        public bool IsHired => _self.Hired;
        public bool IsInPlayerParty => _self.InParty;
        public int Relation => _self.Relation;
        public int PlayerGold => _session._state.Player.Gold;
        public bool ClanIsFull => _session._state.ClanIsFull;
        public int? GetVariable(string reference) => _session._variables.Get(_session._package.Id, reference);
        public int? GoldOf(string actor) => _session.GoldOf(actor);
        public int PlayerItemCount(string itemId) => _session._state.Player.Items.TryGetValue(itemId, out var n) ? n : 0;
    }
}

public sealed class SimulationException : Exception
{
    public SimulationException(string message) : base(message)
    {
    }
}

/// <summary>The result of one simulator call (docs/SIMULATOR_PROTOCOL.md "Step").</summary>
public sealed class SimStep
{
    public const string KindOptions = "options";
    public const string KindEnd = "end";
    public const string KindStuck = "stuck";
    public const string AtHub = "hub";

    /// <summary>"options": the player chooses; "end": the talk closed; "stuck": no companion reply holds.</summary>
    [JsonProperty("kind")] public string Kind { get; set; } = KindOptions;
    /// <summary>For "options": "hub" (the menu) or "&lt;conversation&gt;/&lt;node&gt;" (the node the options follow).</summary>
    [JsonProperty("at", NullValueHandling = NullValueHandling.Ignore)] public string? At { get; set; }
    /// <summary>Greeting selection made when the talk opened (start "greeting").</summary>
    [JsonProperty("selections")] public List<SimSelection> Selections { get; set; } = new List<SimSelection>();
    /// <summary>Every line said by this call, in order, with its selection and applied effects.</summary>
    [JsonProperty("said")] public List<SimSaid> Said { get; set; } = new List<SimSaid>();
    [JsonProperty("options")] public List<SimOption> Options { get; set; } = new List<SimOption>();
    [JsonProperty("state")] public SimState State { get; set; } = new SimState();
}

public sealed class SimSaid
{
    [JsonProperty("conversation")] public string Conversation { get; set; } = "";
    [JsonProperty("node")] public string Node { get; set; } = "";
    [JsonProperty("speaker")] public string Speaker { get; set; } = "";
    [JsonProperty("text")] public string? Text { get; set; }
    /// <summary>How a companion line was chosen; null for a player line.</summary>
    [JsonProperty("selection")] public SimSelection? Selection { get; set; }
    [JsonProperty("effects")] public List<string> Effects { get; set; } = new List<string>();
}

public sealed class SimSelection
{
    /// <summary>"greeting" (which greeting conversation opens the talk) or "npc" (which companion node is said).</summary>
    [JsonProperty("set")] public string Set { get; set; } = "";
    [JsonProperty("candidates")] public List<SimCandidate> Candidates { get; set; } = new List<SimCandidate>();
    /// <summary>The highest priority among the passing candidates; null when none passes.</summary>
    [JsonProperty("topPriority")] public int? TopPriority { get; set; }
    /// <summary>The passing candidates with that priority (the winner is one of them at random).</summary>
    [JsonProperty("group")] public List<string> Group { get; set; } = new List<string>();
    [JsonProperty("winner")] public string? Winner { get; set; }
}

public sealed class SimCandidate
{
    [JsonProperty("id")] public string Id { get; set; } = "";
    [JsonProperty("priority")] public int Priority { get; set; }
    [JsonProperty("passes")] public bool Passes { get; set; }
    [JsonProperty("conditions")] public List<ConditionResult> Conditions { get; set; } = new List<ConditionResult>();
}

public sealed class SimOption
{
    [JsonIgnore] public string At { get; set; } = SimStep.AtHub;
    [JsonProperty("conversation")] public string Conversation { get; set; } = "";
    [JsonProperty("node")] public string Node { get; set; } = "";
    /// <summary>Menu order key: the conversation's priority on the hub, the node's elsewhere (player options are all shown).</summary>
    [JsonProperty("priority")] public int Priority { get; set; }
    /// <summary>Whether the game shows this option now.</summary>
    [JsonProperty("shown")] public bool Shown { get; set; }
    [JsonProperty("conditions")] public List<ConditionResult> Conditions { get; set; } = new List<ConditionResult>();
}
