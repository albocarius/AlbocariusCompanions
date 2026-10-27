"""A ``Companions`` directory: the companion folders in it, and new companions copied from the template. No Qt."""

from __future__ import annotations

import shutil
from pathlib import Path

from . import ids
from .companion import COMPANION_FILE


class WorkspaceError(ValueError):
    pass


def find_repo_root(start: Path) -> Path | None:
    """The mod repository holding ``start`` (the folder with AlbocariusCompanions.sln), if any."""
    for p in [start, *start.parents]:
        if (p / "AlbocariusCompanions.sln").is_file():
            return p
    return None


# A packaged editor (gui/packaging/build.py) carries a copy of tools/companion-template here.
BUNDLED_TEMPLATE = Path(__file__).resolve().parents[1] / "companion_template"


def default_template() -> Path | None:
    """``tools/companion-template`` of the repository this editor runs from, else the copy packaged with the editor."""
    root = find_repo_root(Path(__file__).resolve())
    for template in (root / "tools" / "companion-template" if root else None, BUNDLED_TEMPLATE):
        if template is not None and template.is_dir():
            return template
    return None


def companion_folders(root: Path) -> list[Path]:
    """Folders of a Companions directory that hold a companion.json ('_shared' is reserved and skipped), by name."""
    if not root.is_dir():
        return []
    return sorted(p for p in root.iterdir()
                  if p.is_dir() and p.name != ids.RESERVED_FOLDER and (p / COMPANION_FILE).is_file())


def is_companion_folder(path: Path) -> bool:
    return (path / COMPANION_FILE).is_file()


def new_companion(root: Path, companion_id: str, template: Path | None = None) -> Path:
    """Copies the companion template into ``root/<companion_id>`` (as tools/new-companion.sh does, without calling it)."""
    template = template or default_template()
    if template is None or not template.is_dir():
        raise WorkspaceError("the companion template (tools/companion-template) was not found")
    if not ids.is_valid_companion_id(companion_id):
        raise WorkspaceError(f"'{companion_id}' is not a valid companion id: use a-z, 0-9 and '_' ('_shared' is reserved)")
    target = root / companion_id
    if target.exists():
        raise WorkspaceError(f"{target} already exists")
    root.mkdir(parents=True, exist_ok=True)
    shutil.copytree(template, target)
    return target
