# Build script for SSoTme Windows Installer (WiX v6)
# Integrates with setup.py for Python CLI building via PyInstaller
#
# Generates:
#           - bin/Release/SSoTme-Installer.msi -> installs just ssotme

param (
    [string]$Configuration = "Release",
    [string]$Platform = "x86"
)


# Helper function to update version if changed
function Update-VersionIfChanged {
    param (
        [string]$FilePath,
        [string]$NewVersion,
        [string]$Pattern = '<SSoTmeVersion>([^<]*)</SSoTmeVersion>',
        [string]$Replacement = "<SSoTmeVersion>$NewVersion</SSoTmeVersion>"
    )
    $content = Get-Content $FilePath -Raw
    if ($content -match $Pattern) {
        $currentVersion = $matches[1]
        if ($currentVersion -ne $NewVersion) {
            $content = $content -replace $Pattern, $Replacement
            Set-Content $FilePath $content -NoNewline
            Write-Host "Updated $($(Split-Path $FilePath -Leaf)) from $currentVersion to $NewVersion" -ForegroundColor Green
            return $true
        } else {
            Write-Host "Version in $($(Split-Path $FilePath -Leaf)) already at $NewVersion" -ForegroundColor Cyan
            return $false
        }
    }
    return $false
}

$ErrorActionPreference = "Stop"

# Ensure we're running from the Scripts directory
$ScriptDir = $PSScriptRoot
if (-not $ScriptDir) {
    $ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}

# Calculate paths relative to script location
$InstallerDir = Split-Path -Parent $ScriptDir
$RootDir = Split-Path -Parent (Split-Path -Parent $InstallerDir)

# Validate we're in the right place
if (-not (Test-Path (Join-Path $RootDir "setup.py"))) {
    Write-Error "Cannot find setup.py at expected location: $(Join-Path $RootDir "setup.py")"
    Write-Error "Please run this script from Windows/Installer/Scripts/ directory"
    exit 1
}

$SourceDir = Join-Path $RootDir "Windows\CLI"
$ResourcesDir = Join-Path $InstallerDir "Resources"
$AssetsDir = Join-Path $InstallerDir "Assets"
$binFolder = Join-Path $InstallerDir "bin"
$OutputDir = Join-Path $binFolder "$Configuration"

Write-Host "=== SSoTme WiX v6 Build Script ===" -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
Write-Host "Platform: $Platform" -ForegroundColor Cyan
Write-Host "Script Directory: $ScriptDir" -ForegroundColor Cyan
Write-Host "Installer Directory: $InstallerDir" -ForegroundColor Cyan
Write-Host "Root Directory: $RootDir" -ForegroundColor Cyan

# clean any previous builds
Write-Host "`nCleaning previous builds..." -ForegroundColor Yellow
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $ResourcesDir  # resources are copied into here from the root dir during build
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue (Join-Path $RootDir "build")
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $binFolder
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue (Join-Path $InstallerDir "obj")

Write-Host "Creating necessary directories..." -ForegroundColor Yellow
$Directories = @(
    $ResourcesDir,
    $OutputDir,
    $AssetsDir
)

foreach ($Dir in $Directories) {
    if (-not (Test-Path $Dir)) {
        New-Item -ItemType Directory -Path $Dir -Force | Out-Null
        Write-Host "Created directory: $Dir" -ForegroundColor Green
    }
}

$packageJsonTxt = Get-Content (Join-Path $RootDir "package.json") -Raw | ConvertFrom-Json
$ssotmeVersionOriginal = $packageJsonTxt.version
$ssotmeVersion = $ssotmeVersionOriginal -replace "0", ""  #  Invalid product version '2024.08.23'. Product version must have a major version less than 256, a minor version less than 256
Write-Host "Using version: $ssotmeVersion from package.json"

# update csproj version
$CSPROJ_CONTENT = Get-Content "$SourceDir/SSoTme.OST.CLI.csproj" -Raw
$CSPROJ_CONTENT_new = $CSPROJ_CONTENT -replace "<Version>.*?</Version>", "<Version>$ssotmeVersionOriginal</Version>"
Set-Content "$SourceDir/SSoTme.OST.CLI.csproj" $CSPROJ_CONTENT_new -NoNewline
Write-Host "Updated version of $SourceDir/SSoTme.OST.CLI.csproj to $ssotmeVersionOriginal"

# copy LICENSE from the root dir into Resources/ (this will be included in the app, while Assets/LICENSE.rtf is only showed at install time)
Write-Host "`nCopying license and documentation files..." -ForegroundColor Yellow
$licenseSrc = Join-Path $RootDir "LICENSE"
$licenseDest = Join-Path $ResourcesDir "LICENSE.txt"
if (Test-Path $licenseSrc) {
    Copy-Item $licenseSrc -Destination $licenseDest -Force
    Write-Host "Copied LICENSE to Resources/" -ForegroundColor Green
}
else {
    Write-Warning "LICENSE file not found at root - installer will continue without it"
}

# copy README into Resources/
$rdmeSrc = Join-Path $RootDir "README.md"
$rdmeDest = Join-Path $ResourcesDir "README.md"
if (Test-Path $rdmeSrc) {
    Copy-Item $rdmeSrc -Destination $rdmeDest -Force
    Write-Host "Copied README.md to Resources/" -ForegroundColor Green
}
else {
    Write-Warning "README.md not found at root - installer will continue without it"
}


