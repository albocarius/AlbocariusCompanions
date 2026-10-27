"""The schema layer: reads the CLI's ``export schema`` / ``export triggers`` (docs/EXPORTS.md). No Qt.

The editor never hardcodes fields, conditions, effects or triggers; it asks a :class:`SchemaCatalog`. The catalog comes
from the CLI when it runs, else from the copies bundled in ``fixtures/`` (captured from the same exports; a test keeps
them equal to the CLI's output when the CLI is built).
"""

from __future__ import annotations

import json
import re
from dataclasses import dataclass, field
from functools import cached_property
from pathlib import Path
from typing import Any

FIXTURES = Path(__file__).with_name("fixtures")
SUPPORTED_EXPORT_VERSION = 1

SCALARS = ("string", "int", "number", "bool", "any")


class SchemaError(Exception):
    pass


@dataclass(frozen=True)
class FieldSpec:
    """One field of an exported object. ``type``: a scalar, an object name, ``<type>[]`` or ``map<type>``."""

    name: str
    type: str
    required: bool = False
    description: str = ""
    default: Any = None
    has_default: bool = False
    allowed: tuple[str, ...] | None = None
    min: float | None = None
    max: float | None = None
    pattern: str | None = None

    @classmethod
    def from_json(cls, d: dict[str, Any]) -> FieldSpec:
        return cls(name=d["name"], type=d["type"], required=bool(d.get("required")), description=d.get("description", ""),
                   default=d.get("default"), has_default="default" in d,
                   allowed=tuple(d["allowed"]) if "allowed" in d else None,
                   min=d.get("min"), max=d.get("max"), pattern=d.get("pattern"))

    @property
    def is_list(self) -> bool:
        return self.type.endswith("[]")

    @property
    def is_map(self) -> bool:
        return self.type.startswith("map<")

    @property
    def item_type(self) -> str:
        """The element type of a list or map; the type itself otherwise."""
        if self.is_list:
            return self.type[:-2]
        if self.is_map:
            return self.type[4:-1]
        return self.type

    @property
    def is_scalar(self) -> bool:
        return self.type in SCALARS

    def widget_kind(self) -> str:
        """How a form shows the field: bool → check, allowed → combo, int/number → spin, string → line,
        lists of strings → tags, an object → nested form, maps → key/value table, any → json."""
        if self.type == "bool":
            return "check"
        if self.is_scalar and self.allowed and not any("<" in a for a in self.allowed):
            return "combo"
        if self.type in ("int", "number"):
            return "spin"
        if self.type == "string":
            return "editable-combo" if self.allowed else "line"
        if self.type == "any":
            return "json"
        if self.is_list:
            return "tags" if self.item_type == "string" else "list"
        if self.is_map:
            return "map"
        return "object"


@dataclass(frozen=True)
class CatalogEntry:
    """A condition or effect of the catalogue: the keys it writes into its object (``params``)."""

    name: str
    description: str
    params: tuple[FieldSpec, ...]

    @property
    def keys(self) -> tuple[str, ...]:
        return tuple(p.name for p in self.params)


@dataclass(frozen=True)
class TriggerInfo:
    name: str
    category: str
    common: bool
    description: str
    polling: bool
    in_mission: bool
    subjects: tuple[str, ...]
    default_subject: str
    fields: tuple[dict[str, Any], ...]
    params: tuple[dict[str, Any], ...]
    deliveries: tuple[str, ...] = field(default=())

    @classmethod
    def from_json(cls, d: dict[str, Any]) -> TriggerInfo:
        return cls(name=d["name"], category=d["category"], common=d["common"], description=d["description"],
                   polling=d["polling"], in_mission=d["inMission"], subjects=tuple(d["subjects"]),
                   default_subject=d["defaultSubject"], fields=tuple(d["fields"]), params=tuple(d["params"]),
                   deliveries=tuple(d.get("deliveries", ())))


