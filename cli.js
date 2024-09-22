#!/usr/bin/env node

'use strict';

const { spawn } = require('child_process');
var path = require('path');
var appDir = path.dirname(require.main.filename);

spawn('dotnet', [`${appDir}/Windows/CLI/bin/Release/net7.0/SSoTme.OST.CLI.dll`, process.argv.slice(2).join(' ')], { stdio: 'inherit' });
