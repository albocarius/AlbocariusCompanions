#!/usr/bin/env bash
# Verifies a finished attempt: protected paths, full tests, lint. Prints PASS or FAIL.
# Usage: orchestra/check_worker.sh NN-slug
set -uo pipefail
cd "$(git rev-parse --show-toplevel)" || exit 2
source orchestra/config.env
NAME="${1:?usage: orchestra/check_worker.sh NN-slug}"
LOG="orchestra/logs/$NAME"
fail=0

BASE="$(cat "$LOG.base" 2>/dev/null)"
if [[ -n "$BASE" && "$(git rev-parse HEAD)" != "$BASE" ]]; then
  echo "== VIOLATION: worker moved HEAD (commit/reset/switch). Base was $BASE"
  git log --oneline "$BASE"..HEAD 2>/dev/null
  fail=1
fi

echo "== Changed files"
git status --porcelain

viol="$(git status --porcelain -- $PROTECTED_PATHS | grep -v ' orchestra/logs/' || true)"
if [[ -n "$viol" ]]; then
  echo "== VIOLATION: worker touched protected paths"
  echo "$viol"
  fail=1
fi

echo "== Tests: $TEST_CMD"
bash -c "$TEST_CMD" > "$LOG.test" 2>&1
tc=$?
# dotnet 10.0.113 (distro build) intermittently aborts with "Internal CLR error"; retry once on that only.
for attempt in 1 2 3; do
  [[ $tc -ne 0 ]] && grep -q "Internal CLR error" "$LOG.test" || break
  echo "== CLR crash (infra), retry $attempt/3"
  bash -c "$TEST_CMD" > "$LOG.test" 2>&1
  tc=$?
done
tail -n 25 "$LOG.test"
[[ $tc -ne 0 ]] && fail=1

if [[ -n "${LINT_CMD:-}" ]]; then
  echo "== Lint: $LINT_CMD"
  bash -c "$LINT_CMD" > "$LOG.lint" 2>&1
  lc=$?
  tail -n 15 "$LOG.lint"
  [[ $lc -ne 0 ]] && fail=1
fi

[[ $fail -eq 0 ]] && echo "RESULT: PASS" || echo "RESULT: FAIL"
exit $fail
