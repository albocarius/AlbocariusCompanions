"""Layout-preserving JSON: parse a document, remember how it was written, write it back the same way.

The companion files are hand-written and use several styles in one file (``{ "a": 1 }`` inline objects, ``{"id": …}``
rows, fully expanded blocks). ``json.dumps`` would rewrite all of it. Here:

* every container and string/number remembers its exact source text, keyed by a *path* that survives reordering
  (list items that are objects with an ``"id"`` are keyed by that id, others by index);
* on write, a value equal to what was read at the same path and depth is written as its original text, byte for byte;
* a changed container is written inline if it was inline (keeping its inner padding), else expanded with the file's
  indent; new containers are expanded, empty ones are ``[]`` / ``{}``;
* BOM, line ending (LF / CRLF) and the trailing newline are kept.

So an unedited file round-trips byte-identical, and an edit only changes the lines it touches.
No Qt here.
"""

from __future__ import annotations

import copy
import json
from dataclasses import dataclass, field
from typing import Any

Path = tuple  # tuple of str | int segments


@dataclass
class _Span:
    raw: str
    value: Any
    depth: int
    inline: bool = False
    pad: str = ""


@dataclass
class Layout:
    """How one document was written. Empty for a document that did not exist."""

    spans: dict[Path, _Span] = field(default_factory=dict)
    indent: str = "  "
    newline: str = "\n"
    bom: bool = False
    final_newline: bool = True


class JsonSyntaxError(ValueError):
    pass


def _segment(container: list, index: int) -> str | int:
    item = container[index]
    if isinstance(item, dict) and isinstance(item.get("id"), str):
        return "#" + item["id"]
    return index


def _list_segments(items: list) -> list[str | int]:
    """Segments of a list's items; falls back to indexes when ids repeat (ambiguous)."""
    segments = [_segment(items, i) for i in range(len(items))]
    seen: set = set()
    for s in segments:
        if isinstance(s, str):
            if s in seen:
                return list(range(len(items)))
            seen.add(s)
    return segments


class _Parser:
    def __init__(self, text: str):
        self.text = text
        self.pos = 0
        self.spans: dict[Path, _Span] = {}

    def error(self, message: str) -> JsonSyntaxError:
        line = self.text.count("\n", 0, self.pos) + 1
        column = self.pos - (self.text.rfind("\n", 0, self.pos) + 1) + 1
        return JsonSyntaxError(f"{message} at line {line}, column {column}")

    def ws(self) -> None:
        text, pos = self.text, self.pos
        while pos < len(text) and text[pos] in " \t\r\n":
            pos += 1
        self.pos = pos

    def value(self, path: Path, depth: int) -> Any:
        self.ws()
        if self.pos >= len(self.text):
            raise self.error("unexpected end of input")
        c = self.text[self.pos]
        start = self.pos
        if c == "{":
            result = self.obj(path, depth)
        elif c == "[":
            result = self.arr(path, depth)
        elif c == '"':
            result = self.string()
        elif c in "-0123456789":
            result = self.number()
        elif self.text.startswith("true", self.pos):
            self.pos += 4
            return True
        elif self.text.startswith("false", self.pos):
            self.pos += 5
            return False
        elif self.text.startswith("null", self.pos):
            self.pos += 4
            return None
        else:
            raise self.error(f"unexpected character {c!r}")
        raw = self.text[start:self.pos]
        # A copy: the caller edits the returned value in place, the span must keep what was read.
        span = _Span(raw=raw, value=copy.deepcopy(result) if c in "{[" else result, depth=depth)
        if c in "{[":
            span.inline = "\n" not in raw
            inner = raw[1:-1]
            span.pad = inner[: len(inner) - len(inner.lstrip(" "))] if span.inline else ""
        self.spans[path] = span
        return result

    def string(self) -> str:
        start = self.pos
        self.pos += 1
        text = self.text
        while True:
            if self.pos >= len(text):
                raise self.error("unterminated string")
            ch = text[self.pos]
            if ch == "\\":
                self.pos += 2
                continue
            self.pos += 1
            if ch == '"':
                break
        try:
            return json.loads(text[start:self.pos])
        except json.JSONDecodeError as e:
            raise self.error(f"bad string ({e.msg})") from None

    def number(self) -> int | float:
        start = self.pos
        text = self.text
        while self.pos < len(text) and text[self.pos] in "+-0123456789.eE":
            self.pos += 1
        raw = text[start:self.pos]
        try:
            return json.loads(raw)
        except json.JSONDecodeError:
            raise self.error(f"bad number {raw!r}") from None

    def obj(self, path: Path, depth: int) -> dict:
        self.pos += 1
        result: dict = {}
        self.ws()
        if self.text.startswith("}", self.pos):
            self.pos += 1
            return result
        while True:
            self.ws()
            if not self.text.startswith('"', self.pos):
                raise self.error("expected a property name")
            key = self.string()
            self.ws()
            if not self.text.startswith(":", self.pos):
                raise self.error("expected ':'")
            self.pos += 1
            result[key] = self.value(path + (key,), depth + 1)
            self.ws()
            if self.text.startswith(",", self.pos):
                self.pos += 1
                continue
            if self.text.startswith("}", self.pos):
                self.pos += 1
                return result
            raise self.error("expected ',' or '}'")

    def arr(self, path: Path, depth: int) -> list:
        self.pos += 1
        result: list = []
        self.ws()
        if self.text.startswith("]", self.pos):
            self.pos += 1
            return result
        # Items are parsed under a provisional index path, then re-keyed by id once the whole list is known.
        provisional: list[dict[Path, _Span]] = []
        while True:
            before = self.spans
            self.spans = {}
            result.append(self.value(path + (len(result),), depth + 1))
            provisional.append(self.spans)
            self.spans = before
            self.ws()
            if self.text.startswith(",", self.pos):
                self.pos += 1
                continue
            if self.text.startswith("]", self.pos):
                self.pos += 1
                break
            raise self.error("expected ',' or ']'")
        segments = _list_segments(result)
        n = len(path)
        for i, spans in enumerate(provisional):
            for p, span in spans.items():
                self.spans[p[:n] + (segments[i],) + p[n + 1:]] = span
        return result


