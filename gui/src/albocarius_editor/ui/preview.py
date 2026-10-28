"""Bottom panel, second tab: the simulator (CLI ``serve``, docs/SIMULATOR_PROTOCOL.md), never reimplemented here.

One ``serve`` process is kept alive; requests run one at a time on a worker thread (:class:`workers.SerialRunner`).
* **Conversation**: a test state (generated from the protocol's "State": the companion's met/hired/inParty/relation/gold,
  the player's gold and items, clanIsFull, every declared variable, started conversations), start from the hub or as a
  greeting, the step's options as buttons (hidden ones greyed, their failed conditions as tooltip), a transcript of what
  was said with the selection and effects, Hire / Back to hub / End talk. The node of the last line said is highlighted
  on the canvas.
* **Triggers** (PLAN T17): a trigger session: fire a trigger with a payload, deliver, see candidates and the queue.
"""

from __future__ import annotations

import html
from pathlib import Path
from typing import Any

from PySide6.QtCore import Signal
from PySide6.QtWidgets import (QCheckBox, QFormLayout, QFrame, QHBoxLayout, QLabel, QLineEdit, QPushButton, QScrollArea,
                               QSpinBox, QSplitter, QTabWidget, QTextBrowser, QVBoxLayout, QWidget)

from ..cli.client import CliError, ServeClient
from ..data.companion import CompanionFolder
from ..schema.catalog import SchemaCatalog
from . import theme, workers
from .forms import clear_layout

INT_MAX = 1_000_000_000


def _section(text: str) -> QLabel:
    label = QLabel(text)
    label.setProperty("role", "section")
    return label


def _muted(text: str) -> QLabel:
    label = QLabel(text)
    label.setProperty("role", "muted")
    label.setWordWrap(True)
    return label


def _spin(low: int, high: int, value: int, name: str) -> QSpinBox:
    spin = QSpinBox()
    spin.setRange(low, high)
    spin.setValue(value)
    spin.setObjectName(name)
    return spin


def failed_conditions(conditions: list[dict], depth: int = 0) -> list[str]:
    """The checks that did not hold, one line each (nested ones indented)."""
    out = []
    for c in conditions or []:
        if not c.get("holds", True):
            line = f"{'  ' * depth}{c.get('field')}: expected {c.get('expected')}, is {c.get('actual')}"
            out.append(line)
            out += failed_conditions(c.get("nested") or [], depth + 1)
    return out


def scrollable(widget: QWidget) -> QScrollArea:
    """The widget in a frameless scroll area: the bottom panel can then be made as small as the user likes."""
    scroll = QScrollArea()
    scroll.setWidgetResizable(True)
    scroll.setFrameShape(QFrame.Shape.NoFrame)
    scroll.setWidget(widget)
    widget.setMinimumHeight(260)  # below this the panel scrolls instead of squeezing the forms
    scroll.setMinimumHeight(24)
    return scroll


