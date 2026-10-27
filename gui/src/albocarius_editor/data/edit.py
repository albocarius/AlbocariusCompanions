"""Edits of a companion folder that keep the files consistent: ids, texts in every lang file, references, layout.

Every function changes the :class:`CompanionFolder` in place and raises :class:`EditError` (nothing changed) when the
edit is not allowed. Undo is the caller's: take ``folder.snapshot()`` before, ``folder.restore()`` to go back. No Qt.
"""

from __future__ import annotations

import copy
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from . import graph, ids
from .companion import ENGLISH, CompanionFolder


class EditError(ValueError):
    pass


def _check_new_id(folder: CompanionFolder, new_id: str) -> None:
    if not ids.is_valid_local_id(new_id):
        raise EditError(f"'{new_id}' is not a valid id: use a-z, 0-9 and '_' ('hub' and 'end' are reserved)")
    if new_id in folder.all_ids():
        raise EditError(f"'{new_id}' is already used by another line or node of this companion")


def _rename_key(d: dict, old: str, new: str) -> None:
    """Renames a dict key in place, keeping its position."""
    items = list(d.items())
    d.clear()
    for k, v in items:
        d[new if k == old else k] = v


def _remove_lang_keys(folder: CompanionFolder, local_id: str) -> None:
    for code in folder.langs:
        lines = folder.langs[code].value.get("lines")
        if isinstance(lines, dict):
            lines.pop(local_id, None)


def _require_conversation(folder: CompanionFolder, conversation_id: str) -> dict:
    conv = folder.conversation(conversation_id)
    if conv is None:
        raise EditError(f"no conversation '{conversation_id}'")
    return conv


def _require_node(folder: CompanionFolder, conversation_id: str, node_id: str) -> dict:
    node = folder.node(conversation_id, node_id)
    if node is None:
        raise EditError(f"no node '{node_id}' in conversation '{conversation_id}'")
    return node


# ---- texts ----

def set_text(folder: CompanionFolder, local_id: str, text: str, code: str = ENGLISH) -> None:
    """The text of a line or node in one language (``lang/<code>.json``; the file is created on save when new)."""
    if folder.find_id(local_id) is None:
        raise EditError(f"no line or node '{local_id}'")
    if folder.text(local_id, code) == text:
        raise EditError("the text is unchanged")
    folder.set_text(local_id, text, code)


# ---- conversations ----

def add_conversation(folder: CompanionFolder, conversation_id: str, entry: str | None = "hub") -> dict:
    """A new conversation with one start node (a player option for hub, a companion line otherwise) leading to end."""
    if not ids.is_valid_local_id(conversation_id):
        raise EditError(f"'{conversation_id}' is not a valid conversation id")
    if folder.conversation(conversation_id) is not None:
        raise EditError(f"conversation '{conversation_id}' already exists")
    speaker = "player" if entry == "hub" else "npc"
    start_id = ids.propose_node_id(conversation_id, speaker, folder.all_ids())
    conv: dict[str, Any] = {"id": conversation_id}
    if entry is not None:
        conv["entry"] = entry
    conv["start"] = [start_id]
    conv["nodes"] = [{"id": start_id, "speaker": speaker, "next": ["end"]}]
    folder.dialogs.value.setdefault("conversations", []).append(conv)
    folder.set_text(start_id, "", ENGLISH)
    return conv


def delete_conversation(folder: CompanionFolder, conversation_id: str) -> None:
    conv = _require_conversation(folder, conversation_id)
    for n in conv.get("nodes") or []:
        if isinstance(n, dict) and isinstance(n.get("id"), str):
            _remove_lang_keys(folder, n["id"])
    folder.dialogs.value["conversations"].remove(conv)
    folder.layout.positions.pop(conversation_id, None)
    folder.layout.changed = True


def rename_conversation(folder: CompanionFolder, old_id: str, new_id: str) -> None:
    conv = _require_conversation(folder, old_id)
    if not ids.is_valid_local_id(new_id):
        raise EditError(f"'{new_id}' is not a valid conversation id")
    if folder.conversation(new_id) is not None:
        raise EditError(f"conversation '{new_id}' already exists")
    conv["id"] = new_id
    folder.layout.rename_conversation(old_id, new_id)


# ---- nodes ----

