"""Smoke test of the window under QT_QPA_PLATFORM=offscreen: opens the sample, shows a conversation, moves a card."""

from pathlib import Path

import pytest
from PySide6.QtCore import QPointF, QSettings

from albocarius_editor.data import graph
from albocarius_editor.data.layout import NodeLayout
from albocarius_editor.ui.canvas.items import Card, EdgeItem
from albocarius_editor.ui.canvas.view import CanvasView
from albocarius_editor.ui.lines_view import LinesView
from albocarius_editor.ui.main_window import MainWindow


def test_opens_the_sample_with_lines_and_a_conversation(window: MainWindow, sample: Path):
    assert window.open_path(sample.parent)  # a Companions directory opens its first companion
    assert window.folder is not None and window.folder.id == "test_companion"
    titles = [window.tabs.tabText(i) for i in range(window.tabs.count())]
    assert titles == ["Lines", "the_scar"]
    assert isinstance(window.tabs.widget(0), LinesView)
    assert window.library.companions.count() == 1
    assert window.library.tree.topLevelItemCount() == 1 + 5  # Lines + conversations


def test_conversation_canvas_has_header_nodes_terminals_and_edges(window: MainWindow, sample: Path):
    window.open_path(sample)
    view = window.open_tab("spare_coin")
    assert isinstance(view, CanvasView)
    scene = view.scene()
    assert set(scene.cards) == {graph.HEADER, "p_coin_offer", "p_coin_ask", "coin_refuse", "coin_fine", "coin_thanks",
                                graph.HUB, graph.END}
    assert scene.cards["p_coin_ask"].kind == "player" and scene.cards["coin_fine"].kind == "npc"
    assert scene.cards["coin_fine"].content.text == "For you, Captain? Here, fifty denars."
    edges = [i for i in scene.items() if isinstance(i, EdgeItem)]
    assert len(edges) == 2 + 1 + 2 + 1 + 1 + 1  # start ×2, offer, ask ×2, refuse, fine, thanks
    # Cards placed by the auto layout do not overlap.
    cards = list(scene.cards.values())
    for a in cards:
        for b in cards:
            if a is not b:
                assert not a.sceneBoundingRect().adjusted(6, 6, -6, -6).intersects(b.sceneBoundingRect().adjusted(6, 6, -6, -6))


def test_selecting_a_card_fills_the_inspector(window: MainWindow, sample: Path):
    window.open_path(sample)
    scene = window.open_tab("the_scar").scene()
    scene.select("scar_story")
    text = " ".join(w.text() for w in window.inspector.findChildren(type(window.status_cli)))
    assert "Node scar_story" in text


def test_moving_a_card_is_undoable_and_saved_to_the_sidecar(window: MainWindow, sample: Path, qtbot):
    window.open_path(sample)
    scene = window.open_tab("the_scar").scene()
    card: Card = scene.cards["scar_story"]
    old = QPointF(card.pos())
    new = old + QPointF(100, 40)
    card.setPos(new)
    card.moved.emit("scar_story", old, QPointF(card.pos()))
    assert window.folder.dirty
    assert window.folder.layout.get("the_scar", "scar_story") == (card.pos().x(), card.pos().y())
    window.undo_stack.undo()
    assert card.pos() == old
    window.undo_stack.redo()
    assert window.save()
    assert (sample / NodeLayout.FILE_NAME).is_file()
    assert not window.folder.dirty


def test_layout_entries_of_missing_nodes_are_diagnostics(window: MainWindow, sample: Path):
    (sample / NodeLayout.FILE_NAME).write_text('{"conversations": {"the_scar": {"gone": [0, 0]}}}')
    window.open_path(sample)
    messages = [d["message"] for d in window.diagnostics.all()]
    assert messages == ["layout: node not found: the_scar/gone (dropped on next save)"]
    window.reveal_diagnostic(window.diagnostics.all()[0])
    assert window.tabs.tabText(window.tabs.currentIndex()) == "the_scar"


def test_reveal_and_search(window: MainWindow, sample: Path):
    window.open_path(sample)
    window.reveal("campfire", "p_cf_sing")
    scene = window.tabs.currentWidget().scene()
    assert scene.selected_key() == "p_cf_sing"
    window.library.search.setText("fifty denars")
    visible = [window.library.tree.topLevelItem(i).text(0) for i in range(window.library.tree.topLevelItemCount())
               if not window.library.tree.topLevelItem(i).isHidden()]
    assert visible == ["spare_coin"]


def test_missing_cli_still_opens_and_edits(qtbot, qapp, tmp_path: Path, sample: Path, monkeypatch: pytest.MonkeyPatch):
    monkeypatch.setenv("ALBOCARIUS_CLI", str(tmp_path / "missing.dll"))
    w = MainWindow(QSettings(str(tmp_path / "settings.ini"), QSettings.Format.IniFormat))
    qtbot.addWidget(w)
    assert w.cli is None and w.catalog.source == "bundled"
    assert "not found" in w.status_cli.text()
    assert "CLI was not found" in w.preview.status.text()
    assert w.open_path(sample)
    assert "the_scar" in w.open_tab("the_scar").scene().cards[graph.HEADER].content.title
