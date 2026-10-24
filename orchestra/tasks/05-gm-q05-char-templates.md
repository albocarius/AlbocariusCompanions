# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 05-gm-q05-char-templates: GAME_MAP question Q5

## Goal
Answer SPEC Phase 1 question Q5 by reading the decompiled game source, and write the answer into
the `## Q5 — ...` section of `docs/GAME_MAP.md`. This is research only: no code changes.

The question (from orchestra/SPEC.md):

5. **Character templates:** how wanderer templates are defined (XML in which module, which element/attributes), and whether a mod can create a hero at runtime **without** an XML template.

Known from Q1–Q4 (already VERIFIED in docs/GAME_MAP.md, read them first): the vanilla wanderer template XML (`occupation="Wanderer"`, `is_template="true"`, probably `spspecialcharacters.xml`) is NOT in the local `~/bannerlord/moduledata` extract — do not spend time hunting for it; document the element/attribute format from `CharacterObject.Deserialize` / `BasicCharacterObject.Deserialize` and the template-shaped entries that do exist (`spnpccharactertemplates.xml`). Also find out how a module's XML files get loaded into MBObjectManager (e.g. `<XmlNode>` entries in SubModule.xml / `MBObjectManager.LoadXML` / `XmlResource`), since a mod would need that path.

Where to start: Search `~/bannerlord/moduledata/SandBox/spnpccharactertemplates.xml` and `spnpccharacters.xml` for wanderer templates (occupation="Wanderer", ids like spc_wanderer_*). Record element/attribute names. In decomp, find how templates are selected (CharacterObject.Templates / culture NotableAndWandererTemplates or similar). Also determine whether a CharacterObject can be created at runtime without XML (search `MBObjectManager.CreateObject`, `CharacterObject` constructors). Also list 2-3 valid culture string ids from `~/bannerlord/moduledata/SandBoxCore/spcultures.xml` (or SandBox) and 2-3 equipment roster/template ids usable for a wanderer.

## Files
- Read: `decomp/**` (read-only), `~/bannerlord/moduledata/**` (read-only), `~/bannerlord/dlls/**` (read-only), `docs/GAME_MAP.md` (earlier sections may help).
- Edit: `docs/GAME_MAP.md` — ONLY the `## Q5` section (between its header and the next `## Q` header). Do not touch other sections.
- Search with grep/rg on the decomp folder, e.g. `grep -rn "IsWanderer" decomp/TaleWorlds.CampaignSystem | head -50`. Read files in parts.

## Required section format (an acceptance test checks it)
```
## Q5 — <keep existing title>

Status: DRAFT

### Answer
<plain-language answer, every sub-question of Q5 addressed>

### Evidence
- `decomp/<path to .cs file>` — `Class.Member` — <what this shows, one line>

### Uncertain
- **UNCERTAIN**: <claim> — <what would resolve it>   (or exactly: None.)
```
Rules for the section:
- Every fact in the Answer must be backed by at least one Evidence bullet. Use the exact bullet format:
  dash, space, backticked path, space, em dash (—), space, backticked symbol, space, em dash, space, text.
- The path must be a real file (relative to repo root, starting with `decomp/`; or `~/bannerlord/...` for XML/DLL facts).
- The LAST identifier in the backticked symbol (e.g. `CreateSpecialHero` in `HeroCreator.CreateSpecialHero(...)`) must literally appear in that file. The test checks this.
- Never guess. If you cannot find something, write it under Uncertain as **UNCERTAIN** with what would resolve it.
- Signatures are fine; do not copy method bodies. Code blocks max 6 lines.
- Set `Status: DRAFT` (the orchestrator sets VERIFIED later).

## Check command
`dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build`
(test `GameMapTests` for q=5 must pass).
