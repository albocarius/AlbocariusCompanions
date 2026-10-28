"""Schema-driven forms (PLAN T06–T09): a widget per exported field, the conditions and effects editors, the trigger
editor. Nothing here lists fields, conditions, effects or triggers: they come from the :class:`SchemaCatalog`.

Every widget commits once (Enter, focus out, a click) through its :class:`FormContext`, which turns the change into a
``data.edit`` call run by the window as one undoable command. The inspector is rebuilt after each commit, so the
widgets keep no state of their own.
"""

from __future__ import annotations

import json
import re
from collections.abc import Callable
from dataclasses import dataclass, field
from typing import Any

from PySide6.QtCore import Qt
from PySide6.QtGui import QDoubleValidator, QIntValidator
from PySide6.QtWidgets import (QCheckBox, QComboBox, QCompleter, QFormLayout, QFrame, QHBoxLayout, QLabel, QLineEdit,
                               QMenu, QToolButton, QVBoxLayout, QWidget)

from ..data import edit
from ..data.companion import CompanionFolder
from ..schema.catalog import CatalogEntry, FieldSpec, SchemaCatalog, TriggerInfo

INT_RANGE = (-2_000_000_000, 2_000_000_000)


@dataclass
class FormContext:
    """What a form edits (an item of the folder) and how it commits.

    ``commit(text, fn)`` runs ``fn(folder)`` as one undoable edit and returns whether it was done; ``message`` shows a
    refusal. ``variables`` are the names a variable field offers (the blackboard); ``event_fields`` the ``event.<path>``
    names of the trigger that starts this content; ``companions`` the other companion ids (actors, subjects)."""

    catalog: SchemaCatalog
    folder: CompanionFolder
    ref: tuple
    commit: Callable[[str, Callable[[CompanionFolder], Any]], bool]
    message: Callable[[str], None] = lambda _text: None
    variables: list[str] = field(default_factory=list)
    int_variables: list[str] = field(default_factory=list)
    event_fields: list[str] = field(default_factory=list)
    companions: list[str] = field(default_factory=list)

    def value(self, path: list, default: Any = None) -> Any:
        return edit.get_at(self.folder, self.ref, path, default)

    def has(self, path: list) -> bool:
        sentinel = object()
        return edit.get_at(self.folder, self.ref, path, sentinel) is not sentinel

    def set(self, path: list, value: Any) -> bool:
        if self.has(path) and self.value(path) == value and type(self.value(path)) is type(value):
            return False  # unchanged (e.g. focus left a field that was not edited)
        return self.commit(f"Set {_label(path)}", lambda f: edit.set_at(f, self.ref, path, value))

    def remove(self, path: list) -> bool:
        return self.commit(f"Remove {_label(path)}", lambda f: edit.remove_at(f, self.ref, path))

    def remove_keys(self, path: list, keys: list[str], label: str) -> bool:
        return self.commit(f"Remove {label}", lambda f: edit.remove_keys(f, self.ref, path, keys))

    def merge(self, path: list, values: dict, label: str) -> bool:
        return self.commit(f"Add {label}", lambda f: edit.merge_at(f, self.ref, path, values))

    def append(self, path: list, value: Any, label: str) -> bool:
        return self.commit(f"Add {label}", lambda f: edit.append_at(f, self.ref, path, value))

    def replace(self, path: list, remove: list[str], values: dict, label: str) -> bool:
        return self.commit(f"Set {label}", lambda f: edit.replace_keys(f, self.ref, path, remove, values))


def _label(path: list) -> str:
    return ".".join(str(p) for p in path)


def _compact(value: Any) -> str:
    return value if isinstance(value, str) else json.dumps(value, ensure_ascii=False)


def _muted(text: str) -> QLabel:
    label = QLabel(text)
    label.setProperty("role", "muted")
    label.setWordWrap(True)
    return label


def _title(text: str, tip: str = "") -> QLabel:
    label = QLabel(text)
    label.setProperty("role", "section")
    label.setToolTip(tip)
    return label


def _tool(text: str, tip: str, slot: Callable[[], Any] | None = None, name: str = "") -> QToolButton:
    button = QToolButton()
    button.setText(text)
    button.setToolTip(tip)
    button.setAutoRaise(True)
    if name:
        button.setObjectName(name)
    if slot is not None:
        button.clicked.connect(lambda _=False: slot())
    return button


