import json
import os
from pathlib import Path

import pytest

from albocarius_editor.data import edit, graph, workspace
from albocarius_editor.data.companion import CompanionFolder, CompanionLoadError
from albocarius_editor.data.layout import NodeLayout

from .conftest import TEMPLATE


def snapshot_bytes(folder: Path) -> dict[str, bytes]:
    return {str(p.relative_to(folder)): p.read_bytes() for p in sorted(folder.rglob("*")) if p.is_file()}


def test_open_and_save_without_edits_leaves_the_json_byte_identical(sample: Path, template_copy: Path):
    for path in (sample, template_copy):
        before = snapshot_bytes(path)
        folder = CompanionFolder.load(path)
        assert not folder.dirty
        written = folder.save()
        after = snapshot_bytes(path)
        # Only the layout sidecar is new; every JSON file of the mod is untouched.
        assert written == [path / NodeLayout.FILE_NAME]
        assert {k: v for k, v in after.items() if k != NodeLayout.FILE_NAME} == before
        # A second save writes nothing.
        assert CompanionFolder.load(path).save() == []


def test_reading_the_sample(sample: Path):
    folder = CompanionFolder.load(sample)
    assert folder.id == "test_companion"
    assert [c["id"] for c in folder.conversations] == ["the_scar", "spare_coin", "after_scar", "battle_won", "campfire"]
    assert folder.languages() == ["en", "tr"]
    assert folder.text("p_scar_ask") == "That scar on your arm. Where did it come from?"
    assert "intro_01" in folder.all_ids() and "scar_story" in folder.all_ids()
    assert folder.find_id("scar_story")[0] == "the_scar"
    assert folder.find_id("intro_01")[0] is None
    assert folder.audio_path(folder.line("intro_01")) == sample / "audio" / "intro_01.ogg"


def test_unknown_fields_are_preserved(sample: Path):
    data = json.loads((sample / "dialogs.json").read_text())
    data["futureField"] = {"keep": [1, 2]}
    data["conversations"][0]["nodes"][0]["x_note"] = "author note"
    (sample / "dialogs.json").write_text(json.dumps(data, indent=2) + "\n")
    folder = CompanionFolder.load(sample)
    folder.set_text("p_scar_ask", "Changed.")
    folder.save()
    again = json.loads((sample / "dialogs.json").read_text())
    assert again["futureField"] == {"keep": [1, 2]}
    assert again["conversations"][0]["nodes"][0]["x_note"] == "author note"


def test_text_edit_touches_only_the_lang_file(sample: Path):
    before = snapshot_bytes(sample)
    folder = CompanionFolder.load(sample)
    folder.set_text("scar_story", "A new story.")
    assert folder.dirty
    folder.save()
    after = snapshot_bytes(sample)
    changed = {k for k in after if after[k] != before.get(k)}
    assert changed == {"lang/en.json", NodeLayout.FILE_NAME}
    diff = [l for l in after["lang/en.json"].decode().splitlines() if l not in before["lang/en.json"].decode().splitlines()]
    assert diff == ['    "scar_story": "A new story.",']


def test_missing_or_broken_files_raise(tmp_path: Path, sample: Path):
    with pytest.raises(CompanionLoadError, match="not a folder"):
        CompanionFolder.load(tmp_path / "nope")
    (sample / "dialogs.json").write_text("{ broken")
    with pytest.raises(CompanionLoadError, match="dialogs.json"):
        CompanionFolder.load(sample)
    (sample / "dialogs.json").unlink()
    with pytest.raises(CompanionLoadError, match="dialogs.json is missing"):
        CompanionFolder.load(sample)


def test_snapshot_and_restore(sample: Path):
    folder = CompanionFolder.load(sample)
    state = folder.snapshot()
    edit.delete_node(folder, "the_scar", "scar_story")
    folder.set_text("p_scar_ask", "x", "de")
    assert folder.node("the_scar", "scar_story") is None
    folder.restore(state)
    assert folder.node("the_scar", "scar_story") is not None
    assert "de" not in folder.langs
    assert not any(d.dirty for d in folder.documents())


def test_external_changes_are_detected(sample: Path):
    folder = CompanionFolder.load(sample)
    assert folder.changed_on_disk() == []
    path = sample / "lang" / "en.json"
    st = path.stat()
    path.write_text(path.read_text().replace("Captain", "Chief"))
    os.utime(path, ns=(st.st_atime_ns, st.st_mtime_ns + 10_000_000))
    assert folder.changed_on_disk() == [path]
    (sample / "lang" / "de.json").write_text('{"schemaVersion": 1, "lines": {}}')
    assert sample / "lang" / "de.json" in folder.changed_on_disk()
    folder.save()
    assert folder.changed_on_disk() == []


# ---- edits ----

def test_add_node_creates_its_english_text(sample: Path):
    folder = CompanionFolder.load(sample)
    node = edit.add_node(folder, "the_scar", "npc")
    assert node == {"id": "the_scar_01", "speaker": "npc", "next": ["end"]}
    assert folder.text("the_scar_01") == ""
    player = edit.add_node(folder, "the_scar", "player")
    assert player["id"] == "p_the_scar_01"
    with pytest.raises(edit.EditError, match="already used"):
        edit.add_node(folder, "the_scar", "npc", node_id="intro_01")
    with pytest.raises(edit.EditError, match="not a valid id"):
        edit.add_node(folder, "the_scar", "npc", node_id="hub")


