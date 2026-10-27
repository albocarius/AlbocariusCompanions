"""Id rules of docs/SCHEMA.md: local ids, companion ids, reserved words; proposing free ids. No Qt."""

from __future__ import annotations

import re
from collections.abc import Iterable

# Defaults matching the schema export's "patterns"; the schema layer can pass the exported ones instead.
LOCAL_ID = re.compile(r"^[a-z0-9_]+$")
RESERVED_NEXT = ("hub", "end")
RESERVED_FOLDER = "_shared"


def is_valid_local_id(text: str, pattern: re.Pattern[str] = LOCAL_ID) -> bool:
    return bool(pattern.match(text)) and text not in RESERVED_NEXT


def is_valid_companion_id(text: str, pattern: re.Pattern[str] = LOCAL_ID) -> bool:
    return bool(pattern.match(text)) and text != RESERVED_FOLDER


def slug(text: str) -> str:
    """Lowercase, [a-z0-9_] only, no leading/trailing/double underscores; may be empty."""
    s = re.sub(r"[^a-z0-9]+", "_", text.lower())
    return re.sub(r"_+", "_", s).strip("_")


def propose(base: str, taken: Iterable[str]) -> str:
    """The first free ``<base>_01``, ``<base>_02``, … (base slugged; 'node' when empty)."""
    used = set(taken)
    stem = slug(base) or "node"
    n = 1
    while True:
        candidate = f"{stem}_{n:02d}"
        if candidate not in used and candidate not in RESERVED_NEXT:
            return candidate
        n += 1


def propose_node_id(conversation_id: str, speaker: str, taken: Iterable[str]) -> str:
    """Node ids follow the sample's habit: player nodes start with ``p_``."""
    base = ("p_" if speaker == "player" else "") + conversation_id
    return propose(base, taken)
