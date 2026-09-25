param([switch]$Publish)

$ErrorActionPreference = 'Stop'
$projects = @{
    Service = 'WheelCompatibilityService/WheelCompatibilityService.csproj'
    Configurator = 'WheelCompatibilityConfigurator/WheelCompatibilityConfigurator.csproj'
}

Push-Location $PSScriptRoot
try {
    if ($Publish) {
        $publishDir = (Join-Path $PSScriptRoot 'publish')
        $running = Get-Process WheelCompatibilityService, WheelCompatibilityConfigurator -ErrorAction SilentlyContinue |
            Where-Object { $_.Path -and $_.Path.StartsWith($publishDir, [StringComparison]::OrdinalIgnoreCase) }
        if ($running) {
            $names = ($running | ForEach-Object { "$($_.ProcessName) (PID $($_.Id))" }) -join ', '
            throw "Close the running app first: $names. Stop the service with Ctrl+C in its window (this also unhides the wheel in HidHide), close the configurator, then run build again."
        }
    }

    foreach ($name in @('Service', 'Configurator')) {
        if ($Publish) {
            dotnet publish $projects[$name] -c Release -r win-x64 --self-contained false -o "publish/$name"
        } else {
            dotnet build $projects[$name] -c Release -r win-x64
        }
        if ($LASTEXITCODE -ne 0) { throw "$name build failed (exit $LASTEXITCODE)." }
    }
} finally {
    Pop-Location
}
