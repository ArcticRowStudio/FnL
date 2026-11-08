<#
.SYNOPSIS
    Prepares high resolution frames for a hybrid pixel art pipeline.

.DESCRIPTION
    Downscales a frame sequence to the target character height, cuts the semi transparent
    edge pixels that a plain resize leaves behind, and quantises every frame to one shared
    palette so colours do not shimmer from frame to frame. Also trims the canvas to the
    character, using one box shared by every frame so the animation stays aligned.

    The output is a starting point for hand cleanup, not finished art. What it removes is
    the tedious part: the muddy edges and the thousands of near duplicate colours.

    All per pixel work goes through LockBits, so full HD sequences finish in minutes
    rather than hours.

.EXAMPLE
    Regenerates the whole character at the shipped size. The source frames live outside the
    project at C:\Users\mokos\Desktop\FNL\Sprite and are named per animation, so stage them
    into one folder per animation first, matching the names under ArtSource\Juve_128px.

    Note that the Standing render is mirrored relative to every other animation, so the Idle
    output has to be flipped afterwards and its pivot re-solved.

    .\pixelize-frames.ps1 -SourceFolder "..\ArtSource\Juve_src" -TargetHeight 128 `
        -Colors 32 -FrameStep 1 -Recurse -ScaleFrom Idle
#>
param(
    [Parameter(Mandatory = $true)][string]$SourceFolder,
    [string]$OutputFolder,
    [int]$TargetHeight = 96,
    [int]$Colors = 32,
    [int]$AlphaThreshold = 128,
    [int]$FrameStep = 1,
    [int]$Padding = 2,
    [switch]$Recurse,
    [string]$ScaleFrom,
    [int]$AccentSlots = 4,
    # Accents are small by nature, so they are judged by how colourful they are rather than
    # by area. The brightness floor keeps dark blue tinted blacks out of these slots.
    [double]$AccentMinSaturation = 0.35,
    [double]$AccentMinValue = 0.35,
    [int]$AccentMinPixels = 4
)

Add-Type -AssemblyName System.Drawing

if (-not (Test-Path -LiteralPath $SourceFolder)) { Write-Error "폴더 없음: $SourceFolder"; exit 1 }
if (-not $OutputFolder) { $OutputFolder = Join-Path $SourceFolder ("_pixelized_{0}px" -f $TargetHeight) }
if (-not (Test-Path -LiteralPath $OutputFolder)) { New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null }

# With -Recurse every subfolder is a separate animation, but they all share one bounding box,
# one scale and one palette. Without that the character jumps when animations switch.
$animations = @()
if ($Recurse) {
    foreach ($dir in (Get-ChildItem -LiteralPath $SourceFolder -Directory | Sort-Object Name)) {
        if ($dir.Name.StartsWith("_")) { continue }
        $pngs = Get-ChildItem -LiteralPath $dir.FullName -Filter *.png -File | Sort-Object Name
        if ($pngs.Count -gt 0) { $animations += , @{ Name = $dir.Name; Files = $pngs } }
    }
}
else {
    $pngs = Get-ChildItem -LiteralPath $SourceFolder -Filter *.png -File | Sort-Object Name
    if ($pngs.Count -gt 0) { $animations += , @{ Name = (Split-Path $SourceFolder -Leaf); Files = $pngs } }
}
if ($animations.Count -eq 0) { Write-Error "PNG 없음: $SourceFolder"; exit 1 }

$frames = @()
foreach ($anim in $animations) {
    $picked = @()
    for ($i = 0; $i -lt $anim.Files.Count; $i += $FrameStep) { $picked += $anim.Files[$i] }
    $anim.Picked = $picked
    $frames += $picked
    "{0,-20} {1,4}개 -> {2}개" -f $anim.Name, $anim.Files.Count, $picked.Count
}

function Get-Bytes {
    param($bitmap)
    $rect = New-Object System.Drawing.Rectangle(0, 0, $bitmap.Width, $bitmap.Height)
    $data = $bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $buffer = New-Object byte[] ($data.Stride * $bitmap.Height)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $buffer, 0, $buffer.Length)
    $bitmap.UnlockBits($data)
    return @{ Bytes = $buffer; Stride = $data.Stride }
}

# Pass 1: bounding boxes, per animation and combined.
# The scale comes from the tightest animation, which is the one showing the character with
# no effects around it. The canvas comes from the combined box, so full screen effect frames
# still fit. Deriving both from the combined box would shrink the character to fit the
# effects, which is not what the target height means.
$step = 4
$minX = [int]::MaxValue; $minY = [int]::MaxValue; $maxX = 0; $maxY = 0
$srcW = 0; $srcH = 0
$characterHeight = [int]::MaxValue

foreach ($anim in $animations) {
    $aMinX = [int]::MaxValue; $aMinY = [int]::MaxValue; $aMaxX = 0; $aMaxY = 0
    foreach ($file in $anim.Picked) {
        $img = New-Object System.Drawing.Bitmap($file.FullName)
        if ($srcW -eq 0) { $srcW = $img.Width; $srcH = $img.Height }
        $locked = Get-Bytes $img
        $bytes = $locked.Bytes; $stride = $locked.Stride
        for ($y = 0; $y -lt $srcH; $y += $step) {
            $rowBase = $y * $stride
            for ($x = 0; $x -lt $srcW; $x += $step) {
                if ($bytes[$rowBase + $x * 4 + 3] -gt 16) {
                    if ($x -lt $aMinX) { $aMinX = $x }
                    if ($x -gt $aMaxX) { $aMaxX = $x }
                    if ($y -lt $aMinY) { $aMinY = $y }
                    if ($y -gt $aMaxY) { $aMaxY = $y }
                }
            }
        }
        $img.Dispose()
    }
    $aHeight = $aMaxY - $aMinY + 1
    "{0,-20} box {1}x{2}" -f $anim.Name, ($aMaxX - $aMinX + 1), $aHeight
    $anim.Height = $aHeight
    # Without an explicit reference the tightest animation wins, but a leaning run pose is
    # genuinely shorter than a standing one, so name the idle animation with -ScaleFrom to
    # make the target height mean what you expect.
    if (-not $ScaleFrom -and $aHeight -gt 0 -and $aHeight -lt $characterHeight) { $characterHeight = $aHeight }
    if ($aMinX -lt $minX) { $minX = $aMinX }
    if ($aMaxX -gt $maxX) { $maxX = $aMaxX }
    if ($aMinY -lt $minY) { $minY = $aMinY }
    if ($aMaxY -gt $maxY) { $maxY = $aMaxY }
}

if ($ScaleFrom) {
    $ref = $animations | Where-Object { $_.Name -eq $ScaleFrom } | Select-Object -First 1
    if (-not $ref) { Write-Error "-ScaleFrom '$ScaleFrom' 을(를) 찾을 수 없음"; exit 1 }
    $characterHeight = $ref.Height
    "배율 기준: {0} ({1}px)" -f $ScaleFrom, $characterHeight
}

$minX = [Math]::Max(0, $minX - $step); $minY = [Math]::Max(0, $minY - $step)
$maxX = [Math]::Min($srcW - 1, $maxX + $step); $maxY = [Math]::Min($srcH - 1, $maxY + $step)
$boxW = $maxX - $minX + 1
$boxH = $maxY - $minY + 1
if ($boxH -le 0 -or $characterHeight -eq [int]::MaxValue) { Write-Error "알파가 있는 픽셀을 못 찾음"; exit 1 }

$factor = $TargetHeight / $characterHeight
$outW = [int][Math]::Max(1, [Math]::Round($boxW * $factor)) + ($Padding * 2)
$outH = [int][Math]::Max(1, [Math]::Round($boxH * $factor)) + ($Padding * 2)
"캐릭터 {0}px -> {1}px (축소 {2:N4}배),  캔버스 박스 {3}x{4} @ ({5},{6}) -> 출력 {7}x{8}" -f $characterHeight, $TargetHeight, $factor, $boxW, $boxH, $minX, $minY, $outW, $outH

# Pass 2: crop to the shared box, downscale, hard cut the alpha.
$reduced = @()
foreach ($anim in $animations) {
    $animOut = if ($animations.Count -gt 1) { Join-Path $OutputFolder $anim.Name } else { $OutputFolder }
    if (-not (Test-Path -LiteralPath $animOut)) { New-Item -ItemType Directory -Path $animOut -Force | Out-Null }

    foreach ($file in $anim.Picked) {
        $img = New-Object System.Drawing.Bitmap($file.FullName)
        $small = New-Object System.Drawing.Bitmap($outW, $outH)
        $sgr = [System.Drawing.Graphics]::FromImage($small)
        $sgr.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $sgr.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $dest = New-Object System.Drawing.Rectangle($Padding, $Padding, ($outW - $Padding * 2), ($outH - $Padding * 2))
        $srcRect = New-Object System.Drawing.Rectangle($minX, $minY, $boxW, $boxH)
        $sgr.DrawImage($img, $dest, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
        $sgr.Dispose(); $img.Dispose()
        $reduced += , @{ Name = $file.BaseName; Bitmap = $small; Folder = $animOut }
    }
}

# Pass 3: hard alpha, shared palette, remap. All through LockBits.
$histogram = @{}
$frameData = @()
foreach ($entry in $reduced) {
    $bmp = $entry.Bitmap
    $rect = New-Object System.Drawing.Rectangle(0, 0, $outW, $outH)
    $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadWrite, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride = $data.Stride
    $buffer = New-Object byte[] ($stride * $outH)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $buffer, 0, $buffer.Length)

    for ($y = 0; $y -lt $outH; $y++) {
        $rowBase = $y * $stride
        for ($x = 0; $x -lt $outW; $x++) {
            $i = $rowBase + $x * 4
            if ($buffer[$i + 3] -lt $AlphaThreshold) {
                $buffer[$i] = 0; $buffer[$i + 1] = 0; $buffer[$i + 2] = 0; $buffer[$i + 3] = 0
            }
            else {
                $buffer[$i + 3] = 255
                $key = ([int]($buffer[$i + 2] -shr 3) -shl 10) -bor ([int]($buffer[$i + 1] -shr 3) -shl 5) -bor [int]($buffer[$i] -shr 3)
                if ($histogram.ContainsKey($key)) { $histogram[$key] = $histogram[$key] + 1 } else { $histogram[$key] = 1 }
            }
        }
    }
    $frameData += , @{ Name = $entry.Name; Folder = $entry.Folder; Bitmap = $bmp; Data = $data; Buffer = $buffer; Stride = $stride }
}

# Picking colours purely by area spends nearly every slot on the near identical blacks of a
# dark character and drops small accents entirely. Instead: reserve a few slots for
# saturated colours whatever their area, then spread the rest evenly across brightness so
# the ramp reads as distinct steps.
$entries = @()
foreach ($pair in $histogram.GetEnumerator()) {
    $k = [int]$pair.Key
    $r = (($k -shr 10) -band 31) -shl 3
    $g = (($k -shr 5) -band 31) -shl 3
    $b = ($k -band 31) -shl 3
    $mx = [Math]::Max($r, [Math]::Max($g, $b))
    $mn = [Math]::Min($r, [Math]::Min($g, $b))
    $sat = if ($mx -eq 0) { 0.0 } else { ($mx - $mn) / [double]$mx }
    $entries += , @{ R = $r; G = $g; B = $b; N = [int]$pair.Value
                     Lum = (0.299 * $r + 0.587 * $g + 0.114 * $b)
                     Sat = $sat; Val = $mx / 255.0 }
}

$accents = @($entries |
    Where-Object { $_.Sat -ge $AccentMinSaturation -and $_.Val -ge $AccentMinValue -and $_.N -ge $AccentMinPixels } |
    Sort-Object { -($_.Sat * [Math]::Sqrt($_.N)) } | Select-Object -First $AccentSlots)

$palette = @()
$used = @{}
foreach ($a in $accents) {
    $palette += , @([int]$a.R, [int]$a.G, [int]$a.B)
    $used[($a.R -shl 16) -bor ($a.G -shl 8) -bor $a.B] = $true
}

$minLum = [double]::MaxValue; $maxLum = 0.0
foreach ($e in $entries) {
    if ($e.Lum -lt $minLum) { $minLum = $e.Lum }
    if ($e.Lum -gt $maxLum) { $maxLum = $e.Lum }
}
$span = [Math]::Max(1.0, $maxLum - $minLum)
$rampSlots = $Colors - $palette.Count

for ($slot = 0; $slot -lt $rampSlots; $slot++) {
    $lo = $minLum + $span * $slot / $rampSlots
    $hi = $minLum + $span * ($slot + 1) / $rampSlots
    $pick = $null; $pickN = 0
    foreach ($e in $entries) {
        if ($e.Lum -lt $lo -or $e.Lum -ge $hi) { continue }
        if ($used.ContainsKey(($e.R -shl 16) -bor ($e.G -shl 8) -bor $e.B)) { continue }
        if ($e.N -gt $pickN) { $pick = $e; $pickN = $e.N }
    }
    if ($pick -ne $null) {
        $palette += , @([int]$pick.R, [int]$pick.G, [int]$pick.B)
        $used[($pick.R -shl 16) -bor ($pick.G -shl 8) -bor $pick.B] = $true
    }
}

# Empty brightness bands leave gaps, so top up with the busiest colours still unused.
foreach ($e in ($entries | Sort-Object { -$_.N })) {
    if ($palette.Count -ge $Colors) { break }
    $key = ($e.R -shl 16) -bor ($e.G -shl 8) -bor $e.B
    if ($used.ContainsKey($key)) { continue }
    $palette += , @([int]$e.R, [int]$e.G, [int]$e.B)
    $used[$key] = $true
}

"팔레트 {0}색 (원본 {1}색 버킷, 액센트 {2}색, 밝기 {3:N0}~{4:N0})" -f $palette.Count, $histogram.Count, $accents.Count, $minLum, $maxLum
if ($accents.Count -gt 0) {
    "  액센트: " + (($accents | ForEach-Object { "#{0:X2}{1:X2}{2:X2}({3}px)" -f $_.R, $_.G, $_.B, $_.N }) -join " ")
}

$cache = @{}
foreach ($f in $frameData) {
    $buffer = $f.Buffer; $stride = $f.Stride
    for ($y = 0; $y -lt $outH; $y++) {
        $rowBase = $y * $stride
        for ($x = 0; $x -lt $outW; $x++) {
            $i = $rowBase + $x * 4
            if ($buffer[$i + 3] -eq 0) { continue }
            $r = $buffer[$i + 2]; $g = $buffer[$i + 1]; $b = $buffer[$i]
            $key = ($r -shl 16) -bor ($g -shl 8) -bor $b
            if (-not $cache.ContainsKey($key)) {
                $bestIndex = 0; $bestDist = [int]::MaxValue
                for ($p = 0; $p -lt $palette.Count; $p++) {
                    $dr = $r - $palette[$p][0]; $dg = $g - $palette[$p][1]; $db = $b - $palette[$p][2]
                    $dist = ($dr * $dr) + ($dg * $dg) + ($db * $db)
                    if ($dist -lt $bestDist) { $bestDist = $dist; $bestIndex = $p }
                }
                $cache[$key] = $bestIndex
            }
            $sel = $palette[$cache[$key]]
            $buffer[$i + 2] = [byte]$sel[0]; $buffer[$i + 1] = [byte]$sel[1]; $buffer[$i] = [byte]$sel[2]
        }
    }
    [System.Runtime.InteropServices.Marshal]::Copy($buffer, 0, $f.Data.Scan0, $buffer.Length)
    $f.Bitmap.UnlockBits($f.Data)
    $f.Bitmap.Save((Join-Path $f.Folder ($f.Name + ".png")), [System.Drawing.Imaging.ImageFormat]::Png)
    $f.Bitmap.Dispose()
}

$swatch = New-Object System.Drawing.Bitmap(($palette.Count * 16), 16)
$swg = [System.Drawing.Graphics]::FromImage($swatch)
for ($i = 0; $i -lt $palette.Count; $i++) {
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, $palette[$i][0], $palette[$i][1], $palette[$i][2]))
    $swg.FillRectangle($brush, ($i * 16), 0, 16, 16)
    $brush.Dispose()
}
$swg.Dispose()
$swatch.Save((Join-Path $OutputFolder "_palette.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$swatch.Dispose()

"완료 -> {0}" -f $OutputFolder
