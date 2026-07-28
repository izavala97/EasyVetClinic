[CmdletBinding()]
param(
    [switch]$ResetDatabase
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$apiDirectory = Join-Path $repositoryRoot 'WebAPI'
$clientDirectory = Join-Path $repositoryRoot 'WebClient'
$databasePath = Join-Path $apiDirectory 'easyvetclinic.db'

function Test-PortInUse {
    param([int]$Port)

    return $null -ne (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)
}

if (Test-PortInUse -Port 5120) {
    throw 'Port 5120 is already in use. Stop the existing API process before starting the development environment.'
}

if (Test-PortInUse -Port 5173) {
    throw 'Port 5173 is already in use. Stop the existing Vite process before starting the development environment.'
}

if ($ResetDatabase) {
    Get-ChildItem -Path "$databasePath*" -ErrorAction SilentlyContinue | Remove-Item -Force
    Write-Host 'Local database removed. It will be recreated and seeded when the API starts.'
}

if (-not (Test-Path (Join-Path $clientDirectory 'node_modules'))) {
    Write-Host 'Installing client dependencies...'
    Push-Location $clientDirectory
    try {
        & npm install
    }
    finally {
        Pop-Location
    }
}

Write-Host 'Starting API at http://localhost:5120...'
$apiProcess = Start-Process -FilePath 'dotnet' -ArgumentList @('run', '--launch-profile', 'http') -WorkingDirectory $apiDirectory -PassThru

try {
    Write-Host 'Starting client at http://localhost:5173...'
    Push-Location $clientDirectory
    try {
        & npm run dev -- --host 127.0.0.1 --port 5173 --strictPort
        if ($LASTEXITCODE -ne 0) {
            throw "Vite exited with code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}
finally {
    if (-not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id
    }
}