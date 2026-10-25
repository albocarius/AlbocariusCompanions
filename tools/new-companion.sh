#!/usr/bin/env bash
# Creates a new companion folder from tools/companion-template/.
# Usage: tools/new-companion.sh <id> [companions-root]
#   <id>              folder name = companion id, [a-z0-9_]+ ("_shared" is reserved: an old install may still have it)
#   [companions-root] default: module/ModuleData/Companions next to this script's repo
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
id="${1:-}"
root="${2:-$here/../module/ModuleData/Companions}"

if [[ -z "$id" ]]; then
  echo "usage: $0 <id> [companions-root]" >&2
  exit 2
fi
if [[ ! "$id" =~ ^[a-z0-9_]+$ ]]; then
  echo "error: id '$id' must match [a-z0-9_]+ (lowercase letters, digits, '_')" >&2
  exit 1
fi
if [[ "$id" == "_shared" ]]; then
  echo "error: '_shared' is reserved (old player-lines folder)" >&2
  exit 1
fi
if [[ -e "$root/$id" ]]; then
  echo "error: $root/$id already exists; nothing was changed" >&2
  exit 1
fi

mkdir -p "$root"
cp -R "$here/companion-template" "$root/$id"
echo "Created $root/$id"
echo "Next: edit companion.json and lang/en.json, add audio/<line id>.ogg for voiced lines."
