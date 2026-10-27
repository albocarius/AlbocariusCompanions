"""The mod's CLI (tools/AlbocariusCompanions.Cli): found by path, run as a subprocess, never imported. No Qt.

* :func:`locate` finds it: ``ALBOCARIUS_CLI`` env var, an explicit setting, else the repository's build output, else
  the copy packaged with the editor (gui/packaging).
* :class:`Cli` runs one-shot commands (``validate --json``, ``export schema|triggers``).
* :class:`ServeClient` keeps one ``serve`` process alive and exchanges JSON lines (docs/SIMULATOR_PROTOCOL.md).
"""

from __future__ import annotations

import itertools
import json
import os
import shutil
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from ..data.workspace import find_repo_root

DLL = "AlbocariusCompanions.Cli.dll"
ENV_VAR = "ALBOCARIUS_CLI"
# A packaged editor (gui/packaging/build.py --with-cli) carries a self-contained single-file CLI here.
BUNDLED_DIR = Path(__file__).resolve().parents[1] / "cli_bin"
BUNDLED_NAME = "AlbocariusCompanions.Cli.exe" if sys.platform == "win32" else "AlbocariusCompanions.Cli"


class CliError(Exception):
    """The CLI is missing, failed to start, or answered with something that is not the documented JSON."""


def _no_window() -> int:
    return getattr(subprocess, "CREATE_NO_WINDOW", 0) if sys.platform == "win32" else 0


def candidates(repo_root: Path | None) -> list[Path]:
    """Build outputs of the CLI project, newest configuration first."""
    if repo_root is None:
        return []
    out = repo_root / "tools" / "AlbocariusCompanions.Cli" / "bin"
    return [out / config / "net8.0" / DLL for config in ("Release", "Debug")]


def bundled() -> Path | None:
    """The CLI packaged with the editor, if any (made executable: an unpacked bundle may lose the bit)."""
    path = BUNDLED_DIR / BUNDLED_NAME
    if not path.is_file():
        return None
    if sys.platform != "win32" and not os.access(path, os.X_OK):
        try:
            path.chmod(path.stat().st_mode | 0o755)
        except OSError:
            return None
    return path


def locate(setting: str | None = None) -> Path | None:
    """The CLI dll (or apphost executable): the env var, then the setting, then the repository's build output, then the
    copy packaged with the editor."""
    for value in (os.environ.get(ENV_VAR), setting):
        if value:
            p = Path(value).expanduser()
            return p if p.is_file() else None
    found = [p for p in candidates(find_repo_root(Path(__file__).resolve())) if p.is_file()]
    if found:
        return max(found, key=lambda p: p.stat().st_mtime)
    return bundled()


def command(cli: Path) -> list[str]:
    """How to start it: ``dotnet <dll>`` for a dll, the file itself for an apphost."""
    if cli.suffix.lower() == ".dll":
        dotnet = shutil.which("dotnet")
        if dotnet is None:
            raise CliError("'dotnet' was not found on PATH; it is needed to run the CLI")
        return [dotnet, str(cli)]
    return [str(cli)]


@dataclass
class ValidateResult:
    exit_code: int
    document: dict[str, Any]

    @property
    def diagnostics(self) -> list[dict[str, Any]]:
        return self.document.get("diagnostics", [])


class Cli:
    def __init__(self, path: Path, timeout: float = 60):
        self.path = path
        self.timeout = timeout

    @classmethod
    def find(cls, setting: str | None = None) -> Cli:
        path = locate(setting)
        if path is None:
            raise CliError("the AlbocariusCompanions CLI was not found; build tools/AlbocariusCompanions.Cli, "
                           f"set {ENV_VAR} to its dll, or use an editor package built with the CLI")
        return cls(path)

    def run(self, *args: str) -> subprocess.CompletedProcess[str]:
        try:
            return subprocess.run(command(self.path) + list(args), capture_output=True, text=True, encoding="utf-8",
                                  timeout=self.timeout, creationflags=_no_window())
        except (OSError, subprocess.TimeoutExpired) as e:
            raise CliError(f"running the CLI failed: {e}") from None

    def _json(self, proc: subprocess.CompletedProcess[str], what: str) -> dict[str, Any]:
        try:
            return json.loads(proc.stdout)
        except json.JSONDecodeError:
            detail = (proc.stderr or proc.stdout).strip().splitlines()
            raise CliError(f"{what}: exit code {proc.returncode}: {detail[0] if detail else 'no output'}") from None

    def validate(self, path: Path) -> ValidateResult:
        proc = self.run("validate", "--json", str(path))
        if proc.returncode not in (0, 1):
            raise CliError(f"validate: exit code {proc.returncode}: {proc.stderr.strip()}")
        return ValidateResult(proc.returncode, self._json(proc, "validate"))

    def export(self, what: str) -> dict[str, Any]:
        proc = self.run("export", what)
        if proc.returncode != 0:
            raise CliError(f"export {what}: exit code {proc.returncode}: {proc.stderr.strip()}")
        return self._json(proc, f"export {what}")


class ServeClient:
    """One long-lived ``serve`` process. Requests are answered in order, so a request blocks for its one response line.
    The UI runs it off the GUI thread."""

    def __init__(self, cli: Path):
        self.cli = cli
        self._proc: subprocess.Popen[str] | None = None
        self._ids = itertools.count(1)

    @property
    def running(self) -> bool:
        return self._proc is not None and self._proc.poll() is None

    def start(self) -> None:
        if self.running:
            return
        try:
            self._proc = subprocess.Popen(command(self.cli) + ["serve"], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                          stderr=subprocess.DEVNULL, text=True, encoding="utf-8", bufsize=1,
                                          creationflags=_no_window())
        except OSError as e:
            raise CliError(f"starting the simulator failed: {e}") from None

    def request(self, op: str, **fields: Any) -> dict[str, Any]:
        """Sends one request and returns its response; raises CliError on ``ok: false`` or a dead process."""
        self.start()
        assert self._proc is not None and self._proc.stdin is not None and self._proc.stdout is not None
        request_id = next(self._ids)
        line = json.dumps({"id": request_id, "op": op, **fields})
        try:
            self._proc.stdin.write(line + "\n")
            self._proc.stdin.flush()
            answer = self._proc.stdout.readline()
        except OSError as e:
            raise CliError(f"the simulator stopped: {e}") from None
        if not answer:
            raise CliError("the simulator stopped")
        response = json.loads(answer)
        if response.get("id") != request_id:
            raise CliError(f"the simulator answered request {response.get('id')} instead of {request_id}")
        if not response.get("ok"):
            raise CliError(response.get("error") or "the simulator refused the request")
        return response

    def close(self) -> None:
        if self._proc is None:
            return
        try:
            if self.running and self._proc.stdin:
                self._proc.stdin.write(json.dumps({"op": "quit"}) + "\n")
                self._proc.stdin.flush()
            self._proc.wait(timeout=5)
        except (OSError, subprocess.TimeoutExpired):
            self._proc.kill()
        self._proc = None
