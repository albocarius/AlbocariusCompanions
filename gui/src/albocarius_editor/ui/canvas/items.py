"""Canvas items: cards (conversation header, line nodes, hub/end) and ordered edges between them."""

from __future__ import annotations

from dataclasses import dataclass, field

from PySide6.QtCore import QPointF, QRectF, Qt, Signal
from PySide6.QtGui import QBrush, QColor, QFont, QFontMetricsF, QPainter, QPainterPath, QPen, QPolygonF
from PySide6.QtWidgets import QGraphicsItem, QGraphicsObject, QGraphicsPathItem, QGraphicsSimpleTextItem, QStyleOptionGraphicsItem

from .. import theme

CARD_WIDTH = 280.0
TERMINAL_WIDTH = 170.0
HEADER_HEIGHT = 34.0
PAD = 10.0
RADIUS = 6.0
PORT_RADIUS = 5.0
GRID = 20.0
MAX_TEXT_LINES = 5


@dataclass
class Section:
    """A titled list on a card ("when", "effects", …)."""

    title: str
    items: list[str] = field(default_factory=list)


@dataclass
class CardContent:
    title: str
    badge: str = ""            # short tag in the header: "npc", "player", "hub", …
    subtitle: str = ""         # the id line
    text: str | None = None    # the English text; None = no text block
    sections: list[Section] = field(default_factory=list)
    has_input: bool = True
    has_output: bool = True


def _fonts() -> tuple[QFont, QFont, QFont]:
    base = QFont()
    title = QFont(base)
    title.setPointSizeF(base.pointSizeF() * 1.15)
    title.setWeight(QFont.Weight.DemiBold)
    small = QFont(base)
    small.setPointSizeF(base.pointSizeF() * 0.85)
    return title, base, small


def _wrap(text: str, font: QFont, width: float, max_lines: int) -> list[str]:
    fm = QFontMetricsF(font)
    lines: list[str] = []
    for paragraph in text.split("\n"):
        current = ""
        for word in paragraph.split(" "):
            candidate = f"{current} {word}" if current else word
            if fm.horizontalAdvance(candidate) <= width or not current:
                current = candidate
            else:
                lines.append(current)
                current = word
        lines.append(current)
    if len(lines) > max_lines:
        lines = lines[:max_lines]
        lines[-1] = fm.elidedText(lines[-1] + " …", Qt.TextElideMode.ElideRight, width)
    return lines


