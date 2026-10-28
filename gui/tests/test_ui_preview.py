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


def fire(window: MainWindow, qtbot, deliver: bool = True) -> None:
    preview = window.preview
    before = preview.steps
    preview.triggers.fire(deliver=deliver)
    qtbot.waitUntil(lambda: preview.steps > before and not preview.sim.runner.busy, timeout=TIMEOUT)


def test_fire_and_deliver_a_trigger(window: MainWindow, sample: Path, qtbot):
    assert window.open_path(sample)
    triggers = window.preview.triggers
    triggers.set_trigger("playerBattleEnded")
    assert "battle_won" in triggers.listening.text()
    # The content's "when" (event.playerWon = true) prefills the event details.
    assert triggers.payload_widgets["playerWon"].isChecked()
    fire(window, qtbot)  # fire, then deliver what was queued
    qtbot.waitUntil(lambda: "delivered" in triggers.last, timeout=TIMEOUT)
    assert triggers.last["delivered"]["content"] == "battle_won"
    result = triggers.result.toPlainText()
    assert "✓ battle_won" in result and "Delivered battle_won" in result, result


def test_companions_used_triggers_come_first_and_explain_outcomes(window: MainWindow, sample: Path, qtbot):
    assert window.open_path(sample)
    triggers = window.preview.triggers
    first = [triggers.picker.itemData(i) for i in range(4)]
    assert set(first) == {"daily", "nearSettlement", "playerBattleEnded", "varCrossed"}
    triggers.set_trigger("nearSettlement")
    assert set(triggers.payload_widgets) == {"settlement.id"}  # only the id until a detail is added
    triggers.add_detail("settlement.culture")
    assert set(triggers.payload_widgets) == {"settlement.id", "settlement.culture"}
    triggers.in_party.setChecked(False)
    fire(window, qtbot)
    assert "not in the player's party" in triggers.result.toPlainText()
    triggers.in_party.setChecked(True)  # a new session with the new state
    fire(window, qtbot)
    qtbot.waitUntil(lambda: "Delivered" in triggers.result.toPlainText(), timeout=TIMEOUT)
    assert "near_town_01" in triggers.result.toPlainText()


def test_suggested_payload_follows_where_and_event_conditions():
    from albocarius_editor.ui.preview_triggers import suggested_payload

    content = [{"trigger": {"on": "villageLooted", "where": {"village.culture": "vlandia"}},
                "when": {"all": [{"var": "event.playerInvolved", "eq": True}], "hired": True}}]
    assert suggested_payload(content) == {"village.culture": "vlandia", "playerInvolved": True}
