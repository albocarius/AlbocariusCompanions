# Editor plan

Source: orchestra/SPEC_EDITOR.md. Architecture: gui/ARCHITECTURE.md. Do the tasks **in order, one per session**. Each
task ends with its done-check passing **and** the whole suite green:

```
cd gui && uv run pytest                                  # editor (headless)
dotnet build tools/AlbocariusCompanions.Cli && dotnet test tests/AlbocariusCompanions.Acceptance --no-build --filter "FullyQualifiedName~ExportTests|FullyQualifiedName~CliTests|FullyQualifiedName~ServeTests"
```

Rules for every task: keep the layers (no Qt in `data/`, `schema/`, `cli/`); never hardcode fields, conditions, effects or
triggers — read them from `SchemaCatalog`; every edit goes through `data.edit` (or a new function there, with a pytest)
and is undoable; texts live only in `lang/*.json`; the boundaries of SPEC_EDITOR §0 hold (`src/`, `module/`, `orchestra/`
untouched). Add a pytest(-qt) test for each task.

## Environment notes (cloud session 1)

- The cloud proxy blocks Microsoft's .NET download host (`builds.dotnet.microsoft.com`), so `dotnet-install.sh` fails.
  Ubuntu's own package works: `apt-get install -y dotnet-sdk-10.0` (10.0.113, the distro build docs/BLOCKERS.md mentions;
  no crash seen in this session). The CLI targets net8.0 with RollForward Major and runs on it.
- Headless Qt needs `apt-get install -y libegl1 libgl1 libxkbcommon0 libfontconfig1 libdbus-1-3 libglib2.0-0t64`.
- The solution build (`dotnet build AlbocariusCompanions.sln`) cannot pass in the cloud: the Game project needs the game
  DLLs. Build the CLI and the acceptance tests directly. In the acceptance suite, 84 tests fail in the cloud for the same
  reason (missing `decomp/`, game DLLs, `dist/`); that set did not change with the §3 work. In Core.Tests, 4
  `NpcCharacterXmlGeneratorTests` fail for the missing `xmlschemas/`.
- `PySide6-Essentials` is the dependency; QtMultimedia (inspector play button) needs `uv sync --extra audio`.
- UI tests use the `window` fixture of `tests/conftest.py`; it drops the folder before closing, so the unsaved-changes
  question never blocks a test. Run with a time limit when in doubt (a modal dialog hangs a headless run):
  `timeout 300 uv run pytest -q`. Tests that answer a dialog monkeypatch `QMessageBox.question`.

## Done in session 1

- [x] **S1.1 CLI exports (§3).** `export schema`, `export triggers`, `validate --json` in `tools/AlbocariusCompanions.Cli`
  (`SchemaExport`, `TriggerExport`, `DiagnosticExport`); docs/EXPORTS.md; `ExportTests` (sync with the model classes,
  docs/SCHEMA.md, docs/TRIGGERS.md and the registry). Existing commands unchanged.
- [x] **S1.2 Skeleton.** uv project, `uv run albocarius-editor`, main window with toolbar, library, tabbed canvas,
  inspector/blackboard, diagnostics/preview, status bar, dark theme after `docs/reference.png`.
- [x] **S1.3 Data layer** (load, layout-preserving save, byte-identical round trip, edits with references and lang files,
  snapshots, external changes, workspace/template copy) and **schema layer** (catalog from the CLI or bundled fixtures).
- [x] **S1.4 Canvas** of a real conversation: header, line cards, hub/end, ordered edges, missing-target stubs, auto
  layout, layout sidecar load/save with stale-entry diagnostics, drag with snap, undoable moves.
- [x] **S1.5** `gui/ARCHITECTURE.md`. **S1.6** this plan.

Also already in place (later tasks build on them): inspector (fields, translations, voice + play; since session 2 the
id, English text and Start/Next are editable), read-only blackboard, Lines tab as a grouped list, library search over ids and English text, unsaved-changes guard, reload,
external-change prompt, new companion from the template.

## Done in session 2

