"""Blackboard editing (PLAN T11)."""

from pathlib import Path

from PySide6.QtWidgets import QCheckBox, QComboBox, QInputDialog, QLineEdit, QMessageBox, QToolButton

from albocarius_editor.ui.main_window import MainWindow


def find(window: MainWindow, cls, name: str):
    widget = window.blackboard.findChild(cls, name)
    assert widget is not None, name
    return widget


def test_add_type_change_rename_remove(window: MainWindow, sample: Path, monkeypatch):
    assert window.open_path(sample)
    f = lambda: window.folder  # noqa: E731
    monkeypatch.setattr(QInputDialog, "getText", lambda *a, **k: ("raids", True))
    window.blackboard.add_button.click()
    assert f().variables["raids"] == {"type": "bool"}
    find(window, QCheckBox, "default:raids").setChecked(True)
    assert f().variables["raids"]["default"] is True
    box = find(window, QComboBox, "type:raids")
    box.activated.emit(box.findText("int"))
    assert f().variables["raids"] == {"type": "int"}  # the bool default is reset to the int default
    line = find(window, QLineEdit, "default:raids")
    line.setText("3")
    line.editingFinished.emit()
    assert f().variables["raids"]["default"] == 3

    asked = []
    monkeypatch.setattr(QMessageBox, "question", lambda *a, **k: asked.append(a[2]) or QMessageBox.StandardButton.Yes)
    name = find(window, QLineEdit, "name:told_scar")
    name.setText("knows_scar")
    name.editingFinished.emit()
    assert "lose" in asked[0]
    assert "knows_scar" in f().variables and "told_scar" not in f().variables
    assert f().node("the_scar", "p_scar_sorry")["effects"]["setFlags"] == ["knows_scar"]
    assert f().conversation("spare_coin")["when"]["flags"] == ["knows_scar"]

    find(window, QToolButton, "remove:raids").click()
    assert "raids" not in f().variables
    for _ in range(6):
        window.undo_stack.undo()
    assert "raids" not in f().variables and "told_scar" in f().variables


def test_builtin_mood_is_read_only(window: MainWindow, sample: Path):
    assert window.open_path(sample)
    assert not find(window, QComboBox, "type:mood").isEnabled()
    assert window.blackboard.findChild(QToolButton, "remove:mood") is None
