<#
.SYNOPSIS
    Rebuilds the shared palette of an already processed frame set.

.DESCRIPTION
    The pixelize step picks colours by area, which on a mostly black character spends almost
    every slot on near identical blacks and drops small but defining accents. This rebuilds
    the palette instead:

      - accent slots go to saturated colours regardless of how little area they cover
      - the rest are spread evenly across the brightness range, so the ramp reads as steps

    Frames are remapped in place, so an existing folder layout survives.

.EXAMPLE
    .\repalette-frames.ps1 -Folder "..\ArtSource\Juve_96px" -Colors 32
#>
param(
    [Parameter(Mandatory = $true)][string]$Folder,
    [int]$Colors = 32,
    [int]$AccentSlots = 4,
    [double]$AccentMinSaturation = 0.25,
    [double]$AccentMinValue = 0.25,
    [int]$AccentMinPixels = 15
)

Add-Type -AssemblyName System.Drawing

if (-not (Test-Path -LiteralPath $Folder)) { Write-Error "폴더 없음: $Folder"; exit 1 }
$files = Get-ChildItem -LiteralPath $Folder -Filter *.png -File -Recurse |
         Where-Object { -not $_.Name.StartsWith("_") }
if ($files.Count -eq 0) { Write-Error "PNG 없음"; exit 1 }
"프레임 {0}장" -f $files.Count

function Get-Locked($bitmap, $mode) {
    $rect = New-Object System.Drawing.Rectangle(0, 0, $bitmap.Width, $bitmap.Height)
    $data = $bitmap.LockBits($rect, $mode, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $buffer = New-Object byte[] ($data.Stride * $bitmap.Height)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $buffer, 0, $buffer.Length)
    return @{ Data = $data; Buffer = $buffer; Stride = $data.Stride }
}

# Pass 1: histogram over every opaque pixel, bucketed so the map stays small.
$histogram = @{}
foreach ($file in $files) {
    $bmp = New-Object System.Drawing.Bitmap($file.FullName)
    $lock = Get-Locked $bmp ([System.Drawing.Imaging.ImageLockMode]::ReadOnly)
    $buffer = $lock.Buffer; $stride = $lock.Stride
    for ($y = 0; $y -lt $bmp.Height; $y++) {
        $rowBase = $y * $stride
        for ($x = 0; $x -lt $bmp.Width; $x++) {
            $i = $rowBase + $x * 4
            if ($buffer[$i + 3] -lt 128) { continue }
            $key = ([int]($buffer[$i + 2] -shr 3) -shl 10) -bor ([int]($buffer[$i + 1] -shr 3) -shl 5) -bor [int]($buffer[$i] -shr 3)
            if ($histogram.ContainsKey($key)) { $histogram[$key] = $histogram[$key] + 1 } else { $histogram[$key] = 1 }
        }
    }
    $bmp.UnlockBits($lock.Data)
    $bmp.Dispose()
}
"고유 색(버킷) {0}개" -f $histogram.Count

$entries = @()
foreach ($pair in $histogram.GetEnumerator()) {
    $k = [int]$pair.Key
    $r = (($k -shr 10) -band 31) -shl 3
    $g = (($k -shr 5) -band 31) -shl 3
    $b = ($k -band 31) -shl 3
    $mx = [Math]::Max($r, [Math]::Max($g, $b))
    $mn = [Math]::Min($r, [Math]::Min($g, $b))
    $sat = if ($mx -eq 0) { 0.0 } else { ($mx - $mn) / [double]$mx }
    $entries += , @{ R = $r; G = $g; B = $b; N = $pair.Value
                     Lum = (0.299 * $r + 0.587 * $g + 0.114 * $b)
                     Sat = $sat; Val = $mx / 255.0 }
}

# Accents: saturated and bright enough to be a real colour rather than a dark tint.
$accents = @($entries |
    Where-Object { $_.Sat -ge $AccentMinSaturation -and $_.Val -ge $AccentMinValue -and $_.N -ge $AccentMinPixels } |
    Sort-Object { -$_.N } | Select-Object -First $AccentSlots)
"액센트 {0}색: {1}" -f $accents.Count, (($accents | ForEach-Object { "#{0:X2}{1:X2}{2:X2}({3}px)" -f $_.R, $_.G, $_.B, $_.N }) -join " ")

