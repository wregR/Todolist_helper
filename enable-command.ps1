$ErrorActionPreference = 'Stop'
$bin = Join-Path $PSScriptRoot 'bin'
$existing = Get-Command todo -ErrorAction SilentlyContinue
$entry = Join-Path $bin 'todo.cmd'
if ($existing -and $existing.Source -ne $entry) { throw "todo already resolves to $($existing.Source). Remove the old registration before enabling this copy." }
$userPath = [Environment]::GetEnvironmentVariable('Path','User')
$entries = @($userPath -split ';' | Where-Object { $_ })
if ($entries -notcontains $bin) { [Environment]::SetEnvironmentVariable('Path', (($entries + $bin) -join ';'), 'User') }
$env:Path += ";$bin"
Write-Output 'The todo command is enabled. Restart your terminal to use it from other sessions.'
