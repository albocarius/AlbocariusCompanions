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
