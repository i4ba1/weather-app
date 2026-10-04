param([switch]$InstallDependencies)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot

function Invoke-Check {
    param([string]$Command, [string[]]$Arguments)
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed with exit code $LASTEXITCODE" }
}

Push-Location $projectRoot
try {
    if ($InstallDependencies) { Invoke-Check -Command dotnet -Arguments @('restore', 'WeatherApp.slnx') }
    Invoke-Check -Command dotnet -Arguments @('build', 'WeatherApp.slnx', '-c', 'Release', '--no-restore')
    Invoke-Check -Command dotnet -Arguments @('test', 'WeatherApp.slnx', '-c', 'Release', '--no-build', '--no-restore')
    Push-Location (Join-Path $projectRoot 'frontend')
    try {
        if ($InstallDependencies) {
            Invoke-Check -Command npm -Arguments @('ci')
            Invoke-Check -Command npx -Arguments @('playwright', 'install', 'chromium')
        }
        Invoke-Check -Command npm -Arguments @('run', 'lint')
        Invoke-Check -Command npm -Arguments @('run', 'test', '--', '--run')
        Invoke-Check -Command npm -Arguments @('run', 'build')
        Invoke-Check -Command npm -Arguments @('run', 'test:e2e')
    } finally { Pop-Location }
} finally { Pop-Location }
