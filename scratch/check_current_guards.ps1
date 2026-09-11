$c = Get-Content 'Assets/Prefabs/GameUI/OperatingPanel.prefab'
for ($i = 0; $i -lt $c.Length; $i++) {
    if ($c[$i] -match 'm_Name: (LeftWatchGuard|RightWatchGuard)') {
        Write-Output "=== Found $($matches[1]) at line $i ==="
        for ($j = [Math]::Max(0, $i - 10); $j -lt [Math]::Min($c.Length, $i + 60); $j++) {
            Write-Output $c[$j]
        }
    }
}