class StateForm(QWidget):
    """The test state of one companion (docs/SIMULATOR_PROTOCOL.md "State"), read back from every response."""

    def __init__(self, parent=None):
        super().__init__(parent)
        self.companion = ""
        self.form = QFormLayout(self)
        self.form.setContentsMargins(0, 0, 0, 0)
        self.variables: dict[str, QWidget] = {}

    def build(self, folder: CompanionFolder, catalog: SchemaCatalog) -> None:
        clear_layout(self.form)
        self.companion = folder.id
        self.variables = {}
        self.met, self.hired, self.in_party = QCheckBox("met"), QCheckBox("hired"), QCheckBox("in party")
        for box, name in ((self.met, "met"), (self.hired, "hired"), (self.in_party, "inParty")):
            box.setObjectName(f"state:{name}")
        flags = QHBoxLayout()
        for box in (self.met, self.hired, self.in_party):
            flags.addWidget(box)
        self.form.addRow(folder.id, flags)
        relation_range = catalog.schema["rules"].get("relationRange", [-100, 100])
        self.relation = _spin(relation_range[0], relation_range[1], 0, "state:relation")
        self.form.addRow("relation", self.relation)
        self.companion_gold = _spin(0, INT_MAX, 0, "state:companionGold")
        self.form.addRow("companion gold", self.companion_gold)
        self.player_gold = _spin(0, INT_MAX, 1000, "state:playerGold")
        self.form.addRow("player gold", self.player_gold)
        self.items = QLineEdit()
        self.items.setObjectName("state:items")
        self.items.setPlaceholderText("grain=3, sumpter_horse=1")
        self.form.addRow("player items", self.items)
        self.clan_full = QCheckBox("the clan is full")
        self.clan_full.setObjectName("state:clanIsFull")
        self.form.addRow("", self.clan_full)
        declared = [(n, d.get("type"), d.get("default")) for n, d in folder.variables.items() if isinstance(d, dict)]
        declared += [(b["name"], b["type"], b["default"]) for b in catalog.enum("builtinVariables")]
        for name, var_type, default in declared:
            if var_type == "int":
                widget: QWidget = _spin(-INT_MAX, INT_MAX, int(default or 0), f"state:var:{name}")
            else:
                widget = QCheckBox()
                widget.setChecked(bool(default))
                widget.setObjectName(f"state:var:{name}")
            self.variables[f"{folder.id}.{name}"] = widget
            self.form.addRow(name, widget)
        self.started = QLineEdit()
        self.started.setObjectName("state:started")
        self.started.setPlaceholderText("conversations already started (for once), comma separated")
        self.form.addRow("started", self.started)

    def state(self) -> dict[str, Any]:
        if not self.companion:
            return {}
        items = {}
        for part in self.items.text().split(","):
            if "=" in part:
                key, _, count = part.partition("=")
                if key.strip() and count.strip().isdigit():
                    items[key.strip()] = int(count.strip())
        variables = {k: (w.value() if isinstance(w, QSpinBox) else w.isChecked()) for k, w in self.variables.items()}
        started = [f"{self.companion}:{s.strip()}" for s in self.started.text().split(",") if s.strip()]
        return {
            "companions": {self.companion: {"met": self.met.isChecked(), "hired": self.hired.isChecked(),
                                            "inParty": self.in_party.isChecked(), "relation": self.relation.value(),
                                            "gold": self.companion_gold.value()}},
            "player": {"gold": self.player_gold.value(), "items": items},
            "clanIsFull": self.clan_full.isChecked(),
            "variables": variables,
            "started": started,
        }

    def load(self, state: dict[str, Any]) -> None:
        if not self.companion or not isinstance(state, dict):
            return
        me = (state.get("companions") or {}).get(self.companion) or {}
        self.met.setChecked(bool(me.get("met", self.met.isChecked())))
        self.hired.setChecked(bool(me.get("hired", self.hired.isChecked())))
        self.in_party.setChecked(bool(me.get("inParty", self.in_party.isChecked())))
        self.relation.setValue(int(me.get("relation", self.relation.value())))
        self.companion_gold.setValue(int(me.get("gold", self.companion_gold.value())))
        player = state.get("player") or {}
        self.player_gold.setValue(int(player.get("gold", self.player_gold.value())))
        if isinstance(player.get("items"), dict):
            self.items.setText(", ".join(f"{k}={v}" for k, v in player["items"].items()))
        self.clan_full.setChecked(bool(state.get("clanIsFull", self.clan_full.isChecked())))
        for key, value in (state.get("variables") or {}).items():
            widget = self.variables.get(key)
            if isinstance(widget, QSpinBox) and isinstance(value, int) and not isinstance(value, bool):
                widget.setValue(value)
            elif isinstance(widget, QCheckBox) and isinstance(value, bool):
                widget.setChecked(value)
        prefix = f"{self.companion}:"
        self.started.setText(", ".join(s[len(prefix):] for s in state.get("started") or [] if s.startswith(prefix)))


class Simulator:
    """The ``serve`` process behind the panel: started on first use, requests one at a time off the GUI thread."""

    def __init__(self, owner: QWidget):
        self.runner = workers.SerialRunner(owner)
        self.client: ServeClient | None = None
        self.loaded_root: Path | None = None

    def set_cli(self, cli_path: Path | None) -> None:
        self.close()
        self.client = ServeClient(cli_path) if cli_path else None

    def request(self, op: str, fields: dict[str, Any], on_done, on_error) -> None:
        client = self.client
        if client is None:
            on_error(CliError("the CLI was not found"))
            return
        self.runner.submit(lambda: client.request(op, **fields), on_done, on_error)

    def load(self, root: Path, on_done, on_error) -> None:
        def done(response: dict) -> None:
            self.loaded_root = root
            on_done(response)
        self.request("load", {"root": str(root)}, done, on_error)

    def close(self) -> None:
        if self.client is not None:
            self.client.close()
        self.loaded_root = None


