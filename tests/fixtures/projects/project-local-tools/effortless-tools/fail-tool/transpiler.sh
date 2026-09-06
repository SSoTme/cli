#!/bin/sh
echo "fail-tool: about to fail" >&2
echo "should-not-be-written" > "$EFFORTLESS_OUTPUT_DIR/never.txt"
exit 3
