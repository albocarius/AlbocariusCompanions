"""data.edit functions added for the editor tasks (PLAN T02+)."""

from pathlib import Path

import pytest

from albocarius_editor.data import edit
from albocarius_editor.data.companion import CompanionFolder


def test_set_text(sample: Path):
    folder = CompanionFolder.load(sample)
    edit.set_text(folder, "scar_story", "New.")
    assert folder.text("scar_story") == "New."
    edit.set_text(folder, "intro_01", "Bonjour.", "fr")
    assert folder.text("intro_01", "fr") == "Bonjour."
    with pytest.raises(edit.EditError):
        edit.set_text(folder, "ghost", "x")
    with pytest.raises(edit.EditError):
        edit.set_text(folder, "scar_story", "New.")