def _expand_allowed(allowed: tuple[str, ...] | None, ctx: FormContext) -> list[str]:
    """Allowed values with placeholders: ``<companionId>`` → the other companions' ids; other placeholders are typed."""
    out: list[str] = []
    for a in allowed or ():
        if a == "<companionId>":
            out += ctx.companions
        elif "<" not in a:
            out.append(a)
    return out


def parse_scalar(text: str, type_: str) -> Any:
    """The value a text field means for a field type; raises ValueError when it does not fit."""
    text = text.strip()
    if type_ == "int":
        return int(text)
    if type_ == "number":
        v = float(text)
        return int(v) if v.is_integer() and "." not in text else v
    if type_ == "bool":
        if text.lower() in ("true", "false"):
            return text.lower() == "true"
        raise ValueError("true or false")
    if type_ == "any":
        try:
            return json.loads(text)
        except json.JSONDecodeError:
            return text  # plain text is a string
    return text


def _narrow(combo: QComboBox) -> QComboBox:
    """Combos as wide as the panel, not as their longest entry (the inspector does not scroll sideways)."""
    combo.setSizeAdjustPolicy(QComboBox.SizeAdjustPolicy.AdjustToMinimumContentsLengthWithIcon)
    combo.setMinimumContentsLength(6)
    return combo


# ---- one field ----

class LineField(QLineEdit):
    """A text field committing once on Enter / focus out; also takes a dropped variable name (blackboard)."""

    def __init__(self, text: str, on_commit: Callable[[str], None]):
        super().__init__(text)
        self._on_commit = on_commit
        self._last = text
        self.editingFinished.connect(self._commit)
        self.setAcceptDrops(True)

    def _commit(self) -> None:
        if self.text() != self._last:
            self._last = self.text()
            self._on_commit(self.text())

    def dropEvent(self, event) -> None:
        if event.mimeData().hasText():
            self.setText(event.mimeData().text().strip())
            event.acceptProposedAction()
            self._commit()
        else:
            super().dropEvent(event)


class ComboField(QComboBox):
    """An editable combo committing its text once (Enter, a picked entry, focus out)."""

    def __init__(self, items: list[str], text: str, on_commit: Callable[[str], None], editable: bool = True):
        super().__init__()
        _narrow(self)
        self.setEditable(editable)
        self.addItems(items)
        self._on_commit = on_commit
        self._last = text
        if editable:
            completer = QCompleter(items, self)
            completer.setCaseSensitivity(Qt.CaseSensitivity.CaseInsensitive)
            completer.setFilterMode(Qt.MatchFlag.MatchContains)
            self.setCompleter(completer)
            # After the items and the completer: both may put the first item into the edit field.
            self.setCurrentIndex(self.findText(text) if text in items else -1)
            self.setEditText(text)
            self.lineEdit().editingFinished.connect(lambda: self._commit(self.currentText()))
            self.lineEdit().setAcceptDrops(True)
        else:
            self.setCurrentIndex(max(0, self.findText(text)))
        self.activated.connect(lambda i: self._commit(self.itemText(i)))

    def _commit(self, text: str) -> None:
        if text != self._last:
            self._last = text
            self._on_commit(text)


