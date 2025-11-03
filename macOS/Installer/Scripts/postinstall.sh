#!/bin/bash

LOGFILE="/tmp/ssotme-install.log"

# clear the file
echo "" > $LOGFILE

exec > "$LOGFILE" 2>&1
set -euxo pipefail

echo "[INFO] Starting postinstall script..."
echo "[INFO] Logging to $LOGFILE"

# Resolve actual user's home directory
REAL_USER=$(stat -f "%Su" /dev/console)
REAL_HOME=$(dscl . -read /Users/$REAL_USER NFSHomeDirectory | awk '{print $2}')
TARGETHOMEDIR="$REAL_HOME/.ssotme"

echo "[INFO] Creating target config dir at $TARGETHOMEDIR"
mkdir -p "$TARGETHOMEDIR"

echo "[INFO] Creating symbolic links..."
ln -sf "/Applications/SSoTme/ssotme" "/usr/local/bin/ssotme"
ln -sf "/Applications/SSoTme/aic" "/usr/local/bin/aic"
ln -sf "/Applications/SSoTme/aicapture" "/usr/local/bin/aicapture"
ln -sf "/Applications/SSoTme/effortless" "/usr/local/bin/effortless"

echo "[INFO] Postinstall script completed successfully."
exit 0
