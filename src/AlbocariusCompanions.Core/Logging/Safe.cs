using System;

namespace AlbocariusCompanions.Core.Logging;

/// <summary>
/// Wraps every entry point the game calls (GAME_MAP Q15: the game catches nothing — an exception in a submodule
/// override, campaign event handler or dialog delegate escapes to the engine). Exceptions are logged, never rethrown.
/// </summary>
public static class Safe
{
    public static void Run(ModLogger log, string context, Action action) => throw new NotImplementedException("task 24-core-logging");

    public static T Run<T>(ModLogger log, string context, Func<T> func, T fallback) => throw new NotImplementedException("task 24-core-logging");
}