- [x] **T01** `ui/commands.py`: `EditCommand` (snapshot before/after, obsolete on `EditError`), `MoveCommand` moved
  there; `MainWindow.edit(text, fn)` / `refresh()`. Tests: `tests/test_ui_edit.py`.
- [x] **T02** inline English text (`canvas/items.TextEditor`, double-click the text block) and the inspector's `en` box
  (`CommitTextEdit`), both through `edit.set_text` (new, `tests/test_edit_ops.py`).
- [x] **T03** palette click (view center) / drag onto the canvas (drop point, snapped), Graph → Add companion/player line
  (Ctrl+Shift+N / Ctrl+Shift+P); hub/end entries connect the selected node.
- [x] **T04** drag from an output port onto a card (`edit.connect`), selectable edges + Del (`edit.disconnect`), the
  inspector's Start/Next list with ↑ ↓ × (`edit.move_edge`).
- [x] **T05** Del on cards (one command, asks when texts exist), inspector id field renames (audio file offered, not
  undoable), conversation rename/delete from the tab and header-card context menus (tabs follow renames and undo).
- [x] **Packaging** (asked for by the user, beyond SPEC_EDITOR §6): `packaging/build.py` makes one executable with
  PyInstaller, optionally with a self-contained CLI inside (`--with-cli`); `albocarius-editor --check`. See README.
  Built and checked on Linux (x64, 89 MB with the CLI; runs without .NET). Windows: part of T20.

## Done in session 3

Every task up to T19 (T20 needs Windows). Tests per task: `tests/test_ui_forms.py` (T06–T10), `test_ui_blackboard`
(T11), `test_ui_lines` (T12), `test_ui_conversations` (T13), `test_ui_clipboard` (T14), `test_ui_validate` (T15),
`test_ui_preview` (T16/T17, skipped without the CLI), `test_ui_settings` (T18), `test_ui_arrange` (T19); new
`data.edit` functions in `tests/test_edit_ops.py`.

- [x] **Link validation** (asked for by the user): `data.edit.link_problem` holds the schema's rules for `start`/`next`
  (no player → player, no mixing player options and companion lines in one list, hub conversations start with player
  options, greeting/triggered ones with companion lines, hub/end never a start, no duplicates). `edit.connect` refuses
  them; while a link is dragged the card under the mouse turns green/red and the reason shows in the status bar.
- [x] **T06–T10** `ui/forms.py` + the inspector rebuilt on it. Generic path edits in `data.edit` (`set_at`, `remove_at`
  pruning emptied containers so removing the last condition restores the file, `merge_at`, `append_at`, `remove_keys`,
  `replace_keys`). Conversation "starts from" hub / greeting / trigger (`edit.set_start_mode`). Every language editable;
  + Language (`edit.add_language`), Copy English.
- [x] **T11** blackboard: add, remove, rename (`edit.rename_variable`, references follow, a dialog warns about saved
  values), type/scope/default (`edit.set_variable`; a new type drops the old default); names drag onto fields.
- [x] **T12** `canvas/lines_scene.py` replaces `lines_view.py`. Placeholder cards (`@slot:<slot>`) for required slots
  without a line; Graph → Add line… (Ctrl+Shift+L).
- [x] **T13** Graph → New conversation… (Ctrl+Shift+C), duplicate (`edit.duplicate_conversation`), move up/down
  (`edit.move_conversation`) from the tab, header-card and library context menus.
- [x] **T14** Ctrl+C / Ctrl+V / Ctrl+D; mime `application/x-albocarius-nodes` + text; nodes keep file order. Copied
  nodes are dropped from the clipboard on quit (`app._release_clipboard`): Qt deleting PySide-made clipboard data at
  shutdown segfaults.
- [x] **T15** validation on save and Build → Validate (F7) on a worker thread (`ui/workers.py`).
- [x] **T16/T17** `ui/preview.py` (Conversation) and `ui/preview_triggers.py` (Triggers); Play F5 / Stop Shift+F5.
- [x] **T18** `ui/settings.py`: Settings… (CLI path checked by running `export schema`, default Companions directory,
  opened at start unless a folder is given on the command line); File → Open recent (5).
