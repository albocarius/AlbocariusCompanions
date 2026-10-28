"""The Lines tab as a canvas (PLAN T12)."""

from pathlib import Path

from PySide6.QtWidgets import QMessageBox

from albocarius_editor.ui.canvas.lines_scene import PLACEHOLDER
from albocarius_editor.ui.main_window import MainWindow


def test_lines_are_cards_grouped_by_slot(window: MainWindow, sample: Path):
    assert window.open_path(sample)
    scene = window.lines_scene
    assert "intro_01" in scene.cards and "idle_daily_01" in scene.cards
    assert scene.cards["p_ask_about_01"].kind == "player"
    assert scene.cards["intro_01"].content.text == window.folder.text("intro_01")
    # Cards do not overlap and are not movable.
    cards = list(scene.cards.values())
    for a in cards:
        assert not a.flags() & a.GraphicsItemFlag.ItemIsMovable
        for b in cards:
            if a is not b:
                assert not a.sceneBoundingRect().adjusted(4, 4, -4, -4).intersects(b.sceneBoundingRect().adjusted(4, 4, -4, -4))


def test_add_and_delete_a_greet_line(window: MainWindow, sample: Path, monkeypatch):
    assert window.open_path(sample)
    line_id = window.add_line("greet")
    assert line_id == "greet_02"
    assert window.folder.line(line_id)["slot"] == "greet"
    assert line_id in window.lines_scene.cards and window.lines_scene.editor is not None
    window.lines_scene.select(line_id)
    window.tabs.setCurrentWidget(window.lines_view)
    monkeypatch.setattr(QMessageBox, "question", lambda *a, **k: QMessageBox.StandardButton.Yes)
    window.delete_selection()
    assert window.folder.line(line_id) is None and window.folder.text(line_id) is None
    assert line_id not in window.lines_scene.cards


def test_required_slots_without_a_line_show_a_placeholder(window: MainWindow, sample: Path):
    assert window.open_path(sample)
    window.edit("Delete greet", lambda f: __import__("albocarius_editor.data.edit", fromlist=["x"]).delete_line(f, "greet_01"))
    key = PLACEHOLDER + "greet"
    assert key in window.lines_scene.cards
    window.lines_scene.add_requested.emit("greet")  # a double-click on the placeholder
    assert key not in window.lines_scene.cards
    assert any(l.get("slot") == "greet" for l in window.folder.lines)


def test_line_inspector_and_inline_text(window: MainWindow, sample: Path, qtbot):
    assert window.open_path(sample)
    window.reveal("@lines", "farewell_01")
    assert window.inspector.findChild(object, "id_field") is not None
    editor = window.lines_scene.edit_text("farewell_01")
    editor.setPlainText("Until next time.")
    editor.finish(commit=True)
    qtbot.waitUntil(lambda: window.folder.text("farewell_01") == "Until next time.")
