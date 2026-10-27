"""The packaged editor (gui/packaging): the bundled CLI and template are found when there is no repository."""

import importlib.util
import sys
from pathlib import Path

import pytest

from albocarius_editor import app
from albocarius_editor.cli import client
from albocarius_editor.data import workspace

GUI = Path(__file__).resolve().parents[1]


def test_bundled_cli_is_the_last_resort(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    monkeypatch.delenv(client.ENV_VAR, raising=False)
    monkeypatch.setattr(client, "find_repo_root", lambda _: None)
    monkeypatch.setattr(client, "BUNDLED_DIR", tmp_path)
    assert client.locate() is None
    exe = tmp_path / client.BUNDLED_NAME
    exe.write_bytes(b"")
    exe.chmod(0o644)
    assert client.locate() == exe
    if sys.platform != "win32":
        assert exe.stat().st_mode & 0o111  # made executable
    monkeypatch.setenv(client.ENV_VAR, str(tmp_path / "other.dll"))
    assert client.locate() is None  # an explicit choice still wins, even when it is wrong


def test_bundled_template_without_a_repository(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    monkeypatch.setattr(workspace, "find_repo_root", lambda _: None)
    monkeypatch.setattr(workspace, "BUNDLED_TEMPLATE", tmp_path / "missing")
    assert workspace.default_template() is None
    template = tmp_path / "companion_template"
    template.mkdir()
    monkeypatch.setattr(workspace, "BUNDLED_TEMPLATE", template)
    assert workspace.default_template() == template


def test_check_reports_what_was_found(capsys: pytest.CaptureFixture[str]):
    assert app.main(["albocarius-editor", "--check"]) == 0
    out = capsys.readouterr().out
    assert "CLI:" in out and "Schema:" in out and "Template:" in out


def test_runtime_identifier():
    spec = importlib.util.spec_from_file_location("build", GUI / "packaging" / "build.py")
    build = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(build)
    rid = build.runtime_identifier()
    assert rid.split("-")[0] in ("win", "linux", "osx") and rid.split("-")[1] in ("x64", "arm64")
