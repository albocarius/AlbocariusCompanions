# AlbocariusCompanions editor

A node editor for companion packs (Python + PySide6). Spec: orchestra/SPEC_EDITOR.md. Design: ARCHITECTURE.md.
Remaining work: PLAN.md.

```
cd gui
uv sync                      # once; add --extra audio for the play button (QtMultimedia)
uv run albocarius-editor ../module/ModuleData/Companions
uv run pytest
```

Validation and the preview use the mod's CLI. Build it once with `dotnet build tools/AlbocariusCompanions.Cli` (from the
repository root), or point `ALBOCARIUS_CLI` at `AlbocariusCompanions.Cli.dll`. Without it the editor still opens and
edits files.

Quick tour: double-click a card's text to write it; drag palette entries onto the canvas; drag from a card's right
port onto another card to link them (refused links turn the target red, with the reason in the status bar); select a
card to edit its fields, conditions, effects, trigger and translations in the inspector; variables live on the
Blackboard tab. Save (Ctrl+S) validates with the mod's CLI; Play (F5) simulates a talk in the Preview tab and follows it
on the canvas. Undo/redo covers every edit.

Opening a folder writes nothing. Saving writes only the files you changed, plus `dialogs.layout.json` (card positions,
next to `dialogs.json`; the mod ignores it).

## One-file executable

```
cd gui
uv run --group build python packaging/build.py              # dist/albocarius-editor (.exe on Windows)
uv run --group build python packaging/build.py --with-cli   # also embeds the mod's CLI (needs the .NET SDK to build)
```

Build on each platform for that platform (PyInstaller does not cross-compile). With `--with-cli` the executable needs
nothing else installed: validation and the preview use the embedded CLI. Without it, point `ALBOCARIUS_CLI` at a CLI.
`albocarius-editor --check` prints which CLI, schema and template the executable found.
