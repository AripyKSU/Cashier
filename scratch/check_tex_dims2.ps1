Add-Type -AssemblyName System.Drawing
foreach ($name in @('ChimneySmoke0.png', 'ChimneySmoke1.png', 'CrowdBack.png', 'FogBack.png')) {
    $path = "Assets/Textures/Environment/Dystopia/$name"
    if (Test-Path $path) {
        $bmp = [System.Drawing.Bitmap]::FromFile($path)
        Write-Output "$name : $($bmp.Width) x $($bmp.Height)"
        $bmp.Dispose()
    }
}
