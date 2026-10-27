"""One companion folder in memory: companion.json, dialogs.json, lang/<code>.json and the layout sidecar.

The JSON files are the only source of truth. Values are plain dicts/lists exactly as parsed (unknown fields included);
``save`` writes back only the files whose text changed, through :mod:`jsonfmt` so untouched parts keep their bytes.
No Qt here.
"""

from __future__ import annotations

import copy
import os
import tempfile
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from . import jsonfmt
from .layout import NodeLayout

COMPANION_FILE = "companion.json"
DIALOGS_FILE = "dialogs.json"
LANG_DIR = "lang"
ENGLISH = "en"


class CompanionLoadError(Exception):
    """A file of the folder is missing or is not valid JSON; the folder cannot be edited until it is fixed."""


@dataclass
class JsonDocument:
    """One JSON file: its value, how it was written, and the text last read from / written to disk."""

    path: Path
    value: Any
    layout: jsonfmt.Layout = field(default_factory=jsonfmt.Layout)
    disk_text: str | None = None  # None: the file does not exist yet

    @classmethod
    def read(cls, path: Path) -> JsonDocument:
        # Raw bytes, not read_text: that would translate CRLF, and the line ending is kept on save.
        try:
            text = path.read_bytes().decode("utf-8")
        except FileNotFoundError:
            raise CompanionLoadError(f"{path.name} is missing") from None
        except UnicodeDecodeError as e:
            raise CompanionLoadError(f"{path.name} is not UTF-8 ({e.reason})") from None
        try:
            value, layout = jsonfmt.loads(text)
        except jsonfmt.JsonSyntaxError as e:
            raise CompanionLoadError(f"{path.name}: {e}") from None
        return cls(path=path, value=value, layout=layout, disk_text=text)

    def text(self) -> str:
        return jsonfmt.dumps(self.value, self.layout)

    @property
    def dirty(self) -> bool:
        return self.disk_text != self.text()

    def write(self) -> bool:
        """Writes the file when its text changed; True when written."""
        text = self.text()
        if text == self.disk_text:
            return False
        atomic_write(self.path, text)
        self.disk_text = text
        return True


def atomic_write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, tmp = tempfile.mkstemp(prefix=path.name + ".", suffix=".tmp", dir=path.parent)
    try:
        with os.fdopen(fd, "wb") as f:
            f.write(text.encode("utf-8"))
        os.replace(tmp, path)
    except BaseException:
        if os.path.exists(tmp):
            os.unlink(tmp)
        raise


def _stamp(path: Path) -> tuple[int, int] | None:
    try:
        st = path.stat()
    except FileNotFoundError:
        return None
    return st.st_mtime_ns, st.st_size


