using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace AlbocariusCompanions.Core.Templates;

/// <summary>
/// Builds the NPCCharacters document for mod companion templates (SPEC rule 8 amendment, GAME_MAP Q11).
/// Generated at runtime, loaded with MBObjectManager.LoadXml; never committed or shipped.
/// </summary>
public static class NpcCharacterXml
{
    public static XDocument Build(IEnumerable<ResolvedCompanion> companions) =>
        throw new NotImplementedException("task 20c-core-npc-xml");
}
