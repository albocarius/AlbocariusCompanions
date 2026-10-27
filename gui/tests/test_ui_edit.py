"""Editing through the window (PLAN T01–T05): every edit is one undoable EditCommand over data.edit."""

from pathlib import Path

from albocarius_editor.data import edit, graph
from albocarius_editor.ui.main_window import MainWindow


def open_scar(window: MainWindow, sample: Path):
    assert window.open_path(sample)
    return window.open_tab("the_scar").scene()


# ---- T01 generic undoable edit command ----

def test_edit_command_deletes_undoes_and_redoes(window: MainWindow, sample: Path):
    scene = open_scar(window, sample)
    assert "scar_shrug" in scene.cards
    done, _ = window.edit("Delete scar_shrug", lambda f: edit.delete_node(f, "the_scar", "scar_shrug"))
    assert done
    assert "scar_shrug" not in scene.cards
    assert window.folder.node("the_scar", "scar_shrug") is None
    assert window.folder.dirty
    window.undo_stack.undo()
    assert "scar_shrug" in scene.cards
    assert window.folder.text("scar_shrug") is not None
    window.undo_stack.redo()
    assert "scar_shrug" not in scene.cards
    assert window.undo_stack.count() == 1


def test_edit_error_pushes_nothing(window: MainWindow, sample: Path):
    open_scar(window, sample)
    before = window.folder.snapshot()
    done, _ = window.edit("Delete ghost", lambda f: edit.delete_node(f, "the_scar", "ghost"))
    assert not done
    assert window.undo_stack.count() == 0
    assert "no node 'ghost'" in window.statusBar().currentMessage()
    assert window.folder.snapshot() == before


def test_edit_returns_the_result_and_refreshes_the_library(window: MainWindow, sample: Path):
    open_scar(window, sample)
    done, node = window.edit("Add", lambda f: edit.add_node(f, "the_scar", "npc"))
    assert done and node["id"] == "the_scar_01"
    scar = next(window.library.tree.topLevelItem(i) for i in range(window.library.tree.topLevelItemCount())
                if window.library.tree.topLevelItem(i).text(0) == "the_scar")
    assert any("the_scar_01" in scar.child(j).text(0) for j in range(scar.childCount()))
    assert graph.HEADER in window.scene_for("the_scar").cards
