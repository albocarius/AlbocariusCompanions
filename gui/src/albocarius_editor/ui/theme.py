"""Dark theme close to the reference screenshot (gui/docs/reference.png): colors, the application style sheet."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

from PySide6.QtGui import QColor, QFont, QPalette
from PySide6.QtWidgets import QApplication

WINDOW = "#16181d"
PANEL = "#1b1e24"
PANEL_ALT = "#20242b"
INPUT = "#121418"
BORDER = "#2c313a"
TEXT = "#e4e6ea"
MUTED = "#8d939d"
ACCENT = "#7d6cf0"
ACCENT_SOFT = "#3a3270"
LINK = "#6fd3a0"
ERROR = "#e5676b"
WARNING = "#e0b354"

CANVAS = "#181a1f"
GRID_MINOR = "#1f2228"
GRID_MAJOR = "#262a31"
EDGE = "#7b8494"
EDGE_MISSING = ERROR
PORT = "#9aa3b2"
HIGHLIGHT = "#f2c94c"  # the preview's current node

# Style sheet urls need forward slashes, also on Windows.
ICONS = (Path(__file__).parent / "icons").as_posix()


@dataclass(frozen=True)
class CardStyle:
    header: str
    body: str
    border: str
    badge: str


# Card colors by kind: companion lines are green like the reference's dialogue cards, logic-ish cards are blue.
CARD_STYLES = {
    "npc": CardStyle(header="#2c4c3b", body="#203a2e", border="#3f6b53", badge="#6fd3a0"),
    "player": CardStyle(header="#3a3560", body="#26243f", border="#5a52a0", badge="#b3a8ff"),
    "header": CardStyle(header="#25394f", body="#1c2c3e", border="#3a5572", badge="#7fb3e8"),
    "terminal": CardStyle(header="#25394f", body="#1c2c3e", border="#3a5572", badge=LINK),
    "line": CardStyle(header="#2c4c3b", body="#203a2e", border="#3f6b53", badge="#6fd3a0"),
}


def style_sheet() -> str:
    return f"""
QWidget {{ background: {WINDOW}; color: {TEXT}; }}
QMainWindow::separator {{ background: {BORDER}; width: 1px; height: 1px; }}
QMenuBar {{ background: {WINDOW}; padding: 4px 8px; }}
QMenuBar::item {{ padding: 4px 10px; border-radius: 4px; }}
QMenuBar::item:selected {{ background: {PANEL_ALT}; }}
QMenu {{ background: {PANEL}; border: 1px solid {BORDER}; padding: 4px; }}
QMenu::item {{ padding: 5px 24px 5px 16px; border-radius: 3px; }}
QMenu::item:selected {{ background: {ACCENT_SOFT}; }}
QMenu::separator {{ height: 1px; background: {BORDER}; margin: 4px 6px; }}
QToolBar {{ background: {WINDOW}; border: none; border-bottom: 1px solid {BORDER}; padding: 4px 8px; spacing: 4px; }}
QToolButton {{ background: transparent; border: 1px solid transparent; border-radius: 4px; padding: 4px 10px; }}
QToolButton:hover {{ background: {PANEL_ALT}; }}
QToolButton:checked, QToolButton[accent="true"] {{ background: {ACCENT_SOFT}; border-color: {ACCENT}; }}
QToolButton:disabled {{ color: {MUTED}; }}
QDockWidget {{ titlebar-close-icon: none; }}
QDockWidget::title {{ background: {PANEL}; padding: 6px 10px; color: {ACCENT}; font-weight: 600; }}
QTabWidget::pane {{ border: none; }}
QTabBar {{ background: {WINDOW}; }}
QTabBar::tab {{ background: {PANEL}; color: {MUTED}; padding: 5px 12px; margin: 4px 2px 4px 0; border: 1px solid {BORDER};
               border-radius: 4px; }}
