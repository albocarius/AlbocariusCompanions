#!/usr/bin/env bash
# Starts one task in the background: headless Claude Code pointed at the local Strata model.
# Usage: orchestra/worker.sh orchestra/tasks/NN-slug.md
set -uo pipefail
cd "$(git rev-parse --show-toplevel)" || exit 2
source orchestra/config.env

if [[ "${ORCH_WORKER:-}" == "1" ]]; then
  echo "A worker cannot start another worker." >&2
  exit 2
fi

TASK="${1:?usage: orchestra/worker.sh orchestra/tasks/NN-slug.md}"
[[ -f "$TASK" ]] || { echo "No such task file: $TASK" >&2; exit 2; }
NAME="$(basename "$TASK" .md)"
LOG="orchestra/logs/$NAME"
mkdir -p orchestra/logs

if [[ "${ORCH_FG:-}" != "1" ]]; then
  if [[ -n "$(git status --porcelain --untracked-files=no)" ]]; then
    echo "Working tree has uncommitted changes. Commit tests and the task file first." >&2
    exit 2
  fi
  rm -f "$LOG.exit"
  git rev-parse HEAD > "$LOG.base"
  orchestra/graph.sh update > /dev/null
  ORCH_FG=1 nohup "$0" "$@" > /dev/null 2>&1 &
  echo "STARTED $NAME (pid $!). Next: orchestra/wait_worker.sh $NAME"
  exit 0
fi

PROMPT="Read orchestra/WORKER.md and orchestra/PROJECT.md first. Then do this task:

$(cat "$TASK")"

case "${WORKER_HARNESS:-claude}" in
  claude)
    allowed=(Read Edit Write Glob Grep "Bash(git status:*)" "Bash(git diff:*)" "Bash(ls:*)")
    for p in $WORKER_BASH; do allowed+=("Bash($p:*)"); done
    denied=("Bash(git add:*)" "Bash(git commit:*)" "Bash(git push:*)" "Bash(git checkout:*)"
            "Bash(git switch:*)" "Bash(git reset:*)" "Bash(git restore:*)" "Bash(git stash:*)"
            "Bash(git clean:*)" "Bash(rm -rf:*)" "Bash(sudo:*)" "Bash(docker:*)"
            "Bash(orchestra/worker.sh:*)" "Bash(./orchestra/worker.sh:*)" "Read(./.env)")
    model_args=()
    [[ -n "${STRATA_MODEL:-}" ]] && model_args=(--model "$STRATA_MODEL")
    env -u ANTHROPIC_API_KEY \
      ORCH_WORKER=1 \
      ANTHROPIC_BASE_URL="$STRATA_URL" \
      ANTHROPIC_AUTH_TOKEN="$STRATA_KEY" \
      timeout "$WORKER_TIMEOUT" claude -p "$PROMPT" "${model_args[@]}" \
        --permission-mode acceptEdits \
        --allowedTools "${allowed[@]}" \
        --disallowedTools "${denied[@]}" \
        > "$LOG.log" 2>&1
    ;;
  dsh)
    # dsh has no per-command allow/deny like Claude Code. Writes are confined by the
    # profile's sandbox (workspace-write); git misuse is caught by check_worker.sh.
    patch_args=()
    [[ -n "${DSH_PATCH:-}" ]] && patch_args=(--patch "$DSH_PATCH")
    ORCH_WORKER=1 \
    STRATA_API_KEY="$STRATA_KEY" \
      timeout "$WORKER_TIMEOUT" dsh "${patch_args[@]}" "$DSH_PROFILE" "$PROMPT" \
        > "$LOG.log" 2>&1
    ;;
  *) echo "Unknown WORKER_HARNESS: $WORKER_HARNESS" >&2; echo 2 > "$LOG.exit"; exit 2 ;;
esac
echo $? > "$LOG.exit"