- [x] **T19** Graph → Arrange (Ctrl+L), Edit → Find (Ctrl+F, Enter reveals the first match), toolbar icons.

## Remaining tasks

### ~~T01 Generic undoable edit command~~ (done, session 2)
Add `ui/commands.py` with `EditCommand(window, text, fn)`: on first redo takes `folder.snapshot()`, runs `fn(folder)`
(a `data.edit` call), takes the after-snapshot; undo/redo `folder.restore()` and call `window.refresh()` (new: rebuild
open scenes, library, lines view, blackboard, inspector, title). An `EditError` shows in the status bar and pushes
nothing. Move `MoveCommand` there.
**Done-check:** a pytest-qt test pushes an `EditCommand` wrapping `edit.delete_node`, sees the card gone, undoes, sees it
back, redoes; an `EditError` pushes no command.

### ~~T02 Inline English text on cards~~ (done, session 2)
Double-click a card's text block → an in-place `QGraphicsTextItem`/proxy editor; Enter (Shift+Enter = newline) or
focus-out commits through `EditCommand(set_text)`; Esc cancels. The inspector's `en` box becomes editable too, same command.
**Done-check:** test edits a node's text, saves, only `lang/en.json` (and the sidecar) changed; undo restores the old text.

### ~~T03 Add nodes~~ (done, session 2)
Palette entries (click: at the view center; drag: at the drop point) and Graph → Add companion/player line add a node via
`edit.add_node` (id from `ids.propose_node_id`), selected and with its text editor open (T02). "Back to hub" / "End"
entries connect the selected node via `edit.connect` to `HUB`/`END`.
**Done-check:** test adds an npc and a player node to `the_scar`, both in `dialogs.json` and `lang/en.json` after save,
positions in the sidecar; undo removes both.

### ~~T04 Edges: connect, disconnect, reorder~~ (done, session 2)
Drag from a card's output port to another card (or hub/end) → `edit.connect`; select an edge + Del → `edit.disconnect`;
the inspector shows `next`/`start` as an ordered list with up/down buttons → `edit.move_edge`. Refuse what `edit`
refuses (status bar message).
**Done-check:** test connects `scar_thanks` → `scar_shrug`, reorders `scar_story`'s next, deletes an edge; JSON matches;
each step undoes.

### ~~T05 Delete and rename~~ (done, session 2)
Del on selected cards → `edit.delete_node` for each (one command; confirm when texts exist). Inspector id field editable
→ `edit.rename_id`; when the result names an audio file, ask and run `edit.rename_audio` (not undoable: say so in the
dialog). Conversation rename/delete from the header card / tab context menu.
**Done-check:** tests for delete (references gone, all lang keys gone), rename (lang keys in every file, layout key,
`next` references), audio prompt path via a monkeypatched `QMessageBox`.

### ~~T06 Schema-driven field forms~~ (done, session 3)
`ui/forms.py`: build a widget for a `FieldSpec` by `widget_kind()` (check, combo, editable-combo, spin with min/max,
line with pattern validation, tags for string lists, json for `any`), two-way bound to a dict key, emitting one
`EditCommand` per committed change; absent optional fields show their default as placeholder and an "×" to remove the
key. Use it in the inspector for scalars of `node`, `line`, `conversation` (not `when`/`effects`/`trigger`: T07–T09).
**Done-check:** test sets `priority` and `voiced` on a node through the form, checks JSON and undo; a pattern-invalid id
is refused.

### ~~T07 Conditions editor ("+" sections)~~ (done, session 3)
Card section and inspector section for `when`: "+" opens a menu of `catalog.conditions` (description as tooltip); each
item is a small form of its params (T06 widgets); "×" removes its keys; `all`/`any` hold nested lists (indented forms,
own "+"), `not` a nested object. Variable fields offer the blackboard's names (and `event.<path>` for triggered content
from the trigger's fields). Writes only through `EditCommand`.
**Done-check:** test adds `hasGold` and `any: [{flags}, {var,min}]` to a node, the JSON equals the expected object; removing
them restores the original bytes of `dialogs.json` after save.

