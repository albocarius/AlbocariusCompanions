"""Right panel, first tab: every field of the selected item (from the schema export), its translations side by side,
and its voice (voiced flag, audio file, play button when QtMultimedia is available). Read-only in session 1."""

from __future__ import annotations

import json
from pathlib import Path

from PySide6.QtCore import QUrl
from PySide6.QtWidgets import (QFormLayout, QHBoxLayout, QLabel, QLineEdit, QPlainTextEdit, QPushButton, QScrollArea,
                               QVBoxLayout, QWidget)

from ..data import graph
from ..data.companion import CompanionFolder
from ..schema.catalog import SchemaCatalog


def _section(text: str) -> QLabel:
    label = QLabel(text)
    label.setProperty("role", "section")
    return label


def _muted(text: str) -> QLabel:
    label = QLabel(text)
    label.setProperty("role", "muted")
    label.setWordWrap(True)
    return label


def _compact(value) -> str:
    return value if isinstance(value, str) else json.dumps(value, ensure_ascii=False)


class _Player:
    """QtMultimedia, when the platform has it (PySide6-Addons; the 'audio' extra). Otherwise playing is skipped."""

    def __init__(self):
        self.player = None
        try:
            from PySide6.QtMultimedia import QAudioOutput, QMediaPlayer
        except ImportError:
            return
        try:
            self.player = QMediaPlayer()
            self.output = QAudioOutput()
            self.player.setAudioOutput(self.output)
        except Exception:  # noqa: BLE001 - no audio backend: skip silently
            self.player = None

    def play(self, path: Path) -> None:
        if self.player is not None:
            self.player.setSource(QUrl.fromLocalFile(str(path)))
            self.player.play()


class InspectorPanel(QWidget):
    def __init__(self, catalog: SchemaCatalog, parent=None):
        super().__init__(parent)
        self.catalog = catalog
        self._player: _Player | None = None
        outer = QVBoxLayout(self)
        outer.setContentsMargins(0, 0, 0, 0)
        scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        outer.addWidget(scroll)
        self.body = QWidget()
        scroll.setWidget(self.body)
        self.layout_ = QVBoxLayout(self.body)
        self.layout_.setContentsMargins(10, 8, 10, 8)
        self.show_nothing()

    def _clear(self) -> None:
        while self.layout_.count():
            item = self.layout_.takeAt(0)
            if item.widget() is not None:
                item.widget().deleteLater()

    def show_nothing(self) -> None:
        self._clear()
        self.layout_.addWidget(_section("Inspector"))
        self.layout_.addWidget(_muted("Select a card on the canvas to see its fields, translations and voice."))
        self.layout_.addStretch(1)

    def show_item(self, folder: CompanionFolder, conversation_id: str | None, key: str) -> None:
        """A conversation header, a node, a hub/end terminal, or (conversation_id None) a line."""
        self._clear()
        if conversation_id is None:
            item, object_name, title = folder.line(key), "line", f"Line {key}"
        elif key == graph.HEADER:
            item, object_name, title = folder.conversation(conversation_id), "conversation", f"Conversation {conversation_id}"
        elif key in (graph.HUB, graph.END):
            self.layout_.addWidget(_section("Back to hub" if key == graph.HUB else "End"))
            self.layout_.addWidget(_muted('A "next" of ["hub"] returns to the companion\'s menu; ["end"] closes the talk.'))
            self.layout_.addStretch(1)
            return
        else:
            item, object_name, title = folder.node(conversation_id, key), "node", f"Node {key}"
        if item is None:
            self.show_nothing()
            return

        self.layout_.addWidget(_section(title))
        form = QFormLayout()
        form.setContentsMargins(0, 4, 0, 8)
        for spec in self.catalog.fields(object_name):
            if spec.name == "nodes":
                continue
            value = item.get(spec.name)
            field = QLineEdit(_compact(value) if spec.name in item else "")
            field.setReadOnly(True)
            if spec.name not in item:
                default = f"default {_compact(spec.default)}" if spec.has_default else "not set"
                field.setPlaceholderText(default)
            field.setToolTip(spec.description)
            form.addRow(spec.name, field)
        self.layout_.addLayout(form)

        if object_name in ("node", "line"):
            self._translations(folder, key)
            self._voice(folder, item, object_name)
        self.layout_.addStretch(1)

    def _translations(self, folder: CompanionFolder, local_id: str) -> None:
        self.layout_.addWidget(_section("Text"))
        for code in folder.languages():
            text = folder.text(local_id, code)
            self.layout_.addWidget(_muted(code + ("" if text is not None else " — missing, English is shown")))
            edit = QPlainTextEdit(text or "")
            edit.setReadOnly(True)
            edit.setMaximumHeight(70)
            self.layout_.addWidget(edit)

    def _voice(self, folder: CompanionFolder, item: dict, object_name: str) -> None:
        self.layout_.addWidget(_section("Voice"))
        is_player = item.get("speaker") == "player" or str(item.get("slot", "")).startswith("player_")
        if is_player:
            self.layout_.addWidget(_muted("Player lines are never voiced."))
            return
        path = folder.audio_path(item)
        rel = path.relative_to(folder.path).as_posix()
        exists = path.is_file()
        self.layout_.addWidget(_muted(f"voiced: {'yes' if item.get('voiced') else 'no'}"))
        row = QHBoxLayout()
        row.addWidget(_muted(f"{rel} — {'found' if exists else 'missing'}"), 1)
        if exists:
            if self._player is None:
                self._player = _Player()
            if self._player.player is not None:
                play = QPushButton("▶ Play")
                play.clicked.connect(lambda: self._player.play(path))
                row.addWidget(play)
        self.layout_.addLayout(row)