def field_editor(ctx: FormContext, spec: FieldSpec, path: list, suggestions: list[str] | None = None) -> QWidget:
    """The widget for one scalar / string-list / any field at ``path`` (by ``spec.widget_kind()``), with an "×" that
    removes the key when the field is optional and set."""
    present = ctx.has(path)
    value = ctx.value(path)
    kind = spec.widget_kind()
    name = _label(path)

    def commit_text(text: str) -> None:
        if not text.strip():
            if present and not spec.required:
                ctx.remove(path)
            return
        try:
            if kind == "tags":
                v: Any = [t.strip() for t in text.split(",") if t.strip()]
            else:
                v = parse_scalar(text, spec.type)
        except ValueError:
            ctx.message(f"{spec.name}: '{text}' is not a {spec.type}")
            return
        if spec.pattern and isinstance(v, str) and not re.match(spec.pattern, v):
            ctx.message(f"{spec.name}: '{v}' does not match {spec.pattern}")
            return
        if isinstance(v, (int, float)) and not isinstance(v, bool):
            if (spec.min is not None and v < spec.min) or (spec.max is not None and v > spec.max):
                ctx.message(f"{spec.name}: {v} is outside {spec.min}..{spec.max}")
                return
        ctx.set(path, v)

    if kind == "check":
        box = QCheckBox()
        box.setChecked(bool(value) if present else bool(spec.default))
        box.toggled.connect(lambda on: ctx.set(path, on))
        widget: QWidget = box
    elif kind == "combo":
        combo = _narrow(QComboBox())
        if not present:
            combo.addItem(f"(default {_compact(spec.default)})" if spec.has_default else "(not set)", None)
        for a in _expand_allowed(spec.allowed, ctx):
            combo.addItem(a, a)
        if present:
            combo.setCurrentIndex(max(0, combo.findData(value)))

        def picked(i: int) -> None:
            v = combo.itemData(i)
            if v is not None:
                ctx.set(path, v)
        combo.activated.connect(picked)
        widget = combo
    elif kind == "editable-combo" or suggestions:
        items = list(suggestions or _expand_allowed(spec.allowed, ctx))
        combo = ComboField(items, _compact(value) if present else "", commit_text)
        combo.lineEdit().setPlaceholderText(_placeholder(spec))
        widget = combo
    else:
        text = (", ".join(str(x) for x in value) if kind == "tags" and isinstance(value, list)
                else _compact(value) if present else "")
        line = LineField(text, commit_text)
        line.setPlaceholderText(_placeholder(spec) + (" — comma separated" if kind == "tags" else ""))
        if spec.type == "int":
            line.setValidator(QIntValidator(int(spec.min if spec.min is not None else INT_RANGE[0]),
                                            int(spec.max if spec.max is not None else INT_RANGE[1]), line))
        elif spec.type == "number":
            validator = QDoubleValidator(line)
            validator.setNotation(QDoubleValidator.Notation.StandardNotation)
            line.setValidator(validator)
        if kind == "tags" and ctx.variables and spec.name in edit.VARIABLE_LIST_KEYS:
            completer = QCompleter(ctx.variables, line)
            completer.setCaseSensitivity(Qt.CaseSensitivity.CaseInsensitive)
            line.setCompleter(completer)
        widget = line
    widget.setObjectName(f"field:{name}")
    widget.setToolTip(spec.description)
    if not present or spec.required or kind == "check" and not present:
        return widget
    row = QWidget()
    lay = QHBoxLayout(row)
    lay.setContentsMargins(0, 0, 0, 0)
    lay.setSpacing(2)
    lay.addWidget(widget, 1)
    lay.addWidget(_tool("×", f"Remove {spec.name}", lambda: ctx.remove(path), f"remove:{name}"))
    return row


def _placeholder(spec: FieldSpec) -> str:
    if spec.has_default and spec.default is not None:
        return f"default {_compact(spec.default)}"
    return "required" if spec.required else "not set"


def scalar_form(ctx: FormContext, object_name: str, skip: set[str], path: list | None = None) -> QFormLayout:
    """A form of the object's scalar and string-list fields (T06): nested objects, maps and the condition / effect /
    trigger objects are left to their own editors."""
    form = QFormLayout()
    form.setContentsMargins(0, 4, 0, 4)
    for spec in ctx.catalog.fields(object_name):
        if spec.name in skip or spec.widget_kind() in ("object", "list", "map"):
            continue
        form.addRow(spec.name, field_editor(ctx, spec, [*(path or []), spec.name]))
    return form


def object_form(ctx: FormContext, spec: FieldSpec, path: list) -> QWidget:
    """A nested object (``hasGold``, ``hasItem``, …): its fields one per row."""
    box = QWidget()
    form = QFormLayout(box)
    form.setContentsMargins(12, 0, 0, 0)
    for sub in ctx.catalog.fields(spec.type):
        form.addRow(sub.name, field_editor(ctx, sub, [*path, sub.name]))
    return box


# ---- maps: set / add / where / params ----

