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


# ---- any value of a line, conversation or node (the schema-driven forms) ----

Ref = tuple  # ("line", id) | ("conversation", conversation id) | ("node", conversation id, node id)
Path_ = list  # keys and list indexes from the item down to a value


def resolve(folder: CompanionFolder, ref: Ref) -> dict:
    kind = ref[0]
    if kind == "line":
        item = folder.line(ref[1])
    elif kind == "conversation":
        item = folder.conversation(ref[1])
    elif kind == "node":
        item = folder.node(ref[1], ref[2])
    else:
        raise EditError(f"unknown item kind '{kind}'")
    if item is None:
        raise EditError(f"no {kind} '{ref[-1]}'")
    return item


def _walk(value: Any, path: Path_, create: bool = False) -> Any:
    for i, step in enumerate(path):
        if isinstance(value, dict) and isinstance(step, str):
            if step not in value:
                if not create:
                    raise EditError(f"nothing at {'/'.join(map(str, path[:i + 1]))}")
                value[step] = [] if i + 1 < len(path) and isinstance(path[i + 1], int) else {}
            value = value[step]
        elif isinstance(value, list) and isinstance(step, int) and 0 <= step < len(value):
            value = value[step]
        else:
            raise EditError(f"nothing at {'/'.join(map(str, path[:i + 1]))}")
    return value


def get_at(folder: CompanionFolder, ref: Ref, path: Path_, default: Any = None) -> Any:
    try:
        return _walk(resolve(folder, ref), path)
    except EditError:
        return default


def set_at(folder: CompanionFolder, ref: Ref, path: Path_, value: Any) -> None:
    """Sets one value (creating the objects on the way). The id field is renamed with :func:`rename_id` instead."""
    if not path:
        raise EditError("no field given")
    item = resolve(folder, ref)
    if path == ["id"]:
        raise EditError("ids are renamed, not set")
    parent = _walk(item, path[:-1], create=True)
    key = path[-1]
    if isinstance(parent, dict):
        if key in parent and parent[key] == value and type(parent[key]) is type(value):
            raise EditError("the value is unchanged")
        parent[key] = value
    elif isinstance(parent, list) and isinstance(key, int) and 0 <= key < len(parent):
        parent[key] = value
    else:
        raise EditError(f"cannot set {'/'.join(map(str, path))}")


def remove_at(folder: CompanionFolder, ref: Ref, path: Path_) -> None:
    """Removes a key or list element; objects and lists left empty on the way up are removed too (an emptied ``when``
    disappears, so the file is as before the first condition was added)."""
    item = resolve(folder, ref)
    if not path or path == ["id"]:
        raise EditError("this field cannot be removed")
    parent = _walk(item, path[:-1])
    key = path[-1]
    if isinstance(parent, dict) and key in parent:
        del parent[key]
    elif isinstance(parent, list) and isinstance(key, int) and 0 <= key < len(parent):
        del parent[key]
    else:
        raise EditError(f"nothing at {'/'.join(map(str, path))}")
    for depth in range(len(path) - 1, 0, -1):
        container = _walk(item, path[:depth])
        if container in ({}, []):
            holder = _walk(item, path[:depth - 1])
            del holder[path[depth - 1]]
        else:
            break


def remove_keys(folder: CompanionFolder, ref: Ref, path: Path_, keys: list[str]) -> None:
    """Removes several keys of the object at ``path`` (a condition or effect with more than one key) in one step;
    emptied containers are pruned as in :func:`remove_at`."""
    item = resolve(folder, ref)
    target = _walk(item, path) if path else item
    present = [k for k in keys if isinstance(target, dict) and k in target]
    if not present:
        raise EditError("nothing to remove")
    for k in present[:-1]:
        del target[k]
    remove_at(folder, ref, [*path, present[-1]])


def append_at(folder: CompanionFolder, ref: Ref, path: Path_, value: Any) -> int:
    """Appends to the list at ``path`` (created when absent); returns the new element's index."""
    item = resolve(folder, ref)
    lst = _walk(item, path, create=True) if path else None
    if not isinstance(lst, list):
        if isinstance(lst, dict) and not lst:  # created as an object by _walk: replace with a list
            parent = _walk(item, path[:-1])
            parent[path[-1]] = lst = []
        else:
            raise EditError(f"{'/'.join(map(str, path))} is not a list")
    lst.append(value)
    return len(lst) - 1


def merge_at(folder: CompanionFolder, ref: Ref, path: Path_, values: dict[str, Any]) -> None:
    """Adds keys to the object at ``path`` (created when absent): a new condition or effect writes its keys at once."""
    item = resolve(folder, ref)
    target = _walk(item, path, create=True) if path else item
    if not isinstance(target, dict):
        raise EditError(f"{'/'.join(map(str, path))} is not an object")
    clash = [k for k in values if k in target]
    if clash:
        raise EditError(f"'{clash[0]}' is already set here")
    target.update(values)


