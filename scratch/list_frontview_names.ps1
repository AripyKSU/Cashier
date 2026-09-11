$lines = Get-Content "Assets/DystopiaPrototype/Prefabs/FrontView.prefab"
for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match '^\s+m_Name:\s*(.+)$') {
        Write-Output $matches[1]
    }
}
