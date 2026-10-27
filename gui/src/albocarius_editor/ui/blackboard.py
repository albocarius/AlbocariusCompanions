"""Right panel, second tab: the companion's declared variables (dialogs.json "variables"), as cards like the reference.
Read-only in session 1; add/remove/edit are later tasks (gui/PLAN.md)."""

from __future__ import annotations

import json

from PySide6.QtWidgets import QComboBox, QFrame, QGridLayout, QHBoxLayout, QLabel, QLineEdit, QPushButton, QScrollArea, QVBoxLayout, QWidget

from ..data.companion import CompanionFolder
from ..schema.catalog import SchemaCatalog


class BlackboardPanel(QWidget):
    def __init__(self, catalog: SchemaCatalog, parent=None):
        super().__init__(parent)
        self.catalog = catalog
        outer = QVBoxLayout(self)
        outer.setContentsMargins(10, 8, 10, 8)
        title = QLabel("Blackboard")
        title.setProperty("role", "section")
        outer.addWidget(title)
        hint = QLabel("Variables of this companion (dialogs.json \"variables\").")
        hint.setProperty("role", "muted")
        hint.setWordWrap(True)
        outer.addWidget(hint)
        buttons = QHBoxLayout()
        self.add_button = QPushButton("+ Add variable")
        self.add_button.setProperty("accent", True)
        self.add_button.setEnabled(False)
        self.add_button.setToolTip("Editing variables is a later task")
        buttons.addWidget(self.add_button)
        buttons.addStretch(1)
        outer.addLayout(buttons)
        scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        scroll.setFrameShape(QFrame.Shape.NoFrame)
        self.list = QWidget()
        self.list_layout = QVBoxLayout(self.list)
        self.list_layout.setContentsMargins(0, 4, 0, 4)
        scroll.setWidget(self.list)
        outer.addWidget(scroll, 1)

    def set_folder(self, folder: CompanionFolder | None) -> None:
        while self.list_layout.count():
            item = self.list_layout.takeAt(0)
            if item.widget() is not None:
                item.widget().deleteLater()
        if folder is not None:
            for name, decl in folder.variables.items():
                self.list_layout.addWidget(self._card(name, decl if isinstance(decl, dict) else {}, builtin=False))
            for builtin in self.catalog.enum("builtinVariables"):
                self.list_layout.addWidget(self._card(builtin["name"], {"type": builtin["type"], "default": builtin["default"]},
                                                      builtin=True))
        self.list_layout.addStretch(1)

    def _card(self, name: str, decl: dict, builtin: bool) -> QFrame:
        card = QFrame()
        card.setProperty("role", "card")
        grid = QGridLayout(card)
        grid.setContentsMargins(8, 8, 8, 8)
        name_edit = QLineEdit(name)
        name_edit.setReadOnly(True)
        grid.addWidget(name_edit, 0, 0, 1, 2)
        type_box = QComboBox()
        type_box.addItems(self.catalog.enum("variableTypes"))
        type_box.setCurrentText(str(decl.get("type", "")))
        type_box.setEnabled(False)
        grid.addWidget(type_box, 1, 0)
        scope_box = QComboBox()
        scope_box.addItems(self.catalog.enum("variableScopes"))
        scope_box.setCurrentText(str(decl.get("scope", "local")))
        scope_box.setEnabled(False)
        grid.addWidget(scope_box, 1, 1)
        default = decl.get("default", 0 if decl.get("type") == "int" else False)
        default_edit = QLineEdit(json.dumps(default))
        default_edit.setReadOnly(True)
        default_edit.setToolTip("default")
        grid.addWidget(default_edit, 2, 0, 1, 2)
        if builtin:
            note = QLabel("built in")
            note.setProperty("role", "muted")
            grid.addWidget(note, 3, 0, 1, 2)
        return card
