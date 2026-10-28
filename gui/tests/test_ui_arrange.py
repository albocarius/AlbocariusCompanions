"""Arrange, search, toolbar icons (PLAN T19)."""

import random
from pathlib import Path

from PySide6.QtCore import QPointF

from albocarius_editor.ui.main_window import MainWindow


def overlaps(scene) -> bool:
    cards = list(scene.cards.values())
    return any(a is not b and a.sceneBoundingRect().adjusted(6, 6, -6, -6).intersects(b.sceneBoundingRect().adjusted(6, 6, -6, -6))
               for a in cards for b in cards)


def test_arrange_after_scrambling(window: MainWindow, sample: Path):
    assert window.open_path(sample)
    scene = window.open_tab("spare_coin").scene()
    rng = random.Random(3)
    for key in scene.cards:
        scene.place(key, QPointF(rng.randint(0, 200), rng.randint(0, 200)))
    scene.rebuild()
    assert overlaps(scene)
    scrambled = {k: window.folder.layout.get("spare_coin", k) for k in scene.cards}
    assert window.arrange()
    assert not overlaps(scene)
    window.undo_stack.undo()
    assert {k: window.folder.layout.get("spare_coin", k) for k in scene.cards} == scrambled
    window.undo_stack.redo()
    assert not overlaps(scene)
    assert not window.arrange()  # already arranged: nothing to undo
    assert "already arranged" in window.statusBar().currentMessage()


def test_find_reveals_the_first_match(window: MainWindow, sample: Path):
    assert window.open_path(sample)
    window.act_find.trigger()
    assert window.library.search.hasFocus() or window.library.search.isVisible()
    window.library.search.setText("fifty denars")
    window.library.search.returnPressed.emit()
    assert window.current_scene().conversation_id == "spare_coin"
    assert window.current_scene().selected_key() == "coin_fine"


def test_toolbar_actions_have_icons(window: MainWindow):
    for action in (window.act_new, window.act_open, window.act_save, window.act_validate, window.act_play,
                   window.act_stop, window.act_arrange):
        assert not action.icon().isNull()
        assert not action.icon().pixmap(16, 16).isNull()
