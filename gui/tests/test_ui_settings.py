"""Settings and recent folders (PLAN T18)."""

from pathlib import Path

from PySide6.QtCore import QSettings

from albocarius_editor.cli import client
from albocarius_editor.cli.client import Cli
from albocarius_editor.ui import settings as settings_ui
from albocarius_editor.ui.main_window import MainWindow


def test_cli_path_setting_is_used(window: MainWindow, tmp_path: Path, monkeypatch):
    monkeypatch.delenv(client.ENV_VAR, raising=False)
    fake = tmp_path / "AlbocariusCompanions.Cli.dll"
    fake.write_text("")
    window.settings.setValue("cliPath", str(fake))
    assert Cli.find(window.settings.value("cliPath")).path == fake
    window.apply_cli_setting()
    assert window.cli.path == fake and window.status_cli.text() == "CLI: AlbocariusCompanions.Cli.dll"
    assert "does not exist" in settings_ui.check_cli(str(tmp_path / "nope.dll"))


def test_settings_dialog_refuses_a_broken_cli(window: MainWindow, tmp_path: Path):
    dialog = settings_ui.SettingsDialog(window.settings, window)
    dialog.cli_path.setText(str(tmp_path / "nope.dll"))
    dialog.companions_dir.setText(str(tmp_path))
    dialog.accept()
    assert "does not work" in dialog.cli_status.text()
    assert not window.settings.value("cliPath")
    dialog.cli_path.setText("")
    dialog.accept()
    assert window.settings.value("companionsDir") == str(tmp_path)


def test_recent_list_updates_on_open(window: MainWindow, sample: Path, template_copy: Path):
    assert window.open_path(sample)
    assert window.open_path(template_copy)
    assert settings_ui.recent_list(window.settings)[:2] == [str(template_copy), str(sample)]
    assert [a.text() for a in window.recent_menu.actions()][:2] == [str(template_copy), str(sample)]
    for i in range(6):
        settings_ui.add_recent(window.settings, Path(f"/x/{i}"))
    assert len(settings_ui.recent_list(window.settings)) == settings_ui.RECENT_MAX


def test_default_companions_dir_opens_at_start(qapp, tmp_path: Path, sample: Path, monkeypatch):
    from albocarius_editor import app

    settings = QSettings(str(tmp_path / "s.ini"), QSettings.Format.IniFormat)
    settings.setValue("companionsDir", str(sample.parent))
    opened = []
    monkeypatch.setattr(app, "MainWindow", lambda: _Probe(settings, opened))
    monkeypatch.setattr(qapp, "exec", lambda: 0)
    assert app.main(["x"]) == 0
    assert opened == [sample.parent]
    opened.clear()
    app.main(["x", str(sample)])
    assert opened == [sample.resolve()]


class _Probe:
    def __init__(self, settings, opened):
        self.settings, self.opened = settings, opened

    def open_path(self, path):
        self.opened.append(path)

    def show(self):
        pass
