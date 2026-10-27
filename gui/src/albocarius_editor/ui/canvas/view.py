"""The canvas view: grid background, zoom (wheel, buttons), pan (middle button or space + drag), fit, zoom overlay."""

from __future__ import annotations

import math

from PySide6.QtCore import QPointF, QRectF, Qt, Signal
from PySide6.QtGui import QColor, QPainter, QPen
from PySide6.QtWidgets import QFrame, QGraphicsView, QHBoxLayout, QLabel, QToolButton

from .. import theme
from .items import GRID

MIN_ZOOM = 0.2
MAX_ZOOM = 3.0


class ZoomOverlay(QFrame):
    """The bottom-right zoom box of the reference: − 100% + fit."""

    def __init__(self, view: CanvasView):
        super().__init__(view)
        self.setObjectName("zoomOverlay")
        self.setStyleSheet(f"#zoomOverlay {{ background: {theme.PANEL}; border: 1px solid {theme.BORDER}; border-radius: 6px; }}"
                           f"QToolButton {{ padding: 2px 8px; }} QLabel {{ background: transparent; padding: 0 6px; }}")
        layout = QHBoxLayout(self)
        layout.setContentsMargins(4, 2, 4, 2)
        layout.setSpacing(2)
        for text, tip, slot in (("−", "Zoom out", view.zoom_out), ("+", "Zoom in", view.zoom_in)):
            b = QToolButton(self)
            b.setText(text)
            b.setToolTip(tip)
            b.clicked.connect(slot)
            layout.addWidget(b)
            if text == "−":
                self.label = QLabel("100%", self)
                self.label.setMinimumWidth(48)
                self.label.setAlignment(Qt.AlignmentFlag.AlignCenter)
                layout.addWidget(self.label)
        fit = QToolButton(self)
        fit.setText("Fit")
        fit.setToolTip("Fit everything (F)")
        fit.clicked.connect(view.fit_all)
        layout.addWidget(fit)
        view.zoom_changed.connect(lambda z: self.label.setText(f"{round(z * 100)}%"))


