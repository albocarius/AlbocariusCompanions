# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 42-gm-q18-vanilla-dialog-points: GAME_MAP question Q18

## Goal
Answer Q18 by reading the decompiled game source and write it into the `## Q18 — Vanilla wanderer/companion dialog points` section of `docs/GAME_MAP.md`. Research only, no code.
Sections Q1–Q17 are VERIFIED — read the relevant ones (especially Q6 hiring, Q7 dialog API, Q3 tavern placement, Q1 wanderer spawn) and reference them instead of repeating.
Background: the mod (AlbocariusCompanions) adds custom wanderer-like companions with their own dialog lines; see `orchestra/SPEC_CORE_GAPS.md` for why this is asked.

The question:

**Every vanilla dialog point where a wanderer or a player companion speaks or is spoken to**: first meeting/introduction, backstory, hiring (offer, price, accept, decline, too many companions, not enough gold), conversations with a hired companion (e.g. roles, giving orders, "I'd like to talk about..."), dismissal / leaving the clan, grievances or leave threats, and any other point you find. For EACH point give: the behavior class + method that registers it; input token → output token (the token flow, also what the conversation returns to); the condition method and what it checks (in words); the speaker (NPC or player line); the vanilla English text and its `{=id}` exactly as in the source (C# literal or `*_strings.xml` entry; if the text is assembled from GameTexts, name the GameText id and quote its English text from moduledata). Present the result as one table per behavior (columns: point, tokens in→out, speaker, condition, {=id}, English text) plus a short Answer paragraph. Also say which points apply to wanderers that are NOT yet hired versus companions already in the player clan.

Where to start: `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/LordConversationsCampaignBehavior.cs` (hiring, wanderer intro), `CompanionRolesCampaignBehavior.cs`, `CompanionGrievanceBehavior.cs`, `CompanionsCampaignBehavior.cs` in the same folder, `decomp/SandBox/SandBox.CampaignBehaviors/CompanionDismissCampaignBehavior.cs`; grep `AddDialogLine\|AddPlayerLine` together with `IsWanderer\|IsPlayerCompanion\|CompanionOf` across `decomp/`; wanderer intro texts may come from `~/bannerlord/moduledata/**/*strings*.xml` (grep `str_wanderer\|prebackstory\|backstory`).

## Files
- Read: `decomp/**` (read-only), `~/bannerlord/moduledata/**` (read-only, vanilla XML incl. `*_strings.xml`), `docs/GAME_MAP.md`.
- Edit: `docs/GAME_MAP.md` — ONLY the `## Q18` section (between its header and the next `## ` header). Do not touch other sections.
- Search with grep/rg on the decomp folder. Read files in parts.

## Required section format (an acceptance test checks it)
```
## Q18 — Vanilla wanderer/companion dialog points

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
(test `GameMapTests` for q=18 must pass).
