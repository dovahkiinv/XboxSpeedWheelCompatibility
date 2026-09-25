param([string]$Version = '1.3.0')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Use a three-part numeric version.' }
function Check-Exit { if ($LASTEXITCODE -ne 0) { throw "Build command failed: $LASTEXITCODE" } }
Push-Location $PSScriptRoot
try {
    $output = Join-Path $PSScriptRoot 'artifacts'
    $payload = Join-Path $output "payload-$Version"
    $toolDir = Join-Path $output 'tools'
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    if (!(Test-Path "$toolDir/wix.exe")) {
        dotnet tool install wix --version 4.0.6 --tool-path $toolDir
        Check-Exit
    }
    & "$toolDir/wix.exe" extension add WixToolset.UI.wixext/4.0.6
    Check-Exit
    foreach ($app in @('Service', 'Configurator')) {
        dotnet publish "WheelCompatibility$app/WheelCompatibility$app.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -o "$payload/$app"
        Check-Exit
    }
    $components = [System.Text.StringBuilder]::new()
    foreach ($app in @('Service', 'Configurator')) {
        foreach ($file in Get-ChildItem "$payload/$app" -File -Recurse | Sort-Object FullName) {
            if ($file.Name -eq "WheelCompatibility$app.exe") { continue }
            $source = [System.Security.SecurityElement]::Escape($file.FullName)
            $relativeDirectory = $file.DirectoryName.Substring((Join-Path $payload $app).Length).TrimStart('\')
            $subdirectory = if ($relativeDirectory) { ' Subdirectory="' + [System.Security.SecurityElement]::Escape($relativeDirectory) + '"' } else { '' }
            [void]$components.AppendLine("<Component Directory=`"${app}Folder`"$subdirectory Guid=`"*`"><File Source=`"$source`" KeyPath=`"yes`" /></Component>")
        }
    }
    $xml = '<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"><Fragment><ComponentGroup Id="PayloadFiles">' + $components.ToString() + '</ComponentGroup></Fragment></Wix>'
    $xml | Set-Content "$output/Payload.wxs" -Encoding utf8
    $license = (Get-Content LICENSE -Raw).Replace('\', '\\').Replace('{', '\{').Replace('}', '\}').Replace("`r", '').Replace("`n", '\par ')
    ('{\rtf1\ansi ' + $license + '}') | Set-Content "$output/License.rtf" -Encoding ascii
    & "$toolDir/wix.exe" build installer/Product.wxs "$output/Payload.wxs" -arch x64 -ext WixToolset.UI.wixext -d "Version=$Version" -d "Payload=$payload" -d "License=$output/License.rtf" -o "$output/XboxWheelCompatibility-$Version-win-x64.msi"
    Check-Exit
} finally { Pop-Location }