def add_node(folder: CompanionFolder, conversation_id: str, speaker: str, node_id: str | None = None,
             text: str = "") -> dict:
    """A new node (no edges; ``next`` is ``["end"]`` so it is valid on its own) and its English text."""
    conv = _require_conversation(folder, conversation_id)
    if speaker not in ("npc", "player"):
        raise EditError(f"speaker must be 'npc' or 'player', not '{speaker}'")
    node_id = node_id or ids.propose_node_id(conversation_id, speaker, folder.all_ids())
    _check_new_id(folder, node_id)
    node = {"id": node_id, "speaker": speaker, "next": ["end"]}
    conv.setdefault("nodes", []).append(node)
    folder.set_text(node_id, text, ENGLISH)
    return node


def delete_node(folder: CompanionFolder, conversation_id: str, node_id: str) -> None:
    """Removes the node, every start/next reference to it, its texts in every language and its layout entry."""
    conv = _require_conversation(folder, conversation_id)
    node = _require_node(folder, conversation_id, node_id)
    conv["nodes"].remove(node)
    if isinstance(conv.get("start"), list):
        conv["start"] = [s for s in conv["start"] if s != node_id]
    for n in conv.get("nodes") or []:
        if isinstance(n, dict) and isinstance(n.get("next"), list) and node_id in n["next"]:
            n["next"] = [x for x in n["next"] if x != node_id]
    _remove_lang_keys(folder, node_id)
    folder.layout.remove(conversation_id, node_id)


@dataclass
class RenameResult:
    """A rename's side effects the UI may offer to the user: the audio file to rename along (old, new), if any."""

    audio: tuple[Path, Path] | None = None


def rename_id(folder: CompanionFolder, old_id: str, new_id: str) -> RenameResult:
    """Renames a line or node id: the item, start/next references, keys in every lang file, the layout entry.

    The audio file is not touched: when the item uses the default path and that file exists, the result names the
    rename for the UI to offer (:func:`rename_audio`)."""
    where = folder.find_id(old_id)
    if where is None:
        raise EditError(f"no line or node '{old_id}'")
    _check_new_id(folder, new_id)
    conversation_id, item = where
    old_audio = folder.audio_path(item)
    item["id"] = new_id
    if conversation_id is not None:
        conv = folder.conversation(conversation_id)
        assert conv is not None
        if isinstance(conv.get("start"), list):
            conv["start"] = [new_id if s == old_id else s for s in conv["start"]]
        for n in conv.get("nodes") or []:
            if isinstance(n, dict) and isinstance(n.get("next"), list):
                n["next"] = [new_id if x == old_id else x for x in n["next"]]
        folder.layout.rename(conversation_id, old_id, new_id)
    for code in folder.langs:
        lines = folder.langs[code].value.get("lines")
        if isinstance(lines, dict) and old_id in lines:
            _rename_key(lines, old_id, new_id)
    result = RenameResult()
    if not item.get("audio") and old_audio.exists():
        result.audio = (old_audio, folder.audio_path(item))
    return result


def rename_audio(paths: tuple[Path, Path]) -> None:
    """Renames an audio file and its Rhubarb lipsync .xml next to it (docs/SCHEMA.md folder layout)."""
    old, new = paths
    if new.exists():
        raise EditError(f"{new.name} already exists")
    old.rename(new)
    old_xml, new_xml = old.with_suffix(".xml"), new.with_suffix(".xml")
    if old_xml.exists() and not new_xml.exists():
        old_xml.rename(new_xml)


def copy_nodes(folder: CompanionFolder, conversation_id: str, node_ids: list[str]) -> dict:
    """A clipboard payload for nodes: the node objects and their texts in every language (edges kept as written)."""
    nodes = [copy.deepcopy(_require_node(folder, conversation_id, n)) for n in node_ids]
    texts = {code: {n: folder.text(n, code) for n in node_ids if folder.text(n, code) is not None}
             for code in folder.languages()}
    return {"kind": "albocarius-nodes", "companion": folder.id, "nodes": nodes, "texts": texts}


