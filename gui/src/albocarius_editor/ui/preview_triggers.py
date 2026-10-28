"""The preview's Triggers tab (PLAN T17): a ``triggers`` session of the simulator (docs/SIMULATOR_PROTOCOL.md "Triggers").

Built around the question "does my triggered content play?":
1. pick a trigger (the ones this companion's content listens to come first); the content listening to it is listed with
   what it requires (subject, where, when);
2. the companion's state for the test (in party / hired / met, ticked by default: triggered content needs the companion
   in the party unless ``allowAbsent``); gold, items and variables come from the Conversation tab;
3. the event's details: prefilled from the listening content's ``where`` / ``event.*`` conditions, more on demand;
4. **Fire & deliver**; the result says in words what happened to each content and why (with what to change).
The raw candidate checks and the queue stay available below the result.
"""

from __future__ import annotations

import html
from typing import TYPE_CHECKING, Any

from PySide6.QtCore import Qt
from PySide6.QtGui import QColor
from PySide6.QtWidgets import (QCheckBox, QComboBox, QDoubleSpinBox, QFormLayout, QFrame, QHBoxLayout, QLabel,
                               QLineEdit, QPushButton, QScrollArea, QSpinBox, QSplitter, QTabWidget, QTextBrowser,
                               QTreeWidget, QTreeWidgetItem, QVBoxLayout, QWidget)

from ..data.companion import CompanionFolder
from . import forms, theme
from .canvas import summary
from .forms import clear_layout
from .preview import failed_conditions  # preview imports this module lazily, so no cycle

if TYPE_CHECKING:
    from .preview import PreviewPanel

CANDIDATE_COLUMNS = ["content", "kind", "delivery", "subject", "outcome"]
QUEUE_COLUMNS = ["content", "trigger", "delivery", "priority", "firedAt", "expiresAt"]

# The simulator's outcome codes (docs/SIMULATOR_PROTOCOL.md "Triggers") in words, with what to change.
OUTCOMES = {
    "queued": "queued: it waits for delivery",
    "subject": "the event is not about this content's subject ({subject}); change the event details or the subject",
    "where": "the event details do not match its \"where\" ({where})",
    "no hero": "its subject names a hero the event does not carry",
    "not in party": "the companion is not in the player's party: tick \"in party\" above (or set allowAbsent on the "
                    "content's trigger)",
    "cooldown": "it is on cooldown (cooldownDays): press Reset, or Clear cooldowns",
    "already queued": "it is already waiting in the queue: press Deliver",
    "chance": "the chance roll failed (chance below 1): fire again, or change the seed on the Conversation tab",
    "when": "its conditions (\"when\") do not hold",
}


def listening_content(folder: CompanionFolder, trigger: str) -> list[dict[str, Any]]:
    """This companion's lines and conversations whose trigger is ``trigger``: kind, id, the trigger object, when."""
    out = []
    for line in folder.lines:
        if isinstance(line, dict) and isinstance(line.get("trigger"), dict) and line["trigger"].get("on") == trigger:
            out.append({"kind": "line", "id": line.get("id"), "trigger": line["trigger"], "when": line.get("when")})
    for conv in folder.conversations:
        if isinstance(conv.get("trigger"), dict) and conv["trigger"].get("on") == trigger:
            out.append({"kind": "conversation", "id": conv.get("id"), "trigger": conv["trigger"], "when": conv.get("when")})
    return out


def _event_conditions(when: Any) -> dict[str, Any]:
    """``event.<path>`` → the value an ``eq`` condition wants (top level and nested all/any/not are all searched)."""
    found: dict[str, Any] = {}
    if isinstance(when, dict):
        var = when.get("var")
        if isinstance(var, str) and var.startswith("event.") and "eq" in when:
            found[var[len("event."):]] = when["eq"]
        for key in ("all", "any"):
            for sub in when.get(key) or []:
                found.update(_event_conditions(sub))
        found.update(_event_conditions(when.get("not")) if isinstance(when.get("not"), dict) else {})
    elif isinstance(when, list):
        for sub in when:
            found.update(_event_conditions(sub))
    return found


def suggested_payload(content: list[dict[str, Any]]) -> dict[str, Any]:
    """Payload paths (``field`` or ``field.property``) and values that make the content's ``where`` and ``event.*``
    conditions hold. For a ``not`` condition the value is still the one it names: a starting point to edit."""
    values: dict[str, Any] = {}
    for c in content:
        where = c["trigger"].get("where")
        if isinstance(where, dict):
            values.update(where)
        values.update(_event_conditions(c.get("when")))
    return values