QTabBar::tab:selected {{ background: {ACCENT_SOFT}; color: {TEXT}; border-color: {ACCENT}; }}
QTabBar::tab:hover:!selected {{ color: {TEXT}; }}
QTabBar::close-button {{ image: url({ICONS}/close.svg); subcontrol-position: right; margin-left: 4px; }}
QTabBar::close-button:hover {{ background: {BORDER}; border-radius: 3px; }}
QComboBox::drop-down {{ border: none; width: 18px; }}
QComboBox::down-arrow {{ image: url({ICONS}/chevron.svg); width: 10px; height: 10px; }}
QLineEdit, QPlainTextEdit, QTextEdit, QSpinBox, QDoubleSpinBox, QComboBox {{
    background: {INPUT}; border: 1px solid {BORDER}; border-radius: 4px; padding: 4px 6px; selection-background-color: {ACCENT}; }}
QLineEdit:focus, QPlainTextEdit:focus, QTextEdit:focus, QComboBox:focus {{ border-color: {ACCENT}; }}
QPushButton {{ background: {PANEL_ALT}; border: 1px solid {BORDER}; border-radius: 4px; padding: 5px 12px; }}
QPushButton:hover {{ border-color: {ACCENT}; }}
QPushButton[accent="true"] {{ background: {ACCENT_SOFT}; border-color: {ACCENT}; }}
QPushButton:disabled {{ color: {MUTED}; }}
QTreeWidget, QTreeView, QListWidget, QTableWidget, QTableView {{ background: {PANEL}; border: none; outline: none;
    alternate-background-color: {PANEL_ALT}; }}
QTreeView::item, QListWidget::item {{ padding: 3px 2px; }}
QTreeView::item:selected, QListWidget::item:selected, QTableView::item:selected {{ background: {ACCENT_SOFT}; color: {TEXT}; }}
QHeaderView::section {{ background: {PANEL_ALT}; color: {MUTED}; border: none; border-right: 1px solid {BORDER}; padding: 4px 6px; }}
QScrollBar:vertical {{ background: transparent; width: 10px; }}
QScrollBar:horizontal {{ background: transparent; height: 10px; }}
QScrollBar::handle {{ background: {BORDER}; border-radius: 4px; min-height: 24px; min-width: 24px; }}
QScrollBar::add-line, QScrollBar::sub-line {{ width: 0; height: 0; }}
QStatusBar {{ background: {WINDOW}; color: {MUTED}; border-top: 1px solid {BORDER}; }}
QStatusBar::item {{ border: none; }}
QStatusBar QLabel {{ color: {MUTED}; padding: 0 8px; }}
QLabel[role="section"] {{ color: {ACCENT}; font-weight: 600; background: transparent; }}
QLabel[role="muted"] {{ color: {MUTED}; background: transparent; }}
QFrame[role="card"] {{ background: {PANEL_ALT}; border: 1px solid {BORDER}; border-radius: 6px; }}
QToolTip {{ background: {PANEL}; color: {TEXT}; border: 1px solid {BORDER}; }}
"""


def apply(app: QApplication) -> None:
    app.setStyle("Fusion")
    palette = QPalette()
    for role, color in (
        (QPalette.ColorRole.Window, WINDOW), (QPalette.ColorRole.WindowText, TEXT), (QPalette.ColorRole.Base, INPUT),
        (QPalette.ColorRole.AlternateBase, PANEL_ALT), (QPalette.ColorRole.Text, TEXT), (QPalette.ColorRole.Button, PANEL_ALT),
        (QPalette.ColorRole.ButtonText, TEXT), (QPalette.ColorRole.Highlight, ACCENT), (QPalette.ColorRole.HighlightedText, TEXT),
        (QPalette.ColorRole.ToolTipBase, PANEL), (QPalette.ColorRole.ToolTipText, TEXT), (QPalette.ColorRole.PlaceholderText, MUTED),
    ):
        palette.setColor(role, QColor(color))
    app.setPalette(palette)
    font = QFont(app.font())
    font.setPointSizeF(max(font.pointSizeF(), 9.5))
    app.setFont(font)
    app.setStyleSheet(style_sheet())
