# Create ssotme folder in the user home directory
$destinationFolder = "$HOME\.ssotme"
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
