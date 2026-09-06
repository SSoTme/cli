#!/bin/sh
# Script-shape local tool exercising the per-file overwrite declaration.
# stamp=<value> is written into every file so a second run is observable.
set -e
stamp=$(printf '%s' "$EFFORTLESS_PARAMS" | sed -n 's/.*"stamp=\([^"]*\)".*/\1/p')
printf 'always %s\n' "$stamp" > "$EFFORTLESS_OUTPUT_DIR/always.txt"
printf 'never %s\n' "$stamp"  > "$EFFORTLESS_OUTPUT_DIR/never.txt"
printf 'plain %s\n' "$stamp"  > "$EFFORTLESS_OUTPUT_DIR/plain.txt"
mkdir -p "$EFFORTLESS_OUTPUT_DIR/sql"
printf 'gen %s\n' "$stamp"    > "$EFFORTLESS_OUTPUT_DIR/sql/01-tables.sql"
printf 'seam %s\n' "$stamp"   > "$EFFORTLESS_OUTPUT_DIR/sql/01b-customize-schema.sql"
cat > "$EFFORTLESS_OUTPUT_DIR/effortless-overwrite-modes.json" <<'JSON'
{
  "sql/*b-customize-*.sql": "Never",
  "sql/**": "Always",
  "always.txt": "Always",
  "never.txt": "Never"
}
JSON
