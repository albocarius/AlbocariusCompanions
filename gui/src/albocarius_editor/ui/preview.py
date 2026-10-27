"""Bottom panel, second tab: the simulator preview (``serve``). Session 1 shows the layout and the CLI status; the
state editor, transcript and option buttons are later tasks (gui/PLAN.md)."""

from __future__ import annotations

from PySide6.QtWidgets import QHBoxLayout, QLabel, QPlainTextEdit, QPushButton, QVBoxLayout, QWidget


class PreviewPanel(QWidget):
    def __init__(self, parent=None):
        super().__init__(parent)
        layout = QHBoxLayout(self)
        layout.setContentsMargins(10, 6, 10, 6)

        left = QVBoxLayout()
        title = QLabel("Test state")
        title.setProperty("role", "section")
        left.addWidget(title)
        self.state = QPlainTextEdit()
        self.state.setPlaceholderText('{ "player": { "gold": 1000 } }')
        self.state.setReadOnly(True)
        left.addWidget(self.state)
        layout.addLayout(left, 1)

        right = QVBoxLayout()
        title = QLabel("Transcript")
        title.setProperty("role", "section")
        right.addWidget(title)
        self.status = QLabel()
        self.status.setProperty("role", "muted")
        self.status.setWordWrap(True)
        right.addWidget(self.status)
        self.transcript = QPlainTextEdit()
        self.transcript.setReadOnly(True)
        right.addWidget(self.transcript, 1)
        buttons = QHBoxLayout()
        self.start_button = QPushButton("Start conversation")
        self.start_button.setEnabled(False)
        buttons.addWidget(self.start_button)
        buttons.addStretch(1)
        right.addLayout(buttons)
        layout.addLayout(right, 2)
        self.set_cli(None)

    def set_cli(self, cli_path: str | None, error: str | None = None) -> None:
        if cli_path:
            self.status.setText(f"Simulator: {cli_path}. Playing conversations here is a later task.")
        else:
            self.status.setText(error or "The AlbocariusCompanions CLI was not found: build tools/AlbocariusCompanions.Cli "
                                         "or set ALBOCARIUS_CLI. Editing still works; validation and preview need the CLI.")