# Ramp: split the brightness range into equal bands, take the busiest colour in each.
$rampSlots = $Colors - $accents.Count
$minLum = ($entries | Measure-Object Lum -Minimum).Minimum
$maxLum = ($entries | Measure-Object Lum -Maximum).Maximum
$span = [Math]::Max(1.0, $maxLum - $minLum)
"밝기 범위 {0:N0} ~ {1:N0}, 램프 {2}칸" -f $minLum, $maxLum, $rampSlots

$palette = @()
foreach ($a in $accents) { $palette += , @([int]$a.R, [int]$a.G, [int]$a.B) }

$used = @{}
for ($slot = 0; $slot -lt $rampSlots; $slot++) {
    $lo = $minLum + $span * $slot / $rampSlots
    $hi = $minLum + $span * ($slot + 1) / $rampSlots
    $band = $entries | Where-Object { $_.Lum -ge $lo -and $_.Lum -lt $hi -and -not $used.ContainsKey(($_.R -shl 16) -bor ($_.G -shl 8) -bor $_.B) }
    $pick = $band | Sort-Object { -$_.N } | Select-Object -First 1
    if ($pick) {
        $used[($pick.R -shl 16) -bor ($pick.G -shl 8) -bor $pick.B] = $true
        $palette += , @([int]$pick.R, [int]$pick.G, [int]$pick.B)
    }
}

# Empty bands leave gaps; fill them with the busiest colours not already taken.
$fallback = $entries | Sort-Object { -$_.N }
foreach ($e in $fallback) {
    if ($palette.Count -ge $Colors) { break }
    $key = ($e.R -shl 16) -bor ($e.G -shl 8) -bor $e.B
    if ($used.ContainsKey($key)) { continue }
    $used[$key] = $true
    $palette += , @([int]$e.R, [int]$e.G, [int]$e.B)
}
"최종 팔레트 {0}색" -f $palette.Count

# Pass 2: remap every frame.
$cache = @{}
foreach ($file in $files) {
    $bmp = New-Object System.Drawing.Bitmap($file.FullName)
    $lock = Get-Locked $bmp ([System.Drawing.Imaging.ImageLockMode]::ReadWrite)
    $buffer = $lock.Buffer; $stride = $lock.Stride
    for ($y = 0; $y -lt $bmp.Height; $y++) {
        $rowBase = $y * $stride
        for ($x = 0; $x -lt $bmp.Width; $x++) {
            $i = $rowBase + $x * 4
            if ($buffer[$i + 3] -lt 128) { continue }
            $r = $buffer[$i + 2]; $g = $buffer[$i + 1]; $b = $buffer[$i]
            $key = ($r -shl 16) -bor ($g -shl 8) -bor $b
            if (-not $cache.ContainsKey($key)) {
                $best = 0; $bestDist = [int]::MaxValue
                for ($p = 0; $p -lt $palette.Count; $p++) {
                    $dr = $r - $palette[$p][0]; $dg = $g - $palette[$p][1]; $db = $b - $palette[$p][2]
                    $d = ($dr * $dr * 3) + ($dg * $dg * 6) + ($db * $db)
                    if ($d -lt $bestDist) { $bestDist = $d; $best = $p }
                }
                $cache[$key] = $best
            }
            $sel = $palette[$cache[$key]]
            $buffer[$i + 2] = [byte]$sel[0]; $buffer[$i + 1] = [byte]$sel[1]; $buffer[$i] = [byte]$sel[2]
        }
    }
    [System.Runtime.InteropServices.Marshal]::Copy($buffer, 0, $lock.Data.Scan0, $buffer.Length)
    $bmp.UnlockBits($lock.Data)
    $bmp.Save($file.FullName, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

# Palette strip and a GIMP palette file for Aseprite, Krita and GIMP.
$swatch = New-Object System.Drawing.Bitmap(($palette.Count * 16), 16)
$swg = [System.Drawing.Graphics]::FromImage($swatch)
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("GIMP Palette"); $lines.Add("Name: Juve " + $Colors); $lines.Add("Columns: 8"); $lines.Add("#")
for ($i = 0; $i -lt $palette.Count; $i++) {
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, $palette[$i][0], $palette[$i][1], $palette[$i][2]))
    $swg.FillRectangle($brush, ($i * 16), 0, 16, 16)
    $brush.Dispose()
    $lines.Add(("{0,3} {1,3} {2,3}`tcolor {3}" -f $palette[$i][0], $palette[$i][1], $palette[$i][2], ($i + 1)))
}
$swg.Dispose()
$swatch.Save((Join-Path $Folder "_palette.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$swatch.Dispose()
[System.IO.File]::WriteAllLines((Join-Path $Folder "_palette.gpl"), $lines)

"완료 -> {0}" -f $Folder
