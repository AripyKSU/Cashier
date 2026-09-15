param([Parameter(Mandatory=$true)][string[]]$Paths)
Add-Type -AssemblyName System.Drawing
if (-not ('Stage3AlphaBounds' -as [type])) {
    Add-Type -TypeDefinition @'
public static class Stage3AlphaBounds {
    public static int[] Scan(byte[] bytes, int width, int height, int stride) {
        int minX=width, minY=height, maxX=-1, maxY=-1, transparent=0;
        for (int y=0;y<height;y++) for(int x=0;x<width;x++) {
            if(bytes[y*stride+x*4+3]>8) {
                if(x<minX)minX=x; if(x>maxX)maxX=x;
                if(y<minY)minY=y; if(y>maxY)maxY=y;
            } else transparent++;
        }
        return new int[] {minX, height-maxY-1, maxX-minX+1, maxY-minY+1, transparent};
    }
}
'@
}
foreach ($path in $Paths) {
    $bitmap = [System.Drawing.Bitmap]::new($path)
    try {
        $rect = [System.Drawing.Rectangle]::new(0, 0, $bitmap.Width, $bitmap.Height)
        $bits = $bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $bytes = [byte[]]::new([Math]::Abs($bits.Stride) * $bitmap.Height)
            [System.Runtime.InteropServices.Marshal]::Copy($bits.Scan0, $bytes, 0, $bytes.Length)
            $bounds = [Stage3AlphaBounds]::Scan($bytes, $bitmap.Width, $bitmap.Height, $bits.Stride)
            [pscustomobject]@{Path=$path; Width=$bitmap.Width; Height=$bitmap.Height; X=$bounds[0]; Y=$bounds[1]; CropWidth=$bounds[2]; CropHeight=$bounds[3]; Transparent=$bounds[4]}
        } finally { $bitmap.UnlockBits($bits) }
    } finally { $bitmap.Dispose() }
}
