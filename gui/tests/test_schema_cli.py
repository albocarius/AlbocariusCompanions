import json
import re
from pathlib import Path

import pytest

from albocarius_editor.cli import client
from albocarius_editor.schema.catalog import FIXTURES, FieldSpec, SchemaCatalog

from .conftest import SAMPLE


@pytest.fixture(scope="module")
def catalog() -> SchemaCatalog:
    return SchemaCatalog.bundled()


def cli_or_skip() -> client.Cli:
    try:
        return client.Cli.find()
    except client.CliError as e:
        pytest.skip(str(e))


def test_objects_and_fields(catalog: SchemaCatalog):
    node = {f.name: f for f in catalog.fields("node")}
    assert node["speaker"].allowed == ("npc", "player") and node["speaker"].widget_kind() == "combo"
    assert node["next"].is_list and node["next"].item_type == "string" and node["next"].widget_kind() == "tags"
    assert node["effects"].type == "effects" and catalog.is_object("effects")
    assert node["voiced"].widget_kind() == "check"
    assert catalog.field("variable", "default").widget_kind() == "json"
    assert catalog.field("dialogs", "variables").widget_kind() == "map"
    assert catalog.file_object("lang/tr.json") == "lang"


def test_condition_and_effect_catalogues(catalog: SchemaCatalog):
    names = [c.name for c in catalog.conditions]
    assert {"met", "hired", "hasGold", "var", "all", "any", "not", "role"} <= set(names)
    assert catalog.condition("var").keys == ("var", "min", "max", "eq")
    when = {"hired": True, "flags": ["told_scar"], "var": "mood", "min": 2}
    assert [c.name for c in catalog.conditions_in(when)] == ["hired", "flags", "var"]
    assert [e.name for e in catalog.effects_in({"add": {}, "relation": 2})] == ["relation", "add"]


def test_new_values_follow_the_schema(catalog: SchemaCatalog):
    assert catalog.new_entry(catalog.condition("hasGold")) == {"hasGold": {"who": "player", "min": 1}}
    assert catalog.new_entry(catalog.condition("hired")) == {"hired": True}
    assert catalog.new_entry(catalog.effect("transfer")) == {"transfer": {"from": "player", "to": "player"}}
    assert catalog.new_entry(catalog.effect("relation")) == {"relation": 1}
    assert catalog.new_value(FieldSpec("x", "int", min=5)) == 5


def test_slots_enums_patterns_triggers(catalog: SchemaCatalog):
    assert "intro" in catalog.required_slots and "idle" in catalog.all_slots
    assert catalog.slot_group("intro") == "companion"
    assert catalog.slot_group("player_ask_hire") == "player"
    assert catalog.slot_group("main_hub") == "vanilla"
    assert catalog.slot_group(None) == "triggered"
    assert catalog.enum("speakers") == ["npc", "player"]
    assert catalog.pattern("localId").match("scar_story")
    near = catalog.trigger("nearSettlement")
    assert near and near.polling and [p["name"] for p in near.params] == ["settlement", "radius", "rearmRadius"]
    assert "polling" in catalog.trigger_categories
    assert catalog.trigger("noSuchTrigger") is None


def test_schema_covers_every_field_the_sample_uses(catalog: SchemaCatalog):
    """The editor builds its forms from the export: every key in the sample must be known to it."""
    dialogs = json.loads((SAMPLE / "dialogs.json").read_text())
    known = {o: {f.name for f in catalog.fields(o)} for o in catalog.object_names}
    for line in dialogs["lines"]:
        assert set(line) <= known["line"]
    for conv in dialogs["conversations"]:
        assert set(conv) <= known["conversation"]
        for node in conv["nodes"]:
            assert set(node) <= known["node"]
            assert set(node.get("effects", {})) <= known["effects"]


def test_unsupported_export_version_is_refused(catalog: SchemaCatalog):
    from albocarius_editor.schema.catalog import SchemaError

    with pytest.raises(SchemaError):
        SchemaCatalog({**catalog.schema, "exportVersion": 99}, catalog.triggers_doc, "x")


def test_load_falls_back_to_bundled_without_a_cli():
    class Broken:
        def export(self, what):
            raise client.CliError("no cli")

    assert SchemaCatalog.load(Broken()).source == "bundled"
    assert SchemaCatalog.load(None).source == "bundled"


def test_locate_honours_the_env_var(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    dll = tmp_path / client.DLL
    dll.write_text("")
    monkeypatch.setenv(client.ENV_VAR, str(dll))
    assert client.locate() == dll
    monkeypatch.setenv(client.ENV_VAR, str(tmp_path / "missing.dll"))
    assert client.locate() is None


# ---- against the real CLI (skipped when it is not built) ----

def test_bundled_fixtures_equal_the_cli_exports():
    cli = cli_or_skip()
    for what in ("schema", "triggers"):
        bundled = json.loads((FIXTURES / f"{what}.json").read_text(encoding="utf-8"))
        assert cli.export(what) == bundled, f"fixtures/{what}.json is stale: run the CLI 'export {what}' into it"
    assert SchemaCatalog.load(cli).source == "cli"


def test_validate_json_through_the_cli(sample: Path):
    cli = cli_or_skip()
    assert cli.validate(sample).exit_code == 0
    dialogs = (sample / "dialogs.json").read_text()
    (sample / "dialogs.json").write_text(re.sub(r'"scar_thanks"\n', '"nowhere"\n', dialogs, count=1))
    result = cli.validate(sample)
    assert result.exit_code == 1
    d = next(d for d in result.diagnostics if d["code"] == "unknown_node")
    assert (d["file"], d["conversation"], d["node"]) == ("dialogs.json", "the_scar", "p_scar_sorry")


def test_serve_plays_the_sample(sample: Path):
    cli = cli_or_skip()
    serve = client.ServeClient(cli.path)
    try:
        assert serve.request("hello")["version"] == 1
        assert serve.request("load", root=str(sample.parent))["companions"] == ["test_companion"]
        step = serve.request("start", companion="test_companion", entry="hub", seed=1,
                             state={"companions": {"test_companion": {"hired": True}}})["step"]
        assert any(o["node"] == "p_scar_ask" and o["shown"] for o in step["options"])
        with pytest.raises(client.CliError):
            serve.request("choose", session="nope", conversation="x", node="y")
    finally:
        serve.close()
    assert not serve.running
