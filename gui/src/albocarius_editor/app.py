"""Entry point: ``uv run albocarius-editor [companion folder | Companions directory]``.

``--check`` prints what the editor found (CLI, schema source, companion template) and exits without a window; it is how
a packaged build is checked (gui/packaging)."""

from __future__ import annotations

import sys
from pathlib import Path

from PySide6.QtWidgets import QApplication

from .ui import theme
from .ui.main_window import APP_NAME, MainWindow


def check() -> int:
    from . import __version__
    from .cli.client import Cli, CliError
    from .data import workspace
    from .schema.catalog import SchemaCatalog

    print(f"{APP_NAME} {__version__}")
    cli = None
    try:
        cli = Cli.find()
        print(f"CLI: {cli.path}")
    except CliError as e:
        print(f"CLI: not found ({e})")
    catalog = SchemaCatalog.load(cli)
    print(f"Schema: {catalog.source} ({len(catalog.conditions)} conditions, {len(catalog.effects)} effects)")
    print(f"Template: {workspace.default_template() or 'not found'}")
    return 0


def main(argv: list[str] | None = None) -> int:
    argv = sys.argv if argv is None else argv
    if "--check" in argv[1:]:
        return check()
    app = QApplication.instance() or QApplication(argv)
    app.setApplicationName(APP_NAME)
    app.setOrganizationName("Albocarius")
    theme.apply(app)
    window = MainWindow()
    args = [a for a in argv[1:] if not a.startswith("-")]
    if args:
        window.open_path(Path(args[0]).expanduser().resolve())
    window.show()
    return app.exec()


if __name__ == "__main__":
    raise SystemExit(main())
