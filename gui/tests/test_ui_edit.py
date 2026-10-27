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


# ---- T02 inline English text ----

def changed_files(folder: Path, before: dict[str, bytes]) -> set[str]:
    now = {str(p.relative_to(folder)): p.read_bytes() for p in folder.rglob("*") if p.is_file()}
    return {k for k in now if before.get(k) != now[k]}


def files(folder: Path) -> dict[str, bytes]:
    return {str(p.relative_to(folder)): p.read_bytes() for p in folder.rglob("*") if p.is_file()}


def test_inline_text_edit_changes_only_the_english_file(window: MainWindow, sample: Path, qtbot):
    scene = open_scar(window, sample)
    before = files(sample)
    old = window.folder.text("scar_story")
    editor = scene.edit_text("scar_story")
    assert editor is not None and editor.toPlainText() == old
    editor.setPlainText("A new story.\nTwo lines.")
    editor.finish(commit=True)
    qtbot.waitUntil(lambda: window.folder.text("scar_story") == "A new story.\nTwo lines.")
    assert scene.cards["scar_story"].content.text == "A new story.\nTwo lines."
    assert window.save()
    assert changed_files(sample, before) == {"lang/en.json", "dialogs.layout.json"}
    window.undo_stack.undo()
    assert window.folder.text("scar_story") == old


def test_inline_editor_escape_cancels(window: MainWindow, sample: Path, qtbot):
    scene = open_scar(window, sample)
    editor = scene.edit_text("scar_story")
    editor.setPlainText("never")
    editor.finish(commit=False)
    qtbot.wait(10)
    assert window.undo_stack.count() == 0
    assert scene.edit_text(graph.HEADER) is None  # the header has no text block


def test_inspector_english_box_edits_the_text(window: MainWindow, sample: Path):
    from albocarius_editor.ui.inspector import CommitTextEdit

    scene = open_scar(window, sample)
    scene.select("scar_thanks")
    box = window.inspector.findChild(CommitTextEdit, "text_en")
    assert not box.isReadOnly()
    assert window.inspector.findChild(CommitTextEdit, "text_tr").isReadOnly()
    box.setPlainText("Thanks, friend.")
    box.commit()
    assert window.folder.text("scar_thanks") == "Thanks, friend."
    assert window.undo_stack.count() == 1
    window.undo_stack.undo()
    assert window.folder.text("scar_thanks") != "Thanks, friend."


# ---- T03 add nodes ----

def test_add_npc_and_player_nodes(window: MainWindow, sample: Path, qtbot):
    import json

    from PySide6.QtCore import QPointF

    scene = open_scar(window, sample)
    npc = window.add_from_palette("npc", QPointF(1000, 600))
    assert npc == "the_scar_01"
    assert scene.selected_key() == npc and scene.editor is not None  # selected, text editor open
    scene.editor.setPlainText("Hello there.")
    scene.editor.finish(commit=True)
    qtbot.waitUntil(lambda: window.folder.text(npc) == "Hello there.")
    window.library.palette_activated.emit("player")  # a palette click: at the view center
    player = "p_the_scar_01"
    assert window.folder.node("the_scar", player)["speaker"] == "player"
    assert window.save()
    dialogs = json.loads((sample / "dialogs.json").read_text())
    scar = next(c for c in dialogs["conversations"] if c["id"] == "the_scar")
    assert [n["id"] for n in scar["nodes"]][-2:] == [npc, player]
    en = json.loads((sample / "lang" / "en.json").read_text())["lines"]
    assert en[npc] == "Hello there." and en[player] == ""
    layout = json.loads((sample / "dialogs.layout.json").read_text())["conversations"]["the_scar"]
    assert layout[npc] == [1000, 600] and player in layout
    for _ in range(3):  # player, npc text, npc
        window.undo_stack.undo()
    assert window.folder.node("the_scar", npc) is None and window.folder.node("the_scar", player) is None
    assert npc not in scene.cards


def test_palette_hub_and_end_connect_the_selected_node(window: MainWindow, sample: Path):
    scene = open_scar(window, sample)
    window.add_from_palette("end")  # nothing selected: refused
    assert window.undo_stack.count() == 0
    scene.select("p_scar_enough")
    window.add_from_palette("end")
    assert window.folder.node("the_scar", "p_scar_enough")["next"] == ["end"]
    assert graph.END in scene.cards


def test_drop_from_the_palette(window: MainWindow, sample: Path):
    from PySide6.QtCore import QPointF

    scene = open_scar(window, sample)
    view = window.tabs.currentWidget()
    view.palette_dropped.emit("npc", QPointF(415, 222))
    node_id = scene.selected_key()
    assert window.folder.layout.get("the_scar", node_id) == (420, 220)  # snapped to the grid
