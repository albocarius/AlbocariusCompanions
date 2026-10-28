"""Right panel, second tab: the companion's declared variables (dialogs.json "variables"), as cards like the reference.

Add, remove, rename (references follow: data.edit.rename_variable), type / scope / default. A variable's name can be
dragged onto a condition or effect field to fill it in. The built-in ``mood`` is shown read-only."""

from __future__ import annotations

import json

from PySide6.QtCore import QMimeData, Qt
from PySide6.QtGui import QDrag
from PySide6.QtWidgets import (QCheckBox, QComboBox, QFrame, QGridLayout, QHBoxLayout, QInputDialog, QLabel, QLineEdit,
                               QMessageBox, QPushButton, QScrollArea, QToolButton, QVBoxLayout, QWidget)

from ..data import edit
from ..data.companion import CompanionFolder
from ..schema.catalog import SchemaCatalog
from .forms import LineField, clear_layout


class DragName(QLabel):
    """A variable's name; dragging it carries the name as text (drop it on a condition / effect field)."""

    def __init__(self, name: str):
        super().__init__(f"⠿ {name}")
        self.name = name
        self.setToolTip("Drag onto a condition or effect field to use this variable")
        self.setCursor(Qt.CursorShape.OpenHandCursor)

    def mousePressEvent(self, event) -> None:
        if event.button() == Qt.MouseButton.LeftButton:
            data = QMimeData()
            data.setText(self.name)
            drag = QDrag(self)
            drag.setMimeData(data)
            drag.exec(Qt.DropAction.CopyAction)


