#!/usr/bin/env bash
# Downloads the local embedding model used by the chat assistant.
#
# The model is ~490MB, so it is deliberately NOT committed. Run this once after cloning;
# without it the API still boots and every feature except /api/chat works normally.
#
#   ./scripts/fetch-embedding-model.sh
#
# Docker builds run the same download in src/PortfolioOS.API/Dockerfile.
set -euo pipefail

REPO="intfloat/multilingual-e5-small"
BASE="https://huggingface.co/${REPO}/resolve/main"
DEST="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/src/PortfolioOS.API/models/e5-small"

mkdir -p "$DEST"

# model.onnx lives under onnx/, the tokenizer files at the repo root.
fetch() {
  local remote="$1" local_name="$2"
  if [ -s "$DEST/$local_name" ]; then
    echo "  skip  $local_name (already present)"
    return
  fi
  echo "  get   $local_name"
  curl -fSL --retry 3 -o "$DEST/$local_name" "$BASE/$remote"
}

echo "Fetching $REPO into $DEST"
fetch "onnx/model.onnx"          "model.onnx"
fetch "sentencepiece.bpe.model"  "sentencepiece.bpe.model"
fetch "tokenizer.json"           "tokenizer.json"

echo "Done. $(du -sh "$DEST" | cut -f1) in $DEST"