def replace_keys(folder: CompanionFolder, ref: Ref, path: Path_, remove: list[str], values: dict[str, Any]) -> None:
    """Removes some keys of an object and sets others in one step (e.g. a transfer switching from gold to an item)."""
    item = resolve(folder, ref)
    target = _walk(item, path, create=True) if path else item
    if not isinstance(target, dict):
        raise EditError(f"{'/'.join(map(str, path))} is not an object")
    for k in remove:
        target.pop(k, None)
    target.update(values)


# ---- texts ----

def set_text(folder: CompanionFolder, local_id: str, text: str, code: str = ENGLISH) -> None:
    """The text of a line or node in one language (``lang/<code>.json``; the file is created on save when new)."""
    if folder.find_id(local_id) is None:
        raise EditError(f"no line or node '{local_id}'")
    if folder.text(local_id, code) == text:
        raise EditError("the text is unchanged")
    folder.set_text(local_id, text, code)


def add_language(folder: CompanionFolder, code: str, pattern: str = r"^[a-z]{2}$") -> None:
    """A new ``lang/<code>.json`` (written on save) with no texts yet."""
    import re

    if not re.match(pattern, code):
        raise EditError(f"'{code}' is not a language code (two lowercase letters, e.g. de)")
    if code in folder.langs:
        raise EditError(f"lang/{code}.json already exists")
    folder.lang_lines(code)


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


def duplicate_conversation(folder: CompanionFolder, conversation_id: str, new_id: str | None = None) -> str:
    """A copy right after the original: every node gets a fresh id (texts copied in every language), links inside
    follow; the layout is copied."""
    conv = _require_conversation(folder, conversation_id)
    new_id = new_id or ids.propose(conversation_id + "_copy", {c.get("id") for c in folder.conversations})
    if not ids.is_valid_local_id(new_id):
        raise EditError(f"'{new_id}' is not a valid conversation id")
    if folder.conversation(new_id) is not None:
        raise EditError(f"conversation '{new_id}' already exists")
    taken = folder.all_ids()
    mapping: dict[str, str] = {}
    for n in conv.get("nodes") or []:
        if isinstance(n, dict) and isinstance(n.get("id"), str):
            mapping[n["id"]] = ids.propose(n["id"], taken)
            taken.add(mapping[n["id"]])
    copy_ = copy.deepcopy(conv)
    copy_["id"] = new_id
    if isinstance(copy_.get("start"), list):
        copy_["start"] = [mapping.get(x, x) for x in copy_["start"]]
    for n in copy_.get("nodes") or []:
        if isinstance(n, dict) and isinstance(n.get("id"), str):
            old = n["id"]
            n["id"] = mapping[old]
            if isinstance(n.get("next"), list):
                n["next"] = [mapping.get(x, x) for x in n["next"]]
            for code in folder.languages():
                text = folder.text(old, code)
                if text is not None:
                    folder.set_text(n["id"], text, code)
    convs = folder.dialogs.value["conversations"]
    convs.insert(convs.index(conv) + 1, copy_)
    for key, pos in folder.layout.positions.get(conversation_id, {}).items():
        folder.layout.set(new_id, mapping.get(key, key), pos[0], pos[1])
    return new_id


def move_conversation(folder: CompanionFolder, conversation_id: str, delta: int) -> None:
    """Moves a conversation up (−1) or down (+1) in the file: the hub menu order for equal priorities."""
    conv = _require_conversation(folder, conversation_id)
    convs = folder.dialogs.value["conversations"]
    i = convs.index(conv)
    j = i + delta
    if not 0 <= j < len(convs):
        raise EditError("already at the " + ("top" if delta < 0 else "bottom"))
    convs[i], convs[j] = convs[j], convs[i]


def set_start_mode(folder: CompanionFolder, conversation_id: str, mode: str, trigger_name: str | None = None) -> None:
    """How a conversation starts: ``hub`` / ``greeting`` (an ``entry``) or ``trigger`` (a ``trigger`` object with
    ``on``). Switching removes the other key."""
    conv = _require_conversation(folder, conversation_id)
    if mode in ("hub", "greeting"):
        if conv.get("entry") == mode and "trigger" not in conv:
            raise EditError(f"the conversation already starts from the {mode}")
        conv.pop("trigger", None)
        items = list(conv.items())
        if "entry" not in conv:  # keep the sample's key order: entry right after id
            conv.clear()
            for k, v in items:
                conv[k] = v
                if k == "id":
                    conv["entry"] = mode
        conv["entry"] = mode
    elif mode == "trigger":
        if not trigger_name:
            raise EditError("choose a trigger")
        if isinstance(conv.get("trigger"), dict) and "entry" not in conv:
            raise EditError("the conversation is already triggered")
        conv.pop("entry", None)
        conv["trigger"] = {"on": trigger_name}
    else:
        raise EditError(f"unknown start '{mode}'")


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


def _speaker(folder: CompanionFolder, conversation_id: str, node_id: str) -> str | None:
    node = folder.node(conversation_id, node_id)
    return node.get("speaker") if node is not None else None


