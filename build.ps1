param([switch]$Publish)

$ErrorActionPreference = 'Stop'
$projects = @{
    Service = 'WheelCompatibilityService/WheelCompatibilityService.csproj'
    Configurator = 'WheelCompatibilityConfigurator/WheelCompatibilityConfigurator.csproj'
}

Push-Location $PSScriptRoot
try {
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
