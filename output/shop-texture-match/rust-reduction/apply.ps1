Add-Type -AssemblyName System.Drawing
$workspace=(Get-Location).Path
$generated='C:/Users/PC/.codex/generated_images/01a09d59-ddc3-7e82-bad5-7d345a6d7daf'
$jobs=@(
    @{Name='Stage2Shop';Image='exec-5de95cdc-f3d7-48cd-938e-ae8839e13a62.png'},
    @{Name='Stage2Container';Image='exec-ddc22c01-e93a-4f73-be5a-b3873e91f55e.png'}
)
foreach($job in $jobs){
    $original=[System.Drawing.Bitmap]::new("$workspace/output/shop-texture-match/rust-reduction/$($job.Name).png")
    $edit=[System.Drawing.Bitmap]::new("$generated/$($job.Image)")
    $result=$original.Clone()
    $changed=0
    for($y=0;$y -lt $original.Height;$y++){
        for($x=0;$x -lt $original.Width;$x++){
            $old=$original.GetPixel($x,$y)
            if($old.A -eq 0 -or $old.R -le ($old.G*1.13) -or $old.R -le ($old.B*1.25)){continue}
            $sx=[Math]::Min($edit.Width-1,[int][Math]::Floor($x*$edit.Width/$original.Width))
            $sy=[Math]::Min($edit.Height-1,[int][Math]::Floor($y*$edit.Height/$original.Height))
            $color=$edit.GetPixel($sx,$sy)
            $result.SetPixel($x,$y,[System.Drawing.Color]::FromArgb($old.A,$color.R,$color.G,$color.B))
            $changed++
        }
    }
    $result.Save("$workspace/Assets/DystopiaPrototype/Art/$($job.Name).png",[System.Drawing.Imaging.ImageFormat]::Png)
    Write-Output "$($job.Name): $changed surface pixels packaged; original dimensions and alpha retained"
    $original.Dispose();$edit.Dispose();$result.Dispose()
}