class SchemaCatalog:
    def __init__(self, schema: dict[str, Any], triggers: dict[str, Any], source: str):
        for name, doc in (("schema", schema), ("triggers", triggers)):
            if doc.get("exportVersion") != SUPPORTED_EXPORT_VERSION:
                raise SchemaError(f"export {name} has exportVersion {doc.get('exportVersion')}; "
                                  f"this editor reads {SUPPORTED_EXPORT_VERSION}")
        self.schema = schema
        self.triggers_doc = triggers
        # "cli" or "bundled": shown in the status bar so a stale fallback is visible.
        self.source = source

    @classmethod
    def bundled(cls) -> SchemaCatalog:
        return cls(json.loads((FIXTURES / "schema.json").read_text(encoding="utf-8")),
                   json.loads((FIXTURES / "triggers.json").read_text(encoding="utf-8")), "bundled")

    @classmethod
    def load(cls, cli: Any | None) -> SchemaCatalog:
        """From the CLI (a :class:`~albocarius_editor.cli.client.Cli`) when given and working, else bundled."""
        if cli is not None:
            try:
                return cls(cli.export("schema"), cli.export("triggers"), "cli")
            except Exception:  # noqa: BLE001 - any CLI failure falls back to the bundled copy
                pass
        return cls.bundled()

    # ---- objects and fields ----

    @property
    def object_names(self) -> list[str]:
        return list(self.schema["objects"])

    def fields(self, object_name: str) -> list[FieldSpec]:
        try:
            raw = self.schema["objects"][object_name]["fields"]
        except KeyError:
            raise SchemaError(f"no object '{object_name}' in the schema export") from None
        return [FieldSpec.from_json(f) for f in raw]

    def field(self, object_name: str, field_name: str) -> FieldSpec:
        for f in self.fields(object_name):
            if f.name == field_name:
                return f
        raise SchemaError(f"no field '{field_name}' in '{object_name}'")

    def is_object(self, type_name: str) -> bool:
        return type_name in self.schema["objects"]

    def file_object(self, file_name: str) -> str:
        """``companion.json`` → ``companion``; ``lang/tr.json`` → ``lang``."""
        files = self.schema["files"]
        if file_name in files:
            return files[file_name]
        if file_name.startswith("lang/"):
            return files["lang/<code>.json"]
        raise SchemaError(f"unknown file {file_name}")

    # ---- catalogues ----

    @cached_property
    def conditions(self) -> list[CatalogEntry]:
        return [self._entry(e) for e in self.schema["conditions"]]

    @cached_property
    def effects(self) -> list[CatalogEntry]:
        return [self._entry(e) for e in self.schema["effects"]]

    @staticmethod
    def _entry(e: dict[str, Any]) -> CatalogEntry:
        return CatalogEntry(e["name"], e["description"], tuple(FieldSpec.from_json(p) for p in e["params"]))

    def condition(self, name: str) -> CatalogEntry:
        return next(c for c in self.conditions if c.name == name)

    def effect(self, name: str) -> CatalogEntry:
        return next(e for e in self.effects if e.name == name)

    def conditions_in(self, when: dict[str, Any]) -> list[CatalogEntry]:
        """The catalogue entries a condition object uses, in catalogue order."""
        return [c for c in self.conditions if any(k in when for k in c.keys)]

    def effects_in(self, effects: dict[str, Any]) -> list[CatalogEntry]:
        order = self.schema.get("effectOrder", [])
        used = [e for e in self.effects if any(k in effects for k in e.keys)]
        return sorted(used, key=lambda e: order.index(e.name) if e.name in order else len(order))

    def new_value(self, spec: FieldSpec) -> Any:
        """A starting value for a field the user just added ("+"): its default, else something valid-looking."""
        if spec.has_default and spec.default is not None:
            return spec.default
        if spec.is_list:
            return []
        if spec.is_map:
            return {}
        if self.is_object(spec.type):
            return {f.name: self.new_value(f) for f in self.fields(spec.type) if f.required}
        if spec.allowed:
            return next((a for a in spec.allowed if "<" not in a), "")
        if spec.type == "bool":
            return True
        if spec.type in ("int", "number"):
            # 1 suits most fields (a count, a non-zero change); otherwise the lowest allowed value.
            fits = (spec.min is None or spec.min <= 1) and (spec.max is None or spec.max >= 1)
            return 1 if fits else int(spec.min if spec.min is not None else spec.max)
        return ""

    def new_entry(self, entry: CatalogEntry) -> dict[str, Any]:
        """The keys a new condition/effect writes: the first parameter (or the required ones of a group)."""
        params = [p for p in entry.params if p.required] or list(entry.params[:1])
        return {p.name: self.new_value(p) for p in params}

    # ---- enums, patterns, slots ----

    def enum(self, name: str) -> list[Any]:
        return list(self.schema["enums"][name])

    def pattern(self, name: str) -> re.Pattern[str]:
        return re.compile(self.schema["patterns"][name])

    @property
    def required_slots(self) -> list[str]:
        return list(self.schema["slots"]["required"])

    @property
    def all_slots(self) -> list[str]:
        return list(self.schema["slots"]["required"]) + list(self.schema["slots"]["optional"])

    def slot_group(self, slot: str | None) -> str:
        """For grouping the Lines tab: companion, player, vanilla, optional, triggered (no slot), unknown."""
        slots = self.schema["slots"]
        if slot is None:
            return "triggered"
        if slot in slots["companion"]:
            return "companion"
        if slot in slots["player"]:
            return "player"
        if any(v["slot"] == slot for v in slots["vanilla"]):
            return "vanilla"
        if slot in slots["optional"]:
            return "optional"
        return "unknown"

    # ---- triggers ----

    @cached_property
    def triggers(self) -> list[TriggerInfo]:
        return [TriggerInfo.from_json(t) for t in self.triggers_doc["triggers"]]

    def trigger(self, name: str) -> TriggerInfo | None:
        return next((t for t in self.triggers if t.name == name), None)

    @property
    def trigger_categories(self) -> list[str]:
        return sorted({t.category for t in self.triggers})