class PreviewPanel(QWidget):
    highlight = Signal(str, str)  # conversation id, node id of the last line said ("" when none)
    message = Signal(str)

    def __init__(self, catalog: SchemaCatalog | None = None, parent=None):
        super().__init__(parent)
        self.catalog = catalog
        self.folder: CompanionFolder | None = None
        self.sim = Simulator(self)
        self.session: str | None = None
        self.steps = 0  # answered requests (tests wait on it)
        self.last_step: dict | None = None
        outer = QVBoxLayout(self)
        outer.setContentsMargins(10, 6, 10, 6)
        self.status = _muted("")
        outer.addWidget(self.status)
        self.tabs = QTabWidget()
        self.tabs.setDocumentMode(True)
        outer.addWidget(self.tabs, 1)
        self._build_conversation_tab()
        self.triggers = None  # TriggersPreview (T17), created with a catalog
        if catalog is not None:
            from .preview_triggers import TriggersPreview

            self.triggers = TriggersPreview(self)
            self.tabs.addTab(scrollable(self.triggers), "Triggers")
        self.set_cli(None)

    # ---- layout ----

    def _build_conversation_tab(self) -> None:
        tab = QWidget()
        lay = QHBoxLayout(tab)
        lay.setContentsMargins(0, 4, 0, 0)
        split = QSplitter()
        lay.addWidget(split)

        left = QWidget()
        left_lay = QVBoxLayout(left)
        left_lay.setContentsMargins(0, 0, 6, 0)
        left_lay.addWidget(_section("Test state"))
        self.state_form = StateForm()
        scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        scroll.setFrameShape(QFrame.Shape.NoFrame)
        scroll.setWidget(self.state_form)
        left_lay.addWidget(scroll, 1)
        row = QHBoxLayout()
        self.seed = _spin(0, INT_MAX, 1, "seed")
        self.seed.setToolTip("Random ties between equal candidates are repeatable with the same seed")
        row.addWidget(QLabel("seed"))
        row.addWidget(self.seed)
        self.apply_button = QPushButton("Apply state")
        self.apply_button.setObjectName("apply_state")
        self.apply_button.setToolTip("Send the state to the running session (setState)")
        self.apply_button.clicked.connect(self.apply_state)
        row.addWidget(self.apply_button)
        left_lay.addLayout(row)
        split.addWidget(left)

        middle = QWidget()
        mid_lay = QVBoxLayout(middle)
        mid_lay.setContentsMargins(6, 0, 6, 0)
        buttons = QHBoxLayout()
        self.start_hub = QPushButton("▶ Hub")
        self.start_hub.setToolTip("Start a talk at the companion's menu")
        self.start_hub.setObjectName("start_hub")
        self.start_hub.clicked.connect(lambda: self.start("hub"))
        self.start_greeting = QPushButton("▶ Greeting")
        self.start_greeting.setToolTip("Start a talk as the player talking to the companion (a greeting may open it)")
        self.start_greeting.setObjectName("start_greeting")
        self.start_greeting.clicked.connect(lambda: self.start("greeting"))
        for b in (self.start_hub, self.start_greeting):
            buttons.addWidget(b)
        mid_lay.addLayout(buttons)
        buttons = QHBoxLayout()
        self.hire_button = QPushButton("Hire")
        self.hire_button.setObjectName("hire")
        self.hire_button.setToolTip("The mod's fixed hire dialog (pays hireCost)")
        self.hire_button.clicked.connect(self.hire)
        self.hub_button = QPushButton("Back to hub")
        self.hub_button.setObjectName("hub")
        self.hub_button.clicked.connect(self.back_to_hub)
        self.end_button = QPushButton("End talk")
        self.end_button.setObjectName("end_talk")
        self.end_button.clicked.connect(self.end_talk)
        for b in (self.hire_button, self.hub_button, self.end_button):
            buttons.addWidget(b)
        mid_lay.addLayout(buttons)
        mid_lay.addWidget(_section("Options"))
        self.options = QWidget()
        self.options_layout = QVBoxLayout(self.options)
        self.options_layout.setContentsMargins(0, 0, 0, 0)
        scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        scroll.setFrameShape(QFrame.Shape.NoFrame)
        scroll.setWidget(self.options)
        mid_lay.addWidget(scroll, 1)
        split.addWidget(middle)

        right = QWidget()
        right_lay = QVBoxLayout(right)
        right_lay.setContentsMargins(6, 0, 0, 0)
        head = QHBoxLayout()
        head.addWidget(_section("Transcript"), 1)
        clear = QPushButton("Clear")
        clear.clicked.connect(lambda: self.transcript.clear())
        head.addWidget(clear)
        right_lay.addLayout(head)
        self.transcript = QTextBrowser()
        self.transcript.setObjectName("transcript")
        right_lay.addWidget(self.transcript, 1)
        split.addWidget(right)
        split.setSizes([300, 260, 520])
        self.tabs.addTab(scrollable(tab), "Conversation")
        self._set_running(False)

    # ---- setup ----

    def set_cli(self, cli_path: str | None, error: str | None = None) -> None:
        self.sim.set_cli(Path(cli_path) if cli_path else None)
        self.available = bool(cli_path)
        if cli_path:
            self.status.setText(f"Simulator: {cli_path}")
        else:
            self.status.setText(error or "The AlbocariusCompanions CLI was not found: build tools/AlbocariusCompanions.Cli "
                                         "or set ALBOCARIUS_CLI. Editing still works; validation and preview need the CLI.")
        for b in (self.start_hub, self.start_greeting):
            b.setEnabled(self.available and self.folder is not None)
        if self.triggers is not None:
            self.triggers.set_available(self.available)

    def set_folder(self, folder: CompanionFolder | None) -> None:
        self.stop()
        self.folder = folder
        if folder is not None and self.catalog is not None:
            self.state_form.build(folder, self.catalog)
        for b in (self.start_hub, self.start_greeting):
            b.setEnabled(self.available and folder is not None)
        if self.triggers is not None:
            self.triggers.set_folder(folder)

    def files_saved(self) -> None:
        """The simulator reads the files on disk: reload them before the next request."""
        self.sim.loaded_root = None

    def shutdown(self) -> None:
        self.stop()
        self.sim.close()

    # ---- requests ----

    def _root(self) -> Path | None:
        return self.folder.path.parent if self.folder is not None else None

    def _send(self, op: str, fields: dict[str, Any], on_done) -> None:
        """Loads the Companions directory first when needed (after a save or a folder change)."""
        root = self._root()
        if root is None:
            return
        if self.sim.loaded_root != root:
            self.session = None

            def loaded(response: dict) -> None:
                if self.folder is not None and self.folder.id not in response.get("companions", []):
                    issues = [i.get("message", "") for i in response.get("issues", [])
                              if str(i.get("folder", "")).endswith(self.folder.id)]
                    self._error(f"{self.folder.id} did not load (fix its validation errors first)"
                                + (f": {issues[0]}" if issues else ""))
            self.sim.load(root, loaded, self._failed)
        self.sim.request(op, fields, lambda r: self._answered(on_done, r), self._failed)

    def _answered(self, on_done, response: dict) -> None:
        self.steps += 1
        on_done(response)

    def _failed(self, error: Exception) -> None:
        self.steps += 1
        self._error(str(error))

    def _error(self, text: str) -> None:
        self.transcript.append(f'<p style="color:{theme.ERROR}">{html.escape(text)}</p>')
        self.message.emit(f"Preview: {text}")

    def start(self, entry: str) -> None:
        if self.folder is None:
            return
        if self.session is not None:
            self.sim.request("close", {"session": self.session}, lambda _r: None, lambda _e: None)
            self.session = None
        self.transcript.append(f"<p><b>— new talk ({entry}) —</b></p>")
        fields = {"companion": self.folder.id, "entry": entry, "state": self.state_form.state(), "seed": self.seed.value()}

        def started(response: dict) -> None:
            self.session = response.get("session")
            self._set_running(True)
            self._show_step(response.get("step") or {})
        self._send("start", fields, started)

    def choose(self, conversation: str, node: str) -> None:
        if self.session is None:
            return
        self._send("choose", {"session": self.session, "conversation": conversation, "node": node},
                   lambda r: self._show_step(r.get("step") or {}))

    def hire(self) -> None:
        if self.session is None:
            return

        def hired(response: dict) -> None:
            if response.get("hired"):
                self.transcript.append("<p><i>hired</i></p>")
            else:
                self.transcript.append(f"<p><i>hire refused: {html.escape(str(response.get('reason')))}</i></p>")
            self.state_form.load(response.get("state") or {})
        self._send("hire", {"session": self.session}, hired)

    def back_to_hub(self) -> None:
        if self.session is not None:
            self._send("hub", {"session": self.session}, lambda r: self._show_step(r.get("step") or {}))

    def end_talk(self) -> None:
        if self.session is None:
            return

        def ended(_response: dict) -> None:
            self.transcript.append("<p><i>— talk ended —</i></p>")
            self._show_options([])
            self.highlight.emit("", "")
        self._send("endTalk", {"session": self.session}, ended)

    def apply_state(self) -> None:
        if self.session is None:
            return
        self._send("setState", {"session": self.session, "state": self.state_form.state()},
                   lambda r: (self.state_form.load(r.get("state") or {}), self.transcript.append("<p><i>state set</i></p>")))

    def stop(self) -> None:
        if self.session is not None and self.sim.client is not None and self.sim.client.running:
            self.sim.request("close", {"session": self.session}, lambda _r: None, lambda _e: None)
        self.session = None
        self._show_options([])
        self._set_running(False)
        self.highlight.emit("", "")

    def _set_running(self, on: bool) -> None:
        for b in (self.hire_button, self.hub_button, self.end_button, self.apply_button):
            b.setEnabled(on)

    # ---- showing a step ----

    def _show_step(self, step: dict) -> None:
        self.last_step = step
        for s in step.get("selections") or []:
            if s.get("set") == "greeting" and s.get("winner"):
                self.transcript.append(f"<p><i>greeting: {html.escape(str(s['winner']))}</i></p>")
        last = None
        for said in step.get("said") or []:
            self._show_said(said)
            last = said
        kind = step.get("kind")
        if kind == "end":
            self.transcript.append("<p><i>— the talk ended —</i></p>")
        elif kind == "stuck":
            failed = next((s for s in step.get("selections") or [] if s.get("set") == "npc"), {})
            self.transcript.append(f'<p style="color:{theme.ERROR}">stuck: no companion reply holds '
                                   f"({html.escape(', '.join(c.get('id', '') for c in failed.get('candidates', [])))})</p>")
        self._show_options(step.get("options") or [] if kind == "options" else [])
        if step.get("state"):
            self.state_form.load(step["state"])
        if last is not None:
            self.highlight.emit(str(last.get("conversation", "")), str(last.get("node", "")))
        self.transcript.verticalScrollBar().setValue(self.transcript.verticalScrollBar().maximum())

    def _show_said(self, said: dict) -> None:
        who = "You" if said.get("speaker") == "player" else (self.folder.id if self.folder else "Companion")
        color = theme.CARD_STYLES["player" if said.get("speaker") == "player" else "npc"].badge
        lines = [f'<p><b style="color:{color}">{html.escape(who)}</b>: {html.escape(str(said.get("text", "")))}'
                 f'<br><span style="color:{theme.MUTED}">{html.escape(str(said.get("conversation")))}/'
                 f'{html.escape(str(said.get("node")))}']
        selection = said.get("selection")
        if isinstance(selection, dict) and len(selection.get("candidates") or []) > 1:
            parts = [f"{c.get('id')}{'' if c.get('passes') else ' ✗'} (p{c.get('priority', 0)})"
                     for c in selection["candidates"]]
            lines.append(f" · picked {html.escape(str(selection.get('winner')))} of {html.escape(', '.join(parts))}")
        for effect in said.get("effects") or []:
            lines.append(f"<br>  → {html.escape(str(effect))}")
        lines.append("</span></p>")
        self.transcript.append("".join(lines))

    def _show_options(self, options: list[dict]) -> None:
        clear_layout(self.options_layout)
        for option in options:
            conversation, node = str(option.get("conversation")), str(option.get("node"))
            text = (self.folder.text(node) if self.folder else None) or node
            button = QPushButton(text)
            button.setObjectName(f"option:{conversation}/{node}")
            button.setStyleSheet("text-align: left; padding: 6px 10px;")
            if option.get("shown"):
                button.setToolTip(f"{conversation}/{node}")
                button.clicked.connect(lambda _=False, c=conversation, n=node: self.choose(c, n))
            else:
                button.setEnabled(False)
                why = failed_conditions(option.get("conditions") or [])
                button.setToolTip(f"{conversation}/{node} — hidden:\n" + ("\n".join(why) or "its conditions do not hold"))
            self.options_layout.addWidget(button)
        if not options:
            self.options_layout.addWidget(_muted("Start a talk to see the player's options here."
                                                 if self.session is None else "No options."))
        self.options_layout.addStretch(1)
