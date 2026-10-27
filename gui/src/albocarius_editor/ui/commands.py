"""Undo commands. Every edit of the companion is one of these on the window's ``QUndoStack``.

* :class:`EditCommand` wraps one ``data.edit`` call (or several, as one step): it keeps ``folder.snapshot()`` before and
  after, so undo/redo restore the whole editable state and rebuild the views (``window.refresh()``).
* :class:`MoveCommand` is a card drag: position only, no snapshot.
"""

from __future__ import annotations

from collections.abc import Callable
from typing import TYPE_CHECKING, Any

from PySide6.QtCore import QPointF
from PySide6.QtGui import QUndoCommand

from ..data.companion import CompanionFolder
from ..data.edit import EditError

if TYPE_CHECKING:
    from .main_window import MainWindow


class EditCommand(QUndoCommand):
    """``fn(folder)`` run once (on push), then replayed as snapshots. ``fn``'s return value is kept in ``result``.

    An :class:`EditError` from ``fn`` leaves the folder as it was, marks the command obsolete (the stack drops and deletes
    it, so the error goes to ``window.edit_failed``). ``on_redo`` / ``on_undo`` run after the data changed and before the
    views are rebuilt (e.g. a renamed conversation's tab follows its new id)."""

    def __init__(self, window: MainWindow, text: str, fn: Callable[[CompanionFolder], Any],
                 on_redo: Callable[[], None] | None = None, on_undo: Callable[[], None] | None = None):
        super().__init__(text)
        self.window = window
        self.fn = fn
        self.on_redo, self.on_undo = on_redo, on_undo
        self.before: dict | None = None
        self.after: dict | None = None
        self.result: Any = None

    def redo(self) -> None:
        folder = self.window.folder
        if folder is None:
            self.setObsolete(True)
            return
        if self.after is None:
            self.before = folder.snapshot()
            try:
                self.result = self.fn(folder)
            except EditError as e:
                if folder.snapshot() != self.before:  # edit functions change nothing on error; a composite fn may have
                    folder.restore(self.before)
                self.window.edit_failed(e)
                self.setObsolete(True)
                return
            self.after = folder.snapshot()
        else:
            folder.restore(self.after)
        if self.on_redo is not None:
            self.on_redo()
        self.window.refresh()

    def undo(self) -> None:
        folder = self.window.folder
        if folder is None or self.before is None:
            return
        folder.restore(self.before)
        if self.on_undo is not None:
            self.on_undo()
        self.window.refresh()


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
