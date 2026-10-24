#!/usr/bin/env bash
# Waits up to WAIT_SECONDS for a worker, then reports. Call again if still RUNNING.
# Usage: orchestra/wait_worker.sh NN-slug
set -uo pipefail
cd "$(git rev-parse --show-toplevel)" || exit 2
source orchestra/config.env
NAME="${1:?usage: orchestra/wait_worker.sh NN-slug}"
LOG="orchestra/logs/$NAME"
for ((t = 0; t < WAIT_SECONDS; t += 10)); do
  if [[ -f "$LOG.exit" ]]; then
    code="$(cat "$LOG.exit")"
    echo "FINISHED exit=$code$([[ "$code" == "124" ]] && echo ' (timeout)')"
    echo "--- last lines of worker log ---"
    tail -n 30 "$LOG.log"
    echo "Next: orchestra/check_worker.sh $NAME"
    exit 0
  fi
  sleep 10
done
echo "RUNNING. Changes so far: $(git diff --shortstat)"
