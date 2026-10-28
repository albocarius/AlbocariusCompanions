"""Copy / paste / duplicate of cards (PLAN T14)."""

import json
from pathlib import Path

import pytest

from PySide6.QtCore import QMimeData, QPointF
from PySide6.QtGui import QGuiApplication

from albocarius_editor.ui.main_window import NODES_MIME, MainWindow


@pytest.fixture(autouse=True)
def clean_clipboard(qapp):
    yield
    QGuiApplication.clipboard().clear()  # data left on the clipboard crashes the interpreter's shutdown (offscreen)


def test_copy_two_nodes_and_paste_into_another_conversation(window: MainWindow, sample: Path):
    assert window.open_path(sample)
    scene = window.open_tab("the_scar").scene()
    scene.select("p_scar_sorry")
    scene.cards["scar_thanks"].setSelected(True)
    assert window.copy_selection()
    data = QGuiApplication.clipboard().mimeData()
    assert data.hasFormat(NODES_MIME) and json.loads(data.text())["companion"] == "test_companion"
    target = window.open_tab("spare_coin").scene()
    new_ids = window.paste(at=QPointF(800, 600))
    assert new_ids == ["p_scar_sorry_01", "scar_thanks_01"]
    f = window.folder
    assert f.node("spare_coin", "p_scar_sorry_01")["next"] == ["scar_thanks_01"]  # the internal edge follows
    assert f.node("spare_coin", "scar_thanks_01")["next"] == ["hub"]
    for code in f.languages():
        assert f.text("p_scar_sorry_01", code) == f.text("p_scar_sorry", code)
    assert f.layout.get("spare_coin", "p_scar_sorry_01")[0] == 800
    assert set(target.selected_keys()) == set(new_ids)
    window.undo_stack.undo()
    assert f.node("spare_coin", "p_scar_sorry_01") is None


def test_duplicate_and_foreign_payloads(window: MainWindow, sample: Path):
    assert window.open_path(sample)
    scene = window.open_tab("the_scar").scene()
    scene.select("scar_story")
    assert window.duplicate_selection() == ["scar_story_01"]
    x, y = window.folder.layout.get("the_scar", "scar_story")
    assert window.folder.layout.get("the_scar", "scar_story_01") == (x + 40, y + 40)
    data = QMimeData()
    data.setText(json.dumps({"kind": "albocarius-nodes", "companion": "someone_else", "nodes": [], "texts": {}}))
    QGuiApplication.clipboard().setMimeData(data)
    assert window.paste() == []
    assert "same companion" in window.statusBar().currentMessage()
