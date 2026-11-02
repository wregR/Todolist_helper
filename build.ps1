param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'app'))
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\cli'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.tools\nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$localSdk = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
if (Test-Path -LiteralPath $localSdk) {
    $env:DOTNET_ROOT = Split-Path -Parent $localSdk
    $dotnetExe = $localSdk
} else {
    $dotnetExe = (Get-Command dotnet -ErrorAction Stop).Source
}
$version = (Get-Content -LiteralPath (Join-Path $projectRoot 'VERSION') -Raw).Trim()
Push-Location $projectRoot
try {
    & $dotnetExe run --project "$projectRoot\tests\DesktopTodo.Tests" -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
    & $dotnetExe publish "$projectRoot\src\DesktopTodo" -c Release -r win-x64 --self-contained true -o $OutputDirectory "-p:Version=$version"
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
} finally { Pop-Location }