def loads(text: str) -> tuple[Any, Layout]:
    """Parse JSON text; returns the value (dicts keep key order) and its layout."""
    layout = Layout()
    if text.startswith("﻿"):
        layout.bom = True
        text = text[1:]
    layout.newline = "\r\n" if "\r\n" in text else "\n"
    layout.final_newline = text.endswith("\n")
    parser = _Parser(text)
    value = parser.value((), 0)
    parser.ws()
    if parser.pos != len(text):
        raise parser.error("extra data after the document")
    layout.spans = parser.spans
    layout.indent = _detect_indent(text)
    return value, layout


def _detect_indent(text: str) -> str:
    for line in text.split("\n")[1:]:
        stripped = line.lstrip(" \t")
        if stripped and len(stripped) < len(line):
            return line[: len(line) - len(stripped)]
    return "  "


def _same(a: Any, b: Any) -> bool:
    # bool is an int in Python: 1 == True must not count as unchanged.
    if type(a) is not type(b):
        return False
    if isinstance(a, dict):
        return list(a.keys()) == list(b.keys()) and all(_same(a[k], b[k]) for k in a)
    if isinstance(a, list):
        return len(a) == len(b) and all(_same(x, y) for x, y in zip(a, b))
    return a == b


def _scalar(value: Any) -> str:
    if value is True:
        return "true"
    if value is False:
        return "false"
    if value is None:
        return "null"
    if isinstance(value, (int, float, str)):
        return json.dumps(value, ensure_ascii=False)
    raise TypeError(f"not JSON serializable: {type(value).__name__}")


def dumps(value: Any, layout: Layout | None = None) -> str:
    """Write a value; with the layout of the original document, unchanged parts keep their exact text."""
    layout = layout or Layout()
    nl = layout.newline

    def emit(v: Any, path: Path, depth: int) -> str:
        span = layout.spans.get(path)
        if span is not None and span.depth == depth and _same(span.value, v):
            # The raw text may hold the original newlines; normalize to the file's line ending.
            return span.raw if nl == "\n" else span.raw.replace("\r\n", "\n").replace("\n", nl)
        if isinstance(v, dict):
            items = [(k, emit(v[k], path + (k,), depth + 1)) for k in v]
            if not items:
                return "{}"
            if span is not None and span.inline:
                body = ", ".join(f"{_scalar(k)}: {s}" for k, s in items)
                return "{" + span.pad + body + span.pad + "}"
            inner = layout.indent * (depth + 1)
            body = ("," + nl).join(f"{inner}{_scalar(k)}: {s}" for k, s in items)
            return "{" + nl + body + nl + layout.indent * depth + "}"
        if isinstance(v, list):
            segments = _list_segments(v)
            items = [emit(x, path + (segments[i],), depth + 1) for i, x in enumerate(v)]
            if not items:
                return "[]"
            if span is not None and span.inline:
                return "[" + span.pad + ", ".join(items) + span.pad + "]"
            inner = layout.indent * (depth + 1)
            return "[" + nl + ("," + nl).join(inner + s for s in items) + nl + layout.indent * depth + "]"
        return _scalar(v)

    text = emit(value, (), 0)
    if layout.final_newline:
        text += nl
    if layout.bom:
        text = "﻿" + text
    return text
