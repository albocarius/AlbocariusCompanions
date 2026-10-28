"""Bottom panel, first tab: diagnostics from several sources (the editor's own checks, later ``validate --json``).

A diagnostic is the ``validate --json`` shape (docs/EXPORTS.md): severity, code, folder, file, conversation, node, message.
Activating one emits it so the window can open the tab and select the node."""

from __future__ import annotations

from PySide6.QtCore import Qt, Signal
from PySide6.QtGui import QColor, QGuiApplication
from PySide6.QtWidgets import QComboBox, QHBoxLayout, QLabel, QPushButton, QTreeWidget, QTreeWidgetItem, QVBoxLayout, QWidget

from . import theme


def layout_diagnostics(stale: list[tuple[str, str]], error: str | None) -> list[dict]:
    """The layout sidecar's own findings, in the validate shape."""
    result = [{"severity": "warning", "code": "layout_node_not_found", "folder": "", "file": "dialogs.layout.json",
               "conversation": conv, "node": None if key.startswith("@") else key,
               "message": f"layout: node not found: {conv}/{key} (dropped on next save)"} for conv, key in stale]
    if error:
        result.append({"severity": "warning", "code": "layout_unreadable", "folder": "", "file": "dialogs.layout.json",
                       "conversation": None, "node": None,
                       "message": f"layout: dialogs.layout.json could not be read ({error}); it is rewritten on save"})
    return result


class DiagnosticsPanel(QWidget):
    activated = Signal(dict)

    def __init__(self, parent=None):
        super().__init__(parent)
        self._by_source: dict[str, list[dict]] = {}
        layout = QVBoxLayout(self)
        layout.setContentsMargins(10, 6, 10, 6)
        top = QHBoxLayout()
        self.filter = QComboBox()
        self.filter.addItems(["All", "Errors", "Warnings"])
        self.filter.currentIndexChanged.connect(self._refresh)
        top.addWidget(self.filter)
        self.summary = QLabel("No diagnostics")
        self.summary.setStyleSheet(f"color: {theme.LINK}; background: transparent;")
        top.addWidget(self.summary)
        copy = QPushButton("Copy all")
        copy.clicked.connect(self.copy_all)
        top.addWidget(copy)
        top.addStretch(1)
        layout.addLayout(top)
        self.tree = QTreeWidget()
        self.tree.setMinimumHeight(24)  # the bottom panel may be dragged small
        self.tree.setHeaderLabels(["", "Where", "Message"])
        self.tree.setRootIsDecorated(False)
        self.tree.setColumnWidth(0, 70)
        self.tree.setColumnWidth(1, 260)
        self.tree.itemActivated.connect(lambda item: self.activated.emit(item.data(0, Qt.ItemDataRole.UserRole)))
        layout.addWidget(self.tree)

    def set_diagnostics(self, source: str, diagnostics: list[dict]) -> None:
        self._by_source[source] = list(diagnostics)
        self._refresh()

    def all(self) -> list[dict]:
        return [d for ds in self._by_source.values() for d in ds]

    def _refresh(self) -> None:
        self.tree.clear()
        wanted = {0: None, 1: "error", 2: "warning"}[self.filter.currentIndex()]
        diagnostics = self.all()
        for d in diagnostics:
            if wanted and d.get("severity") != wanted:
                continue
            where = " / ".join(str(x) for x in (d.get("file"), d.get("conversation"), d.get("node")) if x)
            item = QTreeWidgetItem([d.get("severity", ""), where, d.get("message", "")])
            item.setForeground(0, QColor(theme.ERROR if d.get("severity") == "error" else theme.WARNING))
            item.setToolTip(2, d.get("message", ""))
            item.setData(0, Qt.ItemDataRole.UserRole, d)
            self.tree.addTopLevelItem(item)
        errors = sum(1 for d in diagnostics if d.get("severity") == "error")
        warnings = len(diagnostics) - errors
        if not diagnostics:
            self.summary.setText("No diagnostics")
            self.summary.setStyleSheet(f"color: {theme.LINK}; background: transparent;")
        else:
            self.summary.setText(f"{errors} error(s), {warnings} warning(s)")
            self.summary.setStyleSheet(f"color: {theme.ERROR if errors else theme.WARNING}; background: transparent;")

    def copy_all(self) -> None:
        lines = [f"{d.get('severity', '').upper()} {d.get('code', '')} "
                 f"[{'/'.join(str(x) for x in (d.get('file'), d.get('conversation'), d.get('node')) if x)}] {d.get('message', '')}"
                 for d in self.all()]
        QGuiApplication.clipboard().setText("\n".join(lines))
