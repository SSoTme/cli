#!/bin/bash
# exesign.sh - Script to sign macOS executables with Developer ID certificates
# Usage: ./exesign.sh <executable_path> <certificate_id> <identifier> [entitlements_file]
# Example: ./exesign.sh ~/myapp/bin/myapp "Developer ID Application: My Company (ABCDE12345)" "com.company.myapp" "/path/to/entitlements.plist"

# Check if correct number of arguments is provided
if [ $# -lt 3 ]; then
    echo "Usage: $0 <executable_path> <certificate_id> <identifier> [entitlements_file]"
    echo "Example: $0 /path/to/myapp \"Developer ID Application: My Company (ABCDE12345)\" \"com.company.myapp\" \"/path/to/entitlements.plist\""
    exit 1
fi

EXECUTABLE_PATH="$1"
CERTIFICATE_ID="$2"
IDENTIFIER="$3"
ENTITLEMENTS_FILE="$4"

# Check if executable exists
if [ ! -f "$EXECUTABLE_PATH" ]; then
    echo "Error: Executable not found at $EXECUTABLE_PATH"
    exit 1
fi

# Sign the executable with hardened runtime option
echo "Signing executable: $EXECUTABLE_PATH"
echo "Using certificate: $CERTIFICATE_ID"
echo "Using identifier: $IDENTIFIER"

CODESIGN_COMMAND="codesign --force --options runtime --sign \"$CERTIFICATE_ID\" --identifier \"$IDENTIFIER\""

# Add entitlements if provided
if [ -n "$ENTITLEMENTS_FILE" ]; then
    if [ -f "$ENTITLEMENTS_FILE" ]; then
        echo "Using entitlements file: $ENTITLEMENTS_FILE"
        CODESIGN_COMMAND="$CODESIGN_COMMAND --entitlements \"$ENTITLEMENTS_FILE\""
    else
        echo "Warning: Entitlements file not found at $ENTITLEMENTS_FILE"
    fi
fi

# Add executable path to command
CODESIGN_COMMAND="$CODESIGN_COMMAND \"$EXECUTABLE_PATH\""

# Execute the codesign command
eval $CODESIGN_COMMAND

# Verify the signature
echo "Verifying signature..."
codesign -dvv "$EXECUTABLE_PATH"

# Check Gatekeeper acceptance
echo "Checking Gatekeeper acceptance..."
spctl -a -vv "$EXECUTABLE_PATH"

echo "Executable signing complete"