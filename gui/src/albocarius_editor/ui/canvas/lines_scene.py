"""The Lines tab: every entry of dialogs.json "lines" as a card (no edges), in labelled groups by slot kind
(``catalog.slot_group``), sorted by slot. Every required slot shows, as an empty placeholder card when it has no line
(double-click it to add one). Positions are a grid per group and are not stored."""

from __future__ import annotations

from PySide6.QtCore import QPoint, QPointF, Signal
from PySide6.QtGui import QBrush, QColor, QFont
from PySide6.QtWidgets import QGraphicsItem, QGraphicsScene, QGraphicsSimpleTextItem

from ...data.companion import CompanionFolder
from ...schema.catalog import SchemaCatalog
from .. import theme
from . import summary
from .items import CARD_WIDTH, Card, CardContent, Section, TextEditor

GROUP_TITLES = {
    "companion": "Companion: meeting and hire",
    "player": "Player: meeting and hire",
    "vanilla": "Vanilla dialog points",
    "optional": "Reserved slots",
    "triggered": "Triggered lines",
    "unknown": "Unknown slots",
}
COLUMNS = 4
GAP_X, GAP_Y, GROUP_GAP = 30.0, 24.0, 50.0
PLACEHOLDER = "@slot:"  # key prefix of a placeholder card (ids cannot contain '@')


def line_content(folder: CompanionFolder, line: dict, catalog: SchemaCatalog) -> CardContent:
    sections = []
    facts = []
    if line.get("voiced"):
        facts.append("voiced")
    if line.get("priority") is not None:
        facts.append(f"priority {line['priority']}")
    if facts:
        sections.append(Section("options", facts))
    if line.get("trigger"):
        sections.append(Section("trigger", [summary.trigger(line["trigger"])]))
    when = summary.conditions(line.get("when"), catalog)
    if when:
        sections.append(Section("when", when))
    slot = line.get("slot")
    return CardContent(title=str(slot or "triggered line"), badge="line", subtitle=str(line.get("id")),
                       text=folder.text(line.get("id", "")) or "", sections=sections, has_input=False, has_output=False)


class LinesScene(QGraphicsScene):
    item_selected = Signal(str)            # line id, "" when nothing (or a placeholder) is selected
    text_committed = Signal(str, str)      # line id, new English text
    add_requested = Signal(str)            # slot of a placeholder card that was double-clicked
    context_requested = Signal(str, QPoint)

    def __init__(self, folder: CompanionFolder | None, catalog: SchemaCatalog, parent=None):
        super().__init__(parent)
        self.folder = folder
        self.catalog = catalog
        self.cards: dict[str, Card] = {}
        self.editor: TextEditor | None = None
        self.selectionChanged.connect(self._on_selection)  # a bound slot: disconnected when the scene is deleted
        self.rebuild()

    def set_folder(self, folder: CompanionFolder | None) -> None:
        self.folder = folder
        self.rebuild()

    def _order(self, slot: str | None) -> int:
        slots = self.catalog.all_slots
        return slots.index(slot) if slot in slots else len(slots)

    def rebuild(self) -> None:
        selected = self.selected_keys()
        if self.editor is not None:
            self.editor.finished = True
            self.editor = None
        self.blockSignals(True)
        self.clear()
        self.cards = {}
        if self.folder is None:
            self.blockSignals(False)
            return
        groups: dict[str, list[tuple[str, Card]]] = {g: [] for g in GROUP_TITLES}
        lines = [l for l in self.folder.lines if isinstance(l, dict) and isinstance(l.get("id"), str)]
        lines.sort(key=lambda l: (self._order(l.get("slot")), l.get("slot") or "", self.folder.lines.index(l)))
        filled = {l.get("slot") for l in lines}
        for line in lines:
            group = self.catalog.slot_group(line.get("slot"))
            kind = "player" if group == "player" or str(line.get("slot", "")).startswith("player_") else "line"
            groups[group].append((line["id"], Card(line["id"], kind, line_content(self.folder, line, self.catalog))))
        for slot in self.catalog.required_slots:
            if slot not in filled:
                content = CardContent(title=slot, badge="missing", subtitle="no line yet: double-click to add one",
                                      has_input=False, has_output=False)
                card = Card(PLACEHOLDER + slot, "line", content)
                card.setOpacity(0.55)
                groups[self.catalog.slot_group(slot)].append((card.key, card))
        for entries in groups.values():
            entries.sort(key=lambda e: self._order(e[1].content.title if e[0].startswith(PLACEHOLDER)
                                                   else (self.folder.line(e[0]) or {}).get("slot")))
        y = 0.0
        title_font = QFont()
        title_font.setPointSizeF(title_font.pointSizeF() * 1.3)
        title_font.setWeight(QFont.Weight.DemiBold)
        for group, entries in groups.items():
            if not entries:
                continue
            label = QGraphicsSimpleTextItem(f"{GROUP_TITLES[group]} ({sum(1 for k, _ in entries if not k.startswith(PLACEHOLDER))})")
            label.setFont(title_font)
            label.setBrush(QBrush(QColor(theme.MUTED)))
            label.setPos(0, y)
            self.addItem(label)
            y += 36
            for row_start in range(0, len(entries), COLUMNS):
                row = entries[row_start:row_start + COLUMNS]
                for col, (key, card) in enumerate(row):
                    card.setFlag(QGraphicsItem.GraphicsItemFlag.ItemIsMovable, False)
                    card.snap = False
                    card.setPos(col * (CARD_WIDTH + GAP_X), y)
                    self.addItem(card)
                    self.cards[key] = card
                y += max(c.height for _, c in row) + GAP_Y
            y += GROUP_GAP
        for key in selected:
            if key in self.cards:
                self.cards[key].setSelected(True)
        self.blockSignals(False)

    def _on_selection(self) -> None:
        self.item_selected.emit(self.selected_key())

    def selected_key(self) -> str:
        keys = self.selected_keys()
        return keys[0] if len(keys) == 1 and not keys[0].startswith(PLACEHOLDER) else ""

    def selected_keys(self) -> list[str]:
        return [i.key for i in self.selectedItems() if isinstance(i, Card)]

    def select(self, key: str) -> Card | None:
        card = self.cards.get(key)
        if card is not None:
            self.clearSelection()
            card.setSelected(True)
        return card

    def edit_text(self, key: str) -> TextEditor | None:
        card = self.cards.get(key)
        if card is None or card.text_rect() is None:
            return None
        if self.editor is not None:
            self.editor.finish(commit=True)
        self.select(key)
        self.editor = TextEditor(card, self.folder.text(key) or "", lambda text: self.text_committed.emit(key, text))
        self.editor.start()
        return self.editor

    def mouseDoubleClickEvent(self, event) -> None:
        card = next((i for i in self.items(event.scenePos()) if isinstance(i, Card)), None)
        if card is not None and card.key.startswith(PLACEHOLDER):
            self.add_requested.emit(card.key[len(PLACEHOLDER):])
            event.accept()
            return
        super().mouseDoubleClickEvent(event)

    def contextMenuEvent(self, event) -> None:
        card = next((i for i in self.items(event.scenePos()) if isinstance(i, Card)), None)
        if card is None:
            super().contextMenuEvent(event)
            return
        if not card.isSelected():
            self.select(card.key)
        self.context_requested.emit(card.key, event.screenPos())
        event.accept()

    def set_highlight(self, key: str | None) -> None:
        for k, card in self.cards.items():
            card.set_highlighted(k == key)

    def place(self, key: str, pos: QPointF) -> None:  # positions are not stored on the Lines tab
        pass
