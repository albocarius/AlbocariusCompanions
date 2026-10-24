#!/usr/bin/env bash
# Code graph helper (graphify CLI, code only, no LLM, no tokens).
# Usage: orchestra/graph.sh build|update|report
set -uo pipefail
cd "$(git rev-parse --show-toplevel)" || exit 2
source orchestra/config.env
[[ "${GRAPHIFY:-0}" == "1" ]] || exit 0
command -v graphify > /dev/null || { echo "graphify not installed (pipx install graphifyy)" >&2; exit 0; }
mkdir -p orchestra/logs
case "${1:-update}" in
  build)  graphify extract . --code-only --no-viz > orchestra/logs/graph.log 2>&1 \
            && graphify cluster-only . --no-label --no-viz >> orchestra/logs/graph.log 2>&1 ;;
  update) [[ -f graphify-out/graph.json ]] || exec "$0" build
          graphify update . --no-cluster > orchestra/logs/graph.log 2>&1 ;;
  report) graphify update . --no-cluster > orchestra/logs/graph.log 2>&1 \
            && graphify cluster-only . --no-label --no-viz >> orchestra/logs/graph.log 2>&1 ;;
  *) echo "usage: orchestra/graph.sh build|update|report" >&2; exit 2 ;;
esac
rc=$?
[[ $rc -eq 0 ]] && echo "graph $1: ok" || { echo "graph $1 failed, see orchestra/logs/graph.log" >&2; }
exit $rc
