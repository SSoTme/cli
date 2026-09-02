# Releasing Effortless CLI

The public npm package is `@effortlessapi/cli`. It installs the four executable
aliases `effortless`, `ssotme`, `aicapture`, and `aic`.

## Required repository configuration

Configure these GitHub Actions secrets:

- `AIRTABLE_PAT`
- `SSOT_BASE_ID`
- `APPCERTIFICATE`
- `DEVCERTIFICATE`
- `CERT_PASSWORD`
- `DEV_INS_KEYCHAIN_ID`
- `DEV_APP_KEYCHAIN_ID`
- `APPLEID`
- `NOTARY_PASSWORD`

The release operator must also be authenticated with `gh` and npm, with
permission to publish the public `@effortlessapi/cli` package.

Protect `main` with squash merges, linear history, and required checks from
`.github/workflows/ci.yml`:

- Build and test (Ubuntu, macOS, Windows)
- Slow tests
- Generated files and rulebook
- Public npm package
- Legacy parity (until Step 8 removes it)

Require CODEOWNER review and prevent direct pushes except for the guarded
release script.

## Pull requests

Every pull request records the rulebook rows changed, tests added, and any
generated `-help` change. CI must be green before a squash merge to `main`.

When a pull request merges, `.github/workflows/release.yml` tags the version
already stored in `package.json` and creates a draft GitHub release using the
pull request body. Publish that draft only when the version is intended for
distribution.

## Guarded release

From a clean `main` checkout:

```bash
scripts/release.sh --dry-run
scripts/release.sh
```

The release version is derived from UTC as `yyyy.(M*100+d).(H*100+m)`, for
example `2026.901.1430`. The script:

1. verifies the branch, working tree, package identity, and npm authentication;
2. stamps and synchronizes all version sources;
3. runs the full .NET suite and packaged-alias smoke test;
4. commits and pushes `Release v<version>`;
5. creates GitHub release `v<version>`;
6. publishes `@effortlessapi/cli`.

It never publishes from a dirty tree or a non-`main` branch.

## Installers and Airtable

Publishing or prereleasing a GitHub release starts the native
`.github/workflows/build-mac.yml` and `.github/workflows/build-windows.yml`
workflows, producing:

- `SSoTme-Installer_win-x64.msi`
- `SSoTme-Installer_win-arm64.msi`
- `SSoTme-Installer-x86_64.pkg`
- `SSoTme-Installer-arm64.pkg`

The workflows skip installer families already present on the release. The
manual `.github/workflows/build-installers.yml` workflow reruns the release
commit gate and can build all four assets for an existing prerelease tag
without adding a second automatic release trigger.

A push to `main` runs `.github/workflows/update-airtable.yml`, which appends the
version and installer URLs to `CLIVersions`. The cloud bridge reads that table,
so `effortless -upgradeCli` can discover the release.
