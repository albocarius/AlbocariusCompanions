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

Opening a folder writes nothing. Saving writes only the files you changed, plus `dialogs.layout.json` (card positions,
next to `dialogs.json`; the mod ignores it).