$arch = (Get-CimInstance Win32_OperatingSystem).OSArchitecture
$rid = switch ($arch) {
    "64-bit" { "win-x64" }
    "32-bit" { "win-x86" }
    "ARM 64-bit" { "win-arm64" }
    default { "win-x64" }  # fallback
}

# Update version in the cli handler file so ssotme -version works
$CLIHandlerFile = "$RootDir/Windows/Lib/CLIOptions/SSoTmeCLIHandler.cs"
Update-VersionIfChanged -FilePath $CLIHandlerFile -NewVersion $ssotmeVersionOriginal `
    -Pattern 'public string CLI_VERSION = ".*?";' `
    -Replacement "public string CLI_VERSION = `"$ssotmeVersionOriginal`";"

Write-Host "`nBuilding .NET CLI project..." -ForegroundColor Yellow

try {
    Set-Location $SourceDir
    dotnet publish -r $rid -c Release /p:PublishSingleFile=true --self-contained true -o "$ResourcesDir"
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build .NET CLI project"
        exit 1
    }
    Write-Host ".NET project built successfully" -ForegroundColor Green
}
finally {
    Pop-Location
}

try {
    # Verify the generated executable
    $ssotmeExePath = Join-Path $ResourcesDir "SSoTme.OST.CLI.exe"
    if (-not (Test-Path $ssotmeExePath)) {
        Write-Error "built exe not found in resources directory: $ResourcesDir"
        Write-Host "Contents of directory:"
        Get-ChildItem $ResourcesDir -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "  $_" }
        exit 1
    }

    Write-Host "Found ssotme.exe, creating alias executables..."
    
    # Copy the ssotme.exe into ssotme, aic, aicapture (these are the CLI entry points)
    Copy-Item -Path $ssotmeExePath -Destination "$ResourcesDir/ssotme.exe" -Force
    Copy-Item -Path $ssotmeExePath -Destination "$ResourcesDir/aic.exe" -Force
    Copy-Item -Path $ssotmeExePath -Destination "$ResourcesDir/aicapture.exe" -Force

    Write-Host "Created alias executables in: $ResourcesDir"
}
finally {
    Pop-Location
}

Set-Location $InstallerDir
$MsiName = "SSoTme-Installer_$rid"
$MsiPath = Join-Path $OutputDir "$MsiName.msi"

# Update versions in all files if needed
$installerProj = Join-Path $InstallerDir "SSoTmeInstaller.wixproj"
$productwxs = Join-Path $InstallerDir "Product.wxs"

# Update versions in all files
Update-VersionIfChanged -FilePath $installerProj -NewVersion $ssotmeVersion

Update-VersionIfChanged -FilePath $productwxs -NewVersion $ssotmeVersion `
    -Pattern '<Package([^>]*?)Version="([^"]*)"([^>]*?)>' `
    -Replacement "<Package`$1Version=`"$ssotmeVersion`"`$3>"
Update-VersionIfChanged -FilePath $productwxs -NewVersion $ssotmeVersion `
    -Pattern '<RegistryValue([^>]*?)Name="Version" Value="([^"]*)"([^>]*?)>' `
    -Replacement "<RegistryValue`$1Name=`"Version`" Value=`"$ssotmeVersion`"`$3>"

# Rename the target file for the installer
Update-VersionIfChanged -FilePath $installerProj -NewVersion $MsiName `
    -Pattern '<OutputName>[^"]*</OutputName>' `
    -Replacement "<OutputName>$MsiName</OutputName>"

# Build the WiX projects using dotnet build
Write-Host "Building WiX installer projects..."
try {
    # Check if .NET SDK is available (WiX v6 requires .NET)
    $dotnetPath = (Get-Command dotnet.exe -ErrorAction SilentlyContinue).Source

    if (-not $dotnetPath) {
        Write-Error ".NET SDK not found. WiX v6 requires .NET SDK. Please install from https://dotnet.microsoft.com/download"
        return
    }

    Write-Host "Building MSI package..."
    
    # Ensure we're in the installer directory for the build
    Set-Location $InstallerDir
    
    # Build the MSI project first (this should only compile Product.wxs)
    Write-Host "Building SSoTmeInstaller.wixproj (MSI)..." -ForegroundColor Cyan
    & dotnet build SSoTmeInstaller.wixproj --configuration $Configuration --verbosity normal
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build MSI project"
        return
    }

    if (-Not (Test-Path $MsiPath)) {
        Write-Host "ERROR: installer file not found at expected location: $MsiPath"
        Write-Host "Contents of output directory:" -ForegroundColor Yellow
        Get-ChildItem $OutputDir -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
        exit 1
    }
    Write-Host "MSI built successfully: $MsiPath" -ForegroundColor Green
    Invoke-Item (Split-Path -Parent $MsiPath)
}
catch {
    Write-Error "Failed to build WiX installer: $_"
}
finally {
    # Always return to the original directory
    Set-Location $ScriptDir
}

Write-Host "Build script completed" -ForegroundColor Green