class Card(QGraphicsObject):
    """A movable, selectable card. ``key`` is the graph key (node id, @header, @hub, @end)."""

    moved = Signal(str, QPointF, QPointF)  # key, old position, new position (after a drag ends)

    def __init__(self, key: str, kind: str, content: CardContent):
        super().__init__()
        self.key = key
        self.kind = kind
        self.content = content
        self.style = theme.CARD_STYLES[kind]
        self.highlighted = False
        self.snap = True
        self.edges: list[EdgeItem] = []
        self._press_pos: QPointF | None = None
        self.setFlags(QGraphicsItem.GraphicsItemFlag.ItemIsMovable | QGraphicsItem.GraphicsItemFlag.ItemIsSelectable
                      | QGraphicsItem.GraphicsItemFlag.ItemSendsGeometryChanges)
        self.setCacheMode(QGraphicsItem.CacheMode.DeviceCoordinateCache)
        self.setToolTip(content.text or content.subtitle)
        self._layout()

    # ---- geometry ----

    def _layout(self) -> None:
        title_font, body_font, small_font = _fonts()
        self.width = TERMINAL_WIDTH if self.kind == "terminal" else CARD_WIDTH
        inner = self.width - 2 * PAD
        y = HEADER_HEIGHT + PAD
        self._rows: list[tuple[str, QRectF, QFont, str]] = []  # (role, rect, font, text)
        fm_small, fm_body = QFontMetricsF(small_font), QFontMetricsF(body_font)
        if self.content.subtitle:
            self._rows.append(("muted", QRectF(PAD, y, inner, fm_small.height()), small_font,
                               fm_small.elidedText(self.content.subtitle, Qt.TextElideMode.ElideRight, inner)))
            y += fm_small.height() + 6
        if self.content.text is not None:
            lines = _wrap(self.content.text or "(no English text)", body_font, inner - 12, MAX_TEXT_LINES)
            h = len(lines) * fm_body.lineSpacing() + 12
            self._rows.append(("text" if self.content.text else "placeholder", QRectF(PAD, y, inner, h), body_font,
                               "\n".join(lines)))
            y += h + 6
        for section in self.content.sections:
            self._rows.append(("section", QRectF(PAD, y, inner, fm_small.height()), small_font, section.title.upper()))
            y += fm_small.height() + 2
            for item in section.items:
                lines = _wrap(item, small_font, inner - 14, 2)
                h = len(lines) * fm_small.lineSpacing()
                self._rows.append(("item", QRectF(PAD + 10, y, inner - 10, h), small_font, "\n".join(lines)))
                y += h + 2
            y += 4
        self.height = max(y + PAD - 6, HEADER_HEIGHT + 2 * PAD) if self._rows else HEADER_HEIGHT + 22
        self._title_font = title_font
        self._small_font = small_font

    def boundingRect(self) -> QRectF:
        m = PORT_RADIUS + 3
        return QRectF(-m, -m, self.width + 2 * m, self.height + 2 * m)

    def shape(self) -> QPainterPath:
        path = QPainterPath()
        path.addRoundedRect(QRectF(0, 0, self.width, self.height), RADIUS, RADIUS)
        return path

    def input_port(self) -> QPointF:
        return self.mapToScene(QPointF(0, HEADER_HEIGHT / 2))

    def output_port(self) -> QPointF:
        return self.mapToScene(QPointF(self.width, HEADER_HEIGHT / 2))

    # ---- painting ----

    def paint(self, painter: QPainter, option: QStyleOptionGraphicsItem, widget=None) -> None:
        painter.setRenderHint(QPainter.RenderHint.Antialiasing)
        rect = QRectF(0, 0, self.width, self.height)
        s = self.style
        painter.setPen(Qt.PenStyle.NoPen)
        painter.setBrush(QColor(s.body))
        painter.drawRoundedRect(rect, RADIUS, RADIUS)

        header = QPainterPath()
        header.addRoundedRect(QRectF(0, 0, self.width, HEADER_HEIGHT), RADIUS, RADIUS)
        header.addRect(QRectF(0, HEADER_HEIGHT / 2, self.width, HEADER_HEIGHT / 2))
        painter.fillPath(header.simplified(), QColor(s.header))

        # Header: a badge dot, the title, the badge text on the right.
        painter.setBrush(QColor(s.badge))
        painter.drawEllipse(QPointF(PAD + 6, HEADER_HEIGHT / 2), 5, 5)
        painter.setPen(QColor(theme.TEXT))
        painter.setFont(self._title_font)
        badge_w = QFontMetricsF(self._small_font).horizontalAdvance(self.content.badge) + 8 if self.content.badge else 0
        title_rect = QRectF(PAD + 18, 0, self.width - PAD * 2 - 18 - badge_w, HEADER_HEIGHT)
        title = QFontMetricsF(self._title_font).elidedText(self.content.title, Qt.TextElideMode.ElideRight, title_rect.width())
        painter.drawText(title_rect, Qt.AlignmentFlag.AlignVCenter | Qt.AlignmentFlag.AlignLeft, title)
        if self.content.badge:
            painter.setFont(self._small_font)
            painter.setPen(QColor(s.badge))
            painter.drawText(QRectF(0, 0, self.width - PAD, HEADER_HEIGHT),
                             Qt.AlignmentFlag.AlignVCenter | Qt.AlignmentFlag.AlignRight, self.content.badge)

        for role, r, font, text in self._rows:
            painter.setFont(font)
            if role in ("text", "placeholder"):
                painter.setPen(Qt.PenStyle.NoPen)
                painter.setBrush(QColor(theme.INPUT))
                painter.drawRoundedRect(r, 4, 4)
                painter.setPen(QColor(theme.TEXT if role == "text" else theme.MUTED))
                painter.drawText(r.adjusted(6, 6, -6, -6), Qt.AlignmentFlag.AlignLeft | Qt.AlignmentFlag.AlignTop, text)
            elif role == "section":
                painter.setPen(QColor(s.badge))
                painter.drawText(r, Qt.AlignmentFlag.AlignLeft | Qt.AlignmentFlag.AlignVCenter, text)
            elif role == "item":
                painter.setPen(QColor(theme.MUTED))
                painter.drawText(QRectF(r.left() - 10, r.top(), 10, QFontMetricsF(font).height()),
                                 Qt.AlignmentFlag.AlignLeft, "·")
                painter.setPen(QColor(theme.TEXT))
                painter.drawText(r, Qt.AlignmentFlag.AlignLeft | Qt.AlignmentFlag.AlignTop, text)
            else:
                painter.setPen(QColor(theme.MUTED))
                painter.drawText(r, Qt.AlignmentFlag.AlignLeft | Qt.AlignmentFlag.AlignVCenter, text)

        # Border: highlight (preview) > selection > normal.
        painter.setBrush(Qt.BrushStyle.NoBrush)
        if self.highlighted:
            painter.setPen(QPen(QColor(theme.HIGHLIGHT), 2.5))
        elif self.isSelected():
            painter.setPen(QPen(QColor(theme.ACCENT), 2))
        else:
            painter.setPen(QPen(QColor(s.border), 1))
        painter.drawRoundedRect(rect, RADIUS, RADIUS)

        painter.setPen(QPen(QColor(theme.CANVAS), 2))
        painter.setBrush(QColor(theme.PORT))
        if self.content.has_input:
            painter.drawEllipse(QPointF(0, HEADER_HEIGHT / 2), PORT_RADIUS, PORT_RADIUS)
        if self.content.has_output:
            painter.drawEllipse(QPointF(self.width, HEADER_HEIGHT / 2), PORT_RADIUS, PORT_RADIUS)

    def set_highlighted(self, on: bool) -> None:
        if self.highlighted != on:
            self.highlighted = on
            self.update()

    # ---- moving ----

    def itemChange(self, change: QGraphicsItem.GraphicsItemChange, value):
        if change == QGraphicsItem.GraphicsItemChange.ItemPositionChange and self.snap:
            return QPointF(round(value.x() / GRID) * GRID, round(value.y() / GRID) * GRID)
        if change == QGraphicsItem.GraphicsItemChange.ItemPositionHasChanged:
            for edge in self.edges:
                edge.update_path()
        return super().itemChange(change, value)

    def mousePressEvent(self, event) -> None:
        self._press_pos = QPointF(self.pos())
        super().mousePressEvent(event)

    def mouseReleaseEvent(self, event) -> None:
        super().mouseReleaseEvent(event)
        if self._press_pos is not None and self._press_pos != self.pos():
            self.moved.emit(self.key, self._press_pos, QPointF(self.pos()))
        self._press_pos = None


