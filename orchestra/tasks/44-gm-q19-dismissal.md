# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 44-gm-q19-dismissal: GAME_MAP question Q19

## Goal
Answer Q19 by reading the decompiled game source and write it into the `## Q19 — Companion dismissal` section of `docs/GAME_MAP.md`. Research only, no code.
Sections Q1–Q17 are VERIFIED — read the relevant ones (especially Q6 hiring, Q7 dialog API, Q3 tavern placement, Q1 wanderer spawn) and reference them instead of repeating.
Background: the mod (AlbocariusCompanions) adds custom wanderer-like companions with their own dialog lines; see `orchestra/SPEC_CORE_GAPS.md` for why this is asked.

The question:

**Player dismissing a companion**: (a) which dialog/action/event handles the player dismissing a companion from the clan, and what happens to the hero step by step (occupation, clan, party, StayingInSettlement/settlement, IsActive/HeroState, any disable/kill) — this mod observed that a dismissed companion disappears from the map; explain why from the code. (b) How a player dismissal can be told apart from every other way a hero leaves the clan or the party: death, capture/prison, release, other mods calling the same action, kingdom/clan events, marriage, becoming a governor/lord, quests. Name the `RemoveCompanionAction` detail (or other enum/argument) and the `CampaignEvents` event(s) with their parameters that carry the reason. (c) How vanilla turns a hero back into an unhired wanderer and places it in a tavern again (the exact actions/methods: occupation, clan = null, settlement placement like Q3, CompanionsCampaignBehavior lists), and whether vanilla ever does this for a dismissed companion.

Where to start: `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Actions/RemoveCompanionAction.cs`, `decomp/SandBox/SandBox.CampaignBehaviors/CompanionDismissCampaignBehavior.cs`, `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionRolesCampaignBehavior.cs`, `CompanionsCampaignBehavior.cs`, `CampaignEvents.cs` (`CompanionRemoved`), `Hero.cs`, `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Actions/EnterSettlementAction.cs`, `LeaveSettlementAction.cs`, `DisableHeroAction.cs`.

## Files
- Read: `decomp/**` (read-only), `~/bannerlord/moduledata/**` (read-only, vanilla XML incl. `*_strings.xml`), `docs/GAME_MAP.md`.
- Edit: `docs/GAME_MAP.md` — ONLY the `## Q19` section (between its header and the next `## ` header). Do not touch other sections.
- Search with grep/rg on the decomp folder. Read files in parts.

## Required section format (an acceptance test checks it)
```
## Q19 — Companion dismissal

Status: DRAFT

### Answer
<plain-language answer, every sub-question addressed>

### Evidence
- `decomp/<path to .cs file>` — `Class.Member` — <what this shows, one line>

### Uncertain
- **UNCERTAIN**: <claim> — <what would resolve it>   (or exactly: None.)
```
Rules for the section:
- Every fact in the Answer must be backed by at least one Evidence bullet. Use the exact bullet format:
  dash, space, backticked path, space, em dash (—), space, backticked symbol, space, em dash, space, text.
- The path must be a real file (relative to repo root, starting with `decomp/`; or `~/bannerlord/...` for XML facts).
- The LAST identifier in the backticked symbol must literally appear in that file. The test checks this.
- Never guess. If you cannot find something, write it under Uncertain as **UNCERTAIN** with what would resolve it.
- Signatures and short quoted English dialog texts are fine; do not copy method bodies. Code blocks max 6 lines. Tables are fine.
- Set `Status: DRAFT` (the orchestrator sets VERIFIED later).

## Check command
`dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build`
(test `GameMapTests` for q=19 must pass).
