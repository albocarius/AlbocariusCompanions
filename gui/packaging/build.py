"""Builds the editor as one executable file (PyInstaller --onefile) for the platform it runs on.

    cd gui
    uv run --group build python packaging/build.py              # dist/albocarius-editor[.exe]
    uv run --group build python packaging/build.py --with-cli   # + the mod's CLI inside (validate/preview without .NET)

PyInstaller does not cross-compile: build on Windows for Windows, on Linux for Linux. The executable carries Python,
PySide6, the bundled schema exports and the companion template. ``--with-cli`` also publishes
tools/AlbocariusCompanions.Cli as a self-contained single file (needs the .NET SDK at build time only) and embeds it, so
validation and the preview work on a machine without .NET. Without it, the packaged editor finds a CLI the usual way
(``ALBOCARIUS_CLI``, the cliPath setting).
"""

from __future__ import annotations

import argparse
import platform
import shutil
import subprocess
import sys
from pathlib import Path

GUI = Path(__file__).resolve().parents[1]
REPO = GUI.parent
PACKAGE = GUI / "src" / "albocarius_editor"
CLI_PROJECT = REPO / "tools" / "AlbocariusCompanions.Cli"
TEMPLATE = REPO / "tools" / "companion-template"
NAME = "albocarius-editor"

# Qt modules the editor never imports; PyInstaller's PySide6 hooks only collect what is imported, these stay out anyway.
EXCLUDES = ["tkinter", "unittest", "pydoc", "PySide6.QtWebEngineCore", "PySide6.QtQml", "PySide6.QtQuick", "PySide6.Qt3DCore"]


def runtime_identifier() -> str:
    """The .NET runtime identifier of this machine (win-x64, linux-x64, linux-arm64, osx-arm64, …)."""
    machine = platform.machine().lower()
    arch = "arm64" if machine in ("arm64", "aarch64") else "x64"
    os_name = {"win32": "win", "darwin": "osx"}.get(sys.platform, "linux")
    return f"{os_name}-{arch}"


def publish_cli(out: Path) -> Path:
    """dotnet publish of the CLI as one self-contained file; returns its path."""
    dotnet = shutil.which("dotnet")
    if dotnet is None:
        raise SystemExit("--with-cli needs the .NET SDK ('dotnet' on PATH)")
    rid = runtime_identifier()
    print(f"publishing the CLI for {rid} …", flush=True)
    subprocess.run([dotnet, "publish", str(CLI_PROJECT), "-c", "Release", "-r", rid, "--self-contained", "true",
                    "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true",
                    "-p:DebugType=none", "-o", str(out)], check=True)
    exe = out / ("AlbocariusCompanions.Cli.exe" if sys.platform == "win32" else "AlbocariusCompanions.Cli")
    if not exe.is_file():
        raise SystemExit(f"dotnet publish did not produce {exe}")
    return exe


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--with-cli", action="store_true", help="embed a self-contained build of the mod's CLI")
    parser.add_argument("--onedir", action="store_true", help="a folder instead of one file (starts faster)")
    parser.add_argument("--dist", type=Path, default=GUI / "dist", help="output folder (default: gui/dist)")
    args = parser.parse_args(argv)

    try:
        import PyInstaller.__main__ as pyinstaller
    except ImportError:
        raise SystemExit("PyInstaller is missing: run with 'uv run --group build python packaging/build.py'") from None

    work = GUI / "build"
    stage = work / "stage"
    shutil.rmtree(stage, ignore_errors=True)
    stage.mkdir(parents=True)

    sep = ";" if sys.platform == "win32" else ":"  # PyInstaller's --add-data separator (os.pathsep)
    data = [
        (PACKAGE / "schema" / "fixtures", "albocarius_editor/schema/fixtures"),
        (PACKAGE / "ui" / "icons", "albocarius_editor/ui/icons"),
    ]
    if TEMPLATE.is_dir():
        data.append((TEMPLATE, "albocarius_editor/companion_template"))
    else:
        print(f"warning: {TEMPLATE} not found; 'New companion' will not work in the package", flush=True)
    binaries = []
    if args.with_cli:
        cli = publish_cli(stage / "cli")
        binaries.append((cli, "albocarius_editor/cli_bin"))

    options = [
        str(GUI / "packaging" / "entry.py"),
        "--name", NAME,
        "--onedir" if args.onedir else "--onefile",
        "--windowed",
        "--noconfirm",
        "--clean",
        "--distpath", str(args.dist),
        "--workpath", str(work / "pyinstaller"),
        "--specpath", str(work),
        "--paths", str(GUI / "src"),
    ]
    for source, target in data:
        options += ["--add-data", f"{source}{sep}{target}"]
    for source, target in binaries:
        options += ["--add-binary", f"{source}{sep}{target}"]
    for module in EXCLUDES:
        options += ["--exclude-module", module]
    pyinstaller.run(options)

    built = args.dist / (NAME if args.onedir else NAME + (".exe" if sys.platform == "win32" else ""))
    print(f"\nbuilt {built}" + (" (with the CLI)" if args.with_cli else ""))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
