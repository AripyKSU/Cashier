Add-Type -AssemblyName System.Drawing
$bmp = [System.Drawing.Bitmap]::FromFile('Assets/Textures/Environment/Dystopia/WatchGuard.png')
$cx = [int]($bmp.Width / 2)
$cy = [int]($bmp.Height / 2)
$uniqueColors = @{}
for ($y = $cy - 50; $y -lt $cy + 50; $y++) {
    for ($x = $cx - 50; $x -lt $cx + 50; $x++) {
        $p = $bmp.GetPixel($x, $y)
        if ($p.A -gt 10) {
            $key = "$($p.R),$($p.G),$($p.B)"
            $uniqueColors[$key] = 1
        }
    }
}
Write-Output "Unique colors in 100x100 center patch: $($uniqueColors.Count)"
$bmp.Dispose()
