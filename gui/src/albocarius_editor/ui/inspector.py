"""Right panel, first tab: every field of the selected item (from the schema export), its translations side by side,
and its voice (voiced flag, audio file, play button when QtMultimedia is available)."""

from __future__ import annotations

import json
from pathlib import Path

from PySide6.QtCore import QUrl, Qt, Signal
from PySide6.QtWidgets import (QFormLayout, QHBoxLayout, QLabel, QLineEdit, QPlainTextEdit, QPushButton, QScrollArea,
                               QVBoxLayout, QWidget)

from ..data import graph
from ..data.companion import ENGLISH, CompanionFolder
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


class CommitTextEdit(QPlainTextEdit):
    """A text box that reports its text once editing ends (focus out or Ctrl+Enter), when it changed."""

    committed = Signal(str)

    def __init__(self, text: str, parent=None):
        super().__init__(text, parent)
        self._committed_text = text

    def commit(self) -> None:
        text = self.toPlainText()
        if text != self._committed_text:
            self._committed_text = text
            self.committed.emit(text)

    def focusOutEvent(self, event) -> None:
        super().focusOutEvent(event)
        self.commit()

    def keyPressEvent(self, event) -> None:
        if event.key() in (Qt.Key.Key_Return, Qt.Key.Key_Enter) and event.modifiers() & Qt.KeyboardModifier.ControlModifier:
            self.commit()
            return
        super().keyPressEvent(event)


class InspectorPanel(QWidget):
    text_committed = Signal(str, str, str)  # local id, text, language code
    id_committed = Signal(object, str, str)  # conversation id (None for a line), old key (@header: conversation), new id
    edge_move = Signal(str, str, str, int)   # conversation id, source key, target name, new index
    edge_remove = Signal(str, str, str)      # conversation id, source key, target name

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

    def _clear(self, layout=None) -> None:
        """Removes every widget, also those of nested rows and forms (detached now, deleted later: a widget may be
        clearing the panel from inside its own signal)."""
        layout = layout or self.layout_
        while layout.count():
            item = layout.takeAt(0)
            widget, inner = item.widget(), item.layout()
            if widget is not None:
                widget.setParent(None)
                widget.deleteLater()
            elif inner is not None:
                self._clear(inner)
                inner.deleteLater()

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
            if spec.name == "nodes" or (object_name != "line" and spec.name in ("next", "start")):
                continue
            value = item.get(spec.name)
            field = QLineEdit(_compact(value) if spec.name in item else "")
            if spec.name == "id" and isinstance(value, str):
                field.setObjectName("id_field")
                field.editingFinished.connect(
                    lambda f=field, old=value, c=conversation_id, k=key: self._commit_id(f, c, k, old))
            else:
                field.setReadOnly(True)
            if spec.name not in item:
                default = f"default {_compact(spec.default)}" if spec.has_default else "not set"
                field.setPlaceholderText(default)
            field.setToolTip(spec.description + ("\nRenaming also renames references, texts in every language and the "
                                                 "layout (undoable)." if field.objectName() == "id_field" else ""))
            form.addRow(spec.name, field)
        self.layout_.addLayout(form)

        if conversation_id is not None:
            source = graph.HEADER if object_name == "conversation" else key
            self._edges(conversation_id, source, item.get("start" if object_name == "conversation" else "next"))
        if object_name in ("node", "line"):
            self._translations(folder, key)
            self._voice(folder, item, object_name)
        self.layout_.addStretch(1)

    def _commit_id(self, field: QLineEdit, conversation_id: str | None, key: str, old: str) -> None:
        new = field.text().strip()
        if new == old or field.property("committed") == new:  # editingFinished comes on Enter and again on focus out
            return
        field.setProperty("committed", new)
        self.id_committed.emit(conversation_id, graph.HEADER if key == graph.HEADER else old, new)

    def _edges(self, conversation_id: str, source: str, targets) -> None:
        """``start`` / ``next`` as an ordered list: the order is the menu order (player options) or the candidate
        order (companion replies)."""
        self.layout_.addWidget(_section("Start" if source == graph.HEADER else "Next"))
        targets = [t for t in targets if isinstance(t, str)] if isinstance(targets, list) else []
        if not targets:
            self.layout_.addWidget(_muted("Nothing yet: drag from the card's right port onto another card."))
        for i, target in enumerate(targets):
            row = QHBoxLayout()
            label = QLabel(f"{i + 1}. {target}")
            label.setObjectName(f"edge_{i}")
            row.addWidget(label, 1)
            for text, tip, enabled, slot in (
                    ("↑", "Move up", i > 0, lambda _=False, t=target, n=i - 1: self.edge_move.emit(conversation_id, source, t, n)),
                    ("↓", "Move down", i < len(targets) - 1,
                     lambda _=False, t=target, n=i + 1: self.edge_move.emit(conversation_id, source, t, n)),
                    ("×", "Remove this link", True, lambda _=False, t=target: self.edge_remove.emit(conversation_id, source, t))):
                button = QPushButton(text)
                button.setToolTip(tip)
                button.setObjectName(f"edge_{i}_{tip.split()[0].lower()}")
                button.setFixedWidth(28)
                button.setEnabled(enabled)
                button.clicked.connect(slot)
                row.addWidget(button)
            self.layout_.addLayout(row)

    def _translations(self, folder: CompanionFolder, local_id: str) -> None:
        self.layout_.addWidget(_section("Text"))
        for code in folder.languages():
            text = folder.text(local_id, code)
            self.layout_.addWidget(_muted(code + ("" if text is not None else " — missing, English is shown")))
            edit = CommitTextEdit(text or "")
            edit.setObjectName(f"text_{code}")
            edit.setMaximumHeight(70)
            if code == ENGLISH:
                edit.setToolTip("Ctrl+Enter or leaving the box saves the text (undoable)")
                edit.committed.connect(lambda t, i=local_id, c=code: self.text_committed.emit(i, t, c))
            else:
                edit.setReadOnly(True)
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
