using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AlbocariusCompanions.Core.Models;

namespace AlbocariusCompanions.Core.Registry;

/// <summary>Read-only lookup of valid companions by id. Also exposes the id set used by the wanderer-count patch.</summary>
public sealed class CompanionRegistry
{
    private readonly Dictionary<string, CompanionPackage> _byId;

    /// <summary>Throws <see cref="ArgumentException"/> on duplicate ids (the catalog never passes any).</summary>
    public CompanionRegistry(IEnumerable<CompanionPackage> packages)
    {
        if (packages == null) throw new ArgumentNullException(nameof(packages));

        _byId = new Dictionary<string, CompanionPackage>(StringComparer.Ordinal);
        foreach (var package in packages)
        {
            if (_byId.ContainsKey(package.Id))
                throw new ArgumentException($"Duplicate companion id '{package.Id}'.", nameof(packages));
            _byId.Add(package.Id, package);
        }

        All = new ReadOnlyCollection<CompanionPackage>(
            _byId.Values.OrderBy(p => p.Id, StringComparer.Ordinal).ToList());
        Ids = new ReadOnlyCollection<string>(
            _byId.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList());
    }

    public int Count => _byId.Count;

    /// <summary>All mod companion ids (ordinal comparison).</summary>
    public IReadOnlyCollection<string> Ids { get; }

    /// <summary>All companions ordered by id (ordinal).</summary>
    public IReadOnlyList<CompanionPackage> All { get; }

    public bool Contains(string id) => id != null && _byId.ContainsKey(id);

    public bool TryGet(string id, out CompanionPackage? package)
    {
        if (id != null && _byId.TryGetValue(id, out var found))
        {
            package = found;
            return true;
        }

        package = null;
        return false;
    }
}
