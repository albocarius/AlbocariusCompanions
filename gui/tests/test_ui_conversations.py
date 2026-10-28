"""Conversations: new, rename, delete, duplicate, order (PLAN T13)."""

from pathlib import Path

from PySide6.QtWidgets import QInputDialog

from albocarius_editor.data import graph
from albocarius_editor.ui.main_window import MainWindow


def ids(window: MainWindow) -> list[str]:
    return [c["id"] for c in window.folder.conversations]


def library(window: MainWindow) -> list[str]:
    tree = window.library.tree
    return [tree.topLevelItem(i).text(0) for i in range(1, tree.topLevelItemCount())]


def test_new_conversations(window: MainWindow, sample: Path, monkeypatch):
    assert window.open_path(sample)
    answers = iter([("ambush", True)])
    monkeypatch.setattr(QInputDialog, "getText", lambda *a, **k: next(answers))
    monkeypatch.setattr(QInputDialog, "getItem", lambda *a, **k: ("Started by a trigger", True))
    assert window.new_conversation() == "ambush"
    conv = window.folder.conversation("ambush")
    assert "entry" not in conv and "on" in conv["trigger"]
    assert window.folder.node("ambush", conv["start"][0])["speaker"] == "npc"
    assert window.tabs.tabText(window.tabs.currentIndex()) == "ambush"
    assert window.current_scene().selected_key() == graph.HEADER
    assert window.new_conversation("chat", "hub") == "chat"
    assert window.folder.conversation("chat")["entry"] == "hub"
    assert window.new_conversation("chat", "hub") is None  # a duplicate id is refused
    assert library(window)[-2:] == ["ambush", "chat"]


def test_rename_delete_duplicate_and_reorder(window: MainWindow, sample: Path):
    assert window.open_path(sample)
    assert window.rename_conversation("campfire", "camp_talk")
    assert ids(window)[-1] == "camp_talk"
    copy_ = window.duplicate_conversation("the_scar")
    assert ids(window)[:2] == ["the_scar", copy_]
    assert window.tabs.tabText(window.tabs.currentIndex()) == copy_
    assert window.move_conversation("spare_coin", -1)
    assert ids(window)[:3] == ["the_scar", "spare_coin", copy_]
    assert library(window)[:3] == ["the_scar", "spare_coin", copy_]  # the library follows the file order
    assert not window.move_conversation("the_scar", -1)
    assert window.delete_conversation(copy_, confirm=False)
    assert copy_ not in ids(window)
    for _ in range(4):
        window.undo_stack.undo()
    assert ids(window) == ["the_scar", "spare_coin", "after_scar", "battle_won", "campfire"]


def test_new_button_makes_a_conversation_or_a_companion(window: MainWindow, sample: Path, monkeypatch):
    from PySide6.QtWidgets import QFileDialog, QMessageBox, QPushButton

    monkeypatch.setattr(QMessageBox, "question", lambda *a, **k: QMessageBox.StandardButton.Discard)

    answers = iter([("chat", True), ("mira", True)])
    monkeypatch.setattr(QInputDialog, "getText", lambda *a, **k: next(answers))
    monkeypatch.setattr(QInputDialog, "getItem", lambda *a, **k: ("From the hub menu (player options)", True))
    monkeypatch.setattr(QFileDialog, "getExistingDirectory", lambda *a, **k: str(sample.parent))
    assert window.act_new.text() == "New companion"  # nothing open yet
    assert window.open_path(sample)
    assert window.act_new.text() == "New conversation"
    assert window.act_new.shortcut().toString() == "Ctrl+N"
    window.library.findChild(QPushButton, "new_conversation").click()
    assert window.folder.conversation("chat") is not None
    window.act_new_companion.trigger()
    assert window.folder.id == "mira"
    assert window.new_button.defaultAction() is window.act_new