def map_editor(ctx: FormContext, path: list, value_for: Callable[[str], FieldSpec], keys: list[str], label: str,
               new_value: Callable[[str], Any]) -> QWidget:
    """``{key: value}`` rows with "×" and an add row offering ``keys`` (typing a new key works too)."""
    box = QWidget()
    lay = QVBoxLayout(box)
    lay.setContentsMargins(12, 0, 0, 0)
    lay.setSpacing(2)
    current = ctx.value(path)
    current = current if isinstance(current, dict) else {}
    for key in current:
        row = QHBoxLayout()
        row.addWidget(QLabel(key))
        spec = value_for(key)
        row.addWidget(field_editor(ctx, FieldSpec(name=key, type=spec.type, required=True, description=spec.description,
                                                  min=spec.min, max=spec.max), [*path, key]), 1)
        row.addWidget(_tool("×", f"Remove {key}", lambda k=key: ctx.remove([*path, k]), f"remove:{_label([*path, key])}"))
        lay.addLayout(row)
    free = [k for k in keys if k not in current]
    adder = ComboField(free, "", lambda k: k.strip() and ctx.merge(path, {k.strip(): new_value(k.strip())},
                                                                  f"{label} {k.strip()}"))
    adder.lineEdit().setPlaceholderText(f"+ {label}…")
    adder.setObjectName(f"add:{_label(path)}")
    lay.addWidget(adder)
    return box


# ---- conditions (T07) ----

class ConditionsEditor(QFrame):
    """A ``when`` object: "+" adds a catalogue condition, each condition is a small form with "×"; ``all``/``any`` hold
    nested condition objects (each with its own "+"), ``not`` one nested object."""

    def __init__(self, ctx: FormContext, path: list, title: str = "Conditions", depth: int = 0):
        super().__init__()
        self.ctx, self.path = ctx, path
        self.setObjectName(f"conditions:{_label(path)}")
        if depth:
            self.setProperty("role", "card")
        lay = QVBoxLayout(self)
        lay.setContentsMargins(8 if depth else 0, 4, 4 if depth else 0, 4)
        lay.setSpacing(2)
        head = QHBoxLayout()
        head.addWidget(_title(title, "Every condition set here must hold (an implicit \"all\")"), 1)
        add = _tool("+", "Add a condition", name=f"add:{_label(path)}")
        menu = QMenu(add)
        menu.setToolTipsVisible(True)
        when = ctx.value(path)
        when = when if isinstance(when, dict) else {}
        used = {c.name for c in ctx.catalog.conditions_in(when)}
        for entry in ctx.catalog.conditions:
            action = menu.addAction(entry.name, lambda e=entry: self.add(e))
            action.setToolTip(entry.description)
            action.setEnabled(entry.name not in used)
        add.setMenu(menu)
        add.setPopupMode(QToolButton.ToolButtonPopupMode.InstantPopup)
        head.addWidget(add)
        if depth:
            head.addWidget(_tool("×", "Remove this group", lambda: ctx.remove(path), f"remove:{_label(path)}"))
        lay.addLayout(head)
        if not when:
            lay.addWidget(_muted("none: always holds"))
        for entry in ctx.catalog.conditions_in(when):
            lay.addWidget(self._entry(entry, when, depth))
        unknown = [k for k in when if not any(k in c.keys for c in ctx.catalog.conditions)]
        for key in unknown:
            lay.addWidget(_muted(f"{key}: {_compact(when[key])} (not in the schema; kept as it is)"))

    def add(self, entry: CatalogEntry) -> None:
        values = self.ctx.catalog.new_entry(entry)
        for k, v in list(values.items()):
            spec = next(p for p in entry.params if p.name == k)
            if spec.type == "conditions[]":
                values[k] = [{}]  # all/any: one empty group to fill
            elif k == "var" and self.ctx.variables:
                values[k] = self.ctx.variables[0]
        if entry.name == "var" and len(values) == 1:
            values["eq"] = True  # var needs a comparison
        self.ctx.merge(self.path, values, f"condition {entry.name}")

    def _entry(self, entry: CatalogEntry, when: dict, depth: int) -> QWidget:
        box = QFrame()
        box.setObjectName(f"condition:{_label([*self.path, entry.name])}")
        lay = QVBoxLayout(box)
        lay.setContentsMargins(0, 2, 0, 2)
        lay.setSpacing(1)
        head = QHBoxLayout()
        name = QLabel(entry.name)
        name.setToolTip(entry.description)
        name.setStyleSheet("font-weight: 600;")
        head.addWidget(name, 1)
        head.addWidget(_tool("×", f"Remove {entry.name}", lambda: self.ctx.remove_keys(self.path, list(entry.keys),
                                                                                       f"condition {entry.name}"),
                             f"remove:{_label([*self.path, entry.name])}"))
        lay.addLayout(head)
        for param in entry.params:
            p = [*self.path, param.name]
            if param.type == "conditions[]":
                groups = when.get(param.name) if isinstance(when.get(param.name), list) else []
                for i in range(len(groups)):
                    lay.addWidget(ConditionsEditor(self.ctx, [*p, i], f"{param.name} #{i + 1}", depth + 1))
                lay.addWidget(_tool(f"+ {param.name} group", f"Add a condition group to {param.name}",
                                    lambda pp=p: self.ctx.append(pp, {}, f"{param.name} group"), f"append:{_label(p)}"))
            elif param.type == "conditions":
                lay.addWidget(ConditionsEditor(self.ctx, p, param.name, depth + 1))
            elif self.ctx.catalog.is_object(param.type):
                lay.addWidget(object_form(self.ctx, param, p))
            else:
                if param.name not in when and len(entry.params) > 1 and not self._offer(entry, param, when):
                    continue
                suggestions = None
                if param.name == "var":
                    suggestions = self.ctx.variables + self.ctx.event_fields
                row = QFormLayout()
                row.setContentsMargins(12, 0, 0, 0)
                row.addRow(param.name, field_editor(self.ctx, param, p, suggestions))
                lay.addLayout(row)
        return box

    @staticmethod
    def _offer(entry: CatalogEntry, param: FieldSpec, when: dict) -> bool:
        """Optional params of a multi-key condition (relation's min/max, var's min/max/eq) are shown so they can be
        set; ``eq`` and ``min``/``max`` exclude each other (docs/SCHEMA.md)."""
        if entry.name == "var":
            if param.name == "eq":
                return "min" not in when and "max" not in when
            if param.name in ("min", "max"):
                return "eq" not in when
        return True


