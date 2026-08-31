#!/bin/bash
set -e

VERSION=$(date -u +"%Y-%m-%d.%H.%M")

echo "Updating package.json version to ${VERSION}..."
# Update version in package.json
sed -i '' "s/\"version\": \"[^\"]*\"/\"version\": \"${VERSION}\"/" package.json

echo "Committing and pushing to main..."
git add package.json
git commit -m "Release v${VERSION}"
git push

echo "Creating GitHub release v${VERSION}..."
gh release create "v${VERSION}" --title "v${VERSION}" --notes "Release v${VERSION}"

echo "Done! MSI and PKG builds will start automatically."
echo "Track progress: gh run list --workflow=build-windows.yml && gh run list --workflow=build-mac.yml"
