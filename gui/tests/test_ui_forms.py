"""Schema-driven forms in the inspector (PLAN T06–T10)."""

import json
from pathlib import Path

from PySide6.QtWidgets import QCheckBox, QComboBox, QLineEdit, QPushButton, QToolButton, QWidget

from albocarius_editor.data import graph
from albocarius_editor.ui import forms
from albocarius_editor.ui.inspector import CommitTextEdit
from albocarius_editor.ui.main_window import MainWindow


def select(window: MainWindow, sample: Path, conversation: str, key: str):
    if window.folder is None:
        assert window.open_path(sample)
    scene = window.open_tab(conversation).scene()
    scene.select(key)
    return scene


def find(window: MainWindow, cls, name: str):
    widget = window.inspector.findChild(cls, name)
    assert widget is not None, f"no {cls.__name__} {name}"
    return widget


def edit_line(window: MainWindow, name: str, text: str) -> None:
    line = find(window, QLineEdit, name)
    line.setText(text)
    line.editingFinished.emit()


def node(window: MainWindow, conversation: str, node_id: str) -> dict:
    return window.folder.node(conversation, node_id)


# ---- T06 fields ----

def test_scalar_fields_of_a_node(window: MainWindow, sample: Path):
    select(window, sample, "spare_coin", "coin_fine")
    edit_line(window, "field:priority", "3")
    assert node(window, "spare_coin", "coin_fine")["priority"] == 3
    find(window, QCheckBox, "field:voiced").setChecked(True)
    assert node(window, "spare_coin", "coin_fine")["voiced"] is True
    window.undo_stack.undo()
    assert "voiced" not in node(window, "spare_coin", "coin_fine")
    window.undo_stack.undo()
    assert "priority" not in node(window, "spare_coin", "coin_fine")
    # The "×" of a set optional field removes the key.
    edit_line(window, "field:priority", "2")
    find(window, QWidget, "remove:priority").click()
    assert "priority" not in node(window, "spare_coin", "coin_fine")


def test_invalid_values_are_refused(window: MainWindow, sample: Path):
    select(window, sample, "spare_coin", "coin_fine")
    count = window.undo_stack.count()
    edit_line(window, "id_field", "Bad Id!")
    assert "not a valid id" in window.statusBar().currentMessage()
    edit_line(window, "field:audio", "")  # empty on an unset field: nothing
    assert window.undo_stack.count() == count
    assert node(window, "spare_coin", "coin_fine") is not None


def test_conversation_scalars_and_line_slot(window: MainWindow, sample: Path):
    select(window, sample, "the_scar", graph.HEADER)
    find(window, QCheckBox, "field:once").setChecked(False)
    assert window.folder.conversation("the_scar")["once"] is False
    combo = find(window, QComboBox, "start_mode")
    assert combo.currentData() == "hub"


# ---- T07 conditions ----

def test_conditions_editor(window: MainWindow, sample: Path):
    before = (sample / "dialogs.json").read_bytes()
    select(window, sample, "the_scar", "scar_story")
    editor = find(window, forms.ConditionsEditor, "conditions:when")
    editor.add(window.catalog.condition("hasGold"))
    editor = find(window, forms.ConditionsEditor, "conditions:when")
    editor.add(window.catalog.condition("any"))  # one empty group to fill
    group = find(window, forms.ConditionsEditor, "conditions:when.any.0")
    group.add(window.catalog.condition("flags"))
    edit_line(window, "field:when.any.0.flags", "told_scar")
    find(window, QToolButton, "append:when.any").click()  # a second group
    group = find(window, forms.ConditionsEditor, "conditions:when.any.1")
    group.add(window.catalog.condition("var"))
    find(window, QWidget, "remove:when.any.1.eq").click()
    edit_line(window, "field:when.any.1.min", "3")
    find(window, QComboBox, "field:when.any.1.var").setEditText("got_coin")
    find(window, QComboBox, "field:when.any.1.var").lineEdit().editingFinished.emit()
    assert node(window, "the_scar", "scar_story")["when"] == {
        "hasGold": {"who": "player", "min": 1},
        "any": [{"flags": ["told_scar"]}, {"var": "got_coin", "min": 3}],
    }
    # Removing them all restores the original file.
    find(window, QWidget, "remove:when.any").click()
    find(window, QWidget, "remove:when.hasGold").click()
    assert "when" not in node(window, "the_scar", "scar_story")
    assert window.save()
    assert (sample / "dialogs.json").read_bytes() == before


def test_variable_fields_offer_the_blackboard_and_event_fields(window: MainWindow, sample: Path):
    select(window, sample, "battle_won", graph.HEADER)
    var = find(window, QComboBox, "field:when.var")
    items = [var.itemText(i) for i in range(var.count())]
    assert "told_scar" in items and "mood" in items and "event.playerWon" in items


