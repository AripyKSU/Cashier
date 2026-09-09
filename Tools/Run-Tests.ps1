param([ValidateSet('Both','EditMode','PlayMode')][string]$Mode = 'Both', [int]$TimeoutSeconds = 180)
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$runId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N')
$modes = if ($Mode -eq 'Both') { @('EditMode','PlayMode') } else { @($Mode) }
foreach ($currentMode in $modes) {
    $xmlPath = Join-Path $project "Temp/TestResults/$runId/$currentMode.xml"
    $command = 'return CashierTestRun.Start("' + $currentMode + '","' + $runId + '");'
    $start = $command | & unity-cli --project $project --timeout 15000 exec 2>&1
    if ($LASTEXITCODE -ne 0 -or ($start -join "`n") -notmatch [regex]::Escape("$currentMode.xml")) { throw "Test Runner did not start: $start" }
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while (!(Test-Path -LiteralPath $xmlPath) -and $watch.Elapsed.TotalSeconds -lt $TimeoutSeconds) { Start-Sleep -Milliseconds 500 }
    if (!(Test-Path -LiteralPath $xmlPath)) { throw "BLOCKED: timed out; do not start another run until Unity finishes. Expected $xmlPath" }
    [xml]$result = Get-Content -LiteralPath $xmlPath -Raw
    $root = $result.DocumentElement
    $cases = @($result.SelectNodes('//test-case'))
    $passed = @($cases | Where-Object result -EQ 'Passed').Count
    $failed = @($cases | Where-Object result -EQ 'Failed').Count
    $other = $cases.Count - $passed - $failed
    Write-Output "$currentMode total=$($cases.Count) passed=$passed failed=$failed skipped/incomplete=$other; $xmlPath"
    if ($cases.Count -eq 0 -or $failed -gt 0 -or $other -gt 0 -or $root.result -ne 'Passed') { throw "Not PASS: inspect $xmlPath and its .log file." }
}
