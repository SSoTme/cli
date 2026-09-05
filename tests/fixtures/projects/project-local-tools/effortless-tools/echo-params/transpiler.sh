#!/bin/sh
# Script-shape local tool: echoes the EFFORTLESS_* contract it received.
set -e
out="$EFFORTLESS_OUTPUT_DIR/${EFFORTLESS_OUTPUT_NAME:-echo.txt}"
mkdir -p "$(dirname "$out")"
{
  echo "tool=$EFFORTLESS_TOOL_NAME"
  echo "output=$EFFORTLESS_OUTPUT_NAME"
  echo "params=$EFFORTLESS_PARAMS"
  echo "inputs:"
  (cd "$EFFORTLESS_INPUT_DIR" && find . -type f | sed 's|^\./||' | sort | sed 's/^/  /')
  echo "first-input:"
  first=$(cd "$EFFORTLESS_INPUT_DIR" && find . -type f | sort | head -n 1)
  if [ -n "$first" ]; then sed 's/^/  /' "$EFFORTLESS_INPUT_DIR/$first"; fi
} > "$out"
echo "echo-params wrote $(basename "$out")"
