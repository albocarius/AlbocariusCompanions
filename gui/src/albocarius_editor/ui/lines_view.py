"""The Lines tab: every entry of dialogs.json "lines", grouped by slot kind. Session 1 lists them; the card canvas
version (line nodes grouped visually, no edges) is a later task (gui/PLAN.md)."""

from __future__ import annotations

from PySide6.QtCore import Qt, Signal
from PySide6.QtWidgets import QTreeWidget, QTreeWidgetItem

from ..data.companion import CompanionFolder
from ..schema.catalog import SchemaCatalog
from .canvas import summary

GROUP_TITLES = {
    "companion": "Companion: meeting and hire",
    "player": "Player: meeting and hire",
    "vanilla": "Vanilla dialog points",
    "optional": "Reserved slots",
    "triggered": "Triggered lines",
    "unknown": "Unknown slots",
}


class LinesView(QTreeWidget):
    line_selected = Signal(str)

    def __init__(self, catalog: SchemaCatalog, parent=None):
        super().__init__(parent)
        self.catalog = catalog
        self.setHeaderLabels(["Id", "Slot / trigger", "English text"])
        self.setColumnWidth(0, 200)
        self.setColumnWidth(1, 220)
        self.setAlternatingRowColors(True)
        self.itemSelectionChanged.connect(self._selected)

    def set_folder(self, folder: CompanionFolder) -> None:
        self.clear()
        groups: dict[str, QTreeWidgetItem] = {}
        for group in GROUP_TITLES:
            item = QTreeWidgetItem([GROUP_TITLES[group]])
            item.setFirstColumnSpanned(False)
            groups[group] = item
        for line in folder.lines:
            if not isinstance(line, dict) or not isinstance(line.get("id"), str):
                continue
            group = self.catalog.slot_group(line.get("slot"))
            where = line.get("slot") or summary.trigger(line.get("trigger"))
            item = QTreeWidgetItem([line["id"], where, folder.text(line["id"]) or ""])
            item.setData(0, Qt.ItemDataRole.UserRole, line["id"])
            item.setToolTip(2, folder.text(line["id"]) or "")
            groups[group].addChild(item)
        for group, item in groups.items():
            if item.childCount():
                item.setText(0, f"{GROUP_TITLES[group]} ({item.childCount()})")
                self.addTopLevelItem(item)
                item.setExpanded(True)

    def select_line(self, line_id: str) -> None:
        for i in range(self.topLevelItemCount()):
            top = self.topLevelItem(i)
            for j in range(top.childCount()):
                if top.child(j).data(0, Qt.ItemDataRole.UserRole) == line_id:
                    self.setCurrentItem(top.child(j))
                    self.scrollToItem(top.child(j))
                    return

    def _selected(self) -> None:
        items = self.selectedItems()
        line_id = items[0].data(0, Qt.ItemDataRole.UserRole) if items else None
        if line_id:
            self.line_selected.emit(line_id)
