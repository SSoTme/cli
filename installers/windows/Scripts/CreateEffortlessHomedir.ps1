# Create the .effortless folder in the user home directory (the CLI migrates a legacy ~/.ssotme itself on first run)
$destinationFolder = "$HOME\.effortless"
if (-not (Test-Path $destinationFolder)) {
    New-Item -Path $destinationFolder -ItemType Directory -Force
    Write-Host "Created directory: $destinationFolder"
}

# Self-delete after execution
try {
    Remove-Item -Path $PSCommandPath -Force -ErrorAction SilentlyContinue
} catch {
    # Silently ignore errors
}
