# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 12-gm-q12-voice-over: GAME_MAP question Q12

## Goal
Answer SPEC Phase 1 question Q12 by reading the decompiled game source, and write the answer into
the `## Q12 — ...` section of `docs/GAME_MAP.md`. This is research only: no code changes.

The question (from orchestra/SPEC.md):

12. **Voice-over resolution** (mapping only — no audio implementation this run): `DefaultVoiceOverModel.GetSoundPathForCharacter` or equivalent, how the voiced-lines XML is loaded (`language_data.xml` → voiced lines XML), how a dialog text with a `{=ID}` prefix is matched to a voice entry, the file naming / fallback order (culture, gender, voice type, id), required file format (OGG expected) and lipsync XML handling. Reference guide (may be outdated, verify against source): https://docs.bannerlordmodding.lt/guides/custom_audio_with_lipsync/

Where to start: Start at `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.GameComponents/DefaultVoiceOverModel.cs`. Then search decomp (TaleWorlds.Localization, TaleWorlds.Engine, TaleWorlds.MountAndBlade) for `VoicedLine`, `voice_over`, `language_data`, `lipsync`, `.ogg`. Also list the vanilla voice type ids (expected curt, earnest, ironic, softspoken) and where they are defined (decomp or `~/bannerlord/moduledata`). The reference guide https://docs.bannerlordmodding.lt/guides/custom_audio_with_lipsync/ may be outdated: trust only what you find in decomp.

## Files
- Read: `decomp/**` (read-only), `~/bannerlord/moduledata/**` (read-only), `~/bannerlord/dlls/**` (read-only), `docs/GAME_MAP.md` (earlier sections may help).
- Edit: `docs/GAME_MAP.md` — ONLY the `## Q12` section (between its header and the next `## Q` header). Do not touch other sections.
- Search with grep/rg on the decomp folder, e.g. `grep -rn "IsWanderer" decomp/TaleWorlds.CampaignSystem | head -50`. Read files in parts.

## Required section format (an acceptance test checks it)
```
## Q12 — <keep existing title>

Status: DRAFT

### Answer
<plain-language answer, every sub-question of Q12 addressed>

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
(test `GameMapTests` for q=12 must pass).
