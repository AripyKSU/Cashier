$gameUi = Get-Content 'Assets/Prefabs/GameUI/GameUI.prefab'
$operating = Get-Content 'Assets/Prefabs/GameUI/OperatingPanel.prefab' -Raw

$strippedRefs = @()
for ($i = 0; $i -lt $gameUi.Length; $i++) {
    if ($gameUi[$i] -match 'm_CorrespondingSourceObject: \{fileID: (\d+), guid: cb56396611f15064f9ce66b932c05550') {
        $fid = $matches[1]
        $found = $operating.Contains(" &" + $fid)
        Write-Output "GameUI line $($i+1): fileID $fid -> Exists in OperatingPanel: $found"
    }
}