# ---- T08 effects ----

def test_effects_editor(window: MainWindow, sample: Path):
    select(window, sample, "the_scar", "scar_thanks")
    fx = lambda: node(window, "the_scar", "scar_thanks").get("effects")  # noqa: E731
    find(window, forms.EffectsEditor, "effects:effects").add(window.catalog.effect("relation"))
    edit_line(window, "field:effects.relation", "2")
    find(window, forms.EffectsEditor, "effects:effects").add(window.catalog.effect("transfer"))
    kind = find(window, QComboBox, "field:effects.transfer.kind")
    kind.activated.emit(kind.findText("item"))
    edit_line(window, "field:effects.transfer.item", "grain")
    find(window, forms.EffectsEditor, "effects:effects").add(window.catalog.effect("add"))
    assert fx() == {"relation": 2, "transfer": {"from": "player", "to": "self", "item": "grain"}, "add": {"mood": 1}}
    edit_line(window, "field:effects.relation", "500")  # outside −100..100
    assert "outside" in window.statusBar().currentMessage()
    for _ in range(6):
        window.undo_stack.undo()
    assert fx() is None


# ---- T09 trigger ----

def test_trigger_editor_switches_a_hub_conversation(window: MainWindow, sample: Path):
    select(window, sample, "the_scar", graph.HEADER)
    combo = find(window, QComboBox, "start_mode")
    combo.activated.emit(combo.findData("trigger"))
    conv = lambda: window.folder.conversation("the_scar")  # noqa: E731
    assert "entry" not in conv() and "on" in conv()["trigger"]
    picker = find(window, QComboBox, "field:trigger.on")
    picker.activated.emit(picker.findText("villageLooted"))
    adder = find(window, QComboBox, "add:trigger.where")
    adder.activated.emit(adder.findText("village.culture"))
    edit_line(window, "field:trigger.where.village.culture", "vlandia")
    assert conv()["trigger"] == {"on": "villageLooted", "where": {"village.culture": "vlandia"}}
    subject = find(window, QComboBox, "field:trigger.subject")
    subject.setEditText("playerClan")
    subject.lineEdit().editingFinished.emit()
    assert conv()["trigger"]["subject"] == "playerClan"
    combo = find(window, QComboBox, "start_mode")
    combo.activated.emit(combo.findData("hub"))
    assert "trigger" not in conv() and conv()["entry"] == "hub"
    assert list(conv())[:2] == ["id", "entry"]


def test_slot_free_line_gets_a_trigger(window: MainWindow, sample: Path):
    assert window.open_path(sample)
    window.reveal("@lines", "idle_daily_01")
    window._show_in_inspector(None, "idle_daily_01")
    assert window.inspector.findChild(forms.TriggerEditor, "trigger:trigger") is not None


# ---- T10 translations ----

def test_add_a_language_and_translate(window: MainWindow, sample: Path, monkeypatch):
    from PySide6.QtWidgets import QInputDialog

    monkeypatch.setattr(QInputDialog, "getText", lambda *a, **k: ("de", True))
    select(window, sample, "the_scar", "scar_story")
    find(window, QPushButton, "add_language").click()
    assert "de" in window.folder.languages()
    box = find(window, CommitTextEdit, "text_de")
    assert box.placeholderText() == window.folder.text("scar_story")  # missing: English shown greyed
    box.setPlainText("Eine Narbe.")
    box.commit()
    assert window.save()
    de = sample / "lang" / "de.json"
    assert json.loads(de.read_text()) == {"schemaVersion": 1, "lines": {"scar_story": "Eine Narbe."}}


def test_language_undo_drops_the_new_file(window: MainWindow, sample: Path, monkeypatch):
    from PySide6.QtWidgets import QInputDialog

    monkeypatch.setattr(QInputDialog, "getText", lambda *a, **k: ("de", True))
    select(window, sample, "the_scar", "scar_story")
    find(window, QPushButton, "add_language").click()
    find(window, CommitTextEdit, "text_de").setPlainText("x")
    find(window, CommitTextEdit, "text_de").commit()
    window.undo_stack.undo()
    window.undo_stack.undo()
    assert "de" not in window.folder.languages()
    window.save()
    assert not (sample / "lang" / "de.json").exists()


def test_copy_english(window: MainWindow, sample: Path, monkeypatch):
    from PySide6.QtWidgets import QInputDialog

    monkeypatch.setattr(QInputDialog, "getText", lambda *a, **k: ("fr", True))
    select(window, sample, "the_scar", "scar_story")
    find(window, QPushButton, "add_language").click()
    find(window, QPushButton, "copy_en_fr").click()
    assert window.folder.text("scar_story", "fr") == window.folder.text("scar_story")