def paste_nodes(folder: CompanionFolder, conversation_id: str, payload: dict) -> list[str]:
    """Pastes copied nodes with fresh ids; ``next`` links between pasted nodes follow the new ids, links to anything
    else are dropped (``["end"]`` when nothing is left). Returns the new ids in order."""
    if payload.get("kind") != "albocarius-nodes":
        raise EditError("the clipboard holds no nodes")
    conv = _require_conversation(folder, conversation_id)
    taken = folder.all_ids()
    mapping: dict[str, str] = {}
    for node in payload["nodes"]:
        new = ids.propose(node["id"], taken)
        taken.add(new)
        mapping[node["id"]] = new
    for node in payload["nodes"]:
        node = copy.deepcopy(node)
        old = node["id"]
        node["id"] = mapping[old]
        nxt = [mapping.get(x, x) for x in node.get("next") or [] if x in mapping or x in graph.TERMINALS]
        node["next"] = nxt or ["end"]
        conv.setdefault("nodes", []).append(node)
        for code, texts in payload.get("texts", {}).items():
            if old in texts:
                folder.set_text(node["id"], texts[old], code)
    return [mapping[n["id"]] for n in payload["nodes"]]


# ---- edges ----

def _source_list(folder: CompanionFolder, conversation_id: str, source: str) -> list:
    conv = _require_conversation(folder, conversation_id)
    if source == graph.HEADER:
        if not isinstance(conv.get("start"), list):
            conv["start"] = []
        return conv["start"]
    node = _require_node(folder, conversation_id, source)
    if not isinstance(node.get("next"), list):
        node["next"] = []
    return node["next"]


def _target_name(target: str) -> str:
    return {v: k for k, v in graph.TERMINALS.items()}.get(target, target)


def connect(folder: CompanionFolder, conversation_id: str, source: str, target: str, index: int | None = None) -> None:
    """Adds ``target`` (a node id, or graph.HUB / graph.END) to the source's ``next`` (or ``start`` for the header).

    A hub/end target replaces the whole list (they must be the only entry); a node target replaces a lone hub/end."""
    lst = _source_list(folder, conversation_id, source)
    name = _target_name(target)
    if name not in graph.TERMINALS:
        _require_node(folder, conversation_id, name)
    if name in lst:
        raise EditError(f"'{source}' already continues with '{name}'")
    if name in graph.TERMINALS:
        if source == graph.HEADER:
            raise EditError("a conversation cannot start with hub or end")
        lst[:] = [name]
        return
    lst[:] = [x for x in lst if x not in graph.TERMINALS]
    lst.insert(len(lst) if index is None else index, name)


def disconnect(folder: CompanionFolder, conversation_id: str, source: str, target: str) -> None:
    lst = _source_list(folder, conversation_id, source)
    name = _target_name(target)
    if name not in lst:
        raise EditError(f"'{source}' does not continue with '{name}'")
    lst.remove(name)


def move_edge(folder: CompanionFolder, conversation_id: str, source: str, target: str, new_index: int) -> None:
    """Reorders one entry of a ``next``/``start`` list (menu and reply order are meaningful)."""
    lst = _source_list(folder, conversation_id, source)
    name = _target_name(target)
    if name not in lst:
        raise EditError(f"'{source}' does not continue with '{name}'")
    lst.remove(name)
    lst.insert(max(0, min(new_index, len(lst))), name)


# ---- lines ----

def add_line(folder: CompanionFolder, slot: str | None, line_id: str | None = None, text: str = "") -> dict:
    line_id = line_id or ids.propose(slot or "line", folder.all_ids())
    _check_new_id(folder, line_id)
    line: dict[str, Any] = {"id": line_id}
    if slot is not None:
        line["slot"] = slot
    folder.lines.append(line)
    folder.set_text(line_id, text, ENGLISH)
    return line


def delete_line(folder: CompanionFolder, line_id: str) -> None:
    line = folder.line(line_id)
    if line is None:
        raise EditError(f"no line '{line_id}'")
    folder.lines.remove(line)
    _remove_lang_keys(folder, line_id)


# ---- variables ----

def add_variable(folder: CompanionFolder, name: str, var_type: str = "bool", scope: str | None = None) -> dict:
    if not ids.LOCAL_ID.match(name) or name == "mood":
        raise EditError(f"'{name}' is not a valid variable name ('mood' is built in)")
    variables = folder.dialogs.value.setdefault("variables", {})
    if name in variables:
        raise EditError(f"variable '{name}' already exists")
    decl: dict[str, Any] = {"type": var_type}
    if scope is not None:
        decl["scope"] = scope
    variables[name] = decl
    return decl


def remove_variable(folder: CompanionFolder, name: str) -> None:
    variables = folder.dialogs.value.get("variables")
    if not isinstance(variables, dict) or name not in variables:
        raise EditError(f"no variable '{name}'")
    del variables[name]
