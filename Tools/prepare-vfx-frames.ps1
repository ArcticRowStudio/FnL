<#
.SYNOPSIS
    Crops, scales and trims a rendered effect sequence down to the game's pixel density.

.DESCRIPTION
    Effect renders arrive as full frame images, mostly empty, at a resolution far above what
    the game shows. Imported as is, one sequence costs more texture memory than the rest of
    the project put together. This crops every frame to one shared box, scales the sequence so
    it is the size it will actually be drawn at, and drops the blank and repeated frames that
    tend to sit at the end of a render.

    One shared crop box, so the effect does not jitter between frames. The reported pivot is
    what to set on the sprites: give it in source pixels and it comes back as the fraction the
    importer wants.

.EXAMPLE
    Reproduces the shipped Slash clip. The anchor is what the effect is positioned by, so it
    has to be passed explicitly; leaving it out centres the pivot and the swing detaches from
    the character.

    ./Tools/prepare-vfx-frames.ps1 -SourceFolder "ArtSource/Vfx/Horizontal_Slash" `
        -OutputFolder "Assets/Art/Pixel/Vfx/Slash" -TargetWidth 233 -AnchorX 429 -AnchorY 732
#>
param(
    [Parameter(Mandatory=$true)][string]$SourceFolder,
    [Parameter(Mandatory=$true)][string]$OutputFolder,

    # Width in pixels the whole sequence should end up at. Set the sprite's PPU to match:
    # world size is TargetWidth / PPU, so scaling both together changes only pixel density.
    # The shipped Slash is 233 px at 63.90857 PPU.
    [int]$TargetWidth = 233,

    # Anchor in source pixel coordinates, origin top left. Defaults to the middle of the
    # cropped box. This is what the effect gets positioned by, so put it where the effect
    # should meet whatever spawns it.
    [double]$AnchorX = -1,
    [double]$AnchorY = -1,

    # Identical or blank frames at the end of a render are a hold, not animation. Unity plays
    # them as extra time either way, so they are dropped and the caller can hold on the last
    # frame instead if it wants one.
    [switch]$KeepTrailingDuplicates,

    [ValidateSet("Keep","Steps","Hard")]
    [string]$AlphaMode = "Keep",
    [int]$AlphaSteps = 4,
    [int]$AlphaThreshold = 90,
    [int]$Padding = 2
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function Resolve-Here([string]$p) {
    if ([IO.Path]::IsPathRooted($p)) { return $p }
    return (Join-Path (Get-Location) $p)
}
$src = Resolve-Here $SourceFolder
$dst = Resolve-Here $OutputFolder
if (-not (Test-Path -LiteralPath $src)) { throw "source not found: $src" }

$files = @(Get-ChildItem -LiteralPath $src -Filter *.png -File | Sort-Object Name)
if ($files.Count -eq 0) { throw "no png in $src" }

# Trim the tail: blanks and repeats of the frame before.
if (-not $KeepTrailingDuplicates) {
    $hashes = @{}
    foreach ($f in $files) { $hashes[$f.FullName] = (Get-FileHash -LiteralPath $f.FullName -Algorithm MD5).Hash }
    $keep = New-Object System.Collections.Generic.List[object]
    foreach ($f in $files) {
        if ($keep.Count -gt 0 -and $hashes[$f.FullName] -eq $hashes[$keep[$keep.Count-1].FullName]) { continue }
        $keep.Add($f)
    }
    if ($keep.Count -ne $files.Count) {
        "dropped {0} repeated frame(s)" -f ($files.Count - $keep.Count)
    }
    # .ToArray() rather than @(...): wrapping a List[object] in the array operator throws
    # "Argument types do not match" on Windows PowerShell 5.1.
    $files = $keep.ToArray()
}

function Get-Box($path) {
    $bmp = New-Object System.Drawing.Bitmap($path)
    $w=$bmp.Width; $h=$bmp.Height
    $data = $bmp.LockBits((New-Object System.Drawing.Rectangle(0,0,$w,$h)),
            [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride=[int]$data.Stride; $row = [byte[]]::new($stride)
    $minX=[int]::MaxValue;$minY=[int]::MaxValue;$maxX=-1;$maxY=-1
    for($y=0;$y -lt $h;$y++){
        [System.Runtime.InteropServices.Marshal]::Copy([IntPtr]::Add($data.Scan0,[int]($y*$stride)),$row,0,$stride)
        for($x=0;$x -lt $w;$x++){
            if($row[$x*4+3] -gt 8){
                if($x -lt $minX){$minX=$x}; if($x -gt $maxX){$maxX=$x}
                if($y -lt $minY){$minY=$y}; if($y -gt $maxY){$maxY=$y}
            }
        }
    }
    $bmp.UnlockBits($data); $bmp.Dispose()
    if($maxX -lt 0){ return $null }
    return @{ MinX=$minX;MinY=$minY;MaxX=$maxX;MaxY=$maxY;W=$w;H=$h }
}

$minX=[int]::MaxValue;$minY=[int]::MaxValue;$maxX=-1;$maxY=-1
$live = New-Object System.Collections.Generic.List[object]
foreach($f in $files){
    $b = Get-Box $f.FullName
    if($null -eq $b){ continue }         # fully transparent frame
    $live.Add($f)
    if($b.MinX -lt $minX){$minX=$b.MinX}; if($b.MaxX -gt $maxX){$maxX=$b.MaxX}
    if($b.MinY -lt $minY){$minY=$b.MinY}; if($b.MaxY -gt $maxY){$maxY=$b.MaxY}
}
if($live.Count -ne $files.Count){ "dropped {0} blank frame(s)" -f ($files.Count - $live.Count) }
$files = $live.ToArray()
if($maxX -lt 0){ throw "every frame is transparent" }

$boxW = $maxX-$minX+1; $boxH = $maxY-$minY+1
$factor = $TargetWidth / [double]$boxW
$outW = [int][math]::Max(1,[math]::Round($boxW*$factor)) + $Padding*2
$outH = [int][math]::Max(1,[math]::Round($boxH*$factor)) + $Padding*2

if($AnchorX -lt 0){ $AnchorX = $minX + $boxW/2.0 }
if($AnchorY -lt 0){ $AnchorY = $minY + $boxH/2.0 }
$pivotX = ((($AnchorX-$minX)*$factor)+$Padding) / $outW
$pivotY = 1.0 - (((($AnchorY-$minY)*$factor)+$Padding) / $outH)

"{0} frames   crop {1}x{2} @ ({3},{4})   scale {5:N4}   out {6}x{7}" -f `
    $files.Count,$boxW,$boxH,$minX,$minY,$factor,$outW,$outH

