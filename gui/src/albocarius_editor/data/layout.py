"""The layout sidecar ``dialogs.layout.json``: canvas positions per conversation and item. The mod never reads it.

Format::

    {
      "conversations": {
        "the_scar": { "@header": [0, 0], "p_scar_ask": [360, 0], "@end": [1440, 0] }
      }
    }

Keys are the item keys of :mod:`graph`. Entries whose conversation or node no longer exists are *stale*: reported by
:meth:`NodeLayout.stale` (diagnostics "layout: node not found") and dropped on the next write. No Qt.
"""

from __future__ import annotations

import json
from pathlib import Path

from . import graph as graph_mod
from . import jsonfmt

Positions = dict[str, dict[str, list[float]]]


class NodeLayout:
    FILE_NAME = "dialogs.layout.json"

    def __init__(self, path: Path, positions: Positions | None = None, disk_text: str | None = None,
                 error: str | None = None):
        self.path = path
        self.positions: Positions = positions or {}
        self.disk_text = disk_text
        # A sidecar that could not be read is ignored (auto layout) and rewritten on save; the reason is kept for diagnostics.
        self.error = error
        # Changed by the user since the last read/write. Auto-layout positions alone do not make the folder unsaved.
        self.changed = False

    @classmethod
    def read(cls, path: Path) -> NodeLayout:
        try:
            text = path.read_bytes().decode("utf-8")
        except FileNotFoundError:
            return cls(path)
        except UnicodeDecodeError as e:
            return cls(path, error=f"not UTF-8 ({e.reason})")
        try:
            value, _ = jsonfmt.loads(text)
        except jsonfmt.JsonSyntaxError as e:
            return cls(path, disk_text=text, error=str(e))
        positions: Positions = {}
        conversations = value.get("conversations") if isinstance(value, dict) else None
        for conv_id, items in (conversations or {}).items():
            if not isinstance(items, dict):
                continue
            for key, pos in items.items():
                if (isinstance(pos, list) and len(pos) == 2 and all(isinstance(v, (int, float)) and not isinstance(v, bool) for v in pos)):
                    positions.setdefault(conv_id, {})[key] = [float(pos[0]), float(pos[1])]
        return cls(path, positions, text)

    # ---- positions ----

    def get(self, conversation_id: str, key: str) -> tuple[float, float] | None:
        pos = self.positions.get(conversation_id, {}).get(key)
        return (pos[0], pos[1]) if pos else None

    def set(self, conversation_id: str, key: str, x: float, y: float) -> None:
        self.positions.setdefault(conversation_id, {})[key] = [float(x), float(y)]
        self.changed = True

    def rename(self, conversation_id: str, old_key: str, new_key: str) -> None:
        items = self.positions.get(conversation_id)
        if items and old_key in items:
            items[new_key] = items.pop(old_key)
            self.changed = True

    def rename_conversation(self, old_id: str, new_id: str) -> None:
        if old_id in self.positions:
            self.positions[new_id] = self.positions.pop(old_id)
            self.changed = True

    def remove(self, conversation_id: str, key: str) -> None:
        if self.positions.get(conversation_id, {}).pop(key, None) is not None:
            self.changed = True

    def resolve(self, conversation: dict, heights: dict[str, float] | None = None) -> dict[str, tuple[float, float]]:
        """Positions for every item of a conversation: stored ones, auto layout for the rest (stored on the layout).
        ``heights`` (item key → height) lets the auto layout stack items without overlap."""
        g = graph_mod.build(conversation)
        auto = graph_mod.auto_layout(g, heights)
        result: dict[str, tuple[float, float]] = {}
        for key in g.keys():
            stored = self.get(g.conversation_id, key)
            if stored is None:
                stored = auto[key]
                self.positions.setdefault(g.conversation_id, {})[key] = [stored[0], stored[1]]
            result[key] = stored
        return result

    # ---- stale entries / writing ----

    def stale(self, node_ids_by_conversation: dict[str, list[str]]) -> list[tuple[str, str]]:
        """(conversation, key) entries that name nothing that exists."""
        result = []
        for conv_id, items in self.positions.items():
            ids = node_ids_by_conversation.get(conv_id)
            for key in items:
                if ids is None or (key not in ids and not key.startswith("@")):
                    result.append((conv_id, key))
        return result

    def _document(self, node_ids_by_conversation: dict[str, list[str]]) -> dict:
        stale = set(self.stale(node_ids_by_conversation))
        conversations = {}
        for conv_id in node_ids_by_conversation:
            items = {k: _compact(v) for k, v in self.positions.get(conv_id, {}).items() if (conv_id, k) not in stale}
            if items:
                conversations[conv_id] = items
        return {"conversations": conversations}

    def text(self, node_ids_by_conversation: dict[str, list[str]]) -> str:
        doc = self._document(node_ids_by_conversation)
        # One line per item keeps diffs readable.
        lines = ["{", '  "conversations": {']
        conv_parts = []
        for conv_id, items in doc["conversations"].items():
            item_lines = [f"      {json.dumps(k)}: [{_num(v[0])}, {_num(v[1])}]" for k, v in items.items()]
            conv_parts.append(f"    {json.dumps(conv_id)}: {{\n" + ",\n".join(item_lines) + "\n    }")
        if conv_parts:
            lines.append(",\n".join(conv_parts))
            lines.append("  }")
        else:
            lines[-1] = '  "conversations": {}'
        lines.append("}")
        return "\n".join(lines) + "\n"

    def dirty(self, node_ids_by_conversation: dict[str, list[str]]) -> bool:
        """Unsaved user changes. (A save may still write the file: auto positions, dropped stale entries.)"""
        return self.changed and self.text(node_ids_by_conversation) != self.disk_text

    def write(self, node_ids_by_conversation: dict[str, list[str]]) -> bool:
        from .companion import atomic_write  # local: companion imports this module

        text = self.text(node_ids_by_conversation)
        self.changed = False
        if text == self.disk_text:
            return False
        atomic_write(self.path, text)
        self.disk_text = text
        self.error = None
        return True


def _compact(pos: list[float]) -> list[float]:
    return [round(pos[0], 1), round(pos[1], 1)]


def _num(v: float) -> str:
    return str(int(v)) if float(v).is_integer() else repr(v)
