"""A conversation as a graph for the canvas: a header, its nodes, the hub/end terminals and ordered edges. No Qt.

Item keys (also the layout sidecar keys): a node's local id, or one of :data:`HEADER`, :data:`HUB`, :data:`END`
(they cannot clash: node ids match ``[a-z0-9_]+``).
"""

from __future__ import annotations

from collections import deque
from dataclasses import dataclass

HEADER = "@header"
HUB = "@hub"
END = "@end"
TERMINALS = {"hub": HUB, "end": END}


@dataclass(frozen=True)
class Edge:
    source: str  # HEADER (a start edge) or a node id
    target: str  # a node id, HUB or END
    index: int   # position in the source's start/next list (order is meaningful)
    missing: bool = False  # the target names no node of this conversation


@dataclass
class ConversationGraph:
    conversation_id: str
    node_ids: list[str]
    edges: list[Edge]

    def keys(self) -> list[str]:
        keys = [HEADER, *self.node_ids]
        if any(e.target == HUB for e in self.edges):
            keys.append(HUB)
        if any(e.target == END for e in self.edges):
            keys.append(END)
        return keys


def build(conversation: dict) -> ConversationGraph:
    nodes = [n for n in conversation.get("nodes") or [] if isinstance(n, dict) and isinstance(n.get("id"), str)]
    ids = [n["id"] for n in nodes]
    known = set(ids)
    edges: list[Edge] = []

    def target(name: str) -> tuple[str, bool]:
        if name in TERMINALS:
            return TERMINALS[name], False
        return name, name not in known

    for i, start in enumerate(conversation.get("start") or []):
        if isinstance(start, str):
            t, missing = target(start)
            edges.append(Edge(HEADER, t, i, missing))
    for n in nodes:
        for i, nxt in enumerate(n.get("next") or []):
            if isinstance(nxt, str):
                t, missing = target(nxt)
                edges.append(Edge(n["id"], t, i, missing))
    return ConversationGraph(conversation.get("id") or "", ids, edges)


def depths(graph: ConversationGraph) -> dict[str, int]:
    """Graph depth of each key from the header (breadth first); unreachable nodes after the deepest; hub/end last."""
    out: dict[str, list[str]] = {}
    for e in graph.edges:
        if not e.missing:
            out.setdefault(e.source, []).append(e.target)
    depth = {HEADER: 0}
    queue = deque([HEADER])
    while queue:
        key = queue.popleft()
        for t in out.get(key, []):
            if t in (HUB, END) or t in depth:
                continue
            depth[t] = depth[key] + 1
            queue.append(t)
    deepest = max(depth.values())
    for node_id in graph.node_ids:
        if node_id not in depth:
            deepest += 1
            depth[node_id] = deepest
    last = max(depth.values()) + 1
    for key in graph.keys():
        if key in (HUB, END):
            depth[key] = last
    return depth


# Auto-layout grid in scene units (the canvas cards are ~280 wide).
COLUMN_WIDTH = 360
ROW_HEIGHT = 190  # without known heights
ROW_GAP = 30      # between stacked items of known height


def auto_layout(graph: ConversationGraph, heights: dict[str, float] | None = None) -> dict[str, tuple[float, float]]:
    """Left → right by depth; within a column in file order, stacked by the items' heights when given
    (the canvas knows them), else on a fixed row grid."""
    by_depth: dict[int, list[str]] = {}
    for key, d in depths(graph).items():
        by_depth.setdefault(d, []).append(key)
    order = {k: i for i, k in enumerate(graph.keys())}
    positions: dict[str, tuple[float, float]] = {}
    for d, keys in by_depth.items():
        y = 0.0
        for key in sorted(keys, key=order.__getitem__):
            positions[key] = (float(d * COLUMN_WIDTH), y)
            y += (heights[key] + ROW_GAP) if heights and key in heights else ROW_HEIGHT
    return positions