if(Test-Path -LiteralPath $dst){ Get-ChildItem -LiteralPath $dst -Filter *.png | Remove-Item -Force }
else { New-Item -ItemType Directory -Path $dst -Force | Out-Null }

foreach($f in $files){
    $img = New-Object System.Drawing.Bitmap($f.FullName)
    $small = New-Object System.Drawing.Bitmap($outW,$outH)
    $g = [System.Drawing.Graphics]::FromImage($small)
    $g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $dest = New-Object System.Drawing.Rectangle($Padding,$Padding,($outW-$Padding*2),($outH-$Padding*2))
    $srcRect = New-Object System.Drawing.Rectangle($minX,$minY,$boxW,$boxH)
    $g.DrawImage($img,$dest,$srcRect,[System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose(); $img.Dispose()

    if($AlphaMode -ne "Keep"){
        $rect = New-Object System.Drawing.Rectangle(0,0,$outW,$outH)
        $data = $small.LockBits($rect,[System.Drawing.Imaging.ImageLockMode]::ReadWrite,
                                [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $buf = New-Object byte[] ($data.Stride*$outH)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0,$buf,0,$buf.Length)
        for($y=0;$y -lt $outH;$y++){
            $rowBase=$y*$data.Stride
            for($x=0;$x -lt $outW;$x++){
                $i=$rowBase+$x*4; $a=$buf[$i+3]
                if($AlphaMode -eq "Hard"){ if($a -lt $AlphaThreshold){$a=0}else{$a=255} }
                else {
                    if($a -lt 8){ $a=0 }
                    else {
                        $lv=[int][math]::Ceiling($a/255.0*$AlphaSteps)
                        if($lv -lt 1){$lv=1}
                        $a=[int][math]::Round(255.0*$lv/$AlphaSteps)
                    }
                }
                $buf[$i+3]=[byte]$a
                if($a -eq 0){ $buf[$i]=0;$buf[$i+1]=0;$buf[$i+2]=0 }
            }
        }
        [System.Runtime.InteropServices.Marshal]::Copy($buf,0,$data.Scan0,$buf.Length)
        $small.UnlockBits($data)
    }

    $small.Save((Join-Path $dst ($f.BaseName + ".png")),[System.Drawing.Imaging.ImageFormat]::Png)
    $small.Dispose()
}

"pivot ({0:N4}, {1:N4})   anchor source px ({2:N0},{3:N0})" -f $pivotX,$pivotY,$AnchorX,$AnchorY
"RGBA32: {0:N2} MB  ->  {1:N2} MB" -f `
    (($files.Count * (Get-Box $files[0].FullName).W * (Get-Box $files[0].FullName).H * 4)/1MB), `
    (($files.Count * $outW * $outH * 4)/1MB)
"out: $dst"
