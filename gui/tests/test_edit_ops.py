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


def load(sample: Path) -> CompanionFolder:
    return CompanionFolder.load(sample)


def test_link_problems(sample: Path):
    f = load(sample)
    p = lambda s, t, c="the_scar": edit.link_problem(f, c, s, t)  # noqa: E731
    assert p("scar_thanks", "scar_shrug") is None                       # npc → npc: a candidate
    assert "player option is never followed" in p("p_scar_sorry", "p_scar_enough")
    assert "never both" in p("scar_story", "scar_thanks")                # next already holds player options
    assert "hub conversation starts with player options" in p("@header", "scar_story")
    assert "greeting conversation starts with companion lines" in p("@header", "p_greet_glad", "after_scar")
    assert "triggered conversation" in p("@header", "p_bw_cheer", "battle_won")
    assert "cannot start with hub" in p("@header", "@end")
    assert p("scar_story", "@end") is None                               # hub/end replace the list
    assert "already continues" in p("p_scar_ask", "scar_story")
    with pytest.raises(edit.EditError, match="never followed"):
        edit.connect(f, "the_scar", "p_scar_sorry", "p_scar_enough")


def test_path_edits_prune_empty_containers(sample: Path):
    f = load(sample)
    ref = ("node", "the_scar", "scar_story")
    edit.merge_at(f, ref, ["when"], {"hasGold": {"who": "player", "min": 100}})
    i = edit.append_at(f, ref, ["when", "any"], {"flags": ["told_scar"]})
    edit.append_at(f, ref, ["when", "any"], {"var": "mood", "min": 1})
    assert f.node("the_scar", "scar_story")["when"] == {
        "hasGold": {"who": "player", "min": 100}, "any": [{"flags": ["told_scar"]}, {"var": "mood", "min": 1}]}
    edit.set_at(f, ref, ["when", "any", 1, "min"], 2)
    assert edit.get_at(f, ref, ["when", "any", 1, "min"]) == 2
    with pytest.raises(edit.EditError, match="unchanged"):
        edit.set_at(f, ref, ["when", "any", 1, "min"], 2)
    edit.remove_at(f, ref, ["when", "any", i, "flags"])  # the emptied element is removed, the list keeps one
    assert f.node("the_scar", "scar_story")["when"]["any"] == [{"var": "mood", "min": 2}]
    edit.remove_at(f, ref, ["when", "any", 0])
    edit.remove_at(f, ref, ["when", "hasGold"])
    assert "when" not in f.node("the_scar", "scar_story")
    edit.replace_keys(f, ("node", "spare_coin", "p_coin_offer"), ["effects", "transfer"], ["gold"], {"item": "grain"})
    assert f.node("spare_coin", "p_coin_offer")["effects"]["transfer"] == {"from": "player", "to": "self", "item": "grain"}


def test_conversation_edits(sample: Path):
    f = load(sample)
    new = edit.duplicate_conversation(f, "the_scar")
    assert new == "the_scar_copy_01"
    copy_ = f.conversation(new)
    assert [c["id"] for c in f.conversations][:2] == ["the_scar", new]
    assert copy_["start"] == ["p_scar_ask_01"]
    assert f.node(new, "p_scar_ask_01")["next"] == ["scar_story_01"]
    assert f.text("scar_story_01", "tr") == f.text("scar_story", "tr")
    edit.move_conversation(f, new, 1)
    assert [c["id"] for c in f.conversations][1:3] == ["spare_coin", new]
    with pytest.raises(edit.EditError):
        edit.move_conversation(f, "the_scar", -1)
    edit.set_start_mode(f, "the_scar", "trigger", "villageLooted")
    assert "entry" not in f.conversation("the_scar") and f.conversation("the_scar")["trigger"] == {"on": "villageLooted"}
    edit.set_start_mode(f, "the_scar", "hub")
    conv = f.conversation("the_scar")
    assert "trigger" not in conv and list(conv)[:2] == ["id", "entry"]


def test_languages_and_variables(sample: Path):
    f = load(sample)
    edit.add_language(f, "de")
    assert "de" in f.languages()
    with pytest.raises(edit.EditError):
        edit.add_language(f, "deu")
    with pytest.raises(edit.EditError):
        edit.add_language(f, "tr")
    edit.add_variable(f, "raids", "int")
    edit.set_variable(f, "raids", "default", 3)
    edit.set_variable(f, "raids", "type", "bool")
    assert f.variables["raids"] == {"type": "bool"}  # the int default is dropped
    count = edit.rename_variable(f, "told_scar", "knows_scar")
    assert count == 3  # setFlags in the_scar, flags in spare_coin's and after_scar's when
    assert f.node("the_scar", "p_scar_sorry")["effects"]["setFlags"] == ["knows_scar"]
    assert f.conversation("after_scar")["when"]["flags"] == ["knows_scar"]
    assert list(f.variables)[0] == "knows_scar"
    with pytest.raises(edit.EditError):
        edit.rename_variable(f, "got_coin", "mood")
