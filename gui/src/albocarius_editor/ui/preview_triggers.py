"""The preview's Triggers tab (PLAN T17): a ``triggers`` session of the simulator (docs/SIMULATOR_PROTOCOL.md "Triggers").

Pick a trigger, fill its payload (a form from the trigger's fields; entity fields as property rows), fire it at a
campaign hour, then deliver: the candidate table shows each check's outcome, the queue view what waits."""

from __future__ import annotations

import html
import json
from typing import TYPE_CHECKING, Any

from PySide6.QtGui import QColor
from PySide6.QtWidgets import (QCheckBox, QDoubleSpinBox, QFormLayout, QFrame, QHBoxLayout, QLabel, QLineEdit,
                               QPushButton, QScrollArea, QSpinBox, QSplitter, QTextBrowser, QTreeWidget, QTreeWidgetItem,
                               QVBoxLayout, QWidget)

from ..data.companion import CompanionFolder
from . import forms, theme
from .forms import clear_layout
from .preview import failed_conditions  # preview imports this module lazily, so no cycle

if TYPE_CHECKING:
    from .preview import PreviewPanel

CANDIDATE_COLUMNS = ["companion", "content", "kind", "delivery", "subject", "outcome"]
QUEUE_COLUMNS = ["content", "trigger", "delivery", "priority", "firedAt", "expiresAt"]


