using System;
using System.Collections.Generic;
using AlbocariusCompanions.Core.Models;

namespace AlbocariusCompanions.Core.Registry;

/// <summary>Read-only lookup of valid companions by id. Also exposes the id set used by the wanderer-count patch.</summary>
public sealed class CompanionRegistry
{
    /// <summary>Throws <see cref="ArgumentException"/> on duplicate ids (the catalog never passes any).</summary>
    public CompanionRegistry(IEnumerable<CompanionPackage> packages) => throw new NotImplementedException("task 17-core-registry");

    public int Count => throw new NotImplementedException();

    /// <summary>All mod companion ids (ordinal comparison).</summary>
    public IReadOnlyCollection<string> Ids => throw new NotImplementedException();

    /// <summary>All companions ordered by id (ordinal).</summary>
    public IReadOnlyList<CompanionPackage> All => throw new NotImplementedException();

    public bool Contains(string id) => throw new NotImplementedException();

    public bool TryGet(string id, out CompanionPackage? package) => throw new NotImplementedException();
}