### ~~T08 Effects editor~~ (done, session 3)
Same pattern for `effects` from `catalog.effects` (order `effectOrder`); `transfer` form (gold xor item+count; actors from
`enums.actors` + companion ids); `set`/`add` as name → value rows typed by the variable's type.
**Done-check:** test adds relation +2, a transfer and `add: {mood: 1}`; JSON and undo checked.

### ~~T09 Trigger editor~~ (done, session 3)
For a conversation header and for slot-free lines: trigger picker grouped by `catalog.trigger_categories` (common first,
searchable), subject combo from the trigger's `subjects` (`kind:<id>` asks for the id), `where` rows from its fields and
entity properties (`export triggers` `entityProperties`), chance/delivery/expiresAfterHours/cooldownDays/allowAbsent
from the `trigger` object fields, `params` form for polling triggers. Switching between `entry` and `trigger` removes the
other key.
**Done-check:** test turns a hub conversation into a `villageLooted` triggered one with a `where`, then back; JSON checked.

### ~~T10 Translations and languages~~ (done, session 3)
Inspector translations editable for every language (one `EditCommand` per commit); "Add language" creates
`lang/<code>.json` (code must match `patterns.languageCode`) with `schemaVersion` 1; missing translations shown in muted
italics with "copy English" button.
**Done-check:** test adds `de`, writes one text, saves: new file with only that line; undo removes the file from the
in-memory state (and save does not create it).