class CompanionFolder:
    """A companion folder loaded for editing. The companion id is the folder name."""

    def __init__(self, path: Path, companion: JsonDocument, dialogs: JsonDocument, langs: dict[str, JsonDocument],
                 layout: NodeLayout):
        self.path = path
        self.companion = companion
        self.dialogs = dialogs
        self.langs = langs
        self.layout = layout
        self._stamps = self._current_stamps()

    # ---- loading / saving ----

    @classmethod
    def load(cls, path: str | os.PathLike[str]) -> CompanionFolder:
        folder = Path(path)
        if not folder.is_dir():
            raise CompanionLoadError(f"{folder} is not a folder")
        companion = JsonDocument.read(folder / COMPANION_FILE)
        dialogs = JsonDocument.read(folder / DIALOGS_FILE)
        langs: dict[str, JsonDocument] = {}
        lang_dir = folder / LANG_DIR
        if lang_dir.is_dir():
            for file in sorted(lang_dir.glob("*.json")):
                langs[file.stem] = JsonDocument.read(file)
        layout = NodeLayout.read(folder / NodeLayout.FILE_NAME)
        return cls(folder, companion, dialogs, langs, layout)

    @property
    def id(self) -> str:
        return self.path.name

    def documents(self) -> list[JsonDocument]:
        return [self.companion, self.dialogs, *self.langs.values()]

    @property
    def dirty(self) -> bool:
        return any(d.dirty for d in self.documents()) or self.layout_dirty

    @property
    def layout_dirty(self) -> bool:
        return self.layout.dirty(self.node_ids_by_conversation())

    def save(self) -> list[Path]:
        """Writes every changed file (and the layout sidecar); returns the paths written."""
        written = [d.path for d in self.documents() if d.write()]
        if self.layout.write(self.node_ids_by_conversation()):
            written.append(self.layout.path)
        self._stamps = self._current_stamps()
        return written

    def _watched(self) -> list[Path]:
        paths = [d.path for d in self.documents()] + [self.layout.path]
        lang_dir = self.path / LANG_DIR
        if lang_dir.is_dir():
            paths += [p for p in lang_dir.glob("*.json") if p not in paths]
        return paths

    def _current_stamps(self) -> dict[Path, tuple[int, int] | None]:
        return {p: _stamp(p) for p in self._watched()}

    def changed_on_disk(self) -> list[Path]:
        """Files written by someone else since the last load or save (new lang files included)."""
        now = self._current_stamps()
        return [p for p in now if now[p] != self._stamps.get(p)]

    def accept_disk_state(self) -> None:
        """Stops reporting the current external changes (the user chose to keep the editor's version)."""
        self._stamps = self._current_stamps()

    # ---- undo support: the whole editable state as one value ----

    def snapshot(self) -> dict[str, Any]:
        return copy.deepcopy({
            "companion": self.companion.value,
            "dialogs": self.dialogs.value,
            "langs": {code: d.value for code, d in self.langs.items()},
            "layout": self.layout.positions,
        })

    def restore(self, state: dict[str, Any]) -> None:
        state = copy.deepcopy(state)
        self.companion.value = state["companion"]
        self.dialogs.value = state["dialogs"]
        for code in list(self.langs):
            if code not in state["langs"]:
                del self.langs[code]
        for code, value in state["langs"].items():
            if code in self.langs:
                self.langs[code].value = value
            else:
                self.langs[code] = JsonDocument(self.path / LANG_DIR / f"{code}.json", value)
        self.layout.positions = state["layout"]
        self.layout.changed = True

    # ---- reading the content ----

    @property
    def lines(self) -> list[dict]:
        return self.dialogs.value.setdefault("lines", [])

    @property
    def conversations(self) -> list[dict]:
        value = self.dialogs.value.get("conversations")
        return value if isinstance(value, list) else []

    @property
    def variables(self) -> dict[str, dict]:
        value = self.dialogs.value.get("variables")
        return value if isinstance(value, dict) else {}

    def conversation(self, conversation_id: str) -> dict | None:
        return next((c for c in self.conversations if isinstance(c, dict) and c.get("id") == conversation_id), None)

    def node(self, conversation_id: str, node_id: str) -> dict | None:
        conv = self.conversation(conversation_id)
        if conv is None:
            return None
        return next((n for n in conv.get("nodes") or [] if isinstance(n, dict) and n.get("id") == node_id), None)

    def line(self, line_id: str) -> dict | None:
        return next((l for l in self.lines if isinstance(l, dict) and l.get("id") == line_id), None)

    def all_ids(self) -> set[str]:
        """Every local id in use: lines and every conversation's nodes share one namespace."""
        ids = {l.get("id") for l in self.lines if isinstance(l, dict)}
        for conv in self.conversations:
            ids |= {n.get("id") for n in conv.get("nodes") or [] if isinstance(n, dict)}
        return {i for i in ids if isinstance(i, str)}

    def node_ids_by_conversation(self) -> dict[str, list[str]]:
        result: dict[str, list[str]] = {}
        for conv in self.conversations:
            if isinstance(conv, dict) and isinstance(conv.get("id"), str):
                result[conv["id"]] = [n["id"] for n in conv.get("nodes") or []
                                      if isinstance(n, dict) and isinstance(n.get("id"), str)]
        return result

    def find_id(self, local_id: str) -> tuple[str | None, dict] | None:
        """Where a local id lives: (conversation id, node) or (None, line)."""
        line = self.line(local_id)
        if line is not None:
            return None, line
        for conv in self.conversations:
            for n in conv.get("nodes") or []:
                if isinstance(n, dict) and n.get("id") == local_id:
                    return conv.get("id"), n
        return None

    # ---- texts ----

    def languages(self) -> list[str]:
        """Language codes, English first."""
        return sorted(self.langs, key=lambda c: (c != ENGLISH, c))

    def lang_lines(self, code: str) -> dict[str, str]:
        doc = self.langs.get(code)
        if doc is None:
            doc = self.langs[code] = JsonDocument(self.path / LANG_DIR / f"{code}.json", {"schemaVersion": 1, "lines": {}})
        lines = doc.value.get("lines")
        if not isinstance(lines, dict):
            lines = doc.value["lines"] = {}
        return lines

    def text(self, local_id: str, code: str = ENGLISH) -> str | None:
        doc = self.langs.get(code)
        if doc is None:
            return None
        lines = doc.value.get("lines")
        return lines.get(local_id) if isinstance(lines, dict) else None

    def set_text(self, local_id: str, text: str, code: str = ENGLISH) -> None:
        self.lang_lines(code)[local_id] = text

    def audio_path(self, item: dict) -> Path:
        """The audio file of a line or node: its 'audio' or audio/<local id>.ogg (docs/SCHEMA.md)."""
        rel = item.get("audio") or f"audio/{item.get('id')}.ogg"
        return self.path / rel
