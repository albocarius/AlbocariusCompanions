using System;

namespace AlbocariusCompanions.Core.Loading;

/// <summary>Scans &lt;root&gt;/*/ for companion.json + dialogs.json. Never throws for bad data.</summary>
public static class CompanionLoader
{
    public const string CompanionFileName = "companion.json";
    public const string DialogsFileName = "dialogs.json";

    public static LoadResult LoadAll(string companionsRoot) => throw new NotImplementedException("task 16-core-loader");
}
