using System;

namespace AlbocariusCompanions.Core.Logging;

/// <summary>
/// Wraps every entry point the game calls (GAME_MAP Q15: the game catches nothing — an exception in a submodule
/// override, campaign event handler or dialog delegate escapes to the engine). Exceptions are logged, never rethrown.
/// </summary>
public static class Safe
{
    public static void Run(ModLogger log, string context, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            LogSwallowed(log, context, ex);
        }
    }

    public static T Run<T>(ModLogger log, string context, Func<T> func, T fallback)
    {
        try
        {
            return func();
        }
        catch (Exception ex)
        {
            LogSwallowed(log, context, ex);
            return fallback;
        }
    }

    /// <summary>Even the error report is guarded: a dead logger must not turn a swallowed exception into a crash.</summary>
    private static void LogSwallowed(ModLogger? log, string context, Exception ex)
    {
        try { log?.Error(context, ex); }
        catch (Exception) { /* nothing left to do but keep the game running */ }
    }
}