def link_problem(folder: CompanionFolder, conversation_id: str, source: str, target: str) -> str | None:
    """Why ``source`` may not continue with ``target`` (docs/SCHEMA.md "Conversations"), or None when it may.

    A ``next`` lists either player options (all shown ones are offered) or companion lines (one is said); a player
    option is never followed by another; a hub conversation starts with player options, a greeting or triggered one
    with companion lines; hub/end are never a start."""
    conv = folder.conversation(conversation_id)
    if conv is None:
        return f"no conversation '{conversation_id}'"
    name = _target_name(target)
    if source != graph.HEADER and folder.node(conversation_id, source) is None:
        return f"no node '{source}' in conversation '{conversation_id}'"
    if name not in graph.TERMINALS and folder.node(conversation_id, name) is None:
        return f"no node '{name}' in conversation '{conversation_id}'"
    current = conv.get("start") if source == graph.HEADER else folder.node(conversation_id, source).get("next")
    current = [x for x in current if isinstance(x, str)] if isinstance(current, list) else []
    if name in current:
        return f"'{source}' already continues with '{name}'"
    if name in graph.TERMINALS:
        return "a conversation cannot start with hub or end" if source == graph.HEADER else None
    speaker = _speaker(folder, conversation_id, name)
    if source == graph.HEADER:
        if conv.get("trigger") is not None or conv.get("entry") == "greeting":
            if speaker != "npc":
                kind = "triggered" if conv.get("trigger") is not None else "greeting"
                return f"a {kind} conversation starts with companion lines, not player options"
        elif conv.get("entry") == "hub" and speaker != "player":
            return "a hub conversation starts with player options (the menu entries), not companion lines"
    elif _speaker(folder, conversation_id, source) == "player" and speaker == "player":
        return "a player option is never followed by another player option: add a companion reply between them"
    others = {_speaker(folder, conversation_id, x) for x in current if x not in graph.TERMINALS}
    if others and speaker not in others:
        has = "player options" if "player" in others else "companion lines"
        return (f"'{source}' already continues with {has}; one list holds either player options (all shown) or "
                f"companion lines (one is said), never both")
    return None


def connect(folder: CompanionFolder, conversation_id: str, source: str, target: str, index: int | None = None) -> None:
    """Adds ``target`` (a node id, or graph.HUB / graph.END) to the source's ``next`` (or ``start`` for the header).

    A hub/end target replaces the whole list (they must be the only entry); a node target replaces a lone hub/end.
    Links the schema forbids are refused (:func:`link_problem`)."""
    problem = link_problem(folder, conversation_id, source, target)
    if problem is not None:
        raise EditError(problem)
    lst = _source_list(folder, conversation_id, source)
    name = _target_name(target)
    if name in graph.TERMINALS:
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


VARIABLE_LIST_KEYS = ("flags", "notFlags", "setFlags", "clearFlags")  # docs/SCHEMA.md "Variables": references
VARIABLE_MAP_KEYS = ("set", "add")


def _rename_refs(value: Any, old: str, new: str) -> int:
    """Renames variable references in a when/effects/trigger tree; returns how many."""
    count = 0
    if isinstance(value, dict):
        for k in list(value):
            v = value[k]
            if k in VARIABLE_LIST_KEYS and isinstance(v, list):
                for i, x in enumerate(v):
                    if x == old:
                        v[i] = new
                        count += 1
            elif k == "var" and v == old:
                value[k] = new
                count += 1
            elif k in VARIABLE_MAP_KEYS and isinstance(v, dict) and old in v:
                _rename_key(v, old, new)
                count += 1
            else:
                count += _rename_refs(v, old, new)
    elif isinstance(value, list):
        for x in value:
            count += _rename_refs(x, old, new)
    return count


def rename_variable(folder: CompanionFolder, old: str, new: str) -> int:
    """Renames a declared variable and every reference to it in this companion (flags, notFlags, var, setFlags,
    clearFlags, set, add, and a varCrossed trigger's ``params.var``). Saved values of the old name are lost in
    existing campaigns (docs/SCHEMA.md "Variables"). Returns the number of references renamed."""
    variables = folder.dialogs.value.get("variables")
    if not isinstance(variables, dict) or old not in variables:
        raise EditError(f"no variable '{old}'")
    if not ids.LOCAL_ID.match(new) or new == "mood":
        raise EditError(f"'{new}' is not a valid variable name ('mood' is built in)")
    if new in variables:
        raise EditError(f"variable '{new}' already exists")
    _rename_key(variables, old, new)
    return _rename_refs(folder.dialogs.value.get("lines"), old, new) + _rename_refs(folder.conversations, old, new)


def set_variable(folder: CompanionFolder, name: str, field: str, value: Any) -> None:
    """One field of a declaration (type, scope, default). A new type drops a default of the old type."""
    variables = folder.dialogs.value.get("variables")
    if not isinstance(variables, dict) or not isinstance(variables.get(name), dict):
        raise EditError(f"no variable '{name}'")
    decl = variables[name]
    if decl.get(field) == value and field in decl:
        raise EditError("the value is unchanged")
    decl[field] = value
    if field == "type":
        decl.pop("default", None)


def remove_variable(folder: CompanionFolder, name: str) -> None:
    variables = folder.dialogs.value.get("variables")
    if not isinstance(variables, dict) or name not in variables:
        raise EditError(f"no variable '{name}'")
    del variables[name]
