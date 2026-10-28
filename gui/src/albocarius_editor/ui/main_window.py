"""The main window: menus, toolbar, library (left), tabbed canvas (center), inspector/blackboard (right),
diagnostics/preview (bottom), status bar — the layout of gui/docs/reference.png."""

from __future__ import annotations

import json
from pathlib import Path

from PySide6.QtCore import QMimeData, QPointF, QRectF, QSettings, Qt, QTimer
from PySide6.QtGui import QAction, QCloseEvent, QCursor, QGuiApplication, QKeySequence, QUndoStack
from PySide6.QtWidgets import (QDockWidget, QFileDialog, QInputDialog, QLabel, QMainWindow, QMenu, QMessageBox, QTabWidget,
                               QToolBar, QWidget)

from .. import __version__
from ..cli.client import Cli, CliError
from ..data import edit, graph, workspace
from ..data.companion import ENGLISH, CompanionFolder, CompanionLoadError
from ..data.edit import EditError
from ..schema.catalog import SchemaCatalog
from . import settings as settings_ui
from . import workers
from .blackboard import BlackboardPanel
from .canvas.items import GRID
from .canvas.lines_scene import PLACEHOLDER, LinesScene
from .canvas.scene import ConversationScene
from .canvas.view import CanvasView
from .commands import EditCommand, MoveCommand
from .diagnostics import DiagnosticsPanel, layout_diagnostics
from .inspector import InspectorPanel
from .library import LINES_TAB, LibraryPanel
from .preview import PreviewPanel

APP_NAME = "AlbocariusCompanions Editor"
NODES_MIME = "application/x-albocarius-nodes"


