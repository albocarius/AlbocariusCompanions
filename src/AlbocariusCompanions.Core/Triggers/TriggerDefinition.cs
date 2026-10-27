using System;
using System.Collections.Generic;
using System.Linq;

namespace AlbocariusCompanions.Core.Triggers;

/// <summary>One payload field of a trigger: name, kind and what it means (docs, validation, simulator).</summary>
public sealed class TriggerField
{
    public TriggerField(string name, FieldKind kind, string description, string from)
    {
        Name = name;
        Kind = kind;
        Description = description;
        From = from;
    }

    public string Name { get; }
    public FieldKind Kind { get; }
    public string Description { get; }
    /// <summary>
    /// Where the Game adapter reads it: the event argument name, or an expression on an argument with members listed in
    /// GAME_MAP (e.g. <c>mapEvent.MapEventSettlement</c>). Review aid and the API-row list of task 121; not shown to authors.
    /// </summary>
    public string From { get; }
}

/// <summary>A parameter of a polling trigger, written in the content's <c>trigger.params</c> (T4).</summary>
public sealed class TriggerParameter
{
    public TriggerParameter(string name, FieldKind kind, bool required, string description)
    {
        Name = name;
        Kind = kind;
        Required = required;
        Description = description;
    }

    public string Name { get; }
    /// <summary>Int, Number or Text (a settlement / variable id is Text).</summary>
    public FieldKind Kind { get; }
    public bool Required { get; }
    public string Description { get; }
}

/// <summary>
/// One author-facing trigger (spec T1). The registry of these is the single source for validation, the simulator, the
/// docs and the Game adapters (M11).
/// </summary>
public sealed class TriggerDefinition
{
    public const string PollPrefix = "poll:";

    public TriggerDefinition(string name, string category, string description, string source,
        IReadOnlyList<TriggerField>? fields = null, IReadOnlyList<string>? subjectFields = null, string defaultSubject = Subjects.Any,
        bool common = false, bool inMission = false, IReadOnlyList<TriggerParameter>? parameters = null)
    {
        Name = name;
        Category = category;
        Description = description;
        Source = source;
        Fields = fields ?? Array.Empty<TriggerField>();
        SubjectFields = subjectFields ?? Array.Empty<string>();
        DefaultSubject = defaultSubject;
        Common = common;
        InMission = inMission;
        Parameters = parameters ?? Array.Empty<TriggerParameter>();
    }

    /// <summary>camelCase, unique (spec T1 naming pass).</summary>
    public string Name { get; }
    public string Category { get; }
    /// <summary>One line for the docs.</summary>
    public string Description { get; }
    /// <summary>The <c>CampaignEvents</c> member, or <c>poll:&lt;kind&gt;</c> for polling triggers.</summary>
    public string Source { get; }
    public IReadOnlyList<TriggerField> Fields { get; }
    /// <summary>Fields the subject is matched against; the subject matches when any of them matches (G3). Empty → only "any".</summary>
    public IReadOnlyList<string> SubjectFields { get; }
    public string DefaultSubject { get; }
    /// <summary>Listed in the "Common triggers" docs section; every trigger is always available.</summary>
    public bool Common { get; }
    /// <summary>Fires inside a battle or scene: delivery waits for the map (T5).</summary>
    public bool InMission { get; }
    /// <summary>Polling triggers only.</summary>
    public IReadOnlyList<TriggerParameter> Parameters { get; }

    public bool IsPolling => Source.StartsWith(PollPrefix, StringComparison.Ordinal);

    public TriggerField? Field(string name) => Fields.FirstOrDefault(f => f.Name == name);

    /// <summary>The subject kinds (without ids) valid for this trigger, from its subject fields' kinds; "any" always.</summary>
    public IReadOnlyList<string> SubjectKinds()
    {
        var kinds = new List<string>();
        foreach (var field in SubjectFields)
        {
            var f = Field(field);
            if (f == null) continue;
            foreach (var k in Subjects.KindsFor(f.Kind))
                if (!kinds.Contains(k)) kinds.Add(k);
        }
        kinds.Add(Subjects.Any);
        return kinds;
    }

    /// <summary>
    /// The kind of value at a payload path (G2): a primitive field → its kind; "field.property" of an entity → Bool or Text;
    /// an entity field itself → Text (its id). Null when the path is unknown for this trigger.
    /// </summary>
    public FieldKind? KindOfPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var dot = path.IndexOf('.');
        var field = Field(dot < 0 ? path : path.Substring(0, dot));
        if (field == null) return null;
        if (dot < 0) return EntityProperties.IsEntity(field.Kind) ? FieldKind.Text : field.Kind;
        if (!EntityProperties.IsEntity(field.Kind)) return null;
        var property = path.Substring(dot + 1);
        if (!EntityProperties.Of(field.Kind).Contains(property)) return null;
        return EntityProperties.BoolProperties.Contains(property) ? FieldKind.Bool : FieldKind.Text;
    }
}
