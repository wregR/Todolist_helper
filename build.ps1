$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$env:DOTNET_ROOT = Join-Path $projectRoot '.tools\dotnet'
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\cli'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.tools\nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$dotnetExe = Join-Path $env:DOTNET_ROOT 'dotnet.exe'
& $dotnetExe run --project "$projectRoot\tests\DesktopTodo.Tests" -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
& $dotnetExe publish "$projectRoot\src\DesktopTodo" -c Release -r win-x64 --self-contained true -o "$projectRoot\app" -p:PublishSingleFile=false
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