class TriggersPreview(QWidget):
    def __init__(self, panel: PreviewPanel):
        super().__init__()
        self.panel = panel
        self.catalog = panel.catalog
        self.folder: CompanionFolder | None = None
        self.session: str | None = None
        self.trigger = ""
        self.extra_paths: list[str] = []          # entity details the user added ("settlement.culture")
        self.payload_widgets: dict[str, QWidget] = {}
        self.last: dict[str, Any] = {}
        lay = QHBoxLayout(self)
        lay.setContentsMargins(0, 4, 0, 0)
        split = QSplitter()
        lay.addWidget(split)

        # ---- left: what to fire ----
        left = QWidget()
        left_lay = QVBoxLayout(left)
        left_lay.setContentsMargins(0, 0, 6, 0)
        left_lay.setSpacing(4)
        left_lay.addWidget(self._section("1. Trigger"))
        self.picker = QComboBox()
        self.picker.setObjectName("trigger_picker")
        forms._narrow(self.picker)
        self.picker.activated.connect(lambda i: self.set_trigger(str(self.picker.itemData(i) or "")))
        left_lay.addWidget(self.picker)
        self.description = self._muted("")
        left_lay.addWidget(self.description)
        self.listening = self._muted("")
        self.listening.setObjectName("listening")
        self.listening.setTextFormat(Qt.TextFormat.RichText)
        left_lay.addWidget(self.listening)

        left_lay.addWidget(self._section("2. Companion during the test"))
        row = QHBoxLayout()
        self.in_party, self.hired, self.met = QCheckBox("in party"), QCheckBox("hired"), QCheckBox("met")
        for box, name in ((self.in_party, "in_party"), (self.hired, "hired"), (self.met, "met")):
            box.setChecked(True)
            box.setObjectName(f"trigger_state:{name}")
            row.addWidget(box)
        row.addStretch(1)
        left_lay.addLayout(row)
        self.in_party.setToolTip("Triggered content plays only while the companion is in the player's party "
                                 "(unless its trigger has allowAbsent)")
        left_lay.addWidget(self._muted("Gold, items, relation and variables come from the Conversation tab's test state."))

        left_lay.addWidget(self._section("3. What happened (event details)"))
        self.payload = QWidget()
        self.payload_form = QFormLayout(self.payload)
        self.payload_form.setContentsMargins(0, 0, 0, 0)
        scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        scroll.setFrameShape(QFrame.Shape.NoFrame)
        scroll.setWidget(self.payload)
        left_lay.addWidget(scroll, 1)

        row = QHBoxLayout()
        self.fire_deliver_button = QPushButton("Fire && deliver")
        self.fire_deliver_button.setObjectName("fire_deliver")
        self.fire_deliver_button.setProperty("accent", True)
        self.fire_deliver_button.setToolTip("Fire the trigger with these details, then deliver what got queued")
        self.fire_deliver_button.clicked.connect(lambda: self.fire(deliver=True))
        self.fire_button = QPushButton("Fire only")
        self.fire_button.setObjectName("fire")
        self.fire_button.clicked.connect(lambda: self.fire(deliver=False))
        self.deliver_button = QPushButton("Deliver")
        self.deliver_button.setObjectName("deliver")
        self.deliver_button.setToolTip("Deliver the next queued item (delivery limits apply)")
        self.deliver_button.clicked.connect(self.deliver)
        self.new_button = QPushButton("Reset")
        self.new_button.setObjectName("reset")
        self.new_button.setToolTip("A fresh trigger session: empty queue, no cooldowns, clock at 0")
        self.new_button.clicked.connect(self.new_session)
        for b in (self.fire_deliver_button, self.fire_button, self.deliver_button, self.new_button):
            row.addWidget(b)
        left_lay.addLayout(row)
        row = QHBoxLayout()
        row.addWidget(QLabel("hour"))
        self.hours = QDoubleSpinBox()
        self.hours.setObjectName("hours")
        self.hours.setRange(0, 1_000_000)
        self.hours.setToolTip("The campaign clock in hours: it only moves forward. Raise it to let expiry, cooldowns "
                              "and the daily delivery limits pass")
        row.addWidget(self.hours, 1)
        self.on_map = QCheckBox("on the map")
        self.on_map.setChecked(True)
        self.on_map.setToolTip("Conversations and inquiries are delivered only on the campaign map")
        self.can_interrupt = QCheckBox("can interrupt")
        self.can_interrupt.setChecked(True)
        self.can_interrupt.setToolTip("Untick to simulate a moment when the game cannot start a conversation")
        row.addWidget(self.on_map)
        row.addWidget(self.can_interrupt)
        left_lay.addLayout(row)
        split.addWidget(left)

        # ---- right: what happened ----
        right = QTabWidget()
        right.setDocumentMode(True)
        self.result = QTextBrowser()
        self.result.setObjectName("result")
        right.addTab(self.result, "Result")
        self.candidates = QTreeWidget()
        self.candidates.setObjectName("candidates")
        self.candidates.setHeaderLabels(CANDIDATE_COLUMNS)
        self.candidates.setRootIsDecorated(False)
        right.addTab(self.candidates, "Checks")
        self.queue = QTreeWidget()
        self.queue.setObjectName("queue")
        self.queue.setHeaderLabels(QUEUE_COLUMNS)
        self.queue.setRootIsDecorated(False)
        right.addTab(self.queue, "Queue")
        self.details = right
        split.addWidget(right)
        split.setSizes([430, 650])
        self.set_available(False)

    @staticmethod
    def _section(text: str) -> QLabel:
        label = QLabel(text)
        label.setProperty("role", "section")
        return label

    @staticmethod
    def _muted(text: str) -> QLabel:
        label = QLabel(text)
        label.setProperty("role", "muted")
        label.setWordWrap(True)
        return label

    def set_available(self, on: bool) -> None:
        for b in (self.fire_deliver_button, self.fire_button, self.deliver_button, self.new_button):
            b.setEnabled(on)

    def set_folder(self, folder: CompanionFolder | None) -> None:
        self.folder = folder
        self.session = None
        self.candidates.clear()
        self.queue.clear()
        self.result.clear()
        self._fill_picker()
        used = self.picker.itemData(self.picker.currentIndex()) if self.picker.count() else ""
        self.set_trigger(str(used or ""))

    def _fill_picker(self) -> None:
        """Triggers this companion's content listens to first (with the content's ids), then all the others."""
        self.picker.clear()
        used: dict[str, list[str]] = {}
        if self.folder is not None:
            for item in [*self.folder.lines, *self.folder.conversations]:
                trig = item.get("trigger") if isinstance(item, dict) else None
                if isinstance(trig, dict) and trig.get("on"):
                    used.setdefault(str(trig["on"]), []).append(str(item.get("id")))
        for name in sorted(used):
            self.picker.addItem(f"{name}  —  {', '.join(used[name])}", name)
        if used:
            self.picker.insertSeparator(self.picker.count())
        for name in forms.trigger_items(self.catalog):
            if name not in used:
                self.picker.addItem(name, name)
        self.picker.setCurrentIndex(0)

    # ---- the trigger and its event details ----

    def set_trigger(self, name: str) -> None:
        name = name.strip()
        info = self.catalog.trigger(name)
        index = self.picker.findData(name)
        if index >= 0:
            self.picker.setCurrentIndex(index)
        self.trigger = name if info else ""
        self.extra_paths = []
        if info is None:
            self.description.setText(f"unknown trigger '{name}'" if name else "")
            self.listening.setText("")
            self._build_payload({})
            return
        self.description.setText(f"{info.category}{' · polling' if info.polling else ''}: {info.description}")
        content = listening_content(self.folder, name) if self.folder is not None else []
        if content:
            rows = []
            for c in content:
                trig = c["trigger"]
                needs = [f"subject {trig['subject']}" if trig.get("subject") else ""]
                if isinstance(trig.get("where"), dict):
                    needs.append("where " + ", ".join(f"{k} = {v}" for k, v in trig["where"].items()))
                needs += summary.conditions(c.get("when"), self.catalog)
                delivery = trig.get("delivery") or ("notification" if c["kind"] == "line" else "conversation")
                need = "; ".join(n for n in needs if n)
                rows.append(f"<b>{html.escape(str(c['id']))}</b> ({c['kind']}, {html.escape(delivery)})"
                            + (f" — needs {html.escape(need)}" if need else ""))
            self.listening.setText("Listening: " + "<br>".join(rows))
        else:
            self.listening.setText(f"No content of {self.folder.id if self.folder else 'this companion'} listens to "
                                   f"{html.escape(name)}: firing it plays nothing here.")
        self._build_payload(suggested_payload(content))

    def _build_payload(self, values: dict[str, Any]) -> None:
        """Plain fields of the event (Bool / Int / Number / Text) always; of an entity field only its id and the
        details something needs (or that were added with "+ detail")."""
        clear_layout(self.payload_form)
        self.payload_widgets = {}
        info = self.catalog.trigger(self.trigger)
        if info is None:
            return
        props = self.catalog.triggers_doc.get("entityProperties", {})
        extra: list[str] = []
        for field in info.fields:
            name, kind = field["name"], field.get("kind", "Text")
            if field.get("entity"):
                bools = set(props.get(kind, {}).get("boolProperties", []))
                known = props.get(kind, {}).get("properties", [])
                shown = ["id"] + [p for p in known if p != "id" and (f"{name}.{p}" in values
                                                                     or f"{name}.{p}" in self.extra_paths)]
                for prop in shown:
                    path = f"{name}.{prop}"
                    value = values.get(path, values.get(name) if prop == "id" else None)
                    widget = self._widget("Bool" if prop in bools else "Text", value)
                    self._add_row(path, f"{name} {prop}" if prop != "id" else f"{name} ({kind}) id", widget,
                                  field.get("description", ""))
                extra += [f"{name}.{p}" for p in known if p not in shown]
            else:
                self._add_row(name, name, self._widget(kind, values.get(name)), field.get("description", ""))
        if extra:
            adder = QComboBox()
            adder.setObjectName("add_detail")
            forms._narrow(adder)
            adder.addItem("+ detail…", None)
            for path in extra:
                adder.addItem(path, path)
            adder.activated.connect(lambda i: adder.itemData(i) and self.add_detail(adder.itemData(i)))
            self.payload_form.addRow(adder)

    def _widget(self, kind: str, value: Any) -> QWidget:
        if kind == "Bool":
            widget: QWidget = QCheckBox()
            widget.setChecked(bool(value))
        elif kind == "Int":
            widget = QSpinBox()
            widget.setRange(-1_000_000_000, 1_000_000_000)
            widget.setValue(int(value) if isinstance(value, (int, float)) and not isinstance(value, bool) else 0)
        elif kind == "Number":
            widget = QDoubleSpinBox()
            widget.setRange(-1e9, 1e9)
            widget.setValue(float(value) if isinstance(value, (int, float)) and not isinstance(value, bool) else 0.0)
        else:
            widget = QLineEdit("" if value is None else str(value))
        return widget

    def _add_row(self, path: str, label: str, widget: QWidget, tip: str) -> None:
        widget.setObjectName(f"payload:{path}")
        widget.setToolTip(tip)
        self.payload_widgets[path] = widget
        self.payload_form.addRow(label, widget)

    def add_detail(self, path: str) -> None:
        values = self.payload_value(flat=True)
        self.extra_paths.append(path)
        extra = self.extra_paths
        self._build_payload(values)
        self.extra_paths = extra

    def payload_value(self, flat: bool = False) -> dict[str, Any]:
        """Field → value; an entity field → its details that were filled in (others read as ""/false).
        ``flat``: the form's own ``field.property`` keys (to rebuild it)."""
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
            if flat:
                payload[key] = value
            elif "." in key:
                field, prop = key.split(".", 1)
                payload.setdefault(field, {})[prop] = value
            else:
                payload[key] = value
        return payload

    def state(self) -> dict[str, Any]:
        """The Conversation tab's test state with this tab's companion flags."""
        state = self.panel.state_form.state()
        if self.folder is not None:
            me = state.setdefault("companions", {}).setdefault(self.folder.id, {})
            me.update({"inParty": self.in_party.isChecked(), "hired": self.hired.isChecked(), "met": self.met.isChecked()})
        return state

    # ---- requests ----

    def _with_session(self, then) -> None:
        """A trigger session with the current state (a new one when the companion flags changed, or after a save:
        reloading the files ends every session)."""
        state = self.state()
        if self.session is not None and self.panel.sim.loaded_root == self.panel._root() and state == self._session_state:
            then()
            return

        def created(response: dict) -> None:
            self.session = response.get("session")
            self._session_state = state
            then()
        self.panel._send("triggers", {"state": state, "seed": self.panel.seed.value()}, created)

    _session_state: dict | None = None

    def new_session(self) -> None:
        if self.session is not None:
            self.panel.sim.request("close", {"session": self.session}, lambda _r: None, lambda _e: None)
        self.session = None
        self.hours.setValue(0)
        self.candidates.clear()
        self.queue.clear()
        self.result.clear()
        self._with_session(lambda: self.result.append("<p><i>New trigger session: empty queue, clock at hour 0.</i></p>"))

    def fire(self, deliver: bool = True) -> None:
        if not self.trigger:
            self.panel._error("pick a trigger to fire")
            return

        def send() -> None:
            fields = {"session": self.session, "trigger": self.trigger, "payload": self.payload_value(),
                      "hours": self.hours.value()}
            self.panel._send("fire", fields, lambda r: self._fired(r, deliver))
        self._with_session(send)

    def _fired(self, response: dict, deliver: bool) -> None:
        self.last = response
        self.hours.setValue(float(response.get("hours", self.hours.value())))
        candidates = [c for c in response.get("candidates") or []
                      if self.folder is None or c.get("companion") == self.folder.id]
        self.result.append(f"<p><b>Fired {html.escape(self.trigger)}</b> at hour {response.get('hours', 0):g}</p>")
        if not candidates:
            self.result.append("<p>Nothing of this companion listens to it.</p>")
        for c in candidates:
            self.result.append(self._explain(c))
        self.candidates.clear()
        for c in candidates:
            item = QTreeWidgetItem([str(c.get(k, "")) for k in CANDIDATE_COLUMNS])
            ok = c.get("outcome") == "queued"
            item.setForeground(CANDIDATE_COLUMNS.index("outcome"), QColor(theme.LINK if ok else theme.WARNING))
            checks = {k: c.get(k) for k in ("subjectMatches", "whereMatches", "present", "offCooldown", "alreadyQueued",
                                            "chanceHit", "whenHolds")}
            item.setToolTip(CANDIDATE_COLUMNS.index("outcome"), "\n".join(
                [f"{k}: {'—' if v is None else v}" for k, v in checks.items()] + failed_conditions(c.get("conditions") or [])))
            self.candidates.addTopLevelItem(item)
        self._show_queue(response.get("queue") or {})
        if deliver and any(c.get("outcome") == "queued" for c in candidates):
            self.deliver()

    def _explain(self, c: dict) -> str:
        outcome = str(c.get("outcome", ""))
        content = next((x for x in listening_content(self.folder, self.trigger) if x["id"] == c.get("content")), None) \
            if self.folder is not None else None
        where = content["trigger"].get("where") if content else None
        text = OUTCOMES.get(outcome, outcome).format(
            subject=c.get("subject", ""), where=", ".join(f"{k} = {v}" for k, v in (where or {}).items()) or "—")
        failed = failed_conditions(c.get("conditions") or []) if outcome == "when" else []
        color = theme.LINK if outcome == "queued" else theme.WARNING
        mark = "✓" if outcome == "queued" else "✗"
        extra = "".join(f"<br>   {html.escape(f)}" for f in failed)
        return (f'<p><span style="color:{color}">{mark}</span> <b>{html.escape(str(c.get("content")))}</b> '
                f'({html.escape(str(c.get("kind")))} → {html.escape(str(c.get("delivery")))}): {html.escape(text)}{extra}</p>')

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
            how = {"notification": "a line in the message log", "inquiry": "a pop-up",
                   "conversation": "the companion starts a conversation on the map"}.get(str(d.get("delivery")), "")
            options = "".join(f"<br>   [{html.escape(str(o.get('text')))}]{'' if o.get('shown') else ' (hidden)'}"
                              for o in d.get("options") or [])
            self.result.append(f'<p><span style="color:{theme.LINK}">▶ Delivered</span> '
                               f"<b>{html.escape(str(d.get('content')))}</b> as {html.escape(how)}:<br>"
                               f"   “{html.escape(str(d.get('text', '')))}”{options}</p>")
            if d.get("node"):
                self.panel.highlight.emit(str(d.get("content")), str(d.get("node")))
        else:
            waiting = len((response.get("queue") or {}).get("items") or [])
            why = (f"{waiting} item(s) still wait: delivery limits (one interruption per day, a companion speaks up at "
                   f"most every few days), \"on the map\" or \"can interrupt\" hold them; raise the hour to let them pass"
                   if waiting else "the queue is empty")
            self.result.append(f"<p><i>Nothing delivered: {html.escape(why)}.</i></p>")
        for dropped in response.get("dropped") or []:
            self.result.append(f'<p style="color:{theme.WARNING}">Dropped {html.escape(str(dropped.get("content")))}: '
                               f'{html.escape(str(dropped.get("reason")))}</p>')
        self._show_queue(response.get("queue") or {})

    def _show_queue(self, queue: dict) -> None:
        self.queue.clear()
        for item in queue.get("items") or []:
            self.queue.addTopLevelItem(QTreeWidgetItem([str(item.get(k, "")) for k in QUEUE_COLUMNS]))
        self.details.setTabText(2, f"Queue ({len(queue.get('items') or [])})")