### ~~T11 Blackboard editing~~ (done, session 3)
Add/remove/rename variables (`edit.add_variable`/`remove_variable`; add `edit.rename_variable` that also renames
references in `flags`/`notFlags`/`var`/`setFlags`/`clearFlags`/`set`/`add` of this companion — warn that saved values are
lost, docs/SCHEMA.md "Variables"), type/scope/default editors from the `variable` object fields. Dragging a variable onto a
condition/effect form fills its name.
**Done-check:** tests for add, type change (default reset to the new type's default), rename with references, remove.

### ~~T12 Lines tab as a canvas~~ (done, session 3)
Replace `LinesView` with a scene of line cards (no edges) in labelled groups (`catalog.slot_group`), sorted by slot, with the
same inspector/forms; add/delete lines (`edit.add_line`/`delete_line`); slot combo from `catalog.all_slots`; positions
not stored (grid by group).
**Done-check:** test adds a `greet` line and deletes it; required-slot groups show every required slot even when it has
no line (an empty placeholder card).

### ~~T13 Conversations: new, entry, order~~ (done, session 3)
Graph → New conversation (id, entry hub/greeting or triggered) via `edit.add_conversation`; tab context menu rename /
delete / duplicate; library order follows file order, with move up/down (reorders `conversations`, which is the hub
menu order for equal priorities).
**Done-check:** tests for new/rename/delete/reorder; the new conversation opens in a tab.

### ~~T14 Copy / paste~~ (done, session 3)
Ctrl+C / Ctrl+V / Ctrl+D on selected cards using `edit.copy_nodes` / `edit.paste_nodes`; the payload goes to the system
clipboard as JSON (mime `application/x-albocarius-nodes` + text); pasted cards placed at the mouse / offset by 40.
Only within the same companion (the payload's `companion` must match; otherwise refuse with a message).
**Done-check:** test copies two nodes, pastes into another conversation: new ids, internal edge kept, texts copied in
every language.

### ~~T15 Validation through the CLI~~ (done, session 3)
On save and on Build → Validate (toolbar button enabled): run `Cli.validate(folder.path)` on a `QThreadPool` worker; put
the result in the diagnostics panel as source "validate" (keeps "layout"); clicking reveals (already wired); status bar
shows counts; a missing CLI or a `CliError` shows one diagnostic "validation unavailable: <reason>".
**Done-check:** test with a broken copy (unknown `next` target) shows the error on `the_scar/scar_story` and revealing
selects that card; with `ALBOCARIUS_CLI` pointing to a missing file the panel shows the unavailable message.

### ~~T16 Preview: conversations~~ (done, session 3)
Preview panel with a `ServeClient` on a worker thread (one request at a time): `load` the Companions root (after every
save), a state form generated from docs/SIMULATOR_PROTOCOL.md "State" (companion met/hired/inParty/relation/gold, player
gold and items, clanIsFull, every declared variable from the blackboard, started), Start hub/greeting + seed, the step's
options as buttons (hidden ones greyed with their failed conditions as tooltip), transcript of `said` with selections and
effects, the state after each step, Hire / Back to hub / End talk buttons. Toolbar Play/Stop start and close the session.
The canvas highlights (`ConversationScene.set_highlight`) the node of the last line said and opens its tab.
**Done-check:** test (skipped without CLI) plays the sample sequence of ServeTests (start hub, hire, the_scar, spare_coin)
by clicking the option buttons; the final transcript and state (player gold 400) match.

### ~~T17 Preview: triggers~~ (done, session 3)
A "Triggers" sub-tab: `triggers` session with the state form, a trigger picker (T09's), payload form from the trigger's
fields (entity fields as property rows), hours, Fire / Deliver buttons, candidate table (outcome per check) and queue view.
**Done-check:** test fires `playerBattleEnded` with `playerWon: true` and delivers `battle_won` (skipped without CLI).

### ~~T18 Settings and recent files~~ (done, session 3)
Settings dialog: CLI path (validated by running `export schema`), default Companions directory; File → Open recent (5).
Command-line argument still wins.
**Done-check:** test sets a CLI path in settings and `Cli.find` uses it; recent list updates on open.

### ~~T19 Arrange and view polish~~ (done, session 3)
Graph → Arrange (re-run `graph.auto_layout` for all or selected cards, one undoable command); a canvas minimap is
optional; Ctrl+F focuses the library search, Enter reveals the first match; toolbar icons (SVG in `ui/icons/`).
**Done-check:** test runs Arrange after scrambling positions: no overlapping cards, undo restores them.

### T20 Windows pass
Run on Windows: open the sample from a path with spaces, save a CRLF file (stays CRLF), the CLI found as
`dotnet ...Cli.dll` from the repo build output, the external-change prompt, audio play with the `audio` extra. Fix what
breaks; note results here.
Also build the package there: `uv run --group build python packaging/build.py --with-cli`, then run
`dist\albocarius-editor.exe` from a folder outside the repository on a machine without .NET (validate works, New
companion works).
**Done-check:** a checklist in this file with each item ticked on Windows.

## Notes and open points

- **Audio file naming (decided):** the editor follows docs/SCHEMA.md: `audio/<local id>.ogg`, or the line's own `audio`
  field. SPEC_EDITOR §1's `audio/<full id>.ogg` was a mistake in the spec text.
- **`validate --json` locations are a temporary workaround.** `DiagnosticExport` reads `file` / `conversation` / `node`
  from Core's message wording because Core issues carry only a folder and a message; a Core message that changes
  wording silently loses its location (ExportTests cover the main cases). The real fix is structured locations on Core
  issues (`ValidationIssue` with code, conversation id, node id, and the file), set where each issue is raised, with
  `DiagnosticExport` then reading those fields instead of regexes. It touches `src/`, so it is done **outside the cloud
  sessions** (in the orchestra workflow), not by an editor task.
- Hub and End are one card each per conversation; a conversation with many `["end"]` exits shows many edges into one card.
- **Condition / effect editing lives in the inspector.** Cards show a read-only summary of `when` / `effects`; PLAN T07
  also mentioned "+" sections on the cards themselves. Possible follow-up if editing on the canvas is wanted.
- **Variable references** (`flags`, `notFlags`, `var`, `setFlags`, `clearFlags`, `set`, `add`) are named in
  `data.edit.VARIABLE_LIST_KEYS` / `VARIABLE_MAP_KEYS` after docs/SCHEMA.md "Variables": the schema export does not
  mark which fields name a variable. An export flag for it would remove this list.
- A CLI path changed in Settings is used at once for validation and the preview; the schema catalog is re-read at
  the next start.
- Ideas after the prototype: label companion candidate links by priority instead of list position; show a
  conversation's validation issues on its cards; a minimap.
