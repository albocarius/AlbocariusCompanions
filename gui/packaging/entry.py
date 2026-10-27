"""PyInstaller entry point (the package's app module uses relative imports, so it cannot be the script itself)."""

from albocarius_editor.app import main

if __name__ == "__main__":
    raise SystemExit(main())
