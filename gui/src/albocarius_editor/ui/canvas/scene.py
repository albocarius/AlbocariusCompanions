"""One conversation on a canvas: builds cards and edges from the JSON (via data.graph) and the layout sidecar."""

from __future__ import annotations

from PySide6.QtCore import QPointF, Signal
from PySide6.QtWidgets import QGraphicsScene

from ...data import graph
from ...data.companion import CompanionFolder
from ...schema.catalog import SchemaCatalog
from . import summary
from .items import Card, CardContent, EdgeItem, Section, TextEditor


def header_content(conv: dict, catalog: SchemaCatalog) -> CardContent:
    trigger = conv.get("trigger")
    badge = "triggered" if trigger else str(conv.get("entry", "?"))
    sections = []
    if trigger:
        sections.append(Section("trigger", [summary.trigger(trigger)]))
    facts = []
    if conv.get("once"):
        facts.append("once per campaign")
    if conv.get("priority") is not None:
        facts.append(f"priority {conv['priority']}")
    if facts:
        sections.append(Section("options", facts))
    when = summary.conditions(conv.get("when"), catalog)
    if when:
        sections.append(Section("when", when))
    return CardContent(title=str(conv.get("id", "?")), badge=badge, subtitle="conversation", sections=sections,
                       has_input=False)


def node_content(folder: CompanionFolder, node: dict, catalog: SchemaCatalog) -> CardContent:
    speaker = node.get("speaker", "?")
    sections = []
    facts = []
    if node.get("voiced"):
        facts.append("voiced")
    if node.get("priority") is not None:
        facts.append(f"priority {node['priority']}")
    if facts:
        sections.append(Section("options", facts))
    when = summary.conditions(node.get("when"), catalog)
    if when:
        sections.append(Section("when", when))
    fx = summary.effects(node.get("effects"), catalog)
    if fx:
        sections.append(Section("effects", fx))
    title = "Companion" if speaker == "npc" else "Player" if speaker == "player" else str(speaker)
    return CardContent(title=title, badge=speaker, subtitle=str(node.get("id")),
                       text=folder.text(node.get("id", "")) or "", sections=sections)


TERMINAL_TITLES = {graph.HUB: ("Back to hub", "hub"), graph.END: ("End", "end")}


class ConversationScene(QGraphicsScene):
    """Cards keyed by graph key; positions read from / written to ``folder.layout``."""

    item_selected = Signal(str)            # graph key, "" when nothing is selected
    item_moved = Signal(str, QPointF, QPointF)
    text_committed = Signal(str, str)      # node id, new English text (inline editor)

    def __init__(self, folder: CompanionFolder, conversation_id: str, catalog: SchemaCatalog, parent=None):
        super().__init__(parent)
        self.folder = folder
        self.conversation_id = conversation_id
        self.catalog = catalog
        self.cards: dict[str, Card] = {}
        self.editor: TextEditor | None = None
        self.snap = True
        self.selectionChanged.connect(self._on_selection)
        self.rebuild()

    def conversation(self) -> dict | None:
        return self.folder.conversation(self.conversation_id)

    def rebuild(self) -> None:
        """Recreates every item from the data (after an edit or undo); keeps the selection by key."""
        selected = self.selected_keys()
        if self.editor is not None:
            self.editor.finished = True  # a rebuild drops an open inline editor without committing
            self.editor = None
        self.blockSignals(True)
        self.clear()
        self.cards = {}
        conv = self.conversation()
        if conv is None:
            self.blockSignals(False)
            return
        g = graph.build(conv)
        nodes = {n["id"]: n for n in conv.get("nodes") or [] if isinstance(n, dict) and isinstance(n.get("id"), str)}
        for key in g.keys():
            if key == graph.HEADER:
                card = Card(key, "header", header_content(conv, self.catalog))
            elif key in TERMINAL_TITLES:
                title, badge = TERMINAL_TITLES[key]
                card = Card(key, "terminal", CardContent(title=title, badge=badge, has_output=False))
            else:
                node = nodes[key]
                kind = "player" if node.get("speaker") == "player" else "npc"
                card = Card(key, kind, node_content(self.folder, node, self.catalog))
            card.moved.connect(self.item_moved)
            self.addItem(card)
            self.cards[key] = card
        positions = self.folder.layout.resolve(conv, {k: c.height for k, c in self.cards.items()})
        for key, card in self.cards.items():
            card.snap = False  # stored positions are placed as they are; snapping applies to drags
            card.setPos(*positions[key])
            card.snap = self.snap
        counts: dict[str, int] = {}
        for e in g.edges:
            counts[e.source] = counts.get(e.source, 0) + 1
        for e in g.edges:
            target = None if e.missing else self.cards.get(e.target)
            self.addItem(EdgeItem(self.cards[e.source], target, e.index, counts[e.source] > 1, e.target))
        for key in selected:
            if key in self.cards:
                self.cards[key].setSelected(True)
        self.blockSignals(False)

    def set_snap(self, on: bool) -> None:
        self.snap = on
        for card in self.cards.values():
            card.snap = on

    def selected_key(self) -> str:
        cards = [i for i in self.selectedItems() if isinstance(i, Card)]
        return cards[0].key if len(cards) == 1 else ""

    def selected_keys(self) -> list[str]:
        return [i.key for i in self.selectedItems() if isinstance(i, Card)]

    def select(self, key: str) -> Card | None:
        card = self.cards.get(key)
        if card is not None:
            self.clearSelection()
            card.setSelected(True)
        return card

    def edit_text(self, key: str) -> TextEditor | None:
        """Opens the inline editor on a node card's English text; committing emits ``text_committed``."""
        card = self.cards.get(key)
        if card is None or card.text_rect() is None:
            return None
        if self.editor is not None:
            self.editor.finish(commit=True)
        self.select(key)
        self.editor = TextEditor(card, self.folder.text(key) or "", lambda text: self.text_committed.emit(key, text))
        self.editor.start()
        return self.editor

    def set_highlight(self, key: str | None) -> None:
        for k, card in self.cards.items():
            card.set_highlighted(k == key)

    def place(self, key: str, pos: QPointF) -> None:
        """Moves a card without snapping (undo/redo of a move) and records the position."""
        card = self.cards.get(key)
        if card is not None:
            snap, card.snap = card.snap, False
            card.setPos(pos)
            card.snap = snap
        self.folder.layout.set(self.conversation_id, key, pos.x(), pos.y())

    def _on_selection(self) -> None:
        self.item_selected.emit(self.selected_key())
