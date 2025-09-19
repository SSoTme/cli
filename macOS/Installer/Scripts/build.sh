#!/bin/bash
# Build script for SSoTme macOS Installer
#
# Generates:
#           - cli/macOS/Installer/bin/SSoTme-Installer.pkg


# Parse command line arguments
NO_UPDATE=false
ARGS=()
while [[ $# -gt 0 ]]; do
    case $1 in
        --no-update)
            NO_UPDATE=true
            shift
            ;;
        *)
            ARGS+=("$1")
            shift
            ;;
    esac
done

# Set positional parameters from remaining arguments
THE_INSTALLER_FILENAME=${ARGS[0]}
DEV_INSTALLER_KEYCHAIN_ID=${ARGS[1]}
DEV_EXECUTABLE_KEYCHAIN_ID=${ARGS[2]}
APPLE_EMAIL=${ARGS[3]}
NOTARYPASS=${ARGS[4]}

INSTALLER_DIR="$( dirname "$( dirname "${BASH_SOURCE[0]}" )")"

echo "my dir: $INSTALLER_DIR"

set -e  # exit on failure

SCRIPT_DIR="$INSTALLER_DIR/Scripts"

ROOT_DIR="$(dirname "$(dirname "$INSTALLER_DIR")")"

echo "Root dir: $ROOT_DIR"

cd $ROOT_DIR

# Update package.json with current timestamp version (unless --no-update is specified)
if [ "$NO_UPDATE" = false ]; then
    TIMESTAMP=$(date +"%Y.%m.%d.%H%M")
    PACKAGE_JSON_PATH="$ROOT_DIR/package.json"
    if [ -f "$PACKAGE_JSON_PATH" ]; then
        OLD_VERSION=$(grep -o '"version": "[^"]*"' "$PACKAGE_JSON_PATH" | cut -d'"' -f4)
        # Use a temp file to preserve formatting
        jq --arg ver "$TIMESTAMP" '.version = $ver' "$PACKAGE_JSON_PATH" > "$PACKAGE_JSON_PATH.tmp" && mv "$PACKAGE_JSON_PATH.tmp" "$PACKAGE_JSON_PATH"
        echo "Updated package.json version from $OLD_VERSION to $TIMESTAMP"
    else
        echo "ERROR: package.json not found at: $PACKAGE_JSON_PATH"
        exit 1
    fi
else
    echo "Skipping automatic version update (--no-update specified)"
fi

SOURCE_DIR="$ROOT_DIR/ssotme"
RESOURCES_DIR="$INSTALLER_DIR/Resources"
ASSETS_DIR="$INSTALLER_DIR/Assets"
BUILD_DIR="$INSTALLER_DIR/build"
DIST_DIR="$ROOT_DIR/dist"
BIN_DIR="$INSTALLER_DIR/bin"
SSOTME_VERSION=$(grep -o '"version": "[^"]*"' "$ROOT_DIR/package.json" | cut -d'"' -f4)
echo "Using version: $SSOTME_VERSION from package.json"

# Update the version in the .csproj file
CSPROJ_FILE="$ROOT_DIR/Windows/CLI/SSoTme.OST.CLI.csproj"
CLIHANDLER_FILE="$ROOT_DIR/Windows/Lib/CLIOptions/SSoTmeCLIHandler.cs"

# Always use package.json version
NEW_CSPROJ_VERSION="$SSOTME_VERSION"
echo "Using package.json version: $NEW_CSPROJ_VERSION"

if [ ! -f "$CSPROJ_FILE" ]; then
    echo "WARNING: $CSPROJ_FILE not found"
fi

echo "Updating version in $CSPROJ_FILE to $NEW_CSPROJ_VERSION"
sed -i '' "s/<Version>[^<]*<\/Version>/<Version>$NEW_CSPROJ_VERSION<\/Version>/g" "$CSPROJ_FILE"

echo "Updating version in $CLIHANDLER_FILE to $NEW_CSPROJ_VERSION"
sed -i '' "s/public string CLI_VERSION = \".*\";/public string CLI_VERSION = \"$NEW_CSPROJ_VERSION\";/g" "$CLIHANDLER_FILE"

# Clean previous builds
sudo rm -rf "$DIST_DIR"
sudo rm -rf "$BUILD_DIR"
sudo rm -rf "$BIN_DIR"
sudo rm -rf "$RESOURCES_DIR"
sudo rm -rf "$ROOT_DIR/build"

echo "Creating necessary directories..."
mkdir -p "$RESOURCES_DIR" "$BUILD_DIR" "$ASSETS_DIR" "$BIN_DIR" "$BIN_DIR/signed" "$BIN_DIR/unsigned"

# Copy README into Resources
README_SRC="$ROOT_DIR/README.md"
README_DEST="$RESOURCES_DIR/README.md"
if [ -f "$README_SRC" ]; then
    cp "$README_SRC" "$README_DEST"
else
    echo "WARNING: README.md not found at root."
fi

# copy the postinstall script to the build
mkdir -p "$BUILD_DIR/scripts"
if [ -f "$SCRIPT_DIR/postinstall.sh" ]; then
    cp "$SCRIPT_DIR/postinstall.sh" "$BUILD_DIR/scripts/postinstall"
