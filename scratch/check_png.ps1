Add-Type -AssemblyName System.Drawing
$bmp = [System.Drawing.Bitmap]::FromFile('Assets/DystopiaPrototype/TopDownTest/Art/FrontContainerMale.png')
Write-Host "Size: $($bmp.Width) x $($bmp.Height)"
$minX = $bmp.Width; $maxX = 0; $minY = $bmp.Height; $maxY = 0
for ($y = 0; $y -lt $bmp.Height; $y++) {
    for ($x = 0; $x -lt $bmp.Width; $x++) {
        if ($bmp.GetPixel($x, $y).A -gt 10) {
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
}
Write-Host "Bounds: X=$minX..$maxX (Width=$($maxX-$minX+1)), Y=$minY..$maxY (Height=$($maxY-$minY+1))"
$bmp.Dispose()
