#!/bin/bash

LOGFILE="/tmp/effortless-install.log"

# clear the file
echo "" > $LOGFILE

exec > "$LOGFILE" 2>&1
set -euxo pipefail

echo "[INFO] Starting postinstall script..."
echo "[INFO] Logging to $LOGFILE"

# Resolve actual user's home directory
REAL_USER=$(stat -f "%Su" /dev/console)
REAL_HOME=$(dscl . -read /Users/$REAL_USER NFSHomeDirectory | awk '{print $2}')
TARGETHOMEDIR="$REAL_HOME/.effortless"

echo "[INFO] Creating target config dir at $TARGETHOMEDIR"
mkdir -p "$TARGETHOMEDIR"

echo "[INFO] Creating symbolic links..."
ln -sf "/Applications/Effortless/effortless" "/usr/local/bin/effortless"
ln -sf "/Applications/Effortless/ssotme" "/usr/local/bin/ssotme"
ln -sf "/Applications/Effortless/aic" "/usr/local/bin/aic"
ln -sf "/Applications/Effortless/aicapture" "/usr/local/bin/aicapture"

# The symlinks now point at /Applications/Effortless; the legacy payload is dead weight.
if [ -d "/Applications/SSoTme" ]; then
  echo "[INFO] Removing legacy /Applications/SSoTme payload"
  rm -rf "/Applications/SSoTme"
fi

echo "[INFO] Postinstall script completed successfully."
exit 0
