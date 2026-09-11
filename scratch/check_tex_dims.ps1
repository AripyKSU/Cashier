Add-Type -AssemblyName System.Drawing
foreach ($name in @('FarBackground.png', 'MidBackground.png', 'WatchGuard.png', 'LeftWatchTower.png', 'LeftWatchRail.png')) {
    $path = "Assets/Textures/Environment/Dystopia/$name"
    if (Test-Path $path) {
        $bmp = [System.Drawing.Bitmap]::FromFile($path)
        Write-Output "$name : $($bmp.Width) x $($bmp.Height)"
        $bmp.Dispose()
    }
}
