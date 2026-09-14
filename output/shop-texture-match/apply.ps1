Add-Type -AssemblyName System.Drawing
$workspace=(Get-Location).Path
$generated='C:/Users/PC/.codex/generated_images/01a09d59-ddc3-7e82-bad5-7d345a6d7daf'
$jobs=@(
    @{Name='Stage2Shop';Image='exec-83e90364-1fb1-4518-a677-4a2fa2035e94.png'},
    @{Name='Stage2Container';Image='exec-4947cbed-80d1-494e-86f7-effc4b8cbae8.png'}
)
foreach($job in $jobs){
    $original=[System.Drawing.Bitmap]::new("$workspace/output/shop-texture-match/$($job.Name).png")
    $edit=[System.Drawing.Bitmap]::new("$generated/$($job.Image)")
    $result=$original.Clone()
    $changed=0
    for($y=0;$y -lt $original.Height;$y++){
        for($x=0;$x -lt $original.Width;$x++){
            # Composite generated surface pixels only; keep authored geometry, alpha and hardware.
            if($job.Name -eq 'Stage2Shop'){
                $inside=($y -ge 606 -and $y -le 687 -and $x -ge 150 -and $x -le 1520) -or ($y -ge 701 -and $y -le 748 -and $x -ge 96 -and $x -le 1575)
            }else{
                $inside=($x -ge 204 -and $x -le 1425 -and (($y -ge 390 -and $y -le 531) -or ($y -ge 570 -and $y -le 692) -or ($y -ge 730 -and $y -le 811)))
                if($x -ge 680 -and $x -le 970 -and $y -le 535){$inside=$false}
            }
            if(-not $inside){continue}
            $old=$original.GetPixel($x,$y)
            if($old.A -eq 0){continue}
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
