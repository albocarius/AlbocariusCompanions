using AlbocariusCompanions.Core.Logging;
using TaleWorlds.Library;

namespace AlbocariusCompanions.Game;

/// <summary>
/// Shows logged warnings and errors in the message log (SPEC_CORE_GAPS T2.5, GAME_MAP Q17). Nothing listens before the
/// UI is up, so messages are queued until the first campaign start/load, then shown as they come.
/// </summary>
internal static class InGameNotifications
{
    private const string Prefix = "Albocarius's Companions: ";

    private static readonly NotificationQueue Queue = new NotificationQueue();
    private static bool _ready;

    /// <summary>ModLogger.Notify listener.</summary>
    public static void OnLogged(LogLevel level, string text)
    {
        Queue.Add(level, text);
        if (_ready)
            Flush();
    }

    /// <summary>Campaign started or loaded: one summary line, then everything queued so far.</summary>
    public static void OnCampaignReady(int loaded, int skipped)
    {
        _ready = true;
        var summary = NotificationQueue.Summary(loaded, skipped);
        InformationManager.DisplayMessage(skipped > 0 ? new InformationMessage(summary, Colors.Yellow) : new InformationMessage(summary));
        Flush();
    }

    private static void Flush()
    {
        foreach (var notification in Queue.Drain())
        {
            var color = notification.Level == LogLevel.Error ? Colors.Red : Colors.Yellow;
            InformationManager.DisplayMessage(new InformationMessage(Prefix + notification.Text, color));
        }
    }
}
