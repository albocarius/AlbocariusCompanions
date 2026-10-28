"""Running CLI calls off the GUI thread (validation, the simulator).

``run(fn, on_done, on_error)`` runs ``fn()`` on the global ``QThreadPool`` and calls back on the GUI thread.
:class:`SerialRunner` runs jobs one at a time in order (the ``serve`` protocol answers requests in order).
"""

from __future__ import annotations

from collections import deque
from collections.abc import Callable
from typing import Any

from PySide6.QtCore import QObject, QRunnable, QThreadPool, Signal


class _Relay(QObject):
    done = Signal(object)
    failed = Signal(object)


class _Job(QRunnable):
    def __init__(self, fn: Callable[[], Any], relay: _Relay):
        super().__init__()
        self.fn, self.relay = fn, relay

    def run(self) -> None:
        try:
            result = self.fn()
        except Exception as e:  # noqa: BLE001 - reported to the GUI thread
            signal, value = "failed", e
        else:
            signal, value = "done", result
        try:
            getattr(self.relay, signal).emit(value)
        except RuntimeError:  # the owner (and its relay) is gone: nobody waits for the answer
            pass


_live: set[_Relay] = set()  # relays of running jobs (kept alive until they report)


def run(fn: Callable[[], Any], on_done: Callable[[Any], None], on_error: Callable[[Exception], None],
        owner: QObject | None = None) -> None:
    """``owner``: the callbacks are dropped when it is deleted (a closed window does not hear back)."""
    relay = _Relay(owner)
    _live.add(relay)
    if owner is not None:
        relay.destroyed.connect(lambda *_: _live.discard(relay))

    def finish(callback: Callable[[Any], None], value: Any) -> None:
        _live.discard(relay)
        callback(value)
    relay.done.connect(lambda r: finish(on_done, r))
    relay.failed.connect(lambda e: finish(on_error, e))
    QThreadPool.globalInstance().start(_Job(fn, relay))


class SerialRunner(QObject):
    """Jobs one after the other: the next starts when the previous reported. ``busy`` while any is queued."""

    busy_changed = Signal(bool)

    def __init__(self, parent=None):
        super().__init__(parent)
        self._queue: deque[tuple[Callable[[], Any], Callable[[Any], None], Callable[[Exception], None]]] = deque()
        self._running = False

    @property
    def busy(self) -> bool:
        return self._running or bool(self._queue)

    def submit(self, fn: Callable[[], Any], on_done: Callable[[Any], None], on_error: Callable[[Exception], None]) -> None:
        self._queue.append((fn, on_done, on_error))
        if not self._running:
            self.busy_changed.emit(True)
            self._next()

    def _next(self) -> None:
        if not self._queue:
            self._running = False
            self.busy_changed.emit(False)
            return
        self._running = True
        fn, on_done, on_error = self._queue.popleft()

        def done(result: Any) -> None:
            try:
                on_done(result)
            finally:
                self._next()

        def failed(error: Exception) -> None:
            try:
                on_error(error)
            finally:
                self._next()
        run(fn, done, failed, owner=self)
