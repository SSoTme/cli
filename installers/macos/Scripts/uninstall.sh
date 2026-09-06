#!/bin/bash
# Uninstaller script for Effortless CLI macOS package

echo "Are you sure you want to uninstall Effortless CLI? (y/n)"
read -r response

case "$response" in
    [yY][eE][sS]|[yY])  # yes, YES, Y, y was entered
        echo "Uninstalling Effortless CLI..."

        # Remove symbolic links from /usr/local/bin
        sudo rm -f /usr/local/bin/ssotme
        sudo rm -f /usr/local/bin/aic
        sudo rm -f /usr/local/bin/effortless
        sudo rm -f /usr/local/bin/aicapture

        # Remove application files
        sudo rm -rf /Applications/Effortless
        sudo rm -rf /Applications/SSoTme

        #echo "Removing configuration files..."
        #sudo rm -rf ~/.effortless ~/.ssotme

        echo "Effortless CLI has been uninstalled successfully."
        ;;
    *)  # anything else was entered
        echo "Uninstall cancelled."
        exit 1
        ;;
esac

exit 0
