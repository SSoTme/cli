#!/bin/bash
# Uninstaller script for SSoTme CLI macOS package

echo "Are you sure you want to uninstall SSoTme CLI? (y/n)"
read -r response

case "$response" in
    [yY][eE][sS]|[yY])  # yes, YES, Y, y was entered
        echo "Uninstalling SSoTme CLI..."

        # Remove symbolic links from /usr/local/bin
        sudo rm -f /usr/local/bin/ssotme
        sudo rm -f /usr/local/bin/aic
        sudo rm -f /usr/local/bin/effortless
        sudo rm -f /usr/local/bin/aicapture

        # Remove application files
        sudo rm -rf /Applications/SSoTme

        #echo "Removing configuration files..."
        #sudo rm -rf ~/.ssotme

        echo "SSoTme CLI has been uninstalled successfully."
        ;;
    *)  # anything else was entered
        echo "Uninstall cancelled."
        exit 1
        ;;
esac

exit 0
