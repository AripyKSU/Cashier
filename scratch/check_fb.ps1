$c = Get-Content 'Assets/Prefabs/GameUI/OperatingPanel.prefab'
for ($i = 0; $i -lt $c.Length; $i++) {
    if ($c[$i] -match 'm_Name: FarBackground') {
        for ($j = [Math]::Max(0, $i - 10); $j -lt [Math]::Min($c.Length, $i + 45); $j++) {
            Write-Output $c[$j]
        }
        break
    }
}