# ---- effects (T08) ----

class EffectsEditor(QFrame):
    """An ``effects`` object: "+" adds a catalogue effect (shown in the order the game applies them)."""

    def __init__(self, ctx: FormContext, path: list):
        super().__init__()
        self.ctx, self.path = ctx, path
        self.setObjectName(f"effects:{_label(path)}")
        lay = QVBoxLayout(self)
        lay.setContentsMargins(0, 4, 0, 4)
        lay.setSpacing(2)
        head = QHBoxLayout()
        head.addWidget(_title("Effects", "Applied when the node is said, in this order"), 1)
        add = _tool("+", "Add an effect", name=f"add:{_label(path)}")
        menu = QMenu(add)
        menu.setToolTipsVisible(True)
        fx = ctx.value(path)
        fx = fx if isinstance(fx, dict) else {}
        used = {e.name for e in ctx.catalog.effects_in(fx)}
        order = ctx.catalog.schema.get("effectOrder", [])
        for entry in sorted(ctx.catalog.effects, key=lambda e: order.index(e.name) if e.name in order else len(order)):
            action = menu.addAction(entry.name, lambda e=entry: self.add(e))
            action.setToolTip(entry.description)
            action.setEnabled(entry.name not in used)
        add.setMenu(menu)
        add.setPopupMode(QToolButton.ToolButtonPopupMode.InstantPopup)
        head.addWidget(add)
        lay.addLayout(head)
        if not fx:
            lay.addWidget(_muted("none"))
        for entry in ctx.catalog.effects_in(fx):
            lay.addWidget(self._entry(entry, fx))

    def add(self, entry: CatalogEntry) -> None:
        values = self.ctx.catalog.new_entry(entry)
        spec = entry.params[0]
        if spec.type == "transfer":
            values = {spec.name: {"gold": 1, "from": "player", "to": "self"}}
        elif spec.is_map:
            values = {spec.name: {}}
            names = self.ctx.int_variables if spec.item_type == "int" else self.ctx.variables
            if names:
                values[spec.name] = {names[0]: 1 if spec.item_type == "int" else True}
        elif spec.name in edit.VARIABLE_LIST_KEYS:
            bools = [v for v in self.ctx.variables if v not in self.ctx.int_variables]
            values = {spec.name: bools[:1]}
        self.ctx.merge(self.path, values, f"effect {entry.name}")

    def _entry(self, entry: CatalogEntry, fx: dict) -> QWidget:
        box = QFrame()
        box.setObjectName(f"effect:{_label([*self.path, entry.name])}")
        lay = QVBoxLayout(box)
        lay.setContentsMargins(0, 2, 0, 2)
        lay.setSpacing(1)
        head = QHBoxLayout()
        name = QLabel(entry.name)
        name.setToolTip(entry.description)
        name.setStyleSheet("font-weight: 600;")
        head.addWidget(name, 1)
        head.addWidget(_tool("×", f"Remove {entry.name}", lambda: self.ctx.remove_keys(self.path, list(entry.keys),
                                                                                       f"effect {entry.name}"),
                             f"remove:{_label([*self.path, entry.name])}"))
        lay.addLayout(head)
        for param in entry.params:
            p = [*self.path, param.name]
            if param.type == "transfer":
                lay.addWidget(self._transfer(param, p))
            elif param.is_map:
                types = self._variable_types()
                keys = self.ctx.int_variables if param.item_type == "int" else self.ctx.variables

                def value_for(k: str, param=param) -> FieldSpec:
                    t = "int" if param.item_type == "int" else types.get(k, "any")
                    return FieldSpec(name=k, type=t, description=f"{param.name} {k}")
                lay.addWidget(map_editor(self.ctx, p, value_for, keys, "variable",
                                         lambda k, param=param: 1 if param.item_type == "int" or types.get(k) == "int"
                                         else True))
            else:
                row = QFormLayout()
                row.setContentsMargins(12, 0, 0, 0)
                row.addRow(param.name, field_editor(self.ctx, param, p))
                lay.addLayout(row)
        return box

    def _variable_types(self) -> dict[str, str]:
        return {v: ("int" if v in self.ctx.int_variables else "bool") for v in self.ctx.variables}

    def _transfer(self, spec: FieldSpec, path: list) -> QWidget:
        """Gold xor item (+ count), from and to (actors: player, self, world, other companions)."""
        value = self.ctx.value(path)
        value = value if isinstance(value, dict) else {}
        box = QWidget()
        form = QFormLayout(box)
        form.setContentsMargins(12, 0, 0, 0)
        what = ComboField(["gold", "item"], "item" if "item" in value else "gold",
                          lambda t: self.ctx.replace(path, ["gold"] if t == "item" else ["item", "count"],
                                                     {"item": ""} if t == "item" else {"gold": 1}, "transfer kind"),
                          editable=False)
        what.setObjectName(f"field:{_label([*path, 'kind'])}")
        form.addRow("moves", what)
        fields = {f.name: f for f in self.ctx.catalog.fields(spec.type)}
        shown = ["item", "count"] if "item" in value else ["gold"]
        for name in [*shown, "from", "to"]:
            if name in fields:
                form.addRow(name, field_editor(self.ctx, fields[name], [*path, name]))
        return box


