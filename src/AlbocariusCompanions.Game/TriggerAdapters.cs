using System;
using System.Collections.Generic;
using AlbocariusCompanions.Core.Logging;
using AlbocariusCompanions.Core.Triggers;
using TaleWorlds.CampaignSystem;

namespace AlbocariusCompanions.Game;

/// <summary>Where an adapter hands a fired trigger (the trigger host).</summary>
internal delegate void TriggerSink(string trigger, TriggerPayload payload);

/// <summary>Subscribes one trigger's listener on its CampaignEvents member (owner = the campaign behavior, GAME_MAP Q8).</summary>
internal delegate void TriggerAdapter(CampaignBehaviorBase owner, TriggerSink fire);

/// <summary>
/// One adapter per event trigger of the registry (M11 T1/T5): listens on the trigger's CampaignEvents member and builds its
/// payload (TriggerPayloads). Entries live in one partial file per GAME_MAP question, like the registry. Listeners are
/// attached only through <see cref="Subscribe"/>, and only for the triggers loaded content uses (G11).
/// </summary>
internal static partial class TriggerAdapters
{
    private static readonly Lazy<Dictionary<string, TriggerAdapter>> Adapters = new Lazy<Dictionary<string, TriggerAdapter>>(() =>
    {
        var d = new Dictionary<string, TriggerAdapter>(StringComparer.Ordinal);
        Q35(d);
        Q36(d);
        Q37(d);
        Q38(d);
        Q39(d);
        Q40(d);
        Q41(d);
        Q42(d);
        return d;
    });

    /// <summary>Every trigger name that has an adapter.</summary>
    public static IReadOnlyCollection<string> Names => Adapters.Value.Keys;

    /// <summary>
    /// The only place listeners are attached (G11): one per used trigger that has an adapter. Polling triggers have none
    /// (the host polls them). Returns the subscribed names.
    /// </summary>
    public static List<string> Subscribe(CampaignBehaviorBase owner, IEnumerable<string> usedTriggers, TriggerSink fire)
    {
        var subscribed = new List<string>();
        foreach (var name in usedTriggers)
            if (Adapters.Value.TryGetValue(name, out var adapter))
            {
                adapter(owner, fire);
                subscribed.Add(name);
            }
        return subscribed;
    }

    /// <summary>Builds the payload and hands it on; a failure is logged and never reaches the game's event loop.</summary>
    private static void Emit(TriggerSink fire, string trigger, Func<TriggerPayload> payload) =>
        Safe.Run(ModRuntime.Log, "trigger " + trigger, () => fire(trigger, payload()));
}
