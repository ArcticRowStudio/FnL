<#
.SYNOPSIS
    Builds a tracing reference sheet from an exported frame sequence.

.DESCRIPTION
    Takes a folder of per-frame PNGs (as exported from Unreal), picks evenly spaced key
    frames, and lays each one out as: the original, a nearest-neighbour reduction to the
    target pixel size, and a silhouette of that reduction. The reduction is a readability
    check and a tracing guide, not finished art - the pixels still get drawn by hand.

.EXAMPLE
    .\make-pixel-reference.ps1 -SourceFolder "C:\Export\Juve\walking" -FrameCount 8
#>
param(
    [Parameter(Mandatory = $true)][string]$SourceFolder,
    [string]$OutputFile,
    [int]$FrameCount = 6,
    [int]$TargetSize = 32,
    [int]$ZoomFactor = 4
)

Add-Type -AssemblyName System.Drawing

if (-not (Test-Path -LiteralPath $SourceFolder)) {
    Write-Error "폴더를 찾을 수 없습니다: $SourceFolder"
    exit 1
}

$frames = Get-ChildItem -LiteralPath $SourceFolder -Filter *.png -File | Sort-Object Name
if ($frames.Count -eq 0) {
    Write-Error "PNG가 없습니다: $SourceFolder"
    exit 1
}

if (-not $OutputFile) {
    $OutputFile = Join-Path $SourceFolder ("_reference_{0}px.png" -f $TargetSize)
}

$picked = @()
$take = [Math]::Min($FrameCount, $frames.Count)
for ($i = 0; $i -lt $take; $i++) {
    $index = if ($take -eq 1) { 0 } else { [Math]::Round($i * ($frames.Count - 1) / ($take - 1)) }
    $picked += $frames[$index]
}

$cell = $TargetSize * $ZoomFactor
$gap = 12
$labelWidth = 54
$sheetWidth = $labelWidth + ($cell * 3) + ($gap * 4)
$sheetHeight = ($cell * $take) + ($gap * ($take + 1))

$sheet = New-Object System.Drawing.Bitmap($sheetWidth, $sheetHeight)
$g = [System.Drawing.Graphics]::FromImage($sheet)
$g.Clear([System.Drawing.Color]::FromArgb(255, 32, 32, 36))
$font = New-Object System.Drawing.Font("Consolas", 11)
$brush = [System.Drawing.Brushes]::White

for ($row = 0; $row -lt $take; $row++) {
    $file = $picked[$row]
    $source = [System.Drawing.Image]::FromFile($file.FullName)

    $y = $gap + $row * ($cell + $gap)
    $g.DrawString($file.BaseName.Substring([Math]::Max(0, $file.BaseName.Length - 6)), $font, $brush, 6, $y + $cell / 2 - 8)

    # Column 1: the original frame, fitted to the cell.
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($source, (New-Object System.Drawing.Rectangle(($labelWidth + $gap), $y, $cell, $cell)))

    # Column 2: reduced to the target size, then blown back up with hard pixel edges.
    $small = New-Object System.Drawing.Bitmap($TargetSize, $TargetSize)
    $sg = [System.Drawing.Graphics]::FromImage($small)
    $sg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $sg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $sg.DrawImage($source, (New-Object System.Drawing.Rectangle(0, 0, $TargetSize, $TargetSize)))
    $sg.Dispose()

    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.DrawImage($small, (New-Object System.Drawing.Rectangle(($labelWidth + $gap * 2 + $cell), $y, $cell, $cell)))

    # Column 3: silhouette of the reduction - if the pose is unreadable here, it will not
    # read in game either, and the design needs simplifying before any pixels are drawn.
    $silhouette = New-Object System.Drawing.Bitmap($TargetSize, $TargetSize)
    for ($px = 0; $px -lt $TargetSize; $px++) {
        for ($py = 0; $py -lt $TargetSize; $py++) {
            $a = $small.GetPixel($px, $py).A
            $c = if ($a -ge 128) { [System.Drawing.Color]::White } else { [System.Drawing.Color]::FromArgb(255, 32, 32, 36) }
            $silhouette.SetPixel($px, $py, $c)
        }
    }
    $g.DrawImage($silhouette, (New-Object System.Drawing.Rectangle(($labelWidth + $gap * 3 + $cell * 2), $y, $cell, $cell)))

    $silhouette.Dispose()
    $small.Dispose()
    $source.Dispose()
}

$font.Dispose()
$g.Dispose()
$sheet.Save($OutputFile, [System.Drawing.Imaging.ImageFormat]::Png)
$sheet.Dispose()

"프레임 {0}개 중 {1}개 추출 -> {2}" -f $frames.Count, $take, $OutputFile
