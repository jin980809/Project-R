# 유니티 에디터에 명령을 보내고 결과를 기다린다(EditorCommandWatcher와 짝).
# 사용: pwsh -File Tools\EditorCmd.ps1 -Command refresh [-WaitCompile] [-WaitSeconds 5]
param(
    [Parameter(Mandatory = $true)][string]$Command,
    [switch]$WaitCompile,
    [int]$WaitSeconds = 0
)

$temp = Join-Path $PSScriptRoot "..\Temp"
$log = "$env:LOCALAPPDATA\Unity\Editor\Editor.log"
$result = Join-Path $temp "tower_cmd_result.txt"
$start = (Get-Item $log).Length

Remove-Item $result -ErrorAction SilentlyContinue
Set-Content (Join-Path $temp "tower_cmd.txt") $Command -NoNewline

$deadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $deadline -and -not (Test-Path $result)) { Start-Sleep -Milliseconds 500 }
if (Test-Path $result) { Get-Content $result -Raw } else { "no response from editor" }

function ReadNewLog {
    $fs = [IO.File]::Open($log, 'Open', 'Read', 'ReadWrite')
    $fs.Position = $start
    $reader = New-Object IO.StreamReader($fs)
    $text = $reader.ReadToEnd()
    $reader.Close()
    return $text
}

if ($WaitCompile) {
    $deadline = (Get-Date).AddSeconds(120)
    $text = ""
    $done = $false
    while ((Get-Date) -lt $deadline -and -not $done) {
        Start-Sleep -Seconds 3
        $text = ReadNewLog
        if ($text -match "error CS|Domain Reload Profiling") { Start-Sleep -Seconds 3; $done = $true }
    }
    $errors = @(($text -split "`n") | Where-Object { $_ -match "error CS" } | Sort-Object -Unique)
    if ($errors.Count -gt 0) { "COMPILE ERRORS:"; $errors } elseif ($done) { "compile ok" } else { "no compile detected" }
}

if ($WaitSeconds -gt 0) { Start-Sleep -Seconds $WaitSeconds }
