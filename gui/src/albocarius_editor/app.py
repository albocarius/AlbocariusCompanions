"""Entry point: ``uv run albocarius-editor [companion folder | Companions directory]``."""

from __future__ import annotations

import sys
from pathlib import Path

from PySide6.QtWidgets import QApplication

from .ui import theme
from .ui.main_window import APP_NAME, MainWindow


def main(argv: list[str] | None = None) -> int:
    argv = sys.argv if argv is None else argv
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
