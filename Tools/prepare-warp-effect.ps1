<#
.SYNOPSIS
    Splits the warp effect render into a departure clip and an arrival clip, and pixelises
    both to the game's density.

.DESCRIPTION
    The source is one 1080x1080 sequence showing a whole left to right warp: a portal opens
    on the left, a streak crosses, debris settles on the right. The player warps in eight
    directions, so a baked in direction cannot be used as is. Splitting the sequence at the
    two ends gives two clips that can be dropped at whatever origin and destination the warp
    actually had, and the crossing frames, which are the only direction specific part, are
    discarded.

    Each half is cropped to its own content, but both share one scale and one palette so the
    two clips look like the same effect. Anchors are reported as pivot fractions: set those
    on the sprites and the clips line up with the warp endpoints.

.EXAMPLE
    ./Tools/prepare-warp-effect.ps1
    ./Tools/prepare-warp-effect.ps1 -RingHeight 120 -Colors 24
#>
param(
    [string]$SourceFolder = "ArtSource/Vfx/Warp_effect",
    [string]$OutputRoot   = "Assets/Art/Pixel/Vfx",

    # Frames belonging to each end. The rest, the crossing and the trailing blanks, are dropped.
    [int]$DepartFirst = 0,  [int]$DepartLast = 4,
    [int]$ArriveFirst = 7,  [int]$ArriveLast = 20,

    # How tall the portal ring should end up, in pixels. The character is about 128 px for
    # roughly two world units, so matching that keeps one art pixel to one screen pixel.
    # Was 98 back when the character was 96 px; both scaled by 4/3 together.
    [int]$RingHeight = 131,

    # 0 leaves the colours alone. Any other value quantises every frame to one shared palette.
    [int]$Colors = 0,

    # How the soft edges are treated.
    #   Keep  - leave the gradient alone. Closest to the render, reads as a low resolution
    #           effect rather than as pixel art.
    #   Steps - flatten the gradient to -AlphaSteps levels. Keeps the glow but makes the
    #           banding deliberate.
    #   Hard  - one cut at -AlphaThreshold. Proper pixel art, but this effect is mostly soft
    #           glow, so it turns into flat blobs.
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
$outRoot = Resolve-Here $OutputRoot
if (-not (Test-Path -LiteralPath $src)) { throw "source not found: $src" }

function Get-Bytes($bitmap) {
    $rect = New-Object System.Drawing.Rectangle(0,0,$bitmap.Width,$bitmap.Height)
    $data = $bitmap.LockBits($rect,[System.Drawing.Imaging.ImageLockMode]::ReadOnly,
                             [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $buf = New-Object byte[] ($data.Stride * $bitmap.Height)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0,$buf,0,$buf.Length)
    $bitmap.UnlockBits($data)
    return @{ Bytes = $buf; Stride = $data.Stride; W = $bitmap.Width; H = $bitmap.Height }
}

# Bounding box plus alpha weighted centroid. The centroid is what the clip gets anchored on:
# for the arrival it lands in the middle of the debris, which is where the player appears.
function Measure-Frame($path) {
    $bmp = New-Object System.Drawing.Bitmap($path)
    $l = Get-Bytes $bmp
    $bmp.Dispose()
    $minX=[int]::MaxValue; $minY=[int]::MaxValue; $maxX=-1; $maxY=-1
    [double]$sx=0; [double]$sy=0; [double]$sw=0
    for($y=0;$y -lt $l.H;$y++){
        $rowBase = $y * $l.Stride
        for($x=0;$x -lt $l.W;$x++){
            $a = $l.Bytes[$rowBase + $x*4 + 3]
            if($a -le 8){ continue }
            if($x -lt $minX){$minX=$x}; if($x -gt $maxX){$maxX=$x}
            if($y -lt $minY){$minY=$y}; if($y -gt $maxY){$maxY=$y}
            $sx += $x * $a; $sy += $y * $a; $sw += $a
        }
    }
    if($maxX -lt 0){ return $null }
    return @{ MinX=$minX; MinY=$minY; MaxX=$maxX; MaxY=$maxY
              Cx=($sx/$sw); Cy=($sy/$sw) }
}

$all = Get-ChildItem -LiteralPath $src -Filter *.png -File | Sort-Object Name
if ($all.Count -eq 0) { throw "no png in $src" }

function Pick($first,$last) {
    $picked = @()
    foreach($f in $all){
        if($f.BaseName -match '(\d+)$'){
            $n = [int]$Matches[1]
            if($n -ge $first -and $n -le $last){ $picked += $f }
        }
    }
    return $picked
}

$sets = @(
    @{ Name = "Warp_Depart"; Files = (Pick $DepartFirst $DepartLast) },
    @{ Name = "Warp_Arrive"; Files = (Pick $ArriveFirst $ArriveLast) }
)

# Pass 1: per set bounding box, and the anchor for each.
$ringHeightSrc = 0
foreach($s in $sets){
    if($s.Files.Count -eq 0){ throw "no frames selected for $($s.Name)" }
    $minX=[int]::MaxValue; $minY=[int]::MaxValue; $maxX=-1; $maxY=-1
    $measures = @()
    foreach($f in $s.Files){
        $m = Measure-Frame $f.FullName
        if($null -eq $m){ continue }
        $measures += $m
        if($m.MinX -lt $minX){$minX=$m.MinX}; if($m.MaxX -gt $maxX){$maxX=$m.MaxX}
        if($m.MinY -lt $minY){$minY=$m.MinY}; if($m.MaxY -gt $maxY){$maxY=$m.MaxY}
    }
    $s.MinX=$minX; $s.MinY=$minY; $s.MaxX=$maxX; $s.MaxY=$maxY
    $s.BoxW = $maxX-$minX+1; $s.BoxH = $maxY-$minY+1

    # Departure anchors on the ring, which is the first frame and the tightest. Arrival
    # anchors on where the debris settles, so the later frames carry the most weight.
    if($s.Name -eq "Warp_Depart"){
        $first = $measures[0]
        $s.AnchorX = ($first.MinX + $first.MaxX) / 2.0
        $s.AnchorY = ($first.MinY + $first.MaxY) / 2.0
        $ringHeightSrc = $first.MaxY - $first.MinY + 1
    } else {
        $tail = @($measures | Select-Object -Last ([math]::Max(1,[int]($measures.Count/2))))
        [double]$ax = 0; [double]$ay = 0
        foreach($m in $tail){ $ax += $m.Cx; $ay += $m.Cy }
        $s.AnchorX = $ax / $tail.Count
        $s.AnchorY = $ay / $tail.Count
    }
    "{0,-14} {1,2} frames   box {2}x{3} @ ({4},{5})   anchor ({6:N1},{7:N1})" -f `
        $s.Name,$s.Files.Count,$s.BoxW,$s.BoxH,$s.MinX,$s.MinY,$s.AnchorX,$s.AnchorY
}

if($ringHeightSrc -le 0){ throw "could not measure the portal ring" }
$factor = $RingHeight / [double]$ringHeightSrc
"portal ring {0}px -> {1}px  (scale {2:N4}), shared by both clips" -f $ringHeightSrc,$RingHeight,$factor
""

# Pass 2: crop to each set's own box and downscale by the shared factor.
$reduced = @()
foreach($s in $sets){
    $s.OutW = [int][math]::Max(1,[math]::Round($s.BoxW * $factor)) + $Padding*2
    $s.OutH = [int][math]::Max(1,[math]::Round($s.BoxH * $factor)) + $Padding*2
    $s.PivotX = ((($s.AnchorX - $s.MinX) * $factor) + $Padding) / $s.OutW
    # Sprite pivots count up from the bottom, image rows count down from the top.
    $s.PivotY = 1.0 - (((($s.AnchorY - $s.MinY) * $factor) + $Padding) / $s.OutH)

    $dir = Join-Path $outRoot $s.Name
    if(Test-Path -LiteralPath $dir){ Get-ChildItem -LiteralPath $dir -Filter *.png | Remove-Item -Force }
    else { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $s.Dir = $dir

    foreach($f in $s.Files){
        $img = New-Object System.Drawing.Bitmap($f.FullName)
        $small = New-Object System.Drawing.Bitmap($s.OutW,$s.OutH)
        $g = [System.Drawing.Graphics]::FromImage($small)
        $g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $dest = New-Object System.Drawing.Rectangle($Padding,$Padding,($s.OutW-$Padding*2),($s.OutH-$Padding*2))
        $srcRect = New-Object System.Drawing.Rectangle($s.MinX,$s.MinY,$s.BoxW,$s.BoxH)
        $g.DrawImage($img,$dest,$srcRect,[System.Drawing.GraphicsUnit]::Pixel)
        $g.Dispose(); $img.Dispose()
        $reduced += ,@{ Name=$f.BaseName; Bitmap=$small; Dir=$dir; W=$s.OutW; H=$s.OutH }
    }
}

# Pass 3: hard alpha and one palette across both clips, so they match.
$histogram = @{}
$frames = @()
foreach($e in $reduced){
    $bmp = $e.Bitmap
    $rect = New-Object System.Drawing.Rectangle(0,0,$e.W,$e.H)
    $data = $bmp.LockBits($rect,[System.Drawing.Imaging.ImageLockMode]::ReadWrite,
                          [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $buf = New-Object byte[] ($data.Stride * $e.H)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0,$buf,0,$buf.Length)
    for($y=0;$y -lt $e.H;$y++){
        $rowBase = $y * $data.Stride
        for($x=0;$x -lt $e.W;$x++){
            $i = $rowBase + $x*4
            $a = $buf[$i+3]

            switch($AlphaMode){
                "Hard" {
                    if($a -lt $AlphaThreshold){ $a = 0 } else { $a = 255 }
                }
                "Steps" {
                    if($a -lt 8){ $a = 0 }
                    else {
                        $level = [int][math]::Ceiling($a / 255.0 * $AlphaSteps)
                        if($level -lt 1){ $level = 1 }
                        $a = [int][math]::Round(255.0 * $level / $AlphaSteps)
                    }
                }
            }
            $buf[$i+3] = [byte]$a

            if($a -eq 0){ $buf[$i]=0;$buf[$i+1]=0;$buf[$i+2]=0 }
            elseif($Colors -gt 0){
                $key = ([int]($buf[$i+2] -shr 3) -shl 10) -bor ([int]($buf[$i+1] -shr 3) -shl 5) -bor [int]($buf[$i] -shr 3)
                if($histogram.ContainsKey($key)){ $histogram[$key]++ } else { $histogram[$key]=1 }
            }
        }
    }
    $frames += ,@{ Name=$e.Name; Dir=$e.Dir; Bitmap=$bmp; Data=$data; Buffer=$buf; Stride=$data.Stride; W=$e.W; H=$e.H }
}

$palette = @()
if($Colors -le 0){
    "palette: left alone ({0} alpha)" -f $AlphaMode
}
else {

# Most of the effect is near white glow, so picking purely by area would spend every slot on
# whites and lose the purple ring. Spread the slots across brightness instead.
$entries = @()
foreach($pair in $histogram.GetEnumerator()){
    $k=[int]$pair.Key
    $r=(($k -shr 10) -band 31) -shl 3; $g=(($k -shr 5) -band 31) -shl 3; $b=($k -band 31) -shl 3
    $entries += ,@{ R=$r;G=$g;B=$b;N=[int]$pair.Value; Lum=(0.299*$r+0.587*$g+0.114*$b) }
}
$minL=[double]::MaxValue; $maxL=0.0
foreach($e in $entries){ if($e.Lum -lt $minL){$minL=$e.Lum}; if($e.Lum -gt $maxL){$maxL=$e.Lum} }
$span=[math]::Max(1.0,$maxL-$minL)
$palette=@(); $used=@{}
for($slot=0;$slot -lt $Colors;$slot++){
    $lo=$minL+$span*$slot/$Colors; $hi=$minL+$span*($slot+1)/$Colors
    $pick=$null;$pickN=0
    foreach($e in $entries){
        if($e.Lum -lt $lo -or $e.Lum -ge $hi){ continue }
        $key=($e.R -shl 16) -bor ($e.G -shl 8) -bor $e.B
        if($used.ContainsKey($key)){ continue }
        if($e.N -gt $pickN){ $pick=$e;$pickN=$e.N }
    }
    if($null -ne $pick){ $palette += ,@($pick.R,$pick.G,$pick.B); $used[($pick.R -shl 16) -bor ($pick.G -shl 8) -bor $pick.B]=$true }
}
foreach($e in ($entries | Sort-Object { -$_.N })){
    if($palette.Count -ge $Colors){ break }
    $key=($e.R -shl 16) -bor ($e.G -shl 8) -bor $e.B
    if($used.ContainsKey($key)){ continue }
    $palette += ,@($e.R,$e.G,$e.B); $used[$key]=$true
}
"palette {0} colours from {1} buckets" -f $palette.Count,$histogram.Count
}

$cache=@{}
foreach($f in $frames){
    $buf=$f.Buffer
    for($y=0;$y -lt $f.H;$y++){
        $rowBase=$y*$f.Stride
        for($x=0;$x -lt $f.W;$x++){
            $i=$rowBase+$x*4
            if($buf[$i+3] -eq 0){ continue }
            if($palette.Count -eq 0){ continue }
            $r=$buf[$i+2];$g=$buf[$i+1];$b=$buf[$i]
            $key=($r -shl 16) -bor ($g -shl 8) -bor $b
            if(-not $cache.ContainsKey($key)){
                $best=0;$bd=[int]::MaxValue
                for($p=0;$p -lt $palette.Count;$p++){
                    $dr=$r-$palette[$p][0];$dg=$g-$palette[$p][1];$db=$b-$palette[$p][2]
                    $d=$dr*$dr+$dg*$dg+$db*$db
                    if($d -lt $bd){$bd=$d;$best=$p}
                }
                $cache[$key]=$best
            }
            $sel=$palette[$cache[$key]]
            $buf[$i+2]=[byte]$sel[0];$buf[$i+1]=[byte]$sel[1];$buf[$i]=[byte]$sel[2]
        }
    }
    [System.Runtime.InteropServices.Marshal]::Copy($buf,0,$f.Data.Scan0,$buf.Length)
    $f.Bitmap.UnlockBits($f.Data)
    $f.Bitmap.Save((Join-Path $f.Dir ($f.Name + ".png")),[System.Drawing.Imaging.ImageFormat]::Png)
    $f.Bitmap.Dispose()
}

""
foreach($s in $sets){
    "{0,-14} -> {1}   {2} frames, {3}x{4} px, pivot ({5:N4}, {6:N4})" -f `
        $s.Name,$s.Dir,$s.Files.Count,$s.OutW,$s.OutH,$s.PivotX,$s.PivotY
}
$bytes = 0
foreach($s in $sets){ $bytes += $s.Files.Count * $s.OutW * $s.OutH * 4 }
"RGBA32 total: {0:N2} MB   (source sequence was 120.1 MB)" -f ($bytes/1MB)
