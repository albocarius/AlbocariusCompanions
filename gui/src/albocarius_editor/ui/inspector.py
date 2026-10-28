"""Right panel, first tab: every field of the selected item as schema-driven forms (ui/forms.py: fields, conditions,
effects, trigger), its links, its texts in every language, and its voice (voiced flag, audio file, play button when
QtMultimedia is available). Every change is one undoable edit run by the window."""

from __future__ import annotations

from pathlib import Path

from PySide6.QtCore import QUrl, Qt, Signal
from PySide6.QtWidgets import (QComboBox, QFormLayout, QHBoxLayout, QInputDialog, QLabel, QLineEdit, QPlainTextEdit,
                               QPushButton, QScrollArea, QVBoxLayout, QWidget)

from ..data import edit, graph
from ..data.companion import ENGLISH, CompanionFolder
from ..schema.catalog import SchemaCatalog
from . import forms


def _section(text: str) -> QLabel:
    label = QLabel(text)
    label.setProperty("role", "section")
    return label


def _muted(text: str) -> QLabel:
    label = QLabel(text)
    label.setProperty("role", "muted")
    label.setWordWrap(True)
    return label


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
        # Set by the window: run an edit as one undoable step (→ done), show a refusal, the other companions' ids.
        self.commit = lambda _text, _fn: False
        self.message = lambda _text: None
        self.companions: list[str] = []
        self.scroll = scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        scroll.setHorizontalScrollBarPolicy(Qt.ScrollBarPolicy.ScrollBarAlwaysOff)  # long texts wrap instead
        outer.addWidget(scroll)
        self.body = QWidget()
        scroll.setWidget(self.body)
        self.layout_ = QVBoxLayout(self.body)
        self.layout_.setContentsMargins(10, 8, 10, 8)
        self.show_nothing()

    def _clear(self) -> None:
        forms.clear_layout(self.layout_)

    def show_nothing(self) -> None:
        self._clear()
        self.layout_.addWidget(_section("Inspector"))
        self.layout_.addWidget(_muted("Select a card on the canvas to see its fields, translations and voice."))
        self.layout_.addStretch(1)

    def show_item(self, folder: CompanionFolder, conversation_id: str | None, key: str) -> None:
        """A conversation header, a node, a hub/end terminal, or (conversation_id None) a line."""
        scroll = self.scroll.verticalScrollBar().value()
        self._clear()
        if conversation_id is None:
            item, object_name, title, ref = folder.line(key), "line", f"Line {key}", ("line", key)
        elif key == graph.HEADER:
            item, object_name, title = folder.conversation(conversation_id), "conversation", f"Conversation {conversation_id}"
            ref = ("conversation", conversation_id)
        elif key in (graph.HUB, graph.END):
            self.layout_.addWidget(_section("Back to hub" if key == graph.HUB else "End"))
            self.layout_.addWidget(_muted('A "next" of ["hub"] returns to the companion\'s menu; ["end"] closes the talk.'))
            self.layout_.addStretch(1)
            return
        else:
            item, object_name, title = folder.node(conversation_id, key), "node", f"Node {key}"
            ref = ("node", conversation_id, key)
        if item is None:
            self.show_nothing()
            return
        ctx = self._context(folder, ref, conversation_id, item)

        self.layout_.addWidget(_section(title))
        form = QFormLayout()
        form.setContentsMargins(0, 4, 0, 0)
        id_spec = self.catalog.field(object_name, "id")
        id_field = QLineEdit(str(item.get("id", "")))
        id_field.setObjectName("id_field")
        id_field.setToolTip(id_spec.description + "\nRenaming also renames references, texts in every language and the "
                                                  "layout (undoable).")
        id_field.editingFinished.connect(lambda f=id_field, old=str(item.get("id", "")), c=conversation_id, k=key:
                                         self._commit_id(f, c, k, old))
        form.addRow("id", id_field)
        if object_name == "conversation":
            form.addRow("starts from", self._start_mode(ctx, item))
        self.layout_.addLayout(form)
        skip = {"id", "nodes", "next", "start", "when", "effects", "trigger"} | ({"entry"} if object_name == "conversation" else set())
        self.layout_.addLayout(forms.scalar_form(ctx, object_name, skip))

        if object_name == "conversation" and isinstance(item.get("trigger"), dict):
            self.layout_.addWidget(forms.TriggerEditor(ctx, ["trigger"]))
        elif object_name == "line" and not item.get("slot"):
            if isinstance(item.get("trigger"), dict):
                self.layout_.addWidget(forms.TriggerEditor(ctx, ["trigger"]))
            else:
                add = QPushButton("+ Trigger (a slot-free line is started by a trigger)")
                add.setObjectName("add_trigger")
                first = forms.trigger_items(self.catalog)[:1]
                add.clicked.connect(lambda: first and ctx.set(["trigger"], {"on": first[0]}))
                self.layout_.addWidget(add)
        self.layout_.addWidget(forms.ConditionsEditor(ctx, ["when"]))
        if object_name == "node":
            self.layout_.addWidget(forms.EffectsEditor(ctx, ["effects"]))

        if conversation_id is not None:
            source = graph.HEADER if object_name == "conversation" else key
            self._edges(conversation_id, source, item.get("start" if object_name == "conversation" else "next"))
        if object_name in ("node", "line"):
            self._translations(folder, key, ctx)
            self._voice(folder, item, object_name)
        self.layout_.addStretch(1)
        self.scroll.verticalScrollBar().setValue(scroll)

    def _context(self, folder: CompanionFolder, ref: tuple, conversation_id: str | None, item: dict) -> forms.FormContext:
        variables = [*folder.variables, *(b["name"] for b in self.catalog.enum("builtinVariables"))]
        ints = [n for n, d in folder.variables.items() if isinstance(d, dict) and d.get("type") == "int"]
        ints += [b["name"] for b in self.catalog.enum("builtinVariables") if b["type"] == "int"]
        trigger = item.get("trigger")
        if conversation_id is not None and trigger is None:
            trigger = (folder.conversation(conversation_id) or {}).get("trigger")
        return forms.FormContext(self.catalog, folder, ref, self.commit, self.message, variables, ints,
                                 forms.event_fields(self.catalog, trigger), [c for c in self.companions if c != folder.id])

    def _start_mode(self, ctx: forms.FormContext, conv: dict) -> QComboBox:
        """Hub menu / greeting (``entry``) or a trigger: switching removes the other key."""
        combo = forms._narrow(QComboBox())
        combo.setObjectName("start_mode")
        modes = [("Hub menu (entry: hub)", "hub"), ("Greeting (entry: greeting)", "greeting"), ("A trigger", "trigger")]
        for text, mode in modes:
            combo.addItem(text, mode)
        current = "trigger" if isinstance(conv.get("trigger"), dict) else conv.get("entry", "hub")
        combo.setCurrentIndex(max(0, combo.findData(current)))
        conversation_id = ctx.ref[1]
        first = (forms.trigger_items(self.catalog) or [None])[0]

        def picked(i: int) -> None:
            mode = combo.itemData(i)
            if mode != current:
                ctx.commit(f"{conversation_id} starts from {mode}",
                           lambda f: edit.set_start_mode(f, conversation_id, mode, first))
        combo.activated.connect(picked)
        return combo

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

    def _translations(self, folder: CompanionFolder, local_id: str, ctx: forms.FormContext) -> None:
        """The text in every language (T10): editable, one undoable step per commit; a missing translation shows the
        English text greyed with a "Copy English" button."""
        head = QHBoxLayout()
        head.addWidget(_section("Text"), 1)
        add = QPushButton("+ Language")
        add.setObjectName("add_language")
        add.setToolTip("Add lang/<code>.json (written on save)")
        add.clicked.connect(lambda: self._add_language(ctx))
        head.addWidget(add)
        self.layout_.addLayout(head)
        english = folder.text(local_id, ENGLISH) or ""
        for code in folder.languages():
            text = folder.text(local_id, code)
            row = QHBoxLayout()
            row.addWidget(_muted(code + ("" if text is not None else " — missing")), 1)
            if text is None and code != ENGLISH:
                copy_ = QPushButton("Copy English")
                copy_.setObjectName(f"copy_en_{code}")
                copy_.clicked.connect(lambda _=False, c=code: self.text_committed.emit(local_id, english, c))
                row.addWidget(copy_)
            self.layout_.addLayout(row)
            box = CommitTextEdit(text or "")
            box.setObjectName(f"text_{code}")
            box.setMaximumHeight(70)
            box.setToolTip("Ctrl+Enter or leaving the box saves the text (undoable)")
            if text is None:
                box.setPlaceholderText(english)
                box.setStyleSheet("QPlainTextEdit { font-style: italic; }")
            box.committed.connect(lambda t, c=code: self.text_committed.emit(local_id, t, c))
            self.layout_.addWidget(box)

    def _add_language(self, ctx: forms.FormContext) -> None:
        code, ok = QInputDialog.getText(self, "Add language", "Language code (two letters, e.g. de):")
        code = code.strip().lower()
        if ok and code:
            pattern = self.catalog.schema["patterns"]["languageCode"]
            ctx.commit(f"Add language {code}", lambda f: edit.add_language(f, code, pattern))

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