class BlackboardPanel(QWidget):
    def __init__(self, catalog: SchemaCatalog, parent=None):
        super().__init__(parent)
        self.catalog = catalog
        self.folder: CompanionFolder | None = None
        # Set by the window: run an edit as one undoable step (→ done).
        self.commit = lambda _text, _fn: False
        outer = QVBoxLayout(self)
        outer.setContentsMargins(10, 8, 10, 8)
        title = QLabel("Blackboard")
        title.setProperty("role", "section")
        outer.addWidget(title)
        hint = QLabel("Variables of this companion (dialogs.json \"variables\"). Drag a name onto a condition or effect.")
        hint.setProperty("role", "muted")
        hint.setWordWrap(True)
        outer.addWidget(hint)
        buttons = QHBoxLayout()
        self.add_button = QPushButton("+ Add variable")
        self.add_button.setProperty("accent", True)
        self.add_button.setEnabled(False)
        self.add_button.clicked.connect(self.add_variable)
        buttons.addWidget(self.add_button)
        buttons.addStretch(1)
        outer.addLayout(buttons)
        scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        scroll.setFrameShape(QFrame.Shape.NoFrame)
        scroll.setHorizontalScrollBarPolicy(Qt.ScrollBarPolicy.ScrollBarAlwaysOff)
        self.list = QWidget()
        self.list_layout = QVBoxLayout(self.list)
        self.list_layout.setContentsMargins(0, 4, 0, 4)
        scroll.setWidget(self.list)
        outer.addWidget(scroll, 1)

    def set_folder(self, folder: CompanionFolder | None) -> None:
        self.folder = folder
        self.add_button.setEnabled(folder is not None)
        clear_layout(self.list_layout)
        if folder is not None:
            for name, decl in folder.variables.items():
                self.list_layout.addWidget(self._card(name, decl if isinstance(decl, dict) else {}, builtin=False))
            for builtin in self.catalog.enum("builtinVariables"):
                self.list_layout.addWidget(self._card(builtin["name"], {"type": builtin["type"], "default": builtin["default"]},
                                                      builtin=True))
        self.list_layout.addStretch(1)

    # ---- edits ----

    def add_variable(self) -> None:
        name, ok = QInputDialog.getText(self, "Add variable", "Variable name (a-z, 0-9, _):")
        name = name.strip()
        if ok and name:
            self.commit(f"Add variable {name}", lambda f: edit.add_variable(f, name, "bool"))

    def rename(self, old: str, new: str) -> bool:
        new = new.strip()
        if not new or new == old:
            return False
        answer = QMessageBox.question(
            self, "Rename variable", f"Rename '{old}' to '{new}' and every reference to it in this companion?\n\n"
                                     "Campaigns saved with the old name lose its value (docs/SCHEMA.md \"Variables\").")
        if answer != QMessageBox.StandardButton.Yes:
            self.set_folder(self.folder)
            return False
        return self.commit(f"Rename variable {old} → {new}", lambda f: edit.rename_variable(f, old, new))

    def remove(self, name: str) -> bool:
        return self.commit(f"Remove variable {name}", lambda f: edit.remove_variable(f, name))

    def set_field(self, name: str, field: str, value) -> bool:
        return self.commit(f"Set {name}.{field}", lambda f: edit.set_variable(f, name, field, value))

    # ---- cards ----

    def _card(self, name: str, decl: dict, builtin: bool) -> QFrame:
        card = QFrame()
        card.setProperty("role", "card")
        card.setObjectName(f"variable:{name}")
        grid = QGridLayout(card)
        grid.setContentsMargins(8, 8, 8, 8)
        grid.addWidget(DragName(name), 0, 0)
        if builtin:
            name_edit = QLineEdit(name)
            name_edit.setReadOnly(True)
        else:
            name_edit = LineField(name, lambda text, n=name: self.rename(n, text))
            name_edit.setObjectName(f"name:{name}")
            name_edit.setToolTip("Rename (references follow)")
        grid.addWidget(name_edit, 0, 1)
        if not builtin:
            remove = QToolButton()
            remove.setText("×")
            remove.setAutoRaise(True)
            remove.setToolTip(f"Remove {name}")
            remove.setObjectName(f"remove:{name}")
            remove.clicked.connect(lambda _=False, n=name: self.remove(n))
            grid.addWidget(remove, 0, 2)
        var_type = str(decl.get("type", "bool"))
        type_box = QComboBox()
        type_box.setObjectName(f"type:{name}")
        type_box.addItems(self.catalog.enum("variableTypes"))
        type_box.setCurrentText(var_type)
        type_box.setEnabled(not builtin)
        type_box.activated.connect(lambda i, n=name, b=type_box: b.itemText(i) != var_type
                                   and self.set_field(n, "type", b.itemText(i)))
        grid.addWidget(type_box, 1, 0)
        scope_box = QComboBox()
        scope_box.setObjectName(f"scope:{name}")
        scope_box.addItems(self.catalog.enum("variableScopes"))
        scope_box.setCurrentText(str(decl.get("scope", self.catalog.field("variable", "scope").default or "local")))
        scope_box.setEnabled(not builtin)
        scope_box.activated.connect(lambda i, n=name, b=scope_box: self.set_field(n, "scope", b.itemText(i)))
        grid.addWidget(scope_box, 1, 1, 1, 2)
        default = decl.get("default", 0 if var_type == "int" else False)
        if var_type == "bool":
            default_box: QWidget = QCheckBox("default true")
            default_box.setChecked(bool(default))
            default_box.toggled.connect(lambda on, n=name: self.set_field(n, "default", on))
        else:
            def commit_int(text: str, n=name) -> None:
                try:
                    self.set_field(n, "default", int(text))
                except ValueError:
                    self.set_folder(self.folder)
            default_box = LineField(json.dumps(default), commit_int)
            default_box.setToolTip("default")
        default_box.setObjectName(f"default:{name}")
        default_box.setEnabled(not builtin)
        grid.addWidget(default_box, 2, 0, 1, 3)
        if builtin:
            note = QLabel("built in")
            note.setProperty("role", "muted")
            grid.addWidget(note, 3, 0, 1, 3)
        return card