class EdgeItem(QGraphicsPathItem):
    """An entry of a ``start`` / ``next`` list. ``index`` is its position (shown when the list has several entries).
    A target that names no node (``missing``) is drawn as a dashed stub ending in the missing id."""

    def __init__(self, source: Card, target: Card | None, index: int, show_index: bool, missing_name: str = ""):
        super().__init__()
        self.source = source
        self.target = target
        self.index = index
        self.missing_name = missing_name
        self.setZValue(-1)
        color = QColor(theme.EDGE_MISSING if target is None else theme.EDGE)
        pen = QPen(color, 1.6)
        if target is None:
            pen.setStyle(Qt.PenStyle.DashLine)
        self.setPen(pen)
        self._arrow = QPolygonF()
        self._label = QGraphicsSimpleTextItem(str(index + 1) if show_index else "", self)
        self._label.setBrush(QBrush(QColor(theme.MUTED)))
        self._missing = QGraphicsSimpleTextItem(f"? {missing_name}" if target is None else "", self)
        self._missing.setBrush(QBrush(color))
        source.edges.append(self)
        if target is not None:
            target.edges.append(self)
        self.update_path()

    def update_path(self) -> None:
        start = self.source.output_port()
        end = self.target.input_port() if self.target else start + QPointF(90, 24 + 18 * self.index)
        dx = max(abs(end.x() - start.x()) * 0.5, 40)
        path = QPainterPath(start)
        path.cubicTo(start + QPointF(dx, 0), end - QPointF(dx, 0), end)
        self.setPath(path)
        # Arrow head at the target port.
        tip = end - QPointF(PORT_RADIUS + 1, 0)
        self._arrow = QPolygonF([tip, tip + QPointF(-8, -4.5), tip + QPointF(-8, 4.5)])
        self._label.setPos(path.pointAtPercent(0.2) + QPointF(-3, -17))
        self._missing.setPos(end + QPointF(4, -8))

    def boundingRect(self) -> QRectF:
        return super().boundingRect().united(self._arrow.boundingRect()).adjusted(-2, -2, 2, 2)

    def paint(self, painter: QPainter, option: QStyleOptionGraphicsItem, widget=None) -> None:
        painter.setRenderHint(QPainter.RenderHint.Antialiasing)
        super().paint(painter, option, widget)
        if self.target is not None:
            painter.setPen(Qt.PenStyle.NoPen)
            painter.setBrush(self.pen().color())
            painter.drawPolygon(self._arrow)
