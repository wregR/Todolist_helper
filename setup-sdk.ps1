$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$toolsDir = Join-Path $projectRoot '.tools'
$sdkDir = Join-Path $toolsDir 'dotnet'
if (Test-Path -LiteralPath (Join-Path $sdkDir 'dotnet.exe')) { Write-Output 'Project SDK already exists.'; exit 0 }
New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null
$metadata = Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
$sdk = $metadata.releases | ForEach-Object { $_.sdks } | Where-Object { $_.version -eq '10.0.403' } | Select-Object -First 1
if (-not $sdk) { throw 'Pinned SDK 10.0.403 not present in official release metadata.' }
$file = $sdk.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.zip' } | Select-Object -First 1
$archive = Join-Path $toolsDir 'sdk.zip'
Invoke-WebRequest -Uri $file.url -OutFile $archive
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash -ne $file.hash) { throw 'SDK hash mismatch.' }
Expand-Archive -LiteralPath $archive -DestinationPath $sdkDir -Force
& (Join-Path $sdkDir 'dotnet.exe') --version
