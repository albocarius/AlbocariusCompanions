# Editor architecture

A PySide6 node editor for companion packs (orchestra/SPEC_EDITOR.md). The JSON files under
`module/ModuleData/Companions/<id>/` are the only source of truth: the editor loads them, edits them in memory and writes
them back. It keeps no project database. Validation and simulation use the mod's own CLI; nothing of the mod is
reimplemented here.

```
gui/
  pyproject.toml            uv project; `uv run albocarius-editor [folder]`, `uv run pytest`
  docs/reference.png        visual reference (layout and look; not its node types)
  src/albocarius_editor/
    data/                   data layer — no Qt
    schema/                 schema layer — no Qt; reads the CLI exports (bundled copies in schema/fixtures/)
    cli/                    the CLI as a subprocess — no Qt
    ui/                     PySide6: window, panels, canvas
  tests/                    pytest (+ pytest-qt for the UI smoke test, headless)
```

The layers depend downward only: `ui` → `schema`, `cli`, `data`; `schema` and `cli` → `data` (only `workspace.find_repo_root`).
Everything below `ui` is tested without a display.

## Data layer (`data/`)

| Module | Main classes / functions | Job |
|---|---|---|
| `jsonfmt` | `loads`, `dumps`, `Layout` | Layout-preserving JSON (below) |
| `companion` | `CompanionFolder`, `JsonDocument` | One companion folder: `companion.json`, `dialogs.json`, `lang/*.json`, the layout sidecar; dirty state, save, external-change detection, `snapshot()`/`restore()` for undo |
| `edit` | `add_node`, `delete_node`, `rename_id`, `connect`, `disconnect`, `move_edge`, `copy_nodes`/`paste_nodes`, `add_conversation`, `add_line`, `add_variable`, … | Edits that keep the files consistent: ids, references, texts in every lang file, layout entries. Raise `EditError` and change nothing when not allowed |
| `graph` | `build`, `ConversationGraph`, `Edge`, `depths`, `auto_layout` | A conversation as cards + ordered edges; the left → right auto layout |
| `layout` | `NodeLayout` | The `dialogs.layout.json` sidecar |
| `ids` | `is_valid_local_id`, `propose`, `propose_node_id` | Id rules (docs/SCHEMA.md) and free-id proposals |
| `workspace` | `companion_folders`, `new_companion`, `find_repo_root` | A Companions directory; new companions copied from `tools/companion-template/` in Python |

Values are plain `dict`/`list` exactly as parsed, so **unknown fields are preserved** without the editor knowing them.
Typed views are not used on purpose: the schema export describes the shape; the data layer only knows the few fields
it maintains (ids, `start`, `next`, `speaker`, lang `lines`, `variables`).

### Round trip (`jsonfmt`)

