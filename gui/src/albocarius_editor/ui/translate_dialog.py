"""Graph → Translate with LLM…: the user's own endpoint (e.g. a local model), the target language, what to send, the
author's instructions, a token estimate; then a review table (each line checked: placeholders kept, nothing missing)
from which the ticked lines are applied as one undoable step. Requests: translate/llm.py."""

from __future__ import annotations

import re

from PySide6.QtCore import Qt
from PySide6.QtGui import QColor
from PySide6.QtWidgets import (QCheckBox, QComboBox, QDialog, QDialogButtonBox, QFormLayout, QHBoxLayout, QLabel,
                               QLineEdit, QPlainTextEdit, QPushButton, QSplitter, QTableWidget, QTableWidgetItem,
                               QVBoxLayout, QWidget)

from ..data.companion import ENGLISH, CompanionFolder
from ..translate import llm
from . import theme, workers

SETTINGS = "llm"  # QSettings group: url, model, key (only when remembered), prompt, includeDialogs, target


class TranslateDialog(QDialog):
    def __init__(self, folder: CompanionFolder, settings, language_pattern: str, conversation_id: str | None = None,
                 parent=None):
        super().__init__(parent)
        self.folder, self.settings, self.language_pattern = folder, settings, language_pattern
        self.conversation_id = conversation_id
        self.result_: llm.Result | None = None
        self.source: dict[str, str] = {}
        self.busy = False
        self.setWindowTitle(f"Translate {folder.id} with an LLM")
        self.resize(1100, 760)
        outer = QVBoxLayout(self)
        split = QSplitter(Qt.Orientation.Vertical)
        outer.addWidget(split, 1)

        top = QWidget()
        form = QFormLayout(top)
        get = lambda key, default="": str(settings.value(f"{SETTINGS}/{key}") or default)  # noqa: E731
        self.url = QLineEdit(get("url"))
        self.url.setObjectName("llm_url")
        self.url.setPlaceholderText("http://localhost:11434/v1  (OpenAI-compatible: Ollama, LM Studio, llama.cpp, …)")
        form.addRow("Endpoint URL", self.url)
        self.model = QLineEdit(get("model"))
        self.model.setObjectName("llm_model")
        self.model.setPlaceholderText("model name as the server knows it, e.g. qwen2.5:14b")
        form.addRow("Model", self.model)
        row = QHBoxLayout()
        self.key = QLineEdit(get("key"))
        self.key.setObjectName("llm_key")
        self.key.setEchoMode(QLineEdit.EchoMode.Password)
        self.key.setPlaceholderText("empty for a local server")
        row.addWidget(self.key, 1)
        self.remember_key = QCheckBox("remember")
        self.remember_key.setChecked(bool(get("key")))
        self.remember_key.setToolTip("Stored in this editor's user settings (not in the companion folder or the repo)")
        row.addWidget(self.remember_key)
        test = QPushButton("Test")
        test.setObjectName("llm_test")
        test.clicked.connect(self.test_connection)
        row.addWidget(test)
        form.addRow("API key", row)
        self.connection = QLabel()
        self.connection.setProperty("role", "muted")
        self.connection.setWordWrap(True)
        form.addRow("", self.connection)

        self.target = QComboBox()
        self.target.setObjectName("llm_target")
        self.target.setEditable(True)
        self.target.addItems([c for c in folder.languages() if c != ENGLISH])
        self.target.setEditText(get("target", self.target.itemText(0) if self.target.count() else ""))
        self.target.lineEdit().setPlaceholderText("language code, e.g. de")
        form.addRow("Into", self.target)
        row = QHBoxLayout()
        self.scope = QComboBox()
        self.scope.setObjectName("llm_scope")
        self.scope.addItem("the whole companion (lines and every conversation)", None)
        if conversation_id:
            self.scope.addItem(f"conversation {conversation_id}", conversation_id)
            self.scope.setCurrentIndex(1)
        row.addWidget(self.scope, 1)
        self.only_missing = QCheckBox("only lines without a translation")
        self.only_missing.setObjectName("llm_only_missing")
        self.only_missing.setChecked(True)
        row.addWidget(self.only_missing)
        form.addRow("Translate", row)
        self.include_dialogs = QCheckBox("also send dialogs.json (who says what, in which order: better context, "
                                         "more tokens)")
        self.include_dialogs.setObjectName("llm_include_dialogs")
        self.include_dialogs.setChecked(get("includeDialogs") == "true")
        form.addRow("Context", self.include_dialogs)
        self.prompt = QPlainTextEdit(get("prompt"))
        self.prompt.setObjectName("llm_prompt")
        self.prompt.setPlaceholderText("Your instructions: tone, register, terms to keep, how the companion speaks…\n"
                                       "(The app adds its own: reply with one JSON object, keep {placeholders}.)")
        self.prompt.setMaximumHeight(110)
        form.addRow("Instructions", self.prompt)
        row = QHBoxLayout()
        self.estimate = QLabel()
        self.estimate.setObjectName("llm_estimate")
        row.addWidget(self.estimate, 1)
        show = QPushButton("Show request")
        show.setToolTip("The exact messages that will be sent")
        show.clicked.connect(self.show_request)
        row.addWidget(show)
        self.translate_button = QPushButton("Translate")
        self.translate_button.setObjectName("llm_translate")
        self.translate_button.setProperty("accent", True)
        self.translate_button.clicked.connect(self.translate)
        row.addWidget(self.translate_button)
        form.addRow("", row)
        split.addWidget(top)

        bottom = QWidget()
        lay = QVBoxLayout(bottom)
        lay.setContentsMargins(0, 0, 0, 0)
        self.status = QLabel("Results appear here for review; nothing is written until you apply.")
        self.status.setProperty("role", "muted")
        self.status.setWordWrap(True)
        lay.addWidget(self.status)
        self.table = QTableWidget(0, 4)
        self.table.setObjectName("llm_results")
        self.table.setHorizontalHeaderLabels(["id", "English", "Translation (editable)", "Check"])
        self.table.horizontalHeader().setStretchLastSection(True)
        self.table.setColumnWidth(0, 170)
        self.table.setColumnWidth(1, 330)
        self.table.setColumnWidth(2, 380)
        self.table.setWordWrap(True)
        lay.addWidget(self.table, 1)
        split.addWidget(bottom)
        split.setSizes([360, 400])

        buttons = QDialogButtonBox()
        self.apply_button = buttons.addButton("Apply ticked lines", QDialogButtonBox.ButtonRole.AcceptRole)
        self.apply_button.setObjectName("llm_apply")
        self.apply_button.setEnabled(False)
        buttons.addButton(QDialogButtonBox.StandardButton.Close)
        buttons.accepted.connect(self.accept)
        buttons.rejected.connect(self.reject)
        outer.addWidget(buttons)

        for signal in (self.target.editTextChanged, self.scope.currentIndexChanged, self.only_missing.toggled,
                       self.include_dialogs.toggled, self.prompt.textChanged):
            signal.connect(self.update_estimate)
        self.update_estimate()

    # ---- what is sent ----

    def endpoint(self) -> llm.Endpoint:
        return llm.Endpoint(self.url.text().strip(), self.model.text().strip(), self.key.text())

    def target_code(self) -> str:
        return self.target.currentText().strip().lower()

    def ids_in_scope(self) -> list[str] | None:
        conversation_id = self.scope.currentData()
        if conversation_id is None:
            return None
        return self.folder.node_ids_by_conversation().get(conversation_id, [])

    def build(self) -> llm.Request | None:
        code = self.target_code()
        if not re.match(self.language_pattern, code) or code == ENGLISH:
            return None
        self.source = llm.texts_to_translate(self.folder, code, self.only_missing.isChecked(), self.ids_in_scope())
        return llm.build_request(self.folder, code, self.source, self.prompt.toPlainText(),
                                 self.include_dialogs.isChecked())

    def update_estimate(self) -> None:
        request = self.build()
        if request is None:
            self.estimate.setText("Enter a target language code (two letters, not en).")
            self.translate_button.setEnabled(False)
            return
        tokens_in, tokens_out = llm.estimate(request)
        self.estimate.setText(f"{len(self.source)} line(s) to translate · ≈ {tokens_in:,} tokens sent, "
                              f"≈ {tokens_out:,} tokens back (rough: ~4 characters per token)")
        self.translate_button.setEnabled(bool(self.source) and not self.busy)

    def show_request(self) -> None:
        request = self.build()
        if request is None:
            return
        view = QDialog(self)
        view.setWindowTitle("Request")
        view.resize(900, 700)
        lay = QVBoxLayout(view)
        text = QPlainTextEdit("\n\n".join(f"── {m['role']} ──\n{m['content']}" for m in request.messages))
        text.setReadOnly(True)
        lay.addWidget(text)
        view.exec()

    # ---- requests ----

    def _remember(self) -> None:
        s = self.settings
        s.setValue(f"{SETTINGS}/url", self.url.text().strip())
        s.setValue(f"{SETTINGS}/model", self.model.text().strip())
        s.setValue(f"{SETTINGS}/key", self.key.text() if self.remember_key.isChecked() else "")
        s.setValue(f"{SETTINGS}/prompt", self.prompt.toPlainText())
        s.setValue(f"{SETTINGS}/includeDialogs", "true" if self.include_dialogs.isChecked() else "false")
        s.setValue(f"{SETTINGS}/target", self.target_code())

    def _ready(self) -> bool:
        if not self.url.text().strip() or not self.model.text().strip():
            self.connection.setText("Enter the endpoint URL and the model.")
            return False
        return True

    def test_connection(self) -> None:
        if not self._ready():
            return
        self.connection.setText("Testing…")
        endpoint = self.endpoint()
        workers.run(lambda: llm.test_connection(endpoint),
                    lambda reply: self.connection.setText(f"Connected: the model answered “{reply}”."),
                    lambda e: self.connection.setText(f"Failed: {e}"), owner=self)

    def translate(self) -> None:
        request = self.build()
        if request is None or not self.source or not self._ready():
            return
        self._remember()
        self.busy = True
        self.translate_button.setEnabled(False)
        self.status.setText(f"Waiting for {self.model.text().strip()}… (a local model can take minutes)")
        endpoint, source = self.endpoint(), dict(self.source)
        workers.run(lambda: llm.check(source, llm.call(endpoint, request)), self._done, self._failed, owner=self)

    def _done(self, result: llm.Result) -> None:
        self.busy = False
        self.result_ = result
        self.update_estimate()
        self.table.setRowCount(0)
        for line in result.lines:
            row = self.table.rowCount()
            self.table.insertRow(row)
            item = QTableWidgetItem(line.local_id)
            item.setFlags(Qt.ItemFlag.ItemIsEnabled | Qt.ItemFlag.ItemIsUserCheckable)
            item.setCheckState(Qt.CheckState.Checked if line.ok else Qt.CheckState.Unchecked)
            self.table.setItem(row, 0, item)
            english = QTableWidgetItem(line.english)
            english.setFlags(Qt.ItemFlag.ItemIsEnabled | Qt.ItemFlag.ItemIsSelectable)
            self.table.setItem(row, 1, english)
            self.table.setItem(row, 2, QTableWidgetItem(line.translation))
            check = QTableWidgetItem("ok" if line.ok else line.problem)
            check.setFlags(Qt.ItemFlag.ItemIsEnabled)
            check.setForeground(QColor(theme.LINK if line.ok else theme.ERROR))
            self.table.setItem(row, 3, check)
        self.table.resizeRowsToContents()
        good = sum(1 for line in result.lines if line.ok)
        extra = f"; ignored ids that were not asked for: {', '.join(result.extra_ids)}" if result.extra_ids else ""
        self.status.setText(f"{good} of {len(result.lines)} line(s) look right (ticked). Lines with a problem are not "
                            f"ticked: fix the text in the table and tick them, or leave them out{extra}.")
        self.apply_button.setEnabled(True)

    def _failed(self, error: Exception) -> None:
        self.busy = False
        self.update_estimate()
        self.status.setText(f'<span style="color:{theme.ERROR}">Translation failed: {error}</span>')

    # ---- the result ----

    def accepted_translations(self) -> dict[str, str]:
        """id → text of the ticked rows (as edited in the table)."""
        out = {}
        for row in range(self.table.rowCount()):
            if self.table.item(row, 0).checkState() == Qt.CheckState.Checked:
                text = self.table.item(row, 2).text()
                if text.strip():
                    out[self.table.item(row, 0).text()] = text
        return out
