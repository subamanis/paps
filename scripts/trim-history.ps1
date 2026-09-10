$path = Join-Path $env:APPDATA 'Microsoft\Windows\PowerShell\PSReadLine\ConsoleHost_history.txt'

if (-not (Test-Path $path)) { return }
if ((Get-Item $path).Length -lt 1MB) { return }
if (Get-Process pwsh, powershell -ErrorAction Ignore | Where-Object { $_.Id -ne $PID }) { return }

$lines = Get-Content $path
$counts = @{}

foreach ($line in $lines) {
    $counts[$line] = 1 + $counts[$line]
}

$recent = [Collections.Generic.HashSet[string]]::new([string[]] ($lines | Select-Object -Last 1000))
$kept = $lines | Where-Object { $counts[$_] -ge 2 -or $recent.Contains($_) }

Copy-Item $path "$path.bak" -Force
$kept | Set-Content $path -Encoding utf8