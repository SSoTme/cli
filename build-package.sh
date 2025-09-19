# Parse command line arguments for --no-update
NO_UPDATE_ARG=""
for arg in "$@"; do
    if [ "$arg" = "--no-update" ]; then
        NO_UPDATE_ARG="--no-update"
        break
    fi
done

ARTCHITECTURE=$(uname -m)  # detect if arm or intel processor (we need separate installers for each)

INSTALLERDIR="./macOS/Installer"

INSTALLERNAME="SSoTme-Installer-$ARTCHITECTURE.pkg"

DEV_INS_KEYCHAIN_ID=""
DEV_APP_KEYCHAIN_ID=""
NOTARYPASS=""
APPLEID=""

echo "Running build.sh"
echo "=============================="
if [ -n "$NO_UPDATE_ARG" ]; then
    /bin/bash "$INSTALLERDIR/Scripts/build.sh" --no-update $INSTALLERNAME $DEV_INS_KEYCHAIN_ID $DEV_APP_KEYCHAIN_ID $APPLEID $NOTARYPASS
else
    /bin/bash "$INSTALLERDIR/Scripts/build.sh" $INSTALLERNAME $DEV_INS_KEYCHAIN_ID $DEV_APP_KEYCHAIN_ID $APPLEID $NOTARYPASS
fi
