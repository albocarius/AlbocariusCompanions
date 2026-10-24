#!/usr/bin/env bash
# Throws away a failed attempt: back to the commit the worker started from.
# Ignored files (.venv, orchestra/logs) are kept.
# Usage: orchestra/discard_worker.sh NN-slug
set -uo pipefail
cd "$(git rev-parse --show-toplevel)" || exit 2
NAME="${1:?usage: orchestra/discard_worker.sh NN-slug}"
BASE="$(cat "orchestra/logs/$NAME.base")" || exit 2
git add -A --intent-to-add . && git diff > "orchestra/logs/$NAME.rejected.diff"
git reset --hard "$BASE" && git clean -fd
echo "Discarded. Rejected diff kept in orchestra/logs/$NAME.rejected.diff"