The companion files mix styles (`{ "vigor": 4, … }` inline objects in `companion.json`, `{"id": …}` rows in the
template's `dialogs.json`, fully expanded blocks in the sample). `loads` records for every container and every
string/number its exact source text, keyed by a *path* (object keys; list items that are objects with an `"id"` are keyed
by that id so insertions do not shift the others; otherwise by index). `dumps`:

1. a value equal to what was read at the same path and depth (type-exact: `1` ≠ `true`) is written as its original text;
2. a changed container is written inline if it was inline (keeping its inner padding, `", "` and `": "` separators),
   else expanded with the file's indent (detected from the file; 2 spaces when new);
3. new containers are expanded; empty ones are `[]` / `{}`; new strings are written with `ensure_ascii=False`;
4. BOM, line ending (LF/CRLF) and the presence of a final newline are kept.

So an **unedited file is byte-identical** (tested on the sample and the template), and an edit changes only the lines
of the containers it touched. `save()` writes only files whose text differs from disk (atomic: temp file + replace).

### Layout sidecar

`dialogs.layout.json` next to `dialogs.json` (the mod never reads it):

```json
{
  "conversations": {
    "the_scar": {
      "@header": [0, 0],
      "p_scar_ask": [360, 0],
      "@hub": [1800, 0]
    }
  }
}
```

Keys are graph keys: a node id, or `@header` / `@hub` / `@end` (node ids cannot contain `@`). Items without a stored
position are auto laid out (`graph.auto_layout`: depth from the header by breadth-first search, unreachable nodes after
the deepest, hub/end last; stacked by real card heights). Entries naming a missing conversation/node are reported as
diagnostics ("layout: node not found") and dropped on the next save. Auto positions alone do not mark the folder
unsaved; a drag does. A sidecar that cannot be read is reported and rewritten on save.

## Schema layer (`schema/catalog.py`)

`SchemaCatalog` wraps the CLI's `export schema` and `export triggers` (docs/EXPORTS.md): objects and their fields
(`FieldSpec`: type, required, default, allowed values, range, pattern; `widget_kind()` picks the form widget), the
condition and effect catalogues (`CatalogEntry`: the keys each writes, `new_entry()` a valid starting value), slots and
their groups, enums, patterns, triggers (`TriggerInfo`). It is loaded from the CLI at start; when the CLI is missing or
fails, from `schema/fixtures/` (captured from the same exports — `test_bundled_fixtures_equal_the_cli_exports` fails when
they go stale). The status bar shows which (`Schema: cli|bundled`). Nothing in the editor lists fields, conditions,
effects or triggers by hand; summaries on cards (`ui/canvas/summary.py`) format values per catalogue name.

## CLI (`cli/client.py`)

- `locate()`: `ALBOCARIUS_CLI` env var → the `cliPath` setting → `tools/AlbocariusCompanions.Cli/bin/{Release,Debug}/net8.0/AlbocariusCompanions.Cli.dll`
  of the repository the editor runs from (newest). A `.dll` runs as `dotnet <dll>`, anything else as an executable.
  No bash, no Linux-only paths; on Windows the console window is suppressed.
- `Cli.validate(path)` → `validate --json` (exit 0/1 both carry JSON); `Cli.export("schema"|"triggers")`.
- `ServeClient`: one long-lived `serve` process, one JSON line per request with an increasing `id`, the answer read
  synchronously (the protocol answers in order). The UI will run it on a worker thread (PLAN T16/T17).
- A missing CLI never stops the editor: it opens and edits; validation and preview show the reason.

## UI layer (`ui/`)

| Module | Class | In the reference |
|---|---|---|
| `main_window` | `MainWindow` | menus, toolbar (New/Open/Save/Reload, Validate, Play/Stop, Snap), docks, status bar; open/save/reload, unsaved-changes guard, external-change check (every 2 s), undo stack |
| `commands` | `EditCommand`, `MoveCommand` | undo: data edits as snapshots, moves as positions |
| `library` | `LibraryPanel` | left: search, add palette, graph navigation (Lines + conversations → nodes), companions of the opened directory |
| `canvas/view` | `CanvasView`, `ZoomOverlay` | center: grid, wheel zoom, pan (middle button or Space+drag), fit (F), zoom box |
| `canvas/scene` | `ConversationScene` | one conversation: builds cards/edges from `data.graph` + layout, selection, highlight, `place()` |
| `canvas/items` | `Card`, `EdgeItem` | cards with colored headers (companion green, player violet, conversation/hub/end blue), ports, sections; ordered edges (index labels), dashed red stubs for missing targets |
| `lines_view` | `LinesView` | the Lines tab (grouped by slot kind) |
| `inspector` | `InspectorPanel` | right, first tab: every schema field of the selection, translations side by side, voice (play when QtMultimedia exists) |
| `blackboard` | `BlackboardPanel` | right, second tab: variables as cards |
| `diagnostics` | `DiagnosticsPanel` | bottom, first tab: diagnostics in the `validate --json` shape, by source; activating one reveals the node |
| `preview` | `PreviewPanel` | bottom, second tab: simulator (layout only in session 1) |
| `theme` | `apply`, `CARD_STYLES` | the dark palette and style sheet |

### How the canvas maps to JSON

| Canvas | JSON |
|---|---|
| tab | one entry of `dialogs.json` `conversations` (by `id`); plus the **Lines** tab for `lines` |
| header card (`@header`) | the conversation object: `id`, `entry` or `trigger`, `once`, `priority`, `when` |
| edge from the header, label *n* | `start[n-1]` |
| line card | one entry of `nodes`; color by `speaker`; text = `lang/en.json` `lines[<id>]`; sections from `when`/`effects` |
| edge from a card, label *n* | `next[n-1]` (order matters: menu order / candidate order) |
| Back to hub / End card | `next: ["hub"]` / `["end"]` (one shared card each per conversation) |
| dashed red stub | a `start`/`next` entry naming no node of the conversation |
| card position | `dialogs.layout.json` |

### Undo

Every edit is a `QUndoCommand` (`ui/commands.py`). Moves are `MoveCommand` (position only). Data edits go through
`MainWindow.edit(text, fn)`: an `EditCommand` runs `fn(folder)` (one or more `data.edit` calls) once, holding
`folder.snapshot()` before and after; undo/redo `restore()` a snapshot and call `MainWindow.refresh()`, which rebuilds the
open scenes (closing tabs whose conversation is gone), the library, the Lines tab, the blackboard and the inspector. An
`EditError` makes the command obsolete (the stack drops it) and shows in the status bar. Optional `on_redo`/`on_undo`
hooks run before the rebuild (a renamed conversation's tab follows its id). Snapshots are deep copies of the in-memory
JSON (small: a companion is tens of KB).

Widgets that edit commit once (focus out, Enter, a button) and emit a signal; the window turns it into one
`EditCommand`. Inline card text uses `canvas/items.TextEditor`, which commits on the next event-loop turn because the
commit rebuilds the scene that owns it.

## Packaging (`packaging/`)

`packaging/build.py` builds one executable with PyInstaller (`--onefile`, no console) for the platform it runs on:
Python, PySide6, `schema/fixtures`, `ui/icons` and a copy of `tools/companion-template` (unpacked to
`albocarius_editor/companion_template`, found by `workspace.default_template()` when there is no repository).
`--with-cli` also runs `dotnet publish` of the CLI as a self-contained single file and embeds it as
`albocarius_editor/cli_bin/AlbocariusCompanions.Cli[.exe]`, the last place `cli.locate()` looks (after the env var, the
setting and a repository build). `albocarius-editor --check` prints what was found and exits.

## Tests

`cd gui && uv run pytest` (headless: `conftest.py` sets `QT_QPA_PLATFORM=offscreen`).
`test_jsonfmt` (round trip, minimal diffs), `test_companion` (load/save/byte identity, edits, workspace),
`test_layout_graph` (graph, auto layout, sidecar), `test_schema_cli` (catalog; real-CLI tests skip when the CLI is not
built), `test_edit_ops` (edit functions added for the UI), `test_ui_smoke` (window, canvas, inspector, undo, diagnostics,
missing CLI), `test_ui_edit` (editing through the window: PLAN T01–T05), `test_packaging` (bundled CLI/template lookup,
`--check`).
The CLI side (§3) is tested in `tests/AlbocariusCompanions.Acceptance/ExportTests.cs`.
