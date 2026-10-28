"""Settings dialog (PLAN T18): the CLI path (checked by running ``export schema``) and the default Companions directory.
Values live in ``QSettings`` (``cliPath``, ``companionsDir``); a command-line argument still wins when starting."""

from __future__ import annotations

from pathlib import Path

from PySide6.QtWidgets import (QDialog, QDialogButtonBox, QFileDialog, QFormLayout, QHBoxLayout, QLabel, QLineEdit,
                               QPushButton, QVBoxLayout, QWidget)

from ..cli.client import Cli, CliError

RECENT_KEY = "recent"
RECENT_MAX = 5


def check_cli(path: str) -> str | None:
    """None when ``path`` is a working CLI (it answers ``export schema``), else why not."""
    p = Path(path).expanduser()
    if not p.is_file():
        return f"{p} does not exist"
    try:
        Cli(p, timeout=60).export("schema")
    except CliError as e:
        return str(e)
    return None


def recent_list(settings) -> list[str]:
    value = settings.value(RECENT_KEY) or []
    if isinstance(value, str):  # QSettings returns a lone entry as a string
        value = [value]
    return [str(v) for v in value if v]


def add_recent(settings, path: Path) -> list[str]:
    items = [str(path)] + [p for p in recent_list(settings) if p != str(path)]
    items = items[:RECENT_MAX]
    settings.setValue(RECENT_KEY, items)
    return items


class SettingsDialog(QDialog):
    def __init__(self, settings, parent=None):
        super().__init__(parent)
        self.settings = settings
        self.setWindowTitle("Settings")
        self.setMinimumWidth(560)
        lay = QVBoxLayout(self)
        form = QFormLayout()
        self.cli_path = QLineEdit(str(settings.value("cliPath") or ""))
        self.cli_path.setObjectName("cliPath")
        self.cli_path.setPlaceholderText("empty: the repository's build output (or ALBOCARIUS_CLI)")
        form.addRow("CLI", self._with_browse(self.cli_path, self._browse_cli, "Check", self.check))
        self.cli_status = QLabel()
        self.cli_status.setProperty("role", "muted")
        self.cli_status.setWordWrap(True)
        form.addRow("", self.cli_status)
        self.companions_dir = QLineEdit(str(settings.value("companionsDir") or ""))
        self.companions_dir.setObjectName("companionsDir")
        self.companions_dir.setPlaceholderText("opened at start when no folder is given")
        form.addRow("Companions directory", self._with_browse(self.companions_dir, self._browse_dir))
        lay.addLayout(form)
        note = QLabel("A new CLI is used right away for validation and the preview; the schema it exports is read "
                      "at the next start.")
        note.setProperty("role", "muted")
        note.setWordWrap(True)
        lay.addWidget(note)
        buttons = QDialogButtonBox(QDialogButtonBox.StandardButton.Ok | QDialogButtonBox.StandardButton.Cancel)
        buttons.accepted.connect(self.accept)
        buttons.rejected.connect(self.reject)
        lay.addWidget(buttons)

    @staticmethod
    def _with_browse(line: QLineEdit, browse, extra: str = "", extra_slot=None) -> QWidget:
        box = QWidget()
        row = QHBoxLayout(box)
        row.setContentsMargins(0, 0, 0, 0)
        row.addWidget(line, 1)
        b = QPushButton("Browse…")
        b.clicked.connect(browse)
        row.addWidget(b)
        if extra:
            e = QPushButton(extra)
            e.clicked.connect(extra_slot)
            row.addWidget(e)
        return box

    def _browse_cli(self) -> None:
        path, _ = QFileDialog.getOpenFileName(self, "The AlbocariusCompanions CLI", self.cli_path.text(),
                                              "CLI (AlbocariusCompanions.Cli*);;All files (*)")
        if path:
            self.cli_path.setText(path)
            self.check()

    def _browse_dir(self) -> None:
        path = QFileDialog.getExistingDirectory(self, "Companions directory", self.companions_dir.text())
        if path:
            self.companions_dir.setText(path)

    def check(self) -> bool:
        path = self.cli_path.text().strip()
        if not path:
            self.cli_status.setText("default lookup")
            return True
        problem = check_cli(path)
        self.cli_status.setText("works" if problem is None else f"does not work: {problem}")
        return problem is None

    def accept(self) -> None:
        if not self.check():
            return
        self.settings.setValue("cliPath", self.cli_path.text().strip())
        self.settings.setValue("companionsDir", self.companions_dir.text().strip())
        super().accept()
