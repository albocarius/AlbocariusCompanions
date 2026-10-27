# SPEC — AlbocariusCompanions Editor (Qt) — Session 1: architecture + skeleton

> Runs as a Claude Code **cloud session**. The game, the decompiled sources and the `decomp/` / `xmlschemas/` symlinks are **not** available and not needed.
> Session 1 (this spec) builds the architecture, the skeleton and a task plan. Later sessions (cheaper model) continue from `gui/PLAN.md`.

## 0. Boundaries

**Allowed to change:**
- `gui/` — everything for the editor.
- `tools/AlbocariusCompanions.Cli/` — only to add the machine-readable exports in §3 (no behavior change to existing commands) and their tests.
- `docs/SIMULATOR_PROTOCOL.md` — only to document additions made in §3.

**Must not change:** `src/`, `module/`, `tests/` (except new CLI tests for §3), `orchestra/`, `.claude/`, solution layout of the mod, any companion JSON in `module/`. Read them as much as needed.

**Isolation:** `gui/` has its own `pyproject.toml`, managed with **uv** (`gui/.venv` ignored by git). `dotnet build` of the solution must not see or depend on `gui/`. The editor finds the CLI by a configurable path (default: build output of `tools/AlbocariusCompanions.Cli`), never by importing .NET code.

## 1. What the editor is

A desktop node editor for companion packs: open a companion folder, edit its dialog content visually, validate and simulate it using the mod's own CLI. Runs on **Windows and Linux**. Python + **PySide6**.

**Stateless about data:** the JSON files are the only source of truth. The editor loads them, edits them in memory, writes them back. It keeps no project database.

**Visual reference:** the attached SCRYBE screenshot (dark theme, colored card headers, left library/navigation sidebar, center canvas with tabs, right inspector + blackboard panel, bottom diagnostics/preview panel, top toolbar). Take the **look and layout** from it, not its node types — our data model is different.

## 2. Data model and mapping

Read `docs/SCHEMA.md`, `docs/TRIGGERS.md`, `docs/SIMULATOR_PROTOCOL.md`, the sample `module/ModuleData/Companions/test_companion/` and the template `tools/companion-template/` to learn the format. Do not guess fields — derive them from the schema export (§3).

- **Companion folder** = `companion.json` + `dialogs.json` + `lang/<code>.json` (+ `audio/`).
- **Canvas tabs** (like the screenshot's tabs):
  - one tab per entry in `conversations`;
  - one **Lines** tab: every entry of `lines` as a line node (id, slot, voiced, when, priority, trigger), grouped visually by slot. No edges. The template's required slots load here like any other line.
- **Conversation graph:**
  - a **header node** for the conversation itself (id, entry or trigger, once, when, priority, start);
  - one **line node** per entry in `nodes`, styled by speaker (npc / player);
  - special nodes for `hub` and `end`;
  - edges from `start` and from each node's `next` (order of `next` is preserved and editable).
- **Line node card:** shows id, speaker/slot and the `en` text (editable inline). Every list-like field (conditions in `when`, `effects`, `setFlags`, `clearFlags`, transfers, …) is a **section with a "+" button** to add items and an "×" per item; each item is a small form built from the schema. Rarely used fields live in the inspector.
- **Inspector (right):** all fields of the selected node, the translations of its text for every `lang/*.json` side by side, voice info (voiced flag, whether `audio/<full id>.ogg` exists, play button if QtMultimedia works on the platform; skip silently if not).
- **Blackboard (right, second tab):** the `variables` block (name, type, scope, default) with add/remove, like the screenshot.
- **Library (left):** add-node palette (npc line, player line, hub/end links), list of conversations (navigation), list of companions found under an opened `Companions` directory.

### Round-trip rules
- Unknown fields are preserved exactly. Key order and formatting are kept as close to the original as possible (stable 2-space JSON).
- Text lives in `lang/<code>.json`, never in `dialogs.json`. Creating a node creates its `en` entry; renaming an id renames its keys in every lang file and is offered for the audio file too.
- Ids: the editor proposes ids following the schema's rules and refuses duplicates.

### Layout sidecar
- Node positions are stored in `dialogs.layout.json` next to `dialogs.json` (the mod never reads it), keyed by conversation id + node id.
- On open: nodes with a stored position are placed there; nodes without one are auto-laid out (left → right by graph depth). Layout entries whose node no longer exists are reported in diagnostics as "layout: node not found" and dropped on next save.

## 3. Machine-readable exports (CLI additions)

The editor must not hardcode the list of conditions, effects, triggers or their fields. Add to the CLI:
- `export schema` → JSON describing every file type, field, type, default, allowed values, and the full catalogue of **conditions** and **effects** (name, parameters, types, description).
- `export triggers` → the trigger registry as JSON (name, category, common, description, subjects, payload fields, params, delivery options).
- Make sure `validate` can output JSON (diagnostics with file, conversation id, node id, severity, message). Add it if missing.
- Both exports generated from the same code as `docs/SCHEMA.md` / `docs/TRIGGERS.md`, with tests that they stay in sync.
- Document them in `docs/SIMULATOR_PROTOCOL.md` (new section) or a new `docs/EXPORTS.md`.

If installing the .NET SDK in the cloud environment is impossible, stop §3, record it in `gui/PLAN.md`, and continue the editor against JSON fixtures captured from the documented format.

## 4. Validation and simulation (via CLI, never reimplemented)

- **Validate:** on save (and on demand), run `validate --json` on the companion folder; show results in the **Diagnostics** panel; clicking a diagnostic opens the tab and selects the node.
- **Preview / simulate:** a **Preview** panel that keeps one `serve` process alive. The user edits a test state (variables, hired/inParty, gold, relation…), starts a conversation or fires a trigger, clicks player options, and sees npc picks, condition results and the state after effects. The current node is highlighted on the canvas.
- If the CLI is missing or fails, the editor still opens and edits files; validation/preview show a clear message.

## 5. Editor basics
New / open / save / reload, unsaved-changes guard, undo/redo for every edit, copy/paste of nodes within a companion, search nodes by id or text, zoom/pan/fit, snap to grid toggle, detect external changes to the files and offer reload. New companion: copy `tools/companion-template/` into a new folder in Python (do not call the bash script).

## 6. Quality
- Code structure: data layer (load/save/round-trip, no Qt), schema layer (reads §3 exports), UI layer. The data and schema layers are unit-tested with pytest; UI gets a smoke test with pytest-qt under `QT_QPA_PLATFORM=offscreen`.
- Round-trip test: open the sample and the template, save without edits → files byte-identical (or document the exact normalization).
- Runs with `uv run albocarius-editor` from `gui/` on Linux and Windows (no Linux-only paths, no bash calls).
- Packaging (PyInstaller etc.) is out of scope.

## 7. Session 1 deliverables (this session)
1. §3 exports (or the documented fallback).
2. `gui/` project skeleton: uv project, PySide6 main window with the full layout (toolbar, library, tabbed canvas, inspector/blackboard, diagnostics/preview), dark theme close to the reference.
3. Data layer complete with round-trip tests; schema layer reading the exports.
4. Canvas showing a real conversation from the sample (header node, line nodes, hub/end, edges) with layout sidecar load/save.
5. `gui/ARCHITECTURE.md`: layers, main classes, how the canvas maps to JSON, how the CLI is called.
6. `gui/PLAN.md`: the remaining work split into small, ordered tasks a cheaper model can do one at a time, each with a clear done-check. Mark what was finished in this session.

Stop after these. Do not start the remaining tasks — they are for later sessions.