class TriggersPreview(QWidget):
    def __init__(self, panel: PreviewPanel):
        super().__init__()
        self.panel = panel
        self.catalog = panel.catalog
        self.folder: CompanionFolder | None = None
        self.session: str | None = None
        self.payload_widgets: dict[str, QWidget] = {}  # "field" or "field.property" → widget
        lay = QHBoxLayout(self)
        lay.setContentsMargins(0, 4, 0, 0)
        split = QSplitter()
        lay.addWidget(split)

        left = QWidget()
        left_lay = QVBoxLayout(left)
        left_lay.setContentsMargins(0, 0, 6, 0)
        self.picker = forms.ComboField(forms.trigger_items(self.catalog), "", self.set_trigger)
        self.picker.setObjectName("trigger_picker")
        self.picker.lineEdit().setPlaceholderText("trigger…")
        left_lay.addWidget(self.picker)
        self.description = QLabel()
        self.description.setProperty("role", "muted")
        self.description.setWordWrap(True)
        left_lay.addWidget(self.description)
        self.payload = QWidget()
        self.payload_form = QFormLayout(self.payload)
        self.payload_form.setContentsMargins(0, 0, 0, 0)
        scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        scroll.setFrameShape(QFrame.Shape.NoFrame)
        scroll.setWidget(self.payload)
        left_lay.addWidget(scroll, 1)
        row = QHBoxLayout()
        row.addWidget(QLabel("hours"))
        self.hours = QDoubleSpinBox()
        self.hours.setObjectName("hours")
        self.hours.setRange(0, 1_000_000)
        self.hours.setToolTip("The campaign clock in hours (only moves forward)")
        row.addWidget(self.hours, 1)
        left_lay.addLayout(row)
        row = QHBoxLayout()
        self.fire_button = QPushButton("Fire")
        self.fire_button.setObjectName("fire")
        self.fire_button.setProperty("accent", True)
        self.fire_button.clicked.connect(self.fire)
        self.deliver_button = QPushButton("Deliver")
        self.deliver_button.setObjectName("deliver")
        self.deliver_button.clicked.connect(self.deliver)
        self.new_button = QPushButton("New session")
        self.new_button.setToolTip("A fresh trigger session with the test state of the Conversation tab")
        self.new_button.clicked.connect(self.new_session)
        self.cooldowns_button = QPushButton("Clear cooldowns")
        self.cooldowns_button.clicked.connect(self.clear_cooldowns)
        for b in (self.fire_button, self.deliver_button, self.new_button, self.cooldowns_button):
            row.addWidget(b)
        left_lay.addLayout(row)
        row = QHBoxLayout()
        self.on_map = QCheckBox("on the map")
        self.on_map.setChecked(True)
        self.can_interrupt = QCheckBox("can interrupt")
        self.can_interrupt.setChecked(True)
        row.addWidget(self.on_map)
        row.addWidget(self.can_interrupt)
        left_lay.addLayout(row)
        split.addWidget(left)

        right = QWidget()
        right_lay = QVBoxLayout(right)
        right_lay.setContentsMargins(6, 0, 0, 0)
        right_lay.addWidget(self._section("Candidates (last fire)"))
        self.candidates = QTreeWidget()
        self.candidates.setObjectName("candidates")
        self.candidates.setHeaderLabels(CANDIDATE_COLUMNS)
        self.candidates.setRootIsDecorated(False)
        right_lay.addWidget(self.candidates, 1)
        right_lay.addWidget(self._section("Queue"))
        self.queue = QTreeWidget()
        self.queue.setObjectName("queue")
        self.queue.setHeaderLabels(QUEUE_COLUMNS)
        self.queue.setRootIsDecorated(False)
        right_lay.addWidget(self.queue, 1)
        right_lay.addWidget(self._section("Delivered"))
        self.delivered = QTextBrowser()
        self.delivered.setObjectName("delivered")
        self.delivered.setMaximumHeight(110)
        right_lay.addWidget(self.delivered)
        split.addWidget(right)
        split.setSizes([360, 720])
        self.last: dict[str, Any] = {}
        self.set_available(False)

    @staticmethod
    def _section(text: str) -> QLabel:
        label = QLabel(text)
        label.setProperty("role", "section")
        return label

    def set_available(self, on: bool) -> None:
        for b in (self.fire_button, self.deliver_button, self.new_button, self.cooldowns_button):
            b.setEnabled(on)

    def set_folder(self, folder: CompanionFolder | None) -> None:
        self.folder = folder
        self.session = None
        self.candidates.clear()
        self.queue.clear()
        self.delivered.clear()
        if folder is not None:
            used = self._used_trigger(folder)
            if used:
                self.picker.setEditText(used)
                self.set_trigger(used)

    @staticmethod
    def _used_trigger(folder: CompanionFolder) -> str | None:
        for item in [*folder.conversations, *folder.lines]:
            if isinstance(item, dict) and isinstance(item.get("trigger"), dict) and item["trigger"].get("on"):
                return str(item["trigger"]["on"])
        return None

    # ---- the payload form ----

    def set_trigger(self, name: str) -> None:
        name = name.strip()
        info = self.catalog.trigger(name)
        clear_layout(self.payload_form)
        self.payload_widgets = {}
        self.trigger = name if info else ""
        if info is None:
            self.description.setText(f"unknown trigger '{name}'" if name else "")
            return
        self.description.setText(f"{info.category}{' · polling' if info.polling else ''}: {info.description}")
        props = self.catalog.triggers_doc.get("entityProperties", {})
        for field in info.fields:
            kind = field.get("kind", "Text")
            if field.get("entity"):
                title = QLabel(f"{field['name']} ({kind})")
                title.setToolTip(field.get("description", ""))
                title.setStyleSheet("font-weight: 600;")
                self.payload_form.addRow(title)
                bools = set(props.get(kind, {}).get("boolProperties", []))
                for prop in props.get(kind, {}).get("properties", []):
                    key = f"{field['name']}.{prop}"
                    widget = QCheckBox() if prop in bools else QLineEdit()
                    widget.setObjectName(f"payload:{key}")
                    self.payload_widgets[key] = widget
                    self.payload_form.addRow(f"  {prop}", widget)
            else:
                if kind == "Bool":
                    widget = QCheckBox()
                elif kind == "Int":
                    widget = QSpinBox()
                    widget.setRange(-1_000_000_000, 1_000_000_000)
                elif kind == "Number":
                    widget = QDoubleSpinBox()
                    widget.setRange(-1e9, 1e9)
                else:
                    widget = QLineEdit()
                widget.setObjectName(f"payload:{field['name']}")
                widget.setToolTip(field.get("description", ""))
                self.payload_widgets[field["name"]] = widget
                self.payload_form.addRow(field["name"], widget)

    def payload_value(self) -> dict[str, Any]:
        """Field → value; an entity field → its properties that were filled in (others read as ""/false)."""
        payload: dict[str, Any] = {}
        for key, widget in self.payload_widgets.items():
            if isinstance(widget, QCheckBox):
                value: Any = widget.isChecked()
                if "." in key and not value:
                    continue
            elif isinstance(widget, (QSpinBox, QDoubleSpinBox)):
                value = widget.value()
            else:
                value = widget.text().strip()
                if not value and "." in key:
                    continue
            if "." in key:
                field, prop = key.split(".", 1)
                payload.setdefault(field, {})[prop] = value
            else:
                payload[key] = value
        return payload

    # ---- requests ----

    def _with_session(self, then) -> None:
        if self.session is not None and self.panel.sim.loaded_root == self.panel._root():
            then()
            return

        def created(response: dict) -> None:
            self.session = response.get("session")
            used = ", ".join(response.get("usedTriggers", []))
            self.delivered.append(f"<p><i>trigger session {html.escape(str(self.session))}; content listens to: "
                                  f"{html.escape(used or 'nothing')}</i></p>")
            then()
        self.panel._send("triggers", {"state": self.panel.state_form.state(), "seed": self.panel.seed.value()}, created)

    def new_session(self) -> None:
        if self.session is not None:
            self.panel.sim.request("close", {"session": self.session}, lambda _r: None, lambda _e: None)
        self.session = None
        self.candidates.clear()
        self.queue.clear()
        self._with_session(lambda: None)

    def fire(self) -> None:
        if not getattr(self, "trigger", ""):
            self.panel._error("pick a trigger to fire")
            return

        def send() -> None:
            fields = {"session": self.session, "trigger": self.trigger, "payload": self.payload_value(),
                      "hours": self.hours.value()}
            self.panel._send("fire", fields, self._fired)
        self._with_session(send)

    def _fired(self, response: dict) -> None:
        self.last = response
        self.hours.setValue(float(response.get("hours", self.hours.value())))
        self.candidates.clear()
        for c in response.get("candidates") or []:
            item = QTreeWidgetItem([str(c.get(k, "")) for k in CANDIDATE_COLUMNS])
            ok = c.get("outcome") == "queued"
            item.setForeground(CANDIDATE_COLUMNS.index("outcome"), QColor(theme.LINK if ok else theme.WARNING))
            failed = failed_conditions(c.get("conditions") or [])
            item.setToolTip(CANDIDATE_COLUMNS.index("outcome"), "\n".join(failed) or json.dumps(
                {k: c.get(k) for k in ("subjectMatches", "whereMatches", "present", "offCooldown", "alreadyQueued",
                                       "chanceHit", "whenHolds")}))
            self.candidates.addTopLevelItem(item)
        if not response.get("candidates"):
            self.candidates.addTopLevelItem(QTreeWidgetItem(["", "no content of a loaded companion listens to this trigger"]))
        self._show_queue(response.get("queue") or {})

    def deliver(self) -> None:
        def send() -> None:
            fields = {"session": self.session, "hours": self.hours.value(), "onMap": self.on_map.isChecked(),
                      "canInterrupt": self.can_interrupt.isChecked()}
            self.panel._send("deliver", fields, self._delivered)
        self._with_session(send)

    def _delivered(self, response: dict) -> None:
        self.last = response
        self.hours.setValue(float(response.get("hours", self.hours.value())))
        d = response.get("delivered")
        if d:
            text = html.escape(str(d.get("text", "")))
            where = html.escape(f"{d.get('content')} ({d.get('delivery')}, {d.get('trigger')})")
            options = "".join(f"<br>  [{html.escape(str(o.get('text')))}]{'' if o.get('shown') else ' (hidden)'}"
                              for o in d.get("options") or [])
            self.delivered.append(f"<p><b>{where}</b>: {text}{options}</p>")
            if d.get("node"):
                self.panel.highlight.emit(str(d.get("content")), str(d.get("node")))
        else:
            self.delivered.append("<p><i>nothing to deliver</i></p>")
        for dropped in response.get("dropped") or []:
            self.delivered.append(f'<p style="color:{theme.WARNING}">dropped {html.escape(str(dropped.get("content")))}: '
                                  f'{html.escape(str(dropped.get("reason")))}</p>')
        self._show_queue(response.get("queue") or {})

    def clear_cooldowns(self) -> None:
        if self.session is not None:
            self.panel._send("clearCooldowns", {"session": self.session}, lambda r: self._show_queue(r.get("queue") or r))

    def _show_queue(self, queue: dict) -> None:
        self.queue.clear()
        for item in queue.get("items") or []:
            self.queue.addTopLevelItem(QTreeWidgetItem([str(item.get(k, "")) for k in QUEUE_COLUMNS]))
