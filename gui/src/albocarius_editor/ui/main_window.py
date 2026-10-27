"""The main window: menus, toolbar, library (left), tabbed canvas (center), inspector/blackboard (right),
diagnostics/preview (bottom), status bar — the layout of gui/docs/reference.png."""

from __future__ import annotations

from pathlib import Path

from PySide6.QtCore import QPointF, QSettings, Qt, QTimer
from PySide6.QtGui import QAction, QCloseEvent, QKeySequence, QUndoCommand, QUndoStack
from PySide6.QtWidgets import (QDockWidget, QFileDialog, QInputDialog, QLabel, QMainWindow, QMessageBox, QTabWidget,
                               QToolBar, QWidget)

from .. import __version__
from ..cli.client import Cli, CliError
from ..data import graph, workspace
from ..data.companion import CompanionFolder, CompanionLoadError
from ..schema.catalog import SchemaCatalog
from .blackboard import BlackboardPanel
from .canvas.scene import ConversationScene
from .canvas.view import CanvasView
from .diagnostics import DiagnosticsPanel, layout_diagnostics
from .inspector import InspectorPanel
from .library import LINES_TAB, LibraryPanel
from .lines_view import LinesView
from .preview import PreviewPanel

APP_NAME = "AlbocariusCompanions Editor"


class MoveCommand(QUndoCommand):
    """A card dragged on the canvas. The drag already moved the card; redo (also run on push) records the position."""

    def __init__(self, window: MainWindow, conversation_id: str, key: str, old: QPointF, new: QPointF):
        super().__init__(f"Move {key}")
        self.window, self.conversation_id, self.key, self.old, self.new = window, conversation_id, key, old, new

    def _place(self, pos: QPointF) -> None:
        scene = self.window.scene_for(self.conversation_id)
        if scene is not None:
            scene.place(self.key, pos)
        elif self.window.folder is not None:
            self.window.folder.layout.set(self.conversation_id, self.key, pos.x(), pos.y())

    def redo(self) -> None:
        self._place(self.new)
        self.window.update_title()

    def undo(self) -> None:
        self._place(self.old)
        self.window.update_title()


