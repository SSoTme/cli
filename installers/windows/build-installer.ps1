# run the build script for creating the Windows msi installer
param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [switch]$NoUpdate
)

$buildArgs = @()
if ($Configuration -ne "Release") {
    $buildArgs += "-Configuration", $Configuration
}
if ($Platform -ne "x64") {
    $buildArgs += "-Platform", $Platform
}
if ($NoUpdate) {
    $buildArgs += "-NoUpdate"
}

$buildScript = Join-Path $PSScriptRoot "Scripts/build.ps1"
if ($buildArgs.Count -gt 0) {
    powershell -ExecutionPolicy Bypass -File $buildScript @buildArgs
} else {
    powershell -ExecutionPolicy Bypass -File $buildScript
}
