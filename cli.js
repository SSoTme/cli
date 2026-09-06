#!/usr/bin/env node

'use strict';

const { spawn, execSync } = require('child_process');
const path = require('path');
const fs = require('fs');

const appDir = path.dirname(require.main.filename);
const rebuildProjectPath = path.join(appDir, 'src', 'Effortless.Cli', 'Effortless.Cli.csproj');
const outputPath = path.join(appDir, 'src', 'Effortless.Cli', 'bin', 'Release', 'net8.0', 'Effortless.Cli.dll');

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

const versionChanged = syncVersionFromPackageJson();

// Check if we need to build
if (versionChanged || !fs.existsSync(outputPath)) {
    console.log('Building Effortless CLI...');
    try {
        execSync(`dotnet build "${rebuildProjectPath}" --configuration Release`, {
            stdio: 'inherit',
            cwd: appDir
        });
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
