$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$version = (Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'VERSION must contain major.minor.patch' }
$name = "DesktopTodo-$version-win-x64"
$output = Join-Path $root 'artifacts\releases'
$stage = Join-Path $output ("staging\" + [Guid]::NewGuid().ToString('N') + "\$name")
New-Item -ItemType Directory -Force -Path $stage | Out-Null
& (Join-Path $root 'build.ps1') -OutputDirectory (Join-Path $stage 'app')
[IO.File]::WriteAllText((Join-Path $stage 'desktop-todo.portable'), 'DesktopTodo portable root')
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'bin') | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'bin\todo.cmd') -Destination (Join-Path $stage 'bin\todo.cmd')
foreach ($file in @('README.md','README.en.md','CHANGELOG.md','VERSION','enable-command.ps1','THIRD_PARTY_NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $root $file) -Destination $stage
}
foreach ($file in @('LICENSE')) {
    if(Test-Path -LiteralPath (Join-Path $root $file)) { Copy-Item -LiteralPath (Join-Path $root $file) -Destination $stage }
}
# Keep dependency changes from silently shipping stale or missing license notices.
$review = Get-Content -LiteralPath (Join-Path $root 'licenses\manifest.json') -Raw | ConvertFrom-Json
foreach ($entry in $review.files) {
    $licenseFile = Join-Path $root ("licenses\" + $entry.name)
    if ((Get-FileHash -LiteralPath $licenseFile -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) {
        throw "License text changed; review manifest: $($entry.name)"
    }
}
$assets = Get-Content -LiteralPath (Join-Path $root 'src\DesktopTodo\obj\project.assets.json') -Raw | ConvertFrom-Json
$actualPackages = @($assets.libraries.PSObject.Properties | Where-Object { $_.Value.type -eq 'package' } | ForEach-Object { $_.Name })
if (Compare-Object ($review.packages | Sort-Object) ($actualPackages | Sort-Object)) { throw 'Dependencies changed: update the license review first.' }
$runtime = Get-Content -LiteralPath (Join-Path $stage 'app\DesktopTodo.runtimeconfig.json') -Raw | ConvertFrom-Json
foreach ($framework in $runtime.runtimeOptions.includedFrameworks) {
    if ($framework.version -ne $review.runtimeVersion) { throw 'Runtime changed: update bundled license notices first.' }
}
Copy-Item -LiteralPath (Join-Path $root 'licenses') -Destination $stage -Recurse
Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination $stage -Recurse
$zip = Join-Path $output "$name.zip"
Compress-Archive -LiteralPath $stage -DestinationPath $zip -Force
$checksum = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $output "$name.sha256"), "$checksum  $name.zip" + [Environment]::NewLine)
Write-Output "Package: $zip"
Write-Output "SHA256: $checksum"
