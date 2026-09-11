Add-Type -AssemblyName System.Drawing
$bmp = [System.Drawing.Bitmap]::FromFile('Assets/Textures/Environment/Dystopia/WatchGuard.png')
Write-Output "Size: $($bmp.Width) x $($bmp.Height)"
$uniqueColors = @{}
for ($y = 0; $y -lt [Math]::Min(100, $bmp.Height); $y += 5) {
    for ($x = 0; $x -lt [Math]::Min(100, $bmp.Width); $x += 5) {
        $p = $bmp.GetPixel($x, $y)
        if ($p.A -gt 0) {
            $key = "$($p.R),$($p.G),$($p.B),$($p.A)"
            $uniqueColors[$key] = 1
        }
    }
}
Write-Output "Unique sampled colors in a small patch: $($uniqueColors.Count)"
$bmp.Dispose()