class MainWindow(QMainWindow):
    def __init__(self, settings: QSettings | None = None):
        super().__init__()
        self.settings = settings or QSettings("Albocarius", "CompanionsEditor")
        self.folder: CompanionFolder | None = None
        self._inspected: tuple[str | None, str] | None = None  # (conversation id or None for a line, key)
        self._edit_error: EditError | None = None
        self.validation_runs = 0  # finished validations (tests wait on it)
        self.companions_root: Path | None = None
        self.undo_stack = QUndoStack(self)
        self.undo_stack.cleanChanged.connect(self._clean_changed)

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
        self.act_new = self._action("New", self.new_companion, QKeySequence.StandardKey.New, "New companion from the template")
        self.act_open = self._action("Open", self.open_dialog, QKeySequence.StandardKey.Open, "Open a companion folder")
        self.act_open_root = self._action("Open Companions folder…", self.open_root_dialog, tip="Open a Companions directory")
        self.act_save = self._action("Save", self.save, QKeySequence.StandardKey.Save, "Save changed files")
        self.act_reload = self._action("Reload", self.reload, QKeySequence("Ctrl+R"), "Reload from disk")
        self.act_quit = self._action("Quit", self.close, QKeySequence.StandardKey.Quit)
        self.act_settings = self._action("Settings…", self.open_settings, QKeySequence.StandardKey.Preferences,
                                         "CLI path, default Companions directory")
        self.act_undo = self.undo_stack.createUndoAction(self, "Undo")
        self.act_undo.setShortcut(QKeySequence.StandardKey.Undo)
        self.act_redo = self.undo_stack.createRedoAction(self, "Redo")
        self.act_redo.setShortcut(QKeySequence.StandardKey.Redo)
        self.act_delete = self._action("Delete", self.delete_selection, QKeySequence.StandardKey.Delete,
                                       "Delete the selected cards, or disconnect the selected links")
        self.act_delete.setShortcutContext(Qt.ShortcutContext.WidgetWithChildrenShortcut)
        self.act_copy = self._action("Copy", self.copy_selection, QKeySequence.StandardKey.Copy, "Copy the selected cards")
        self.act_paste = self._action("Paste", self.paste, QKeySequence.StandardKey.Paste,
                                      "Paste cards (same companion) into this conversation")
        self.act_duplicate = self._action("Duplicate", self.duplicate_selection, QKeySequence("Ctrl+D"),
                                          "Duplicate the selected cards")
        for a in (self.act_copy, self.act_paste, self.act_duplicate):
            a.setShortcutContext(Qt.ShortcutContext.WidgetWithChildrenShortcut)
        self.act_zoom_in = self._action("Zoom in", lambda: self._view_call("zoom_in"), QKeySequence.StandardKey.ZoomIn)
        self.act_zoom_out = self._action("Zoom out", lambda: self._view_call("zoom_out"), QKeySequence.StandardKey.ZoomOut)
        self.act_fit = self._action("Fit", lambda: self._view_call("fit_all"), QKeySequence("Ctrl+0"))
        self.act_snap = self._action("Snap: On", self._toggle_snap, tip="Snap cards to the grid while dragging", checkable=True)
        self.act_snap.setChecked(self.settings.value("snap", "true") == "true")
        self._toggle_snap(self.act_snap.isChecked())
        self.act_validate = self._action("Validate", self.validate, QKeySequence("F7"),
                                         "Validate the saved files with the mod's CLI (also runs on save)")
        self.act_play = self._action("Play", self.play, QKeySequence("F5"), "Start a simulated talk from the hub (Preview)")
        self.act_stop = self._action("Stop", self.stop_preview, QKeySequence("Shift+F5"), "Close the simulated talk")
        self.act_about = self._action("About", self.about)

    def _build_menus(self) -> None:
        bar = self.menuBar()
        file = bar.addMenu("&File")
        for a in (self.act_new, self.act_open, self.act_open_root):
            file.addAction(a)
        self.recent_menu = file.addMenu("Open recent")
        self._fill_recent()
        for a in (None, self.act_save, self.act_reload, None, self.act_settings, None, self.act_quit):
            file.addSeparator() if a is None else file.addAction(a)
        edit = bar.addMenu("&Edit")
        edit.addAction(self.act_undo)
        edit.addAction(self.act_redo)
        edit.addSeparator()
        for a in (self.act_copy, self.act_paste, self.act_duplicate, self.act_delete):
            edit.addAction(a)
        view = bar.addMenu("&View")
        for a in (self.act_zoom_in, self.act_zoom_out, self.act_fit, None, self.act_snap):
            view.addSeparator() if a is None else view.addAction(a)
        self.view_menu = view
        graph_menu = bar.addMenu("&Graph")
        graph_menu.addAction(self._action("Add companion line", lambda: self.add_from_palette("npc"),
                                          QKeySequence("Ctrl+Shift+N"), "A companion (npc) node at the view center"))
        graph_menu.addAction(self._action("Add player line", lambda: self.add_from_palette("player"),
                                          QKeySequence("Ctrl+Shift+P"), "A player option at the view center"))
        graph_menu.addAction(self._action("New conversation…", self.new_conversation, QKeySequence("Ctrl+Shift+C"),
                                          "A conversation from the hub menu, a greeting or a trigger"))
        graph_menu.addSeparator()
        graph_menu.addAction(self._action("Add line…", lambda: self.add_line(ask=True), QKeySequence("Ctrl+Shift+L"),
                                          "A line of dialogs.json \"lines\" (a slot, or a triggered line)"))
        graph_menu.addSeparator()
        graph_menu.addAction(self._action("Continue with: Back to hub", lambda: self.add_from_palette("hub"),
                                          tip="The selected node returns to the companion's menu"))
        graph_menu.addAction(self._action("Continue with: End", lambda: self.add_from_palette("end"),
                                          tip="The selected node ends the talk"))
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
        # Del / copy / paste act while the canvas has focus (text fields keep their own).
        for a in (self.act_delete, self.act_copy, self.act_paste, self.act_duplicate):
            self.tabs.addAction(a)
        self.tabs.tabBar().setContextMenuPolicy(Qt.ContextMenuPolicy.CustomContextMenu)
        self.tabs.tabBar().customContextMenuRequested.connect(self._tab_menu)
        self.setCentralWidget(self.tabs)
        self.lines_scene = LinesScene(None, self.catalog, self)
        self.lines_scene.item_selected.connect(lambda line_id: self._show_in_inspector(None, line_id))
        self.lines_scene.text_committed.connect(self.set_text)
        self.lines_scene.add_requested.connect(self.add_line)
        self.lines_scene.context_requested.connect(self._line_menu)
        self.lines_view = CanvasView()
        self.lines_view.setScene(self.lines_scene)

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
        self.library.palette_activated.connect(self.add_from_palette)
        self.library.conversation_menu.connect(self._library_menu)
        self.library_dock = self._dock("Library", self.library, Qt.DockWidgetArea.LeftDockWidgetArea, "library")

        self.inspector = InspectorPanel(self.catalog)
        self.inspector.text_committed.connect(self.set_text)
        self.inspector.commit = lambda text, fn: self.edit(text, fn)[0]
        self.inspector.message = lambda text: self.statusBar().showMessage(text, 6000)
        self.inspector.id_committed.connect(self.rename_item)
        self.inspector.edge_move.connect(lambda c, s, t, i: self.edit(
            f"Reorder {s} → {t}", lambda f: edit.move_edge(f, c, s, t, i)))
        self.inspector.edge_remove.connect(lambda c, s, t: self.edit(
            f"Disconnect {s} → {t}", lambda f: edit.disconnect(f, c, s, t)))
        self.blackboard = BlackboardPanel(self.catalog)
        self.blackboard.commit = lambda text, fn: self.edit(text, fn)[0]
        right = QTabWidget()
        right.addTab(self.inspector, "Inspector")
        right.addTab(self.blackboard, "Blackboard")
        self.right_tabs = right
        self.right_dock = self._dock("Properties", right, Qt.DockWidgetArea.RightDockWidgetArea, "properties")
        self.right_dock.setTitleBarWidget(QWidget())

        self.diagnostics = DiagnosticsPanel()
        self.diagnostics.activated.connect(self.reveal_diagnostic)
        self.preview = PreviewPanel(self.catalog)
        self.preview.set_cli(str(self.cli.path) if self.cli else None, self.cli_error)
        self.preview.highlight.connect(self._preview_highlight)
        self.preview.message.connect(lambda text: self.statusBar().showMessage(text, 8000))
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

    def open_tab(self, conversation_id: str) -> CanvasView | None:
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
                self._wire_scene(scene)
                widget = CanvasView()
                widget.setScene(scene)
                widget.palette_dropped.connect(lambda kind, pos: self.add_from_palette(kind, pos))
                title = conversation_id
            widget.setProperty("tabKey", conversation_id)
            index = self.tabs.addTab(widget, title)
            if widget is self.lines_view:  # many cards: start readable at the top left, not fitted
                QTimer.singleShot(0, lambda: (self.lines_view.set_zoom(0.85),
                                              self.lines_view.ensureVisible(QRectF(-20, -20, 10, 10))))
            elif isinstance(widget, CanvasView):
                QTimer.singleShot(0, widget.fit_all)
        self.tabs.setCurrentIndex(index)
        return self.tabs.widget(index)

    def _wire_scene(self, scene: ConversationScene) -> None:
        """Connects a conversation canvas; handlers read ``scene.conversation_id`` when called (renames retarget it)."""
        scene.item_selected.connect(lambda key: self._show_in_inspector(scene.conversation_id, key))
        scene.item_moved.connect(lambda key, old, new: self.undo_stack.push(
            MoveCommand(self, scene.conversation_id, key, old, new)))
        scene.text_committed.connect(self.set_text)
        scene.context_requested.connect(lambda key, pos: self._card_menu(scene.conversation_id, key, pos))
        scene.link_checker = lambda source, target: (
            edit.link_problem(self.folder, scene.conversation_id, source, target) if self.folder else None)
        scene.link_hint.connect(lambda text: self.statusBar().showMessage(f"Cannot link: {text}", 6000)
                                if text else self.statusBar().clearMessage())
        scene.connect_requested.connect(lambda source, target: self.connect_nodes(scene.conversation_id, source, target))

    def _close_tab(self, index: int) -> None:
        self.tabs.removeTab(index)

    def _tab_changed(self, index: int) -> None:
        widget = self.tabs.widget(index)
        if isinstance(widget, CanvasView) and isinstance(widget.scene(), ConversationScene):
            scene = widget.scene()
            self._show_in_inspector(scene.conversation_id, scene.selected_key())
        elif widget is self.lines_view:
            self._show_in_inspector(None, self.lines_scene.selected_key())

    def reveal(self, conversation_id: str, key: str) -> None:
        """Opens the tab of a node (or line) and selects it."""
        widget = self.open_tab(conversation_id)
        if isinstance(widget, CanvasView):
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
            self._inspected = None
            self.inspector.show_nothing()
            return
        self._inspected = (conversation_id, key)
        self.inspector.show_item(self.folder, conversation_id, key)
        self.right_tabs.setCurrentWidget(self.inspector)

    # ---- editing ----

    def edit(self, text: str, fn, on_redo=None, on_undo=None) -> tuple[bool, object]:
        """Runs ``fn(folder)`` (a ``data.edit`` call) as one undoable step. Returns (done, fn's result); an
        :class:`EditError` is shown in the status bar and pushes nothing."""
        if self.folder is None:
            return False, None
        self._edit_error = None
        command = EditCommand(self, text, fn, on_redo, on_undo)
        self.undo_stack.push(command)  # runs redo(); an obsolete (failed) command is deleted by the stack
        if self._edit_error is not None:
            self.statusBar().showMessage(f"{text}: {self._edit_error}", 6000)
            return False, None
        return True, command.result

    def set_text(self, local_id: str, text: str, code: str = ENGLISH) -> bool:
        """The text of a line or node in one language, as one undoable step (nothing when unchanged)."""
        if self.folder is None or self.folder.text(local_id, code) == text:
            return False
        return self.edit(f"Edit text of {local_id}", lambda f: edit.set_text(f, local_id, text, code))[0]

    def current_scene(self) -> ConversationScene | None:
        widget = self.tabs.currentWidget()
        if isinstance(widget, CanvasView) and isinstance(widget.scene(), ConversationScene):
            return widget.scene()
        return None

    def add_from_palette(self, kind: str, pos: QPointF | None = None) -> str | None:
        """npc/player: a new node in the current conversation at ``pos`` (default: the view center), selected with its
        text editor open; returns its id. hub/end: the selected node continues with Back to hub / End."""
        scene = self.current_scene()
        if self.folder is not None and self.tabs.currentWidget() is self.lines_view and kind in ("npc", "player"):
            self.statusBar().showMessage("Lines are added with Graph → Add line… or by double-clicking a missing slot", 5000)
            return None
        if self.folder is None or scene is None:
            self.statusBar().showMessage("Open a conversation tab first", 4000)
            return None
        conv = scene.conversation_id
        if kind in ("hub", "end"):
            key = scene.selected_key()
            if not key or key.startswith("@"):
                self.statusBar().showMessage("Select a node card first", 4000)
                return None
            self.edit(f"{key} → {kind}", lambda f: edit.connect(f, conv, key, graph.TERMINALS[kind]))
            return None
        if pos is None:
            pos = self.tabs.currentWidget().center_scene_pos()
        x, y = pos.x(), pos.y()
        if self.act_snap.isChecked():
            x, y = round(x / GRID) * GRID, round(y / GRID) * GRID

        def add(f: CompanionFolder) -> str:
            node = edit.add_node(f, conv, kind)
            f.layout.set(conv, node["id"], x, y)
            return node["id"]

        done, node_id = self.edit(f"Add {'player' if kind == 'player' else 'companion'} line", add)
        if not done:
            return None
        scene.edit_text(node_id)
        return node_id

    def connect_nodes(self, conversation_id: str, source: str, target: str) -> bool:
        return self.edit(f"Connect {source} → {target}", lambda f: edit.connect(f, conversation_id, source, target))[0]

    def delete_selection(self) -> None:
        """Del on the canvas: selected edges are disconnected, else the selected node cards are deleted."""
        if self.tabs.currentWidget() is self.lines_view:
            self.delete_lines([k for k in self.lines_scene.selected_keys() if not k.startswith(PLACEHOLDER)])
            return
        scene = self.current_scene()
        if scene is None:
            return
        conv = scene.conversation_id
        edges = [(e.source.key, e.target_key) for e in scene.selected_edges()]
        if edges:
            def disconnect_all(f: CompanionFolder) -> None:
                for source, target in edges:
                    edit.disconnect(f, conv, source, target)
            self.edit("Disconnect" if len(edges) == 1 else f"Disconnect {len(edges)} links", disconnect_all)
            return
        self.delete_nodes(conv, [k for k in scene.selected_keys() if not k.startswith("@")])

    # ---- copy / paste (T14) ----

    def _copy_payload(self) -> dict | None:
        scene = self.current_scene()
        if self.folder is None or scene is None:
            return None
        order = self.folder.node_ids_by_conversation().get(scene.conversation_id, [])
        keys = sorted((k for k in scene.selected_keys() if not k.startswith("@")), key=order.index)  # file order
        if not keys:
            self.statusBar().showMessage("Select node cards to copy", 4000)
            return None
        payload = edit.copy_nodes(self.folder, scene.conversation_id, keys)
        payload["positions"] = {k: [scene.cards[k].pos().x(), scene.cards[k].pos().y()] for k in keys if k in scene.cards}
        return payload

    def copy_selection(self) -> bool:
        payload = self._copy_payload()
        if payload is None:
            return False
        text = json.dumps(payload, ensure_ascii=False)
        data = QMimeData()
        data.setData(NODES_MIME, text.encode("utf-8"))
        data.setText(text)
        QGuiApplication.clipboard().setMimeData(data)
        self.statusBar().showMessage(f"Copied {len(payload['nodes'])} node(s)", 3000)
        return True

    def paste(self, payload: dict | None = None, at: QPointF | None = None) -> list[str]:
        """Pastes copied nodes into the current conversation with fresh ids (at ``at``, the mouse, or 40 px off the
        originals). Only nodes copied from this companion are accepted."""
        scene = self.current_scene()
        if self.folder is None or scene is None:
            self.statusBar().showMessage("Open a conversation tab to paste into", 4000)
            return []
        if payload is None:
            data = QGuiApplication.clipboard().mimeData()
            raw = bytes(data.data(NODES_MIME)).decode("utf-8") if data.hasFormat(NODES_MIME) else data.text()
            try:
                payload = json.loads(raw)
            except (json.JSONDecodeError, TypeError):
                payload = None
        if not isinstance(payload, dict) or payload.get("kind") != "albocarius-nodes":
            self.statusBar().showMessage("The clipboard holds no nodes", 4000)
            return []
        if payload.get("companion") != self.folder.id:
            self.statusBar().showMessage(f"These nodes were copied from {payload.get('companion')}; nodes are pasted only "
                                         f"within the same companion", 6000)
            return []
        positions = payload.get("positions", {})
        if positions:
            left = min(p[0] for p in positions.values())
            top = min(p[1] for p in positions.values())
        else:
            left = top = 0.0
        if at is None:
            view = self.tabs.currentWidget()
            cursor = view.viewport().mapFromGlobal(QCursor.pos())
            at = (view.mapToScene(cursor) if view.viewport().rect().contains(cursor)
                  else QPointF(left + 40, top + 40))
        conv = scene.conversation_id

        def paste_all(f: CompanionFolder) -> list[str]:
            new_ids = edit.paste_nodes(f, conv, payload)
            for i, (node, new_id) in enumerate(zip(payload["nodes"], new_ids)):
                old = positions.get(node["id"], [left + 40 * i, top + 40 * i])
                f.layout.set(conv, new_id, at.x() + old[0] - left, at.y() + old[1] - top)
            return new_ids
        done, new_ids = self.edit(f"Paste {len(payload['nodes'])} node(s)", paste_all)
        if not done:
            return []
        scene.clearSelection()
        for new_id in new_ids:
            if new_id in scene.cards:
                scene.cards[new_id].setSelected(True)
        return new_ids

    def duplicate_selection(self) -> list[str]:
        payload = self._copy_payload()
        if payload is None:
            return []
        positions = payload.get("positions", {})
        left = min((p[0] for p in positions.values()), default=0.0)
        top = min((p[1] for p in positions.values()), default=0.0)
        return self.paste(payload, QPointF(left + 40, top + 40))

    def delete_nodes(self, conversation_id: str, node_ids: list[str]) -> bool:
        """Deletes nodes (references, texts in every language, layout) as one step; asks first when they have text."""
        if self.folder is None or not node_ids:
            return False
        if any(self.folder.text(n, code) for n in node_ids for code in self.folder.languages()):
            what = node_ids[0] if len(node_ids) == 1 else f"{len(node_ids)} nodes"
            if QMessageBox.question(self, APP_NAME, f"Delete {what} and its texts in every language?") \
                    != QMessageBox.StandardButton.Yes:
                return False

        def delete_all(f: CompanionFolder) -> None:
            for node_id in node_ids:
                edit.delete_node(f, conversation_id, node_id)
        return self.edit(f"Delete {node_ids[0]}" if len(node_ids) == 1 else f"Delete {len(node_ids)} nodes", delete_all)[0]

    def delete_lines(self, line_ids: list[str]) -> bool:
        if self.folder is None or not line_ids:
            return False
        if any(self.folder.text(n, code) for n in line_ids for code in self.folder.languages()):
            what = line_ids[0] if len(line_ids) == 1 else f"{len(line_ids)} lines"
            if QMessageBox.question(self, APP_NAME, f"Delete {what} and its texts in every language?") \
                    != QMessageBox.StandardButton.Yes:
                return False

        def delete_all(f: CompanionFolder) -> None:
            for line_id in line_ids:
                edit.delete_line(f, line_id)
        return self.edit(f"Delete {line_ids[0]}" if len(line_ids) == 1 else f"Delete {len(line_ids)} lines", delete_all)[0]

    def add_line(self, slot: str | None = None, ask: bool = False) -> str | None:
        """A new line for ``slot`` (None: a slot-free, triggered line), shown on the Lines tab with its text editor open.
        With ``ask`` the slot is chosen in a dialog."""
        if self.folder is None:
            return None
        if ask:
            free = "(no slot: a triggered line)"
            choice, ok = QInputDialog.getItem(self, "Add line", "Slot:", [*self.catalog.all_slots, free], 0, False)
            if not ok:
                return None
            slot = None if choice == free else choice
        done, line = self.edit(f"Add line {slot or 'triggered'}", lambda f: edit.add_line(f, slot))
        if not done:
            return None
        self.open_tab(LINES_TAB)
        card = self.lines_scene.select(line["id"])
        if card is not None:
            self.lines_view.centerOn(card)
        self.lines_scene.edit_text(line["id"])
        return line["id"]

    def _line_menu(self, key: str, screen_pos) -> None:
        menu = QMenu(self)
        if key.startswith(PLACEHOLDER):
            slot = key[len(PLACEHOLDER):]
            menu.addAction(f"Add a line for {slot}", lambda: self.add_line(slot))
        else:
            menu.addAction("Edit text", lambda: self.lines_scene.edit_text(key))
            menu.addSeparator()
            menu.addAction("Delete", lambda: self.delete_lines([key]))
        menu.exec(screen_pos)

    def rename_item(self, conversation_id: str | None, old: str, new: str) -> bool:
        """Renames a line/node id (or, with key @header, the conversation). Offers to rename the audio file too."""
        if self.folder is None or old == new:
            return False
        if conversation_id is not None and old == graph.HEADER:
            return self.rename_conversation(conversation_id, new)
        done, result = self.edit(f"Rename {old} → {new}", lambda f: edit.rename_id(f, old, new))
        if not done:
            return False
        self._inspected = (conversation_id, new)
        self.reveal(conversation_id or LINES_TAB, new)
        if result.audio is not None:
            old_path, new_path = result.audio
            answer = QMessageBox.question(
                self, APP_NAME, f"Also rename the audio file {old_path.name} to {new_path.name} (and its lipsync .xml)?\n\n"
                                "Renaming files on disk cannot be undone; Undo only renames the id back.")
            if answer == QMessageBox.StandardButton.Yes:
                try:
                    edit.rename_audio(result.audio)
                except (EditError, OSError) as e:
                    QMessageBox.warning(self, APP_NAME, f"The audio file was not renamed:\n{e}")
        return True

    def _retarget_tab(self, old: str, new: str) -> None:
        index = self._tab_index(old)
        if index >= 0:
            widget = self.tabs.widget(index)
            widget.setProperty("tabKey", new)
            self.tabs.setTabText(index, new)
            if isinstance(widget, CanvasView) and isinstance(widget.scene(), ConversationScene):
                widget.scene().conversation_id = new
        if self._inspected and self._inspected[0] == old:
            self._inspected = (new, self._inspected[1])

    def rename_conversation(self, old: str, new: str | None = None) -> bool:
        if self.folder is None:
            return False
        if new is None:
            new, ok = QInputDialog.getText(self, "Rename conversation", "Conversation id (a-z, 0-9, _):", text=old)
            if not ok or not new.strip() or new.strip() == old:
                return False
            new = new.strip()
        return self.edit(f"Rename conversation {old} → {new}", lambda f: edit.rename_conversation(f, old, new),
                         on_redo=lambda: self._retarget_tab(old, new), on_undo=lambda: self._retarget_tab(new, old))[0]

    def delete_conversation(self, conversation_id: str, confirm: bool = True) -> bool:
        if self.folder is None:
            return False
        if confirm and QMessageBox.question(self, APP_NAME, f"Delete the conversation {conversation_id}, its nodes and "
                                                            f"their texts in every language?") != QMessageBox.StandardButton.Yes:
            return False
        return self.edit(f"Delete conversation {conversation_id}", lambda f: edit.delete_conversation(f, conversation_id),
                         on_undo=lambda: self.open_tab(conversation_id))[0]

    def new_conversation(self, conversation_id: str | None = None, start: str | None = None) -> str | None:
        """A new conversation (asked for when not given): ``start`` is hub, greeting or trigger. Opens its tab."""
        if self.folder is None:
            return None
        if conversation_id is None:
            conversation_id, ok = QInputDialog.getText(self, "New conversation", "Conversation id (a-z, 0-9, _):")
            if not ok or not conversation_id.strip():
                return None
            conversation_id = conversation_id.strip()
        if start is None:
            labels = {"From the hub menu (player options)": "hub", "As a greeting (companion speaks first)": "greeting",
                      "Started by a trigger": "trigger"}
            choice, ok = QInputDialog.getItem(self, "New conversation", "Starts:", list(labels), 0, False)
            if not ok:
                return None
            start = labels[choice]
        first_trigger = next(iter(sorted(self.catalog.triggers, key=lambda t: (not t.common, t.category, t.name))), None)

        def create(f: CompanionFolder) -> str:
            edit.add_conversation(f, conversation_id, None if start == "trigger" else start)
            if start == "trigger" and first_trigger is not None:
                edit.set_start_mode(f, conversation_id, "trigger", first_trigger.name)
            return conversation_id
        done, _ = self.edit(f"New conversation {conversation_id}", create)
        if not done:
            return None
        self.open_tab(conversation_id)
        self.reveal(conversation_id, graph.HEADER)
        return conversation_id

    def duplicate_conversation(self, conversation_id: str) -> str | None:
        done, new_id = self.edit(f"Duplicate {conversation_id}", lambda f: edit.duplicate_conversation(f, conversation_id))
        if done:
            self.open_tab(new_id)
        return new_id if done else None

    def move_conversation(self, conversation_id: str, delta: int) -> bool:
        return self.edit(f"Move {conversation_id} {'up' if delta < 0 else 'down'}",
                         lambda f: edit.move_conversation(f, conversation_id, delta))[0]

    def _conversation_menu(self, menu: QMenu, conversation_id: str) -> None:
        menu.addAction("Rename conversation…", lambda: self.rename_conversation(conversation_id))
        menu.addAction("Duplicate conversation", lambda: self.duplicate_conversation(conversation_id))
        menu.addSeparator()
        menu.addAction("Move up (hub menu order)", lambda: self.move_conversation(conversation_id, -1))
        menu.addAction("Move down (hub menu order)", lambda: self.move_conversation(conversation_id, 1))
        menu.addSeparator()
        menu.addAction("Delete conversation…", lambda: self.delete_conversation(conversation_id))

    def _library_menu(self, conversation_id: str, pos) -> None:
        menu = QMenu(self)
        menu.addAction("Open", lambda: self.open_tab(conversation_id))
        menu.addSeparator()
        self._conversation_menu(menu, conversation_id)
        menu.exec(pos)

    def _tab_menu(self, pos) -> None:
        index = self.tabs.tabBar().tabAt(pos)
        if index < 0:
            return
        key = self.tabs.widget(index).property("tabKey")
        if key == LINES_TAB:
            return
        menu = QMenu(self)
        self._conversation_menu(menu, key)
        menu.exec(self.tabs.tabBar().mapToGlobal(pos))

    def _card_menu(self, conversation_id: str, key: str, screen_pos) -> None:
        menu = QMenu(self)
        scene = self.scene_for(conversation_id)
        if key == graph.HEADER:
            self._conversation_menu(menu, conversation_id)
        elif not key.startswith("@") and scene is not None:
            menu.addAction("Edit text", lambda: scene.edit_text(key))
            menu.addAction("Continue with: Back to hub", lambda: self.connect_nodes(conversation_id, key, graph.HUB))
            menu.addAction("Continue with: End", lambda: self.connect_nodes(conversation_id, key, graph.END))
            menu.addSeparator()
            menu.addAction("Delete", lambda: self.delete_nodes(conversation_id, [key]))
        else:
            return
        menu.exec(screen_pos)

    def edit_failed(self, error: EditError) -> None:
        self._edit_error = error

    def refresh(self) -> None:
        """Rebuilds every view from the data after an edit, undo or redo."""
        folder = self.folder
        if folder is None:
            return
        for i in reversed(range(self.tabs.count())):
            widget = self.tabs.widget(i)
            if isinstance(widget, CanvasView) and isinstance(widget.scene(), ConversationScene):
                scene = widget.scene()
                if folder.conversation(scene.conversation_id) is None:
                    self.tabs.removeTab(i)
                else:
                    scene.rebuild()
        self.lines_scene.rebuild()
        self.library.set_folder(folder)
        self.blackboard.set_folder(folder)
        self._refresh_inspector()
        self.update_title()

    def _refresh_inspector(self) -> None:
        if self.folder is None or self._inspected is None:
            self.inspector.show_nothing()
            return
        conversation_id, key = self._inspected
        exists = (self.folder.line(key) is not None if conversation_id is None
                  else self.folder.conversation(conversation_id) is not None
                  and (key.startswith("@") or self.folder.node(conversation_id, key) is not None))
        if exists:
            self.inspector.show_item(self.folder, conversation_id, key)
        else:
            self._inspected = None
            self.inspector.show_nothing()

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

    # ---- settings and recent folders (T18) ----

    def _fill_recent(self) -> None:
        self.recent_menu.clear()
        recent = settings_ui.recent_list(self.settings)
        for path in recent:
            self.recent_menu.addAction(path, lambda p=path: self.open_path(Path(p)))
        self.recent_menu.setEnabled(bool(recent))

    def open_settings(self) -> bool:
        dialog = settings_ui.SettingsDialog(self.settings, self)
        if dialog.exec() != dialog.DialogCode.Accepted:
            return False
        self.apply_cli_setting()
        return True

    def apply_cli_setting(self) -> None:
        """Finds the CLI again (after the setting changed) for validation and the preview."""
        try:
            self.cli, self.cli_error = Cli.find(self.settings.value("cliPath") or None), None
        except CliError as e:
            self.cli, self.cli_error = None, str(e)
        self.status_cli.setText(f"CLI: {self.cli.path.name}" if self.cli else "CLI: not found")
        self.status_cli.setToolTip(str(self.cli.path) if self.cli else (self.cli_error or ""))
        self.preview.shutdown()
        self.preview.set_cli(str(self.cli.path) if self.cli else None, self.cli_error)
        self.preview.set_folder(self.folder)

    def _start_dir(self) -> str:
        return str(self.companions_root or self.settings.value("companionsDir") or self.settings.value("lastFolder")
                   or Path.home())

    def open_dialog(self) -> None:
        path = QFileDialog.getExistingDirectory(self, "Open a companion folder", self._start_dir())
        if path:
            self.open_path(Path(path))

    def open_root_dialog(self) -> None:
        path = QFileDialog.getExistingDirectory(self, "Open a Companions directory", self._start_dir())
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
        settings_ui.add_recent(self.settings, Path(path))
        self._fill_recent()
        return True

    def _set_folder(self, folder: CompanionFolder | None) -> None:
        self.folder = folder
        self._inspected = None
        self.undo_stack.clear()
        while self.tabs.count():
            self.tabs.removeTab(0)
        self.library.set_folder(folder)
        self.blackboard.set_folder(folder)
        self.preview.set_folder(folder)
        self.inspector.show_nothing()
        if self.companions_root is not None:
            folders = workspace.companion_folders(self.companions_root)
            self.library.set_companions(folders, folder.path if folder else None)
            self.inspector.companions = [p.name for p in folders]
        if folder is not None:
            self.lines_scene.set_folder(folder)
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
        self.preview.files_saved()
        self.validate()
        return True

    # ---- validation (T15) ----

    # ---- preview (T16/T17) ----

    def play(self) -> None:
        if self.folder is None:
            return
        self.bottom_tabs.setCurrentWidget(self.preview)
        self.preview.tabs.setCurrentIndex(0)
        self.preview.start("hub")

    def stop_preview(self) -> None:
        self.preview.stop()

    def _preview_highlight(self, conversation_id: str, node_id: str) -> None:
        """The canvas follows the simulation: the node of the last line said is highlighted (its tab opened)."""
        for scene in self._scenes():
            scene.set_highlight(None)
        if not conversation_id or self.folder is None or self.folder.conversation(conversation_id) is None:
            return
        view = self.open_tab(conversation_id)
        scene = view.scene() if isinstance(view, CanvasView) else None
        if isinstance(scene, ConversationScene):
            scene.set_highlight(node_id)
            card = scene.cards.get(node_id)
            if card is not None:
                view.ensureVisible(card)

    def validate(self) -> None:
        """``validate --json`` of the companion folder (the files on disk) on a worker thread; the result replaces the
        "validate" diagnostics."""
        if self.folder is None:
            return
        if self.cli is None:
            self._validation_unavailable(self.cli_error or "the CLI was not found")
            return
        path, cli = self.folder.path, self.cli
        self.statusBar().showMessage("Validating…")
        self.act_validate.setEnabled(False)

        def done(result) -> None:
            self.act_validate.setEnabled(True)
            self.validation_runs += 1
            if self.folder is None or self.folder.path != path:
                return
            self.diagnostics.set_diagnostics("validate", result.diagnostics)
            errors = sum(1 for d in result.diagnostics if d.get("severity") == "error")
            warnings = len(result.diagnostics) - errors
            note = " (of the saved files; save to include your changes)" if self.folder.dirty else ""
            self.statusBar().showMessage(f"Validation: {errors} error(s), {warnings} warning(s){note}", 8000)
            if result.diagnostics:
                self.bottom_tabs.setCurrentWidget(self.diagnostics)

        def failed(error: Exception) -> None:
            self.act_validate.setEnabled(True)
            self.validation_runs += 1
            self._validation_unavailable(str(error))
        workers.run(lambda: cli.validate(path), done, failed, owner=self)

    def _validation_unavailable(self, reason: str) -> None:
        self.diagnostics.set_diagnostics("validate", [{
            "severity": "warning", "code": "validation_unavailable", "folder": "", "file": None, "conversation": None,
            "node": None, "message": f"validation unavailable: {reason}"}])
        self.statusBar().showMessage(f"Validation unavailable: {reason}", 8000)

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

    def _clean_changed(self, _clean: bool) -> None:
        self.update_title()

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
            self.preview.shutdown()
            event.accept()
        else:
            event.ignore()
