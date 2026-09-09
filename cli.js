#!/usr/bin/env node

'use strict';

const { spawn, execSync } = require('child_process');
const path = require('path');
const fs = require('fs');

const appDir = path.dirname(require.main.filename);
const rebuildProjectPath = path.join(appDir, 'src', 'Effortless.Cli', 'Effortless.Cli.csproj');
const outputPath = path.join(appDir, 'src', 'Effortless.Cli', 'bin', 'Release', 'net8.0', 'Effortless.Cli.dll');
const buildStampPath = path.join(appDir, 'src', 'Effortless.Cli', 'bin', 'Release', 'net8.0', '.built-version');

// Sync version from package.json into .csproj <Version> and CLI_VERSION constant.
// Mirrors installers/windows/Scripts/build.ps1 so dev builds and installers match.
// Returns true if any source file was modified (caller forces a rebuild).
function syncVersionFromPackageJson() {
    const pkgVersion = require(path.join(appDir, 'package.json')).version;
    // npm-safe UTC stamp: "2026.424.1854" -> "2026.4.24.1854"
    const m = pkgVersion.match(/^(\d{4})\.(\d{3,4})\.(\d{1,4})$/);
    if (!m) {
        throw new Error(
            `Invalid package version '${pkgVersion}'; expected YYYY.MDD.HHMM without zero-padded numeric components.`
        );
    }
    const monthDay = Number(m[2]);
    const hourMinute = Number(m[3]);
    const month = Math.floor(monthDay / 100);
    const day = monthDay % 100;
    const hour = Math.floor(hourMinute / 100);
    const minute = hourMinute % 100;
    if (month < 1 || month > 12 || day < 1 || day > 31 || hour > 23 || minute > 59) {
        throw new Error(`Invalid UTC date/time in package version '${pkgVersion}'.`);
    }
    const csprojVersion = `${Number(m[1])}.${month}.${day}.${hourMinute}`;
    // Zero-padded, hyphenated, human-unambiguous form of the same instant:
    // "v{yyyy}-{MM}-{dd}-{HHmm}" (24h UTC).
    const pad = (n, width) => String(n).padStart(width, '0');
    const displayVersion = `v${m[1]}-${pad(month, 2)}-${pad(day, 2)}-${pad(hourMinute, 4)}`;

    let changed = false;
    const updates = [
        {
            file: rebuildProjectPath,
            pattern: /<Version>.*?<\/Version>/,
            replacement: `<Version>${csprojVersion}</Version>`,
        },
        {
            file: path.join(appDir, 'src', 'Effortless.Cli.Core', 'CliVersion.cs'),
            pattern: /public const string Value = ".*?";/,
            replacement: `public const string Value = "${pkgVersion}";`,
        },
        {
            file: path.join(appDir, 'src', 'Effortless.Cli.Core', 'CliVersion.cs'),
            pattern: /public const string DisplayVersion = ".*?";/,
            replacement: `public const string DisplayVersion = "${displayVersion}";`,
        },
    ];
    for (const u of updates) {
        if (!fs.existsSync(u.file)) continue;
        const before = fs.readFileSync(u.file, 'utf8');
        const after = before.replace(u.pattern, u.replacement);
        if (after !== before) {
            fs.writeFileSync(u.file, after);
            changed = true;
        }
    }
    return changed;
}

syncVersionFromPackageJson();

// Rebuild whenever the compiled DLL wasn't built for the current
// package.json version. syncVersionFromPackageJson()'s own "did I have to
// edit a file" signal is not enough: a fresh git pull/clone of a released
// commit already has the .csproj/CliVersion.cs pre-stamped to that release's
// version (release.sh commits them already synced), so no edit happens and
// the stale, previously-compiled DLL would otherwise run forever with no
// error and no visible change in -version.
const pkgVersion = require(path.join(appDir, 'package.json')).version;
const builtVersion = fs.existsSync(buildStampPath)
    ? fs.readFileSync(buildStampPath, 'utf8').trim()
    : null;

if (builtVersion !== pkgVersion || !fs.existsSync(outputPath)) {
    console.log('Building Effortless CLI...');
    try {
        execSync(`dotnet build "${rebuildProjectPath}" --configuration Release`, {
            stdio: 'inherit',
            cwd: appDir
        });
        fs.mkdirSync(path.dirname(buildStampPath), { recursive: true });
        fs.writeFileSync(buildStampPath, pkgVersion);
    } catch (error) {
        console.error('Failed to build .NET solution:', error);
        process.exit(1);
    }
}

// Run the CLI
try {
    const child = spawn('dotnet', [
        outputPath,
        ...process.argv.slice(2)
    ], {
        stdio: 'inherit',
        // cwd: appDir
    });
    child.on('error', (error) => {
        console.error('Failed to run CLI:', error);
        process.exit(1);
    });
    child.on('exit', (code, signal) => {
        process.exit(signal ? 1 : (code ?? 1));
    });
} catch (error) {
    console.error('Failed to run CLI:', error);
    process.exit(1);
}
