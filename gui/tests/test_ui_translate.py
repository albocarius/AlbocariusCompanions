"""Translate with LLM (dialog + apply) against the fake OpenAI-compatible server of test_translate."""

import json
from pathlib import Path

from PySide6.QtCore import Qt

from albocarius_editor.ui.main_window import MainWindow
from albocarius_editor.ui.translate_dialog import TranslateDialog

from .test_translate import FakeChat, translate_all


def make_dialog(window: MainWindow, conversation: str | None = None) -> TranslateDialog:
    return TranslateDialog(window.folder, window.settings, window.catalog.schema["patterns"]["languageCode"],
                           conversation, window)


def test_translate_review_and_apply(window: MainWindow, sample: Path, qtbot):
    server = FakeChat(translate_all)
    try:
        assert window.open_path(sample)
        dialog = make_dialog(window, "the_scar")
        qtbot.addWidget(dialog)
        dialog.url.setText(server.url)
        dialog.model.setText("local")
        dialog.test_connection()
        qtbot.waitUntil(lambda: "Connected" in dialog.connection.text(), timeout=10000)
        dialog.target.setEditText("de")
        assert dialog.ids_in_scope() == window.folder.node_ids_by_conversation()["the_scar"]
        assert "6 line(s)" in dialog.estimate.text() and "tokens sent" in dialog.estimate.text()
        dialog.prompt.setPlainText("Keep it short.")
        dialog.translate()
        qtbot.waitUntil(lambda: dialog.result_ is not None, timeout=10000)
        assert dialog.table.rowCount() == 6
        assert "Keep it short." in server.requests[-1]["body"]["messages"][0]["content"]
        dialog.table.item(0, 0).setCheckState(Qt.CheckState.Unchecked)  # leave the first line out
        dialog.table.item(1, 2).setText("Edited by hand.")
        texts = dialog.accepted_translations()
        assert len(texts) == 5 and "Edited by hand." in texts.values()
        assert window.settings.value("llm/url") == server.url and not window.settings.value("llm/key")
        assert window.apply_translations(dialog.target_code(), texts)
        assert window.undo_stack.count() == 1
        assert window.save()
        de = json.loads((sample / "lang" / "de.json").read_text())
        assert de["schemaVersion"] == 1 and len(de["lines"]) == 5
        window.undo_stack.undo()
        assert "de" not in window.folder.languages()
    finally:
        server.close()


def test_problem_lines_are_not_ticked(window: MainWindow, sample: Path, qtbot):
    server = FakeChat(lambda body: (200, {"choices": [{"message": {"content": json.dumps(
        {"scar_story": "Ohne Platzhalter.", "p_scar_ask": ""})}}]}))
    try:
        assert window.open_path(sample)
        dialog = make_dialog(window)
        qtbot.addWidget(dialog)
        dialog.url.setText(server.url)
        dialog.model.setText("m")
        dialog.target.setEditText("fr")
        dialog.translate()
        qtbot.waitUntil(lambda: dialog.result_ is not None, timeout=10000)
        assert dialog.accepted_translations().get("scar_story") == "Ohne Platzhalter."  # no placeholders in English
        rows = {dialog.table.item(r, 0).text(): r for r in range(dialog.table.rowCount())}
        assert dialog.table.item(rows["p_scar_ask"], 0).checkState() == Qt.CheckState.Unchecked
        assert dialog.table.item(rows["p_scar_ask"], 3).text() == "empty"
        assert dialog.table.item(rows["intro_01"], 3).text() == "missing from the reply"
    finally:
        server.close()


def test_failure_is_shown(window: MainWindow, sample: Path, qtbot):
    assert window.open_path(sample)
    dialog = make_dialog(window)
    qtbot.addWidget(dialog)
    dialog.url.setText("http://127.0.0.1:9/v1")
    dialog.model.setText("m")
    dialog.target.setEditText("de")
    dialog.translate()
    qtbot.waitUntil(lambda: "failed" in dialog.status.text(), timeout=15000)
    assert dialog.translate_button.isEnabled()