def test_rename_updates_references_lang_files_and_layout(sample: Path):
    folder = CompanionFolder.load(sample)
    folder.layout.resolve(folder.conversation("the_scar"))
    edit.rename_id(folder, "scar_story", "scar_tale")
    conv = folder.conversation("the_scar")
    assert folder.node("the_scar", "p_scar_ask")["next"] == ["scar_tale"]
    assert "scar_tale" in [n["id"] for n in conv["nodes"]]
    assert folder.text("scar_tale", "en").startswith("A border skirmish")
    assert folder.text("scar_story", "en") is None
    # Key position in the lang file is kept.
    keys = list(folder.langs["en"].value["lines"])
    assert keys.index("scar_tale") == keys.index("p_scar_ask") + 1
    assert folder.layout.get("the_scar", "scar_tale") is not None
    assert folder.layout.get("the_scar", "scar_story") is None
    with pytest.raises(edit.EditError, match="already used"):
        edit.rename_id(folder, "scar_tale", "intro_01")


def test_rename_offers_the_audio_file(sample: Path):
    folder = CompanionFolder.load(sample)
    (sample / "audio").mkdir(exist_ok=True)
    (sample / "audio" / "intro_01.ogg").write_bytes(b"ogg")
    (sample / "audio" / "intro_01.xml").write_text("<x/>")
    result = edit.rename_id(folder, "intro_01", "intro_02")
    assert result.audio == (sample / "audio" / "intro_01.ogg", sample / "audio" / "intro_02.ogg")
    edit.rename_audio(result.audio)
    assert (sample / "audio" / "intro_02.ogg").exists() and (sample / "audio" / "intro_02.xml").exists()


def test_delete_node_removes_every_reference(sample: Path):
    folder = CompanionFolder.load(sample)
    edit.delete_node(folder, "spare_coin", "p_coin_offer")
    conv = folder.conversation("spare_coin")
    assert conv["start"] == ["p_coin_ask"]
    assert all(folder.text("p_coin_offer", c) is None for c in folder.languages())


def test_edges_keep_order_and_terminal_rules(sample: Path):
    folder = CompanionFolder.load(sample)
    edit.move_edge(folder, "the_scar", "scar_story", "p_scar_enough", 0)
    assert folder.node("the_scar", "scar_story")["next"] == ["p_scar_enough", "p_scar_sorry"]
    edit.connect(folder, "the_scar", "scar_thanks", graph.END)
    assert folder.node("the_scar", "scar_thanks")["next"] == ["end"]
    edit.connect(folder, "the_scar", "scar_thanks", "scar_shrug")
    assert folder.node("the_scar", "scar_thanks")["next"] == ["scar_shrug"]
    edit.disconnect(folder, "the_scar", "scar_story", "p_scar_sorry")
    assert folder.node("the_scar", "scar_story")["next"] == ["p_scar_enough"]
    edit.connect(folder, "the_scar", graph.HEADER, "p_scar_sorry")
    assert folder.conversation("the_scar")["start"] == ["p_scar_ask", "p_scar_sorry"]
    with pytest.raises(edit.EditError):
        edit.connect(folder, "the_scar", graph.HEADER, graph.HUB)
    with pytest.raises(edit.EditError):
        edit.connect(folder, "the_scar", "scar_story", "nowhere")


def test_copy_paste_nodes(sample: Path):
    folder = CompanionFolder.load(sample)
    payload = edit.copy_nodes(folder, "the_scar", ["scar_story", "p_scar_sorry"])
    new = edit.paste_nodes(folder, "spare_coin", payload)
    assert new == ["scar_story_01", "p_scar_sorry_01"]
    assert folder.node("spare_coin", "scar_story_01")["next"] == ["p_scar_sorry_01"]
    assert folder.node("spare_coin", "p_scar_sorry_01")["next"] == ["end"]
    assert folder.text("scar_story_01", "tr") == folder.text("scar_story", "tr")


def test_conversations_lines_and_variables(sample: Path):
    folder = CompanionFolder.load(sample)
    conv = edit.add_conversation(folder, "new_talk")
    assert conv["entry"] == "hub" and folder.node("new_talk", conv["start"][0])["speaker"] == "player"
    edit.rename_conversation(folder, "new_talk", "old_talk")
    assert folder.conversation("old_talk") is not None
    edit.delete_conversation(folder, "old_talk")
    assert folder.conversation("old_talk") is None
    line = edit.add_line(folder, "greet")  # the sample already has greet_01
    assert line["id"] == "greet_02" and line["slot"] == "greet"
    assert folder.text("greet_02") == ""
    edit.delete_line(folder, "greet_02")
    assert folder.line("greet_02") is None and folder.text("greet_02") is None
    edit.add_variable(folder, "trust", "int", "shared")
    assert folder.variables["trust"] == {"type": "int", "scope": "shared"}
    with pytest.raises(edit.EditError):
        edit.add_variable(folder, "mood")
    edit.remove_variable(folder, "trust")
    assert "trust" not in folder.variables


# ---- workspace ----

def test_workspace_lists_companions_and_copies_the_template(tmp_path: Path, sample: Path):
    root = sample.parent
    (root / "_shared").mkdir()
    (root / "_shared" / "companion.json").write_text("{}")
    (root / "not_a_companion").mkdir()
    assert workspace.companion_folders(root) == [sample]
    created = workspace.new_companion(root, "jeremus", TEMPLATE)
    assert (created / "companion.json").read_bytes() == (TEMPLATE / "companion.json").read_bytes()
    assert workspace.companion_folders(root) == [created, sample]
    for bad in ("jeremus", "Bad-Id", "_shared"):
        with pytest.raises(workspace.WorkspaceError):
            workspace.new_companion(root, bad, TEMPLATE)
    assert workspace.default_template() == TEMPLATE
