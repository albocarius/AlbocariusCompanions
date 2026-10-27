import os
import shutil
from pathlib import Path

import pytest

# The UI smoke tests run headless (also on CI without a display).
os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")

GUI = Path(__file__).resolve().parents[1]
REPO = GUI.parent
SAMPLE = REPO / "module" / "ModuleData" / "Companions" / "test_companion"
TEMPLATE = REPO / "tools" / "companion-template"


@pytest.fixture
def sample(tmp_path: Path) -> Path:
    """A writable copy of the sample companion inside a Companions folder."""
    target = tmp_path / "Companions" / "test_companion"
    shutil.copytree(SAMPLE, target)
    return target


@pytest.fixture
def template_copy(tmp_path: Path) -> Path:
    target = tmp_path / "Companions" / "new_one"
    shutil.copytree(TEMPLATE, target)
    return target


@pytest.fixture
def window(qtbot, tmp_path: Path, qapp):
    """The main window (headless), with its own settings file."""
    from PySide6.QtCore import QSettings

    from albocarius_editor.ui import theme
    from albocarius_editor.ui.main_window import MainWindow

    theme.apply(qapp)
    w = MainWindow(QSettings(str(tmp_path / "settings.ini"), QSettings.Format.IniFormat))
    w.show()
    yield w
    w.folder = None  # closing must not stop on the unsaved-changes question
    w.close()
    w.deleteLater()