class MainWindow(QMainWindow):
    def __init__(self, settings: QSettings | None = None):
        super().__init__()
        self.settings = settings or QSettings("Albocarius", "CompanionsEditor")
        self.folder: CompanionFolder | None = None
        self.companions_root: Path | None = None
        self.undo_stack = QUndoStack(self)
        self.undo_stack.cleanChanged.connect(lambda _: self.update_title())

        self.cli: Cli | None = None
        self.cli_error: str | None = None
        try:
            self.cli = Cli.find(self.settings.value("cliPath") or None)
        except CliError as e:
            self.cli_error = str(e)
        self.catalog = SchemaCatalog.load(self.cli)

        self.resize(1500, 900)
        self._build_actions()
        self._build_menus()
        self._build_toolbar()
        self._build_center()
        self._build_docks()
        self._build_status()

        self._disk_timer = QTimer(self)
        self._disk_timer.setInterval(2000)
        self._disk_timer.timeout.connect(self._check_disk)
        self._disk_timer.start()
        self._asking_disk = False
        self.update_title()

    # ---- construction ----

    def _action(self, text: str, slot=None, shortcut=None, tip: str = "", checkable: bool = False,
                enabled: bool = True) -> QAction:
        action = QAction(text, self)
        if shortcut is not None:
            action.setShortcut(shortcut)
        if tip:
            action.setToolTip(tip)
            action.setStatusTip(tip)
        action.setCheckable(checkable)
        action.setEnabled(enabled)
        if slot is not None:
            action.triggered.connect(slot)
        return action

    def _build_actions(self) -> None:
        later = "Comes in a later task (gui/PLAN.md)"
        self.act_new = self._action("New", self.new_companion, QKeySequence.StandardKey.New, "New companion from the template")
        self.act_open = self._action("Open", self.open_dialog, QKeySequence.StandardKey.Open, "Open a companion folder")
        self.act_open_root = self._action("Open Companions folder…", self.open_root_dialog, tip="Open a Companions directory")
        self.act_save = self._action("Save", self.save, QKeySequence.StandardKey.Save, "Save changed files")
        self.act_reload = self._action("Reload", self.reload, QKeySequence("Ctrl+R"), "Reload from disk")
        self.act_quit = self._action("Quit", self.close, QKeySequence.StandardKey.Quit)
        self.act_undo = self.undo_stack.createUndoAction(self, "Undo")
        self.act_undo.setShortcut(QKeySequence.StandardKey.Undo)
        self.act_redo = self.undo_stack.createRedoAction(self, "Redo")
        self.act_redo.setShortcut(QKeySequence.StandardKey.Redo)
        self.act_zoom_in = self._action("Zoom in", lambda: self._view_call("zoom_in"), QKeySequence.StandardKey.ZoomIn)
        self.act_zoom_out = self._action("Zoom out", lambda: self._view_call("zoom_out"), QKeySequence.StandardKey.ZoomOut)
        self.act_fit = self._action("Fit", lambda: self._view_call("fit_all"), QKeySequence("Ctrl+0"))
        self.act_snap = self._action("Snap: On", self._toggle_snap, tip="Snap cards to the grid while dragging", checkable=True)
        self.act_snap.setChecked(self.settings.value("snap", "true") == "true")
        self._toggle_snap(self.act_snap.isChecked())
        self.act_validate = self._action("Validate", tip=later, enabled=False)
        self.act_play = self._action("Play", tip=later, enabled=False)
        self.act_stop = self._action("Stop", tip=later, enabled=False)
        self.act_about = self._action("About", self.about)

    def _build_menus(self) -> None:
        bar = self.menuBar()
        file = bar.addMenu("&File")
        for a in (self.act_new, self.act_open, self.act_open_root, None, self.act_save, self.act_reload, None, self.act_quit):
            file.addSeparator() if a is None else file.addAction(a)
        edit = bar.addMenu("&Edit")
        edit.addAction(self.act_undo)
        edit.addAction(self.act_redo)
        view = bar.addMenu("&View")
        for a in (self.act_zoom_in, self.act_zoom_out, self.act_fit, None, self.act_snap):
            view.addSeparator() if a is None else view.addAction(a)
        self.view_menu = view
        graph_menu = bar.addMenu("&Graph")
        graph_menu.addAction(self._action("Add companion line", enabled=False, tip="Comes in a later task (gui/PLAN.md)"))
        graph_menu.addAction(self._action("Add player line", enabled=False, tip="Comes in a later task (gui/PLAN.md)"))
        build = bar.addMenu("&Build")
        for a in (self.act_validate, self.act_play, self.act_stop):
            build.addAction(a)
        bar.addMenu("&Help").addAction(self.act_about)

    def _build_toolbar(self) -> None:
        tb = QToolBar("Main")
        tb.setObjectName("mainToolbar")
        tb.setMovable(False)
        for a in (self.act_new, self.act_open, self.act_save, self.act_reload):
            tb.addAction(a)
        tb.addSeparator()
        tb.addAction(self.act_validate)
        tb.widgetForAction(self.act_validate).setProperty("accent", True)
        tb.addAction(self.act_play)
        tb.addAction(self.act_stop)
        tb.addSeparator()
        tb.addAction(self.act_snap)
        self.addToolBar(tb)

    def _build_center(self) -> None:
        self.tabs = QTabWidget()
        self.tabs.setDocumentMode(True)
        self.tabs.setTabsClosable(True)
        self.tabs.setMovable(True)
        self.tabs.tabCloseRequested.connect(self._close_tab)
        self.tabs.currentChanged.connect(self._tab_changed)
        self.setCentralWidget(self.tabs)
        self.lines_view = LinesView(self.catalog)
        self.lines_view.line_selected.connect(lambda line_id: self._show_in_inspector(None, line_id))

    def _dock(self, title: str, widget: QWidget, area: Qt.DockWidgetArea, name: str) -> QDockWidget:
        dock = QDockWidget(title, self)
        dock.setObjectName(name)
        dock.setWidget(widget)
        dock.setFeatures(QDockWidget.DockWidgetFeature.DockWidgetMovable | QDockWidget.DockWidgetFeature.DockWidgetFloatable
                         | QDockWidget.DockWidgetFeature.DockWidgetClosable)
        self.addDockWidget(area, dock)
        self.view_menu.addAction(dock.toggleViewAction())
        return dock

    def _build_docks(self) -> None:
        self.view_menu.addSeparator()
        self.library = LibraryPanel()
        self.library.open_conversation.connect(self.open_tab)
        self.library.open_node.connect(self.reveal)
        self.library.open_companion.connect(self.open_companion)
        self.library.palette_activated.connect(
            lambda kind: self.statusBar().showMessage("Adding nodes comes in a later task (gui/PLAN.md)", 4000))
        self.library_dock = self._dock("Library", self.library, Qt.DockWidgetArea.LeftDockWidgetArea, "library")

        self.inspector = InspectorPanel(self.catalog)
        self.blackboard = BlackboardPanel(self.catalog)
        right = QTabWidget()
        right.addTab(self.inspector, "Inspector")
        right.addTab(self.blackboard, "Blackboard")
        self.right_tabs = right
        self.right_dock = self._dock("Properties", right, Qt.DockWidgetArea.RightDockWidgetArea, "properties")
        self.right_dock.setTitleBarWidget(QWidget())

        self.diagnostics = DiagnosticsPanel()
        self.diagnostics.activated.connect(self.reveal_diagnostic)
        self.preview = PreviewPanel()
        self.preview.set_cli(str(self.cli.path) if self.cli else None, self.cli_error)
        bottom = QTabWidget()
        bottom.addTab(self.diagnostics, "Diagnostics")
        bottom.addTab(self.preview, "Preview")
        self.bottom_tabs = bottom
        self.bottom_dock = self._dock("Output", bottom, Qt.DockWidgetArea.BottomDockWidgetArea, "output")
        self.bottom_dock.setTitleBarWidget(QWidget())

        self.resizeDocks([self.library_dock, self.right_dock], [260, 340], Qt.Orientation.Horizontal)
        self.resizeDocks([self.bottom_dock], [190], Qt.Orientation.Vertical)

    def _build_status(self) -> None:
        bar = self.statusBar()
        self.status_cli = QLabel()
        self.status_schema = QLabel()
        bar.addPermanentWidget(self.status_cli)
        bar.addPermanentWidget(self.status_schema)
        self.status_cli.setText(f"CLI: {self.cli.path.name}" if self.cli else "CLI: not found")
        self.status_cli.setToolTip(str(self.cli.path) if self.cli else (self.cli_error or ""))
        self.status_schema.setText(f"Schema: {self.catalog.source}")
        bar.showMessage("Ready", 3000)

    # ---- tabs and scenes ----

    def _view_call(self, name: str) -> None:
        view = self.tabs.currentWidget()
        if isinstance(view, CanvasView):
            getattr(view, name)()

    def _toggle_snap(self, on: bool) -> None:
        self.act_snap.setText(f"Snap: {'On' if on else 'Off'}")
        self.settings.setValue("snap", "true" if on else "false")
        for scene in self._scenes():
            scene.set_snap(on)

    def _scenes(self) -> list[ConversationScene]:
        if not hasattr(self, "tabs"):
            return []
        return [w.scene() for w in (self.tabs.widget(i) for i in range(self.tabs.count()))
                if isinstance(w, CanvasView) and isinstance(w.scene(), ConversationScene)]

    def scene_for(self, conversation_id: str) -> ConversationScene | None:
        return next((s for s in self._scenes() if s.conversation_id == conversation_id), None)

    def _tab_index(self, key: str) -> int:
        for i in range(self.tabs.count()):
            if self.tabs.widget(i).property("tabKey") == key:
                return i
        return -1

    def open_tab(self, conversation_id: str) -> CanvasView | LinesView | None:
        """Shows (opening if needed) the Lines tab or a conversation's canvas."""
        if self.folder is None:
            return None
        index = self._tab_index(conversation_id)
        if index < 0:
            if conversation_id == LINES_TAB:
                widget = self.lines_view
                title = "Lines"
            else:
                if self.folder.conversation(conversation_id) is None:
                    return None
                scene = ConversationScene(self.folder, conversation_id, self.catalog, self)
                scene.set_snap(self.act_snap.isChecked())
                scene.item_selected.connect(lambda key, c=conversation_id: self._show_in_inspector(c, key))
                scene.item_moved.connect(lambda key, old, new, c=conversation_id:
                                         self.undo_stack.push(MoveCommand(self, c, key, old, new)))
                widget = CanvasView()
                widget.setScene(scene)
                title = conversation_id
            widget.setProperty("tabKey", conversation_id)
            index = self.tabs.addTab(widget, title)
            if isinstance(widget, CanvasView):
                QTimer.singleShot(0, widget.fit_all)
        self.tabs.setCurrentIndex(index)
        return self.tabs.widget(index)

    def _close_tab(self, index: int) -> None:
        self.tabs.removeTab(index)

    def _tab_changed(self, index: int) -> None:
        widget = self.tabs.widget(index)
        if isinstance(widget, CanvasView) and isinstance(widget.scene(), ConversationScene):
            scene = widget.scene()
            self._show_in_inspector(scene.conversation_id, scene.selected_key())

    def reveal(self, conversation_id: str, key: str) -> None:
        """Opens the tab of a node (or line) and selects it."""
        widget = self.open_tab(conversation_id)
        if isinstance(widget, LinesView):
            widget.select_line(key)
        elif isinstance(widget, CanvasView):
            card = widget.scene().select(key)
            if card is not None:
                widget.centerOn(card)

    def reveal_diagnostic(self, d: dict) -> None:
        if self.folder is None:
            return
        node, conv = d.get("node"), d.get("conversation")
        if conv and self.folder.conversation(conv) is not None:
            self.reveal(conv, node if node and self.folder.node(conv, node) else graph.HEADER)
        elif node:
            where = self.folder.find_id(node)
            if where is not None:
                self.reveal(where[0] or LINES_TAB, node)

    def _show_in_inspector(self, conversation_id: str | None, key: str) -> None:
        if self.folder is None or not key:
            self.inspector.show_nothing()
            return
        self.inspector.show_item(self.folder, conversation_id, key)
        self.right_tabs.setCurrentWidget(self.inspector)

    # ---- opening, saving ----

    def confirm_discard(self) -> bool:
        """True when it is fine to drop the current folder (saved, discarded, or nothing open)."""
        if self.folder is None or not self.folder.dirty:
            return True
        answer = QMessageBox.question(self, APP_NAME, f"Save the changes to {self.folder.id}?",
                                      QMessageBox.StandardButton.Save | QMessageBox.StandardButton.Discard
                                      | QMessageBox.StandardButton.Cancel, QMessageBox.StandardButton.Save)
        if answer == QMessageBox.StandardButton.Save:
            return self.save()
        return answer == QMessageBox.StandardButton.Discard

    def open_dialog(self) -> None:
        start = str(self.companions_root or self.settings.value("lastFolder") or Path.home())
        path = QFileDialog.getExistingDirectory(self, "Open a companion folder", start)
        if path:
            self.open_path(Path(path))

    def open_root_dialog(self) -> None:
        start = str(self.companions_root or self.settings.value("lastFolder") or Path.home())
        path = QFileDialog.getExistingDirectory(self, "Open a Companions directory", start)
        if path:
            self.open_path(Path(path))

    def open_path(self, path: Path) -> bool:
        """A companion folder (has companion.json) or a Companions directory (opens its first companion)."""
        if workspace.is_companion_folder(path):
            self.companions_root = path.parent
            return self.open_companion(path)
        folders = workspace.companion_folders(path)
        self.companions_root = path
        self.library.set_companions(folders, None)
        if folders:
            return self.open_companion(folders[0])
        self.statusBar().showMessage(f"No companion folders in {path}", 5000)
        return False

    def open_companion(self, path: Path) -> bool:
        if self.folder is not None and Path(path) == self.folder.path:
            return True
        if not self.confirm_discard():
            return False
        try:
            folder = CompanionFolder.load(path)
        except CompanionLoadError as e:
            QMessageBox.warning(self, APP_NAME, f"Cannot open {path}:\n{e}")
            return False
        self._set_folder(folder)
        self.settings.setValue("lastFolder", str(path))
        return True

    def _set_folder(self, folder: CompanionFolder | None) -> None:
        self.folder = folder
        self.undo_stack.clear()
        while self.tabs.count():
            self.tabs.removeTab(0)
        self.library.set_folder(folder)
        self.blackboard.set_folder(folder)
        self.inspector.show_nothing()
        if self.companions_root is not None:
            self.library.set_companions(workspace.companion_folders(self.companions_root), folder.path if folder else None)
        if folder is not None:
            self.lines_view.set_folder(folder)
            self.open_tab(LINES_TAB)
            first = next((c.get("id") for c in folder.conversations if isinstance(c.get("id"), str)), None)
            if first:
                self.open_tab(first)
            self.diagnostics.set_diagnostics("layout", layout_diagnostics(
                folder.layout.stale(folder.node_ids_by_conversation()), folder.layout.error))
            self.statusBar().showMessage(f"Opened {folder.path}", 4000)
        self.update_title()

    def save(self) -> bool:
        if self.folder is None:
            return True
        try:
            written = self.folder.save()
        except OSError as e:
            QMessageBox.critical(self, APP_NAME, f"Saving failed:\n{e}")
            return False
        self.undo_stack.setClean()
        self.diagnostics.set_diagnostics("layout", layout_diagnostics([], None))
        self.statusBar().showMessage(f"Saved {len(written)} file(s)" if written else "Nothing to save", 4000)
        self.update_title()
        return True

    def reload(self) -> None:
        if self.folder is None:
            return
        if self.folder.dirty and QMessageBox.question(
                self, APP_NAME, "Discard unsaved changes and reload from disk?") != QMessageBox.StandardButton.Yes:
            return
        path = self.folder.path
        self.folder = None  # no second question from open_companion
        self.open_companion(path)

    def new_companion(self) -> None:
        root = self.companions_root
        if root is None:
            chosen = QFileDialog.getExistingDirectory(self, "Choose the Companions directory", str(Path.home()))
            if not chosen:
                return
            root = Path(chosen)
        companion_id, ok = QInputDialog.getText(self, "New companion", "Companion id (a-z, 0-9, _):")
        if not ok or not companion_id:
            return
        try:
            path = workspace.new_companion(root, companion_id.strip())
        except (workspace.WorkspaceError, OSError) as e:
            QMessageBox.warning(self, APP_NAME, str(e))
            return
        self.companions_root = root
        self.open_companion(path)

    def _check_disk(self) -> None:
        if self.folder is None or self._asking_disk:
            return
        changed = self.folder.changed_on_disk()
        if not changed:
            return
        self._asking_disk = True
        try:
            names = "\n".join(str(p.relative_to(self.folder.path)) for p in changed)
            extra = "\n\nYour unsaved changes will be lost." if self.folder.dirty else ""
            answer = QMessageBox.question(self, APP_NAME, f"These files changed on disk:\n{names}\n\nReload?{extra}")
            if answer == QMessageBox.StandardButton.Yes:
                path = self.folder.path
                self.folder = None
                self.open_companion(path)
            else:
                self.folder.accept_disk_state()
        finally:
            self._asking_disk = False

    def update_title(self) -> None:
        if self.folder is None:
            self.setWindowTitle(APP_NAME)
            return
        dirty = self.folder.dirty
        self.setWindowTitle(f"{'• ' if dirty else ''}{self.folder.id} — {self.folder.path.parent} — {APP_NAME}")
        self.setWindowModified(dirty)

    def about(self) -> None:
        QMessageBox.about(self, APP_NAME, f"{APP_NAME} {__version__}\n\nNode editor for AlbocariusCompanions companion "
                                           f"packs.\nSchema: {self.catalog.source}\nCLI: {self.cli.path if self.cli else 'not found'}")

    def closeEvent(self, event: QCloseEvent) -> None:
        if self.confirm_discard():
            event.accept()
        else:
            event.ignore()
