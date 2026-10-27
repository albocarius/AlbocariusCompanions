using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Registry;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Core.Simulation;

/// <summary>
/// One JSON request per line in, one JSON response per line out (docs/SIMULATOR_PROTOCOL.md). Never throws: a bad
/// request gets <c>{"id":…,"ok":false,"error":"…"}</c> and the server keeps going.
/// </summary>
public sealed class SimulatorProtocol
{
    public const int Version = 1;

    private readonly Dictionary<string, SimSession> _sessions = new Dictionary<string, SimSession>(StringComparer.Ordinal);
    private readonly Dictionary<string, SimTriggerSession> _triggerSessions = new Dictionary<string, SimTriggerSession>(StringComparer.Ordinal);
    private CompanionRegistry? _registry;
    private int _nextSession = 1;

    /// <summary>True after a "quit" request: the server loop stops.</summary>
    public bool Quit { get; private set; }

    public string Handle(string line)
    {
        JToken? id = null;
        JObject response;
        try
        {
            JObject request;
            try
            {
                request = JObject.Parse(line);
            }
            catch (JsonException e)
            {
                throw new SimulationException("not a JSON object: " + e.Message);
            }
            id = request["id"];
            response = Dispatch(request);
            response["ok"] = true;
        }
        catch (Exception e) when (e is SimulationException || e is JsonException || e is ArgumentException || e is InvalidCastException)
        {
            response = new JObject { ["ok"] = false, ["error"] = e.Message };
        }
        catch (Exception e)
        {
            response = new JObject { ["ok"] = false, ["error"] = "internal error: " + e.Message };
        }

        var result = new JObject { ["id"] = id?.DeepClone() ?? JValue.CreateNull() };
        foreach (var property in response.Properties())
            result[property.Name] = property.Value;
        return result.ToString(Formatting.None);
    }

    private JObject Dispatch(JObject request)
    {
        var op = Text(request, "op");
        switch (op)
        {
            case "hello":
                return new JObject { ["version"] = Version };
            case "load":
                return Load(Text(request, "root"));
            case "start":
                return Start(request);
            case "choose":
                return StepResult(Session(request).Choose(Text(request, "conversation"), Text(request, "node")));
            case "hub":
                return StepResult(Session(request).Hub());
            case "hire":
            {
                var session = Session(request);
                var refused = session.Hire();
                var result = new JObject { ["hired"] = refused == null, ["state"] = JToken.FromObject(session.State) };
                if (refused != null)
                    result["reason"] = refused;
                return result;
            }
            case "endTalk":
                Session(request).EndTalk();
                return new JObject();
            case "getState":
                return new JObject { ["state"] = JToken.FromObject(Session(request).State) };
            case "setState":
            {
                var session = Session(request);
                session.SetState(StateOf(request) ?? throw new SimulationException("'state' is required"));
                return new JObject { ["state"] = JToken.FromObject(session.State) };
            }
            case "triggers":
                return StartTriggers(request);
            case "fire":
                return TriggerSession(request).Fire(Text(request, "trigger"), ObjectOf(request, "payload"), Hours(request));
            case "deliver":
                return TriggerSession(request).Deliver(Hours(request), Flag(request, "onMap", true), Flag(request, "canInterrupt", true));
            case "queue":
                return TriggerSession(request).QueueJson();
            case "clearCooldowns":
            {
                var session = TriggerSession(request);
                session.ClearCooldowns();
                return session.QueueJson();
            }
            case "close":
                if (!_sessions.Remove(Text(request, "session")) && !_triggerSessions.Remove(Text(request, "session")))
                    throw new SimulationException("unknown session");
                return new JObject();
            case "quit":
                Quit = true;
                return new JObject();
            default:
                throw new SimulationException($"unknown op '{op}'");
        }
    }

    private JObject Load(string root)
    {
        var catalog = CompanionCatalog.Load(root);
        _registry = catalog.Registry;
        _sessions.Clear();
        _triggerSessions.Clear();
        return new JObject
        {
            ["companions"] = new JArray(catalog.Registry.All.Select(p => p.Id)),
            ["errors"] = catalog.Issues.Count(i => i.IsError),
            ["warnings"] = catalog.Issues.Count(i => !i.IsError),
            ["issues"] = new JArray(catalog.Issues.Select(i =>
                new JObject { ["severity"] = i.IsError ? "error" : "warning", ["code"] = i.Code, ["folder"] = i.Folder, ["message"] = i.Message })),
        };
    }

    private JObject Start(JObject request)
    {
        if (_registry == null)
            throw new SimulationException("nothing loaded; send 'load' first");
        var seed = request["seed"]?.Type == JTokenType.Integer ? (int?)request.Value<int>("seed") : null;
        var session = new SimSession(_registry, Text(request, "companion"), StateOf(request), new SystemRandomSource(seed));
        var name = "s" + _nextSession++;
        _sessions[name] = session;
        var result = StepResult(session.Start(Text(request, "entry")));
        result["session"] = name;
        return result;
    }

    /// <summary><c>triggers</c>: a trigger session over every loaded companion (M11 T7).</summary>
    private JObject StartTriggers(JObject request)
    {
        if (_registry == null)
            throw new SimulationException("nothing loaded; send 'load' first");
        var seed = request["seed"]?.Type == JTokenType.Integer ? (int?)request.Value<int>("seed") : null;
        var session = new SimTriggerSession(_registry, StateOf(request), new SystemRandomSource(seed));
        var name = "t" + _nextSession++;
        _triggerSessions[name] = session;
        return new JObject { ["session"] = name, ["usedTriggers"] = new JArray(session.Engine.UsedTriggers.OrderBy(t => t, StringComparer.Ordinal)) };
    }

    private SimTriggerSession TriggerSession(JObject request) =>
        _triggerSessions.TryGetValue(Text(request, "session"), out var session) ? session : throw new SimulationException("unknown trigger session");

    private static double? Hours(JObject request)
    {
        var token = request["hours"];
        if (token == null || token.Type == JTokenType.Null) return null;
        if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)
            throw new SimulationException("'hours' must be a number");
        return (double)token;
    }

    private static bool Flag(JObject request, string key, bool defaultValue)
    {
        var token = request[key];
        if (token == null || token.Type == JTokenType.Null) return defaultValue;
        return token.Type == JTokenType.Boolean ? (bool)token : throw new SimulationException($"'{key}' must be true/false");
    }

    private static JObject? ObjectOf(JObject request, string key)
    {
        var token = request[key];
        if (token == null || token.Type == JTokenType.Null) return null;
        return token as JObject ?? throw new SimulationException($"'{key}' must be an object");
    }

    private static JObject StepResult(SimStep step) => new JObject { ["step"] = JToken.FromObject(step) };

    private SimSession Session(JObject request) =>
        _sessions.TryGetValue(Text(request, "session"), out var session) ? session : throw new SimulationException("unknown session");

    private static SimState? StateOf(JObject request)
    {
        var token = request["state"];
        if (token == null || token.Type == JTokenType.Null)
            return null;
        if (token.Type != JTokenType.Object)
            throw new SimulationException("'state' must be an object");
        var serializer = JsonSerializer.Create(new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
        return token.ToObject<SimState>(serializer);
    }

    private static string Text(JObject request, string key) =>
        request[key]?.Type == JTokenType.String ? request.Value<string>(key)! : throw new SimulationException($"'{key}' (string) is required");
}
