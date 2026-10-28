"""The simulator preview (PLAN T16 conversations, T17 triggers). Skipped without a built CLI."""

from pathlib import Path

import pytest
from PySide6.QtWidgets import QCheckBox, QPushButton, QSpinBox

from albocarius_editor.cli.client import locate
from albocarius_editor.ui.main_window import MainWindow

pytestmark = pytest.mark.skipif(locate() is None, reason="the CLI is not built")
TIMEOUT = 60000


def click(window: MainWindow, qtbot, name: str) -> None:
    """Clicks a preview button once it exists and waits for the request's answer."""
    qtbot.waitUntil(lambda: window.preview.findChild(QPushButton, name) is not None, timeout=TIMEOUT)
    button = window.preview.findChild(QPushButton, name)
    assert button.isEnabled(), name
    before = window.preview.steps
    button.click()
    qtbot.waitUntil(lambda: window.preview.steps > before and not window.preview.sim.runner.busy, timeout=TIMEOUT)


def test_plays_the_sample_sequence(window: MainWindow, sample: Path, qtbot):
    assert window.open_path(sample)
    preview = window.preview
    preview.state_form.player_gold.setValue(1000)
    preview.seed.setValue(5)
    click(window, qtbot, "start_hub")
    assert preview.session
    click(window, qtbot, "hire")
    assert preview.state_form.player_gold.value() == 500 and preview.state_form.hired.isChecked()
    click(window, qtbot, "hub")
    click(window, qtbot, "option:the_scar/p_scar_ask")
    assert [s["node"] for s in preview.last_step["said"]] == ["p_scar_ask", "scar_story"]
    # The canvas follows: the_scar is open with scar_story highlighted.
    scene = window.current_scene()
    assert scene.conversation_id == "the_scar" and scene.cards["scar_story"].highlighted
    click(window, qtbot, "option:the_scar/p_scar_sorry")
    click(window, qtbot, "option:spare_coin/p_coin_offer")
    step = preview.last_step
    assert step["kind"] == "end"
    assert [s["node"] for s in step["said"]] == ["p_coin_offer", "coin_thanks"]
    assert preview.state_form.player_gold.value() == 400
    assert preview.state_form.companion_gold.value() == 600
    assert preview.findChild(QCheckBox, "state:var:got_coin").isChecked()
    transcript = preview.transcript.toPlainText()
    assert window.folder.text("scar_story") in transcript and "relation" in transcript


def test_hidden_options_are_greyed_with_their_conditions(window: MainWindow, sample: Path, qtbot):
    assert window.open_path(sample)
    click(window, qtbot, "start_hub")  # not hired: the_scar needs hired
    button = window.preview.findChild(QPushButton, "option:the_scar/p_scar_ask")
    assert not button.isEnabled() and "hired" in button.toolTip()


def test_play_and_stop_from_the_toolbar(window: MainWindow, sample: Path, qtbot):
    assert window.open_path(sample)
    window.act_play.trigger()
    qtbot.waitUntil(lambda: window.preview.session is not None, timeout=TIMEOUT)
    window.act_stop.trigger()
    assert window.preview.session is None


def test_fire_and_deliver_a_trigger(window: MainWindow, sample: Path, qtbot):
    assert window.open_path(sample)
    preview = window.preview
    preview.state_form.in_party.setChecked(True)  # triggered content needs the companion in the party
    preview.state_form.hired.setChecked(True)
    preview.state_form.met.setChecked(True)
    triggers = preview.triggers
    triggers.picker.setEditText("playerBattleEnded")
    triggers.set_trigger("playerBattleEnded")
    triggers.payload_widgets["playerWon"].setChecked(True)
    triggers.payload_widgets["playerInvolved"].setChecked(True)
    before = preview.steps
    triggers.fire()
    qtbot.waitUntil(lambda: "candidates" in triggers.last and not preview.sim.runner.busy, timeout=TIMEOUT)
    outcomes = {c["content"]: c["outcome"] for c in triggers.last["candidates"]}
    assert outcomes.get("battle_won") == "queued", triggers.last["candidates"]
    assert triggers.queue.topLevelItemCount() >= 1
    triggers.deliver()
    qtbot.waitUntil(lambda: "delivered" in triggers.last and not preview.sim.runner.busy, timeout=TIMEOUT)
    assert triggers.last["delivered"]["content"] == "battle_won"
    assert "battle_won" in triggers.delivered.toPlainText()
    assert preview.steps > before
