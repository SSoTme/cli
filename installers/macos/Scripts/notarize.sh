THE_INSTALLER_FILENAME=$1
APPLE_EMAIL=$2
NOTARYPASS=$3

echo ""
echo "Running notary tool..."
(echo "xcrun notarytool submit "$THE_INSTALLER_FILENAME" --apple-id $APPLE_EMAIL --password $NOTARYPASS --team-id SLMGMPYNKS --wait --output-format json")

NOTARY_RESULT=$(xcrun notarytool submit "$THE_INSTALLER_FILENAME" --apple-id $APPLE_EMAIL \
                                         --password $NOTARYPASS \
                                         --team-id SLMGMPYNKS --wait \
                                         --output-format json)

echo "$NOTARY_RESULT"

# Check if notarization was successful
if echo "$NOTARY_RESULT" | grep -q '"status":"Accepted"'; then
    echo "Stapling notarization ticket to package..."
    if xcrun stapler staple -v "$THE_INSTALLER_FILENAME"; then
        echo "Stapling completed successfully!"
    else
        echo "Stapling failed, package might be blocked from being opened"
    fi
else
    echo "Notarization failed. Package will not pass Gatekeeper validation."
    echo "See the notarization result for details."
fi
