"""Validation through the CLI (PLAN T15)."""

import json
from pathlib import Path

import pytest
from PySide6.QtCore import QSettings

from albocarius_editor.cli.client import locate
from albocarius_editor.ui.main_window import MainWindow

needs_cli = pytest.mark.skipif(locate() is None, reason="the CLI is not built")


@needs_cli
def test_a_broken_next_is_shown_and_revealed(window: MainWindow, sample: Path, qtbot):
    data = json.loads((sample / "dialogs.json").read_text())
    scar = next(c for c in data["conversations"] if c["id"] == "the_scar")
    next(n for n in scar["nodes"] if n["id"] == "scar_story")["next"] = ["p_scar_sorry", "ghost"]
    (sample / "dialogs.json").write_text(json.dumps(data, indent=2))
    assert window.open_path(sample)
    window.validate()
    qtbot.waitUntil(lambda: window.validation_runs == 1, timeout=60000)
    errors = [d for d in window.diagnostics.all() if d["severity"] == "error"]
    hit = next(d for d in errors if d.get("conversation") == "the_scar" and d.get("node") == "scar_story")
    assert "ghost" in hit["message"]
    assert "error" in window.statusBar().currentMessage()
    window.open_tab("spare_coin")
    window.reveal_diagnostic(hit)
    assert window.tabs.tabText(window.tabs.currentIndex()) == "the_scar"
    assert window.current_scene().selected_key() == "scar_story"


@needs_cli
def test_saving_validates(window: MainWindow, sample: Path, qtbot):
    assert window.open_path(sample)
    window.set_text("scar_story", "Changed.")
    assert window.save()
    qtbot.waitUntil(lambda: window.validation_runs == 1, timeout=60000)
    assert "Validation: 0 error(s)" in window.statusBar().currentMessage()


def test_missing_cli_shows_unavailable(qtbot, qapp, tmp_path: Path, sample: Path, monkeypatch):
    monkeypatch.setenv("ALBOCARIUS_CLI", str(tmp_path / "missing.dll"))
    w = MainWindow(QSettings(str(tmp_path / "settings.ini"), QSettings.Format.IniFormat))
    qtbot.addWidget(w)
    assert w.open_path(sample)
    w.validate()
    messages = [d["message"] for d in w.diagnostics.all()]
    assert any(m.startswith("validation unavailable:") for m in messages)
