"""Left sidebar: add-node palette, search, navigation (conversations → nodes) and the companions of the opened folder."""

from __future__ import annotations

from pathlib import Path

from PySide6.QtCore import Qt, Signal
from PySide6.QtWidgets import QLabel, QLineEdit, QListWidget, QListWidgetItem, QTreeWidget, QTreeWidgetItem, QVBoxLayout, QWidget

from ..data.companion import CompanionFolder

LINES_TAB = "@lines"

# The palette entries: (label, what it adds). Adding by click/drag is a later task (gui/PLAN.md); listed for the layout.
PALETTE = [
    ("Companion line", "npc"),
    ("Player line", "player"),
    ("Back to hub", "hub"),
    ("End", "end"),
]


def _section(text: str) -> QLabel:
    label = QLabel(text)
    label.setProperty("role", "section")
    return label


class LibraryPanel(QWidget):
    open_conversation = Signal(str)          # conversation id, or LINES_TAB
    open_node = Signal(str, str)             # conversation id (or LINES_TAB), node / line id
    open_companion = Signal(Path)
    palette_activated = Signal(str)          # "npc", "player", "hub", "end"

    def __init__(self, parent=None):
        super().__init__(parent)
        layout = QVBoxLayout(self)
        layout.setContentsMargins(10, 8, 10, 8)
        layout.setSpacing(6)

        self.search = QLineEdit()
        self.search.setPlaceholderText("Search nodes by id or text…")
        self.search.setClearButtonEnabled(True)
        self.search.textChanged.connect(self._filter)
        layout.addWidget(self.search)

        layout.addWidget(_section("Add"))
        self.palette = QListWidget()
        self.palette.setMaximumHeight(4 * 28 + 6)
        for label, kind in PALETTE:
            item = QListWidgetItem(("◆  " if kind in ("npc", "player") else "→  ") + label)
            item.setData(Qt.ItemDataRole.UserRole, kind)
            self.palette.addItem(item)
        self.palette.itemActivated.connect(lambda i: self.palette_activated.emit(i.data(Qt.ItemDataRole.UserRole)))
        layout.addWidget(self.palette)

        layout.addWidget(_section("Graph navigation"))
        self.tree = QTreeWidget()
        self.tree.setHeaderHidden(True)
        self.tree.itemActivated.connect(self._activated)
        self.tree.itemClicked.connect(self._activated)
        layout.addWidget(self.tree, 3)

        layout.addWidget(_section("Companions"))
        self.companions = QListWidget()
        self.companions.itemActivated.connect(lambda i: self.open_companion.emit(Path(i.data(Qt.ItemDataRole.UserRole))))
        layout.addWidget(self.companions, 1)

        self._folder: CompanionFolder | None = None

    def set_folder(self, folder: CompanionFolder | None) -> None:
        self._folder = folder
        self.tree.clear()
        if folder is None:
            return
        lines = QTreeWidgetItem([f"Lines ({len(folder.lines)})"])
        lines.setData(0, Qt.ItemDataRole.UserRole, (LINES_TAB, ""))
        for line in folder.lines:
            if isinstance(line, dict) and isinstance(line.get("id"), str):
                child = QTreeWidgetItem([line["id"]])
                child.setData(0, Qt.ItemDataRole.UserRole, (LINES_TAB, line["id"]))
                child.setToolTip(0, folder.text(line["id"]) or "")
                lines.addChild(child)
        self.tree.addTopLevelItem(lines)
        for conv in folder.conversations:
            conv_id = conv.get("id")
            if not isinstance(conv_id, str):
                continue
            item = QTreeWidgetItem([conv_id])
            item.setData(0, Qt.ItemDataRole.UserRole, (conv_id, ""))
            for node in conv.get("nodes") or []:
                if isinstance(node, dict) and isinstance(node.get("id"), str):
                    marker = "▸ " if node.get("speaker") == "player" else "• "
                    child = QTreeWidgetItem([marker + node["id"]])
                    child.setData(0, Qt.ItemDataRole.UserRole, (conv_id, node["id"]))
                    child.setToolTip(0, folder.text(node["id"]) or "")
                    item.addChild(child)
            self.tree.addTopLevelItem(item)
        self._filter(self.search.text())

    def set_companions(self, folders: list[Path], current: Path | None) -> None:
        self.companions.clear()
        for path in folders:
            item = QListWidgetItem(path.name)
            item.setData(Qt.ItemDataRole.UserRole, str(path))
            self.companions.addItem(item)
            if current is not None and path == current:
                item.setSelected(True)

    def _activated(self, item: QTreeWidgetItem) -> None:
        conv_id, node_id = item.data(0, Qt.ItemDataRole.UserRole)
        if node_id:
            self.open_node.emit(conv_id, node_id)
        else:
            self.open_conversation.emit(conv_id)

    def _filter(self, text: str) -> None:
        """Shows nodes whose id or English text contains the search text (and their conversation)."""
        needle = text.strip().lower()
        for i in range(self.tree.topLevelItemCount()):
            top = self.tree.topLevelItem(i)
            any_child = False
            for j in range(top.childCount()):
                child = top.child(j)
                _, node_id = child.data(0, Qt.ItemDataRole.UserRole)
                hay = f"{node_id} {child.toolTip(0)}".lower()
                match = not needle or needle in hay
                child.setHidden(not match)
                any_child |= match
            top.setHidden(bool(needle) and not any_child and needle not in top.text(0).lower())
            top.setExpanded(bool(needle) and any_child)
