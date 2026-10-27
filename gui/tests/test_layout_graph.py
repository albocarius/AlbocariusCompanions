import json
from pathlib import Path

from albocarius_editor.data import graph
from albocarius_editor.data.companion import CompanionFolder
from albocarius_editor.data.layout import NodeLayout


def test_graph_of_a_conversation(sample: Path):
    folder = CompanionFolder.load(sample)
    g = graph.build(folder.conversation("spare_coin"))
    assert g.keys() == [graph.HEADER, "p_coin_offer", "p_coin_ask", "coin_refuse", "coin_fine", "coin_thanks", graph.HUB, graph.END]
    assert graph.Edge(graph.HEADER, "p_coin_ask", 1) in g.edges
    assert graph.Edge("p_coin_ask", "coin_fine", 1) in g.edges
    assert graph.Edge("coin_refuse", graph.HUB, 0) in g.edges


def test_missing_targets_are_marked():
    g = graph.build({"id": "c", "start": ["a"], "nodes": [{"id": "a", "speaker": "npc", "next": ["ghost"]}]})
    assert g.edges[-1] == graph.Edge("a", "ghost", 0, missing=True)


def test_auto_layout_goes_left_to_right_by_depth(sample: Path):
    folder = CompanionFolder.load(sample)
    g = graph.build(folder.conversation("the_scar"))
    d = graph.depths(g)
    assert d[graph.HEADER] == 0 and d["p_scar_ask"] == 1 and d["scar_story"] == 2
    assert d["p_scar_sorry"] == d["p_scar_enough"] == 3
    assert d[graph.HUB] == max(d.values())
    pos = graph.auto_layout(g)
    assert pos["p_scar_ask"][0] < pos["scar_story"][0] < pos["p_scar_sorry"][0]
    assert pos["p_scar_sorry"][1] != pos["p_scar_enough"][1]


def test_unreachable_nodes_still_get_a_column():
    g = graph.build({"id": "c", "start": ["a"], "nodes": [{"id": "a", "next": ["end"]}, {"id": "lost", "next": ["end"]}]})
    d = graph.depths(g)
    assert d["lost"] > d["a"] and d[graph.END] > d["lost"]


def test_sidecar_round_trip_and_stale_entries(sample: Path):
    folder = CompanionFolder.load(sample)
    folder.layout.resolve(folder.conversation("the_scar"))
    folder.layout.set("the_scar", "scar_story", 12.5, -40)
    folder.layout.set("the_scar", "removed_node", 1, 1)
    folder.layout.set("gone_conversation", "x", 1, 1)
    assert folder.dirty
    assert set(folder.layout.stale(folder.node_ids_by_conversation())) == {("the_scar", "removed_node"), ("gone_conversation", "x")}
    folder.save()
    data = json.loads((sample / NodeLayout.FILE_NAME).read_text())
    assert data["conversations"]["the_scar"]["scar_story"] == [12.5, -40]
    assert "removed_node" not in data["conversations"]["the_scar"]
    assert "gone_conversation" not in data["conversations"]

    again = CompanionFolder.load(sample)
    assert again.layout.get("the_scar", "scar_story") == (12.5, -40.0)
    assert again.layout.stale(again.node_ids_by_conversation()) == []
    assert not again.dirty


def test_auto_positions_alone_do_not_make_the_folder_unsaved(sample: Path):
    folder = CompanionFolder.load(sample)
    for conv in folder.conversations:
        folder.layout.resolve(conv)
    assert not folder.dirty


def test_broken_sidecar_is_reported_and_replaced(sample: Path):
    (sample / NodeLayout.FILE_NAME).write_text("{ nope")
    folder = CompanionFolder.load(sample)
    assert folder.layout.error and "line 1" in folder.layout.error
    folder.layout.resolve(folder.conversation("the_scar"))
    folder.save()
    assert json.loads((sample / NodeLayout.FILE_NAME).read_text())["conversations"]["the_scar"]
