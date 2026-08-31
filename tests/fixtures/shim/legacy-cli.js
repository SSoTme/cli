#!/usr/bin/env node

'use strict';

const { spawn, execSync } = require('child_process');
const path = require('path');
const fs = require('fs');

const appDir = path.dirname(require.main.filename);
const solutionPath = path.join(appDir, 'SSoTme-OST-CLI.sln');
const outputPath = path.join(appDir, 'Windows', 'CLI', 'bin', 'Release', 'net8.0', 'SSoTme.OST.CLI.dll');

// Frozen at legacy-final for characterization tests.
function syncVersionFromPackageJson() {
    const pkgVersion = require(path.join(appDir, 'package.json')).version;
    const m = pkgVersion.match(/^(\d{4})-(\d{2})-(\d{2})\.(\d{1,2})\.(\d{1,2})$/);
    const csprojVersion = m
        ? `${+m[1]}.${+m[2]}.${+m[3]}.${+m[4]}${m[5].padStart(2, '0')}`
        : pkgVersion;

    let changed = false;
    const updates = [
        {
            file: path.join(appDir, 'Windows', 'CLI', 'SSoTme.OST.CLI.csproj'),
            pattern: /<Version>.*?<\/Version>/,
            replacement: `<Version>${csprojVersion}</Version>`,
        },
        {
            file: path.join(appDir, 'Windows', 'Lib', 'CLIOptions', 'SSoTmeCLIHandler.cs'),
            pattern: /public string CLI_VERSION = ".*?";/,
            replacement: `public string CLI_VERSION = "${pkgVersion}";`,
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

if (versionChanged || !fs.existsSync(outputPath)) {
    console.log('Building .NET solution...');
    try {
        execSync(`dotnet build "${solutionPath}" --configuration Release`, {
            stdio: 'inherit',
            cwd: appDir
        });
    } catch (error) {
        console.error('Failed to build .NET solution:', error);
        process.exit(1);
    }
}

try {
    spawn('dotnet', [
        outputPath,
        process.argv.slice(2).join(' ')
    ], {
        stdio: 'inherit',
    });
} catch (error) {
    console.error('Failed to run CLI:', error);
    process.exit(1);
}
