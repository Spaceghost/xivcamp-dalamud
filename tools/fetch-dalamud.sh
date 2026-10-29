#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
dest="${DALAMUD_HOME:-$root/.dalamud}"
[[ -f "$dest/Dalamud.dll" ]] && exit 0
archive="$(mktemp)"
trap 'rm -f "$archive"' EXIT
curl -fsSL --retry 3 --max-time 180 "${DALAMUD_REFERENCE_URL:-https://raw.githubusercontent.com/goatcorp/dalamud-distrib/3e8e6eb456c928401febd1d9452c4b8d4cad0eb5/latest.zip}" -o "$archive"
mkdir -p "$dest"
unzip -q "$archive" -d "$dest"
# tools/build.py checks the reviewed assembly hashes before compiling native consumers.
