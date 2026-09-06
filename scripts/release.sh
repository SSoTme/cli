#!/bin/bash
set -euo pipefail

EXPECTED_PACKAGE="@effortlessapi/cli"
DRY_RUN=false

for argument in "$@"; do
    case "$argument" in
        --dry-run)
            DRY_RUN=true
            ;;
        *)
            echo "Usage: scripts/release.sh [--dry-run]" >&2
            exit 2
            ;;
    esac
done

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

if [ -n "$(git status --porcelain)" ]; then
    echo "ERROR: release requires a clean working tree." >&2
    exit 1
fi

BRANCH="$(git branch --show-current)"
if [ "$BRANCH" != "main" ]; then
    echo "ERROR: release must run from main; current branch is '${BRANCH:-detached}'." >&2
    exit 1
fi

PACKAGE_NAME="$(node -p "require('./package.json').name")"
if [ "$PACKAGE_NAME" != "$EXPECTED_PACKAGE" ]; then
    echo "ERROR: package.json name must be $EXPECTED_PACKAGE; got $PACKAGE_NAME" >&2
    exit 1
fi

VERSION="$(node -e 'const d = new Date(); console.log(`${d.getUTCFullYear()}.${(d.getUTCMonth() + 1) * 100 + d.getUTCDate()}.${d.getUTCHours() * 100 + d.getUTCMinutes()}`)')"
TAG="v${VERSION}"

if [ "$DRY_RUN" = true ]; then
    echo "Dry run: release ${EXPECTED_PACKAGE}@${VERSION}"
    echo "Would verify npm authentication."
    echo "Would stamp package.json, Effortless.Cli.csproj, and CliVersion.cs."
    echo "Would run the full .NET and packaged-alias test suites."
    echo "Would commit and push ${TAG} from main."
    echo "Would create GitHub release ${TAG}."
    echo "Would publish ${EXPECTED_PACKAGE}@${VERSION}."
    exit 0
fi

echo "Checking npm authentication..."
npm whoami >/dev/null

echo "Updating package.json version to ${VERSION}..."
npm pkg set "version=${VERSION}"

echo "Synchronizing and building CLI version sources..."
node cli.js -version

echo "Running the full test suite..."
dotnet test Effortless.Cli.sln --configuration Release

echo "Validating public npm package..."
npm publish --dry-run --access public
npm run test:package

echo "Committing and pushing release..."
git add \
    package.json \
    src/Effortless.Cli/Effortless.Cli.csproj \
    src/Effortless.Cli.Core/CliVersion.cs
git commit -m "Release ${TAG}"
git push

echo "Creating GitHub release ${TAG}..."
gh release create "${TAG}" --title "${TAG}" --notes "Release ${TAG}"

echo "Publishing ${EXPECTED_PACKAGE}@${VERSION}..."
npm publish --access public

echo "Done! ${EXPECTED_PACKAGE}@${VERSION} is published and MSI/PKG builds will start automatically."
echo "Install: npm install -g ${EXPECTED_PACKAGE}"
echo "Track progress: gh run list --workflow=build-windows.yml && gh run list --workflow=build-mac.yml"