# ---- trigger (T09) ----

def trigger_items(catalog: SchemaCatalog) -> list[str]:
    """Trigger names, common ones first, then by category and name."""
    triggers = sorted(catalog.triggers, key=lambda t: (not t.common, t.category, t.name))
    return [t.name for t in triggers]


def where_keys(catalog: SchemaCatalog, info: TriggerInfo) -> list[str]:
    """The ``where`` paths of a trigger: each field, and ``field.property`` for entity fields."""
    props = catalog.triggers_doc.get("entityProperties", {})
    out = []
    for f in info.fields:
        out.append(f["name"])
        if f.get("entity"):
            out += [f"{f['name']}.{p}" for p in props.get(f["kind"], {}).get("properties", [])]
    return out


def event_fields(catalog: SchemaCatalog, trigger: Any) -> list[str]:
    """``event.<path>`` names a condition may read in content started by ``trigger``."""
    info = catalog.trigger(trigger.get("on")) if isinstance(trigger, dict) else None
    return [f"event.{k}" for k in where_keys(catalog, info)] if info else []


class TriggerEditor(QFrame):
    """A ``trigger`` object: the trigger (grouped, searchable), its subject, ``where`` rows from its fields, the
    delivery fields of the ``trigger`` object and, for polling triggers, ``params``."""

    def __init__(self, ctx: FormContext, path: list):
        super().__init__()
        self.ctx, self.path = ctx, path
        self.setObjectName(f"trigger:{_label(path)}")
        lay = QVBoxLayout(self)
        lay.setContentsMargins(0, 4, 0, 4)
        lay.setSpacing(2)
        lay.addWidget(_title("Trigger", "The game event or check that starts this content (docs/TRIGGERS.md)"))
        spec = ctx.value(path)
        spec = spec if isinstance(spec, dict) else {}
        info = ctx.catalog.trigger(spec.get("on", ""))
        form = QFormLayout()
        form.setContentsMargins(0, 0, 0, 0)
        picker = ComboField(trigger_items(ctx.catalog), str(spec.get("on", "")), self.set_trigger)
        picker.setObjectName(f"field:{_label([*path, 'on'])}")
        for i in range(picker.count()):
            t = ctx.catalog.trigger(picker.itemText(i))
            if t is not None:
                picker.setItemData(i, f"{t.category}{' · common' if t.common else ''}: {t.description}",
                                   Qt.ItemDataRole.ToolTipRole)
        picker.setToolTip(info.description if info else "Pick a trigger")
        form.addRow("on", picker)
        if info is not None:
            form.addRow("", _muted(f"{info.category}{' · polling' if info.polling else ''}: {info.description}"))
            subject_spec = ctx.catalog.field("trigger", "subject")
            subjects = [s.split("<")[0] if "<" in s else s for s in info.subjects]
            form.addRow("subject", field_editor(ctx, FieldSpec(
                name="subject", type="string", description=subject_spec.description + f" Default: {info.default_subject}.",
                default=info.default_subject, has_default=True), [*path, "subject"], subjects))
        for f in ctx.catalog.fields("trigger"):
            if f.name in ("on", "subject", "where", "params"):
                continue
            if f.name == "delivery" and info is not None:
                f = FieldSpec(name=f.name, type=f.type, description=f.description, default=f.default,
                              has_default=f.has_default, allowed=info.deliveries or f.allowed)
            form.addRow(f.name, field_editor(ctx, f, [*path, f.name]))
        lay.addLayout(form)
        if info is not None:
            lay.addWidget(_title("where", ctx.catalog.field("trigger", "where").description))
            types = self._where_types(info)
            lay.addWidget(map_editor(ctx, [*path, "where"],
                                     lambda k: FieldSpec(name=k, type=types.get(k, "any"), description=f"where {k}"),
                                     where_keys(ctx.catalog, info), "where",
                                     lambda k: False if types.get(k) == "bool" else 0 if types.get(k) == "number" else ""))
            if info.params:
                lay.addWidget(_title("params", "Settings of this polling trigger"))
                pform = QFormLayout()
                pform.setContentsMargins(12, 0, 0, 0)
                for p in info.params:
                    t = {"Number": "number", "Int": "int", "Bool": "bool"}.get(p.get("kind"), "string")
                    pform.addRow(p["name"], field_editor(ctx, FieldSpec(
                        name=p["name"], type=t, required=False, description=p.get("description", "")),
                        [*path, "params", p["name"]], ctx.int_variables if p["name"] == "var" else None))
                lay.addLayout(pform)

    def _where_types(self, info: TriggerInfo) -> dict[str, str]:
        props = self.ctx.catalog.triggers_doc.get("entityProperties", {})
        out: dict[str, str] = {}
        for f in info.fields:
            kind = f.get("kind", "")
            out[f["name"]] = "string" if f.get("entity") else {"Bool": "bool", "Number": "number"}.get(kind, "any")
            if f.get("entity"):
                bools = set(props.get(kind, {}).get("boolProperties", []))
                for p in props.get(kind, {}).get("properties", []):
                    out[f"{f['name']}.{p}"] = "bool" if p in bools else "string"
        return out

    def set_trigger(self, name: str) -> None:
        """Another trigger: a subject, ``where`` keys and ``params`` that do not fit it are dropped."""
        name = name.strip()
        info = self.ctx.catalog.trigger(name)
        if info is None:
            self.ctx.message(f"unknown trigger '{name}'")
            return
        spec = self.ctx.value(self.path)
        spec = spec if isinstance(spec, dict) else {}
        drop = []
        subject = spec.get("subject")
        if isinstance(subject, str) and not any(subject == s or ("<" in s and subject.startswith(s.split("<")[0]))
                                                for s in info.subjects):
            drop.append("subject")
        if "where" in spec:
            keys = set(where_keys(self.ctx.catalog, info))
            if not all(k in keys for k in spec["where"]):
                drop.append("where")
        if "params" in spec and not info.params:
            drop.append("params")
        self.ctx.replace(self.path, drop, {"on": name}, f"trigger {name}")