class CanvasView(QGraphicsView):
    zoom_changed = Signal(float)
    palette_dropped = Signal(str, QPointF)  # palette kind, scene position

    PALETTE_MIME = "application/x-albocarius-palette"  # library.PALETTE_MIME (the canvas does not import the library)

    def __init__(self, parent=None):
        super().__init__(parent)
        self.setRenderHints(QPainter.RenderHint.Antialiasing | QPainter.RenderHint.TextAntialiasing)
        self.setViewportUpdateMode(QGraphicsView.ViewportUpdateMode.BoundingRectViewportUpdate)
        self.setTransformationAnchor(QGraphicsView.ViewportAnchor.AnchorUnderMouse)
        self.setDragMode(QGraphicsView.DragMode.RubberBandDrag)
        self.setFrameShape(QFrame.Shape.NoFrame)
        self.setBackgroundBrush(QColor(theme.CANVAS))
        self._panning = False
        self._space = False
        self._last = QPointF()
        self.overlay = ZoomOverlay(self)
        self.setAcceptDrops(True)

    @property
    def zoom(self) -> float:
        return self.transform().m11()

    def set_zoom(self, z: float) -> None:
        z = max(MIN_ZOOM, min(MAX_ZOOM, z))
        factor = z / self.zoom
        self.scale(factor, factor)
        self.zoom_changed.emit(self.zoom)

    def zoom_in(self) -> None:
        self.set_zoom(self.zoom * 1.2)

    def zoom_out(self) -> None:
        self.set_zoom(self.zoom / 1.2)

    def fit_all(self) -> None:
        if self.scene() is None:
            return
        rect = self.scene().itemsBoundingRect().adjusted(-40, -40, 40, 40)
        if rect.isEmpty():
            return
        self.fitInView(rect, Qt.AspectRatioMode.KeepAspectRatio)
        if self.zoom > 1.0:
            self.set_zoom(1.0)
        self.zoom_changed.emit(self.zoom)

    def resizeEvent(self, event) -> None:
        super().resizeEvent(event)
        self.overlay.adjustSize()
        self.overlay.move(self.viewport().width() - self.overlay.width() - 12,
                          self.viewport().height() - self.overlay.height() - 12)

    def wheelEvent(self, event) -> None:
        steps = event.angleDelta().y() / 120
        if steps:
            self.set_zoom(self.zoom * math.pow(1.15, steps))

    # ---- panning: middle button, or space held + left button ----

    def keyPressEvent(self, event) -> None:
        if event.key() == Qt.Key.Key_Space and not event.isAutoRepeat():
            self._space = True
            self.viewport().setCursor(Qt.CursorShape.OpenHandCursor)
            return
        if event.key() == Qt.Key.Key_F:
            self.fit_all()
            return
        super().keyPressEvent(event)

    def keyReleaseEvent(self, event) -> None:
        if event.key() == Qt.Key.Key_Space and not event.isAutoRepeat():
            self._space = False
            self.viewport().unsetCursor()
            return
        super().keyReleaseEvent(event)

    def mousePressEvent(self, event) -> None:
        if event.button() == Qt.MouseButton.MiddleButton or (self._space and event.button() == Qt.MouseButton.LeftButton):
            self._panning = True
            self._last = event.position()
            self.viewport().setCursor(Qt.CursorShape.ClosedHandCursor)
            return
        super().mousePressEvent(event)

    def mouseMoveEvent(self, event) -> None:
        if self._panning:
            delta = event.position() - self._last
            self._last = event.position()
            self.horizontalScrollBar().setValue(int(self.horizontalScrollBar().value() - delta.x()))
            self.verticalScrollBar().setValue(int(self.verticalScrollBar().value() - delta.y()))
            return
        super().mouseMoveEvent(event)

    def mouseReleaseEvent(self, event) -> None:
        if self._panning:
            self._panning = False
            if self._space:
                self.viewport().setCursor(Qt.CursorShape.OpenHandCursor)
            else:
                self.viewport().unsetCursor()
            return
        super().mouseReleaseEvent(event)

    def center_scene_pos(self) -> QPointF:
        return self.mapToScene(self.viewport().rect().center())

    # ---- drops from the library palette (the scene's own drag handling would refuse them) ----

    def dragEnterEvent(self, event) -> None:
        if event.mimeData().hasFormat(self.PALETTE_MIME):
            event.acceptProposedAction()
        else:
            super().dragEnterEvent(event)

    def dragMoveEvent(self, event) -> None:
        if event.mimeData().hasFormat(self.PALETTE_MIME):
            event.acceptProposedAction()
        else:
            super().dragMoveEvent(event)

    def dropEvent(self, event) -> None:
        if event.mimeData().hasFormat(self.PALETTE_MIME):
            kind = bytes(event.mimeData().data(self.PALETTE_MIME)).decode()
            self.palette_dropped.emit(kind, self.mapToScene(event.position().toPoint()))
            event.acceptProposedAction()
        else:
            super().dropEvent(event)

    def drawBackground(self, painter: QPainter, rect: QRectF) -> None:
        super().drawBackground(painter, rect)
        if self.zoom < 0.35:
            return
        left = math.floor(rect.left() / GRID) * GRID
        top = math.floor(rect.top() / GRID) * GRID
        minor, major = QPen(QColor(theme.GRID_MINOR), 0), QPen(QColor(theme.GRID_MAJOR), 0)
        x = left
        while x < rect.right():
            painter.setPen(major if round(x / GRID) % 5 == 0 else minor)
            painter.drawLine(QPointF(x, rect.top()), QPointF(x, rect.bottom()))
            x += GRID
        y = top
        while y < rect.bottom():
            painter.setPen(major if round(y / GRID) % 5 == 0 else minor)
            painter.drawLine(QPointF(rect.left(), y), QPointF(rect.right(), y))
            y += GRID
