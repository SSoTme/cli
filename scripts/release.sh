#!/bin/bash
set -euo pipefail

EXPECTED_PACKAGE="@effortlessapi/cli"
PACKAGE_NAME=$(node -p "require('./package.json').name")
if [ "$PACKAGE_NAME" != "$EXPECTED_PACKAGE" ]; then
    echo "ERROR: package.json name must be $EXPECTED_PACKAGE; got $PACKAGE_NAME" >&2
    exit 1
fi

echo "Checking npm authentication..."
npm whoami >/dev/null

VERSION=$(node -e 'const d = new Date(); console.log(`${d.getUTCFullYear()}.${(d.getUTCMonth() + 1) * 100 + d.getUTCDate()}.${d.getUTCHours() * 100 + d.getUTCMinutes()}`)')

echo "Updating package.json version to ${VERSION}..."
npm pkg set "version=${VERSION}"

echo "Synchronizing and building CLI version sources..."
node cli.js -version

echo "Validating public npm package..."
npm publish --dry-run --access public

echo "Committing and pushing release..."
git add \
    package.json \
    src/Effortless.Cli/Effortless.Cli.csproj \
    src/Effortless.Cli.Core/CliVersion.cs
git commit -m "Release v${VERSION}"
git push

echo "Creating GitHub release v${VERSION}..."
gh release create "v${VERSION}" --title "v${VERSION}" --notes "Release v${VERSION}"

echo "Publishing ${EXPECTED_PACKAGE}@${VERSION}..."
npm publish --access public

echo "Done! ${EXPECTED_PACKAGE}@${VERSION} is published and MSI/PKG builds will start automatically."
echo "Install: npm install -g ${EXPECTED_PACKAGE}"
echo "Track progress: gh run list --workflow=build-windows.yml && gh run list --workflow=build-mac.yml"
