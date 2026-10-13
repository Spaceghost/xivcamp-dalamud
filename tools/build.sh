#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p node_modules/.cache/project-preview
node_modules/.bin/imbac --platform node --silent --output node_modules/.cache/project-preview tools/build.imba
node node_modules/.cache/project-preview/build.js "$@"