else
    echo "FATAL: $SCRIPT_DIR/postinstall.sh does not exist!"
    exit 1
fi
chmod +x "$BUILD_DIR/scripts/postinstall"

# we need to make sure that the target mac's cpu type matches the type we're building on
TARGET_ARCH=$(uname -m)
echo "#!/bin/bash
      TARGET_ARCH=\"$TARGET_ARCH\" 
      if [ \$(uname -m) != \"\$TARGET_ARCH\" ]; then 
        echo \"[ERROR] CPU mismatch: expected \$TARGET_ARCH, got \$(uname -m)\" >&2 
        exit 64  # error code other than 1 
      fi
" > "$BUILD_DIR/scripts/preinstall"
chmod +x "$BUILD_DIR/scripts/preinstall"

if [ -f "$SCRIPT_DIR/uninstall.sh" ]; then
    cp "$SCRIPT_DIR/uninstall.sh" "$RESOURCES_DIR/uninstall"
    chmod +x "$RESOURCES_DIR/uninstall"
else
    echo "No such file: $SCRIPT_DIR/uninstall.sh"
fi

if [ "$TARGET_ARCH" = "x86_64" ]; then
    PUB_ARCH="x64"
else
    PUB_ARCH=$TARGET_ARCH  # amd64 -> already valid for dotnet publish
fi

dotnet publish "$CSPROJ_FILE" -r "osx-$PUB_ARCH" -c Release /p:PublishSingleFile=true \
  --self-contained true -o "$RESOURCES_DIR"
mv "$RESOURCES_DIR/SSoTme.OST.CLI" "$RESOURCES_DIR/ssotme"

# copy into aic and aicapture
cp "$RESOURCES_DIR/ssotme" "$RESOURCES_DIR/aic"
cp "$RESOURCES_DIR/ssotme" "$RESOURCES_DIR/aicapture"

chmod +x "$RESOURCES_DIR/ssotme"
chmod +x "$RESOURCES_DIR/aic"
chmod +x "$RESOURCES_DIR/aicapture"

# sign the exes
codesign --force --timestamp --options runtime \
  --entitlements "$SOURCE_DIR/entitlements.plist" \
  --sign "$DEV_EXECUTABLE_KEYCHAIN_ID" "$RESOURCES_DIR/ssotme" --identifier "com.effortlessapi.ssotme"
codesign --force --timestamp --options runtime \
  --entitlements "$SOURCE_DIR/entitlements.plist" \
  --sign "$DEV_EXECUTABLE_KEYCHAIN_ID" "$RESOURCES_DIR/aic" --identifier "com.effortlessapi.aic"
codesign --force --timestamp --options runtime \
  --entitlements "$SOURCE_DIR/entitlements.plist" \
  --sign "$DEV_EXECUTABLE_KEYCHAIN_ID" "$RESOURCES_DIR/aicapture" --identifier "com.effortlessapi.aicapture"


echo "Building package..."
mkdir -p "$BUILD_DIR/payload/Applications/SSoTme"
cp -r "$RESOURCES_DIR"/* "$BUILD_DIR/payload/Applications/SSoTme/"

echo "Verifying code signature on copied binaries..."
codesign -dv --verbose=4 "$RESOURCES_DIR/ssotme"
codesign -dv --verbose=4 "$RESOURCES_DIR/aic"
codesign -dv --verbose=4 "$RESOURCES_DIR/aicapture"

# Build a single package directly
pkgbuild --root "$BUILD_DIR/payload" \
    --install-location "/" \
    --scripts "$BUILD_DIR/scripts" \
    --identifier "com.effortlessapi.ssotmecli" \
    --version "$SSOTME_VERSION" \
    "$BIN_DIR/unsigned/$THE_INSTALLER_FILENAME"

echo "Signing package $BIN_DIR/unsigned/$THE_INSTALLER_FILENAME -> $BIN_DIR/signed/$THE_INSTALLER_FILENAME"
productsign --sign $DEV_INSTALLER_KEYCHAIN_ID "$BIN_DIR/unsigned/$THE_INSTALLER_FILENAME"  "$BIN_DIR/signed/$THE_INSTALLER_FILENAME"

echo "Build completed. Installer is at: $BIN_DIR/signed/$THE_INSTALLER_FILENAME"

echo ""
echo "$SCRIPT_DIR/notarize.sh" "$BIN_DIR/signed/$THE_INSTALLER_FILENAME" $APPLE_EMAIL $NOTARYPASS
/bin/bash "$SCRIPT_DIR/notarize.sh" "$BIN_DIR/signed/$THE_INSTALLER_FILENAME" $APPLE_EMAIL $NOTARYPASS

# run on the x86 one too
#echo "$SCRIPT_DIR/notarize.sh" "$HOME/Downloads/SSoTme-Installer-x86_64.pkg" $APPLE_EMAIL $NOTARYPASS
#/bin/bash "$SCRIPT_DIR/notarize.sh" "$HOME/Downloads/SSoTme-Installer-x86_64.pkg" $APPLE_EMAIL $NOTARYPASS
#open "$BIN_DIR/signed" -a Finder
