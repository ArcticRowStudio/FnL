<#
.SYNOPSIS
    Turns a level sprite's alpha into a list of world space collision boxes.

.DESCRIPTION
    The sprite is imported at 2048x1024 while the source is 15360x7680, so a
    Polygon Collider 2D generated from it sits up to 0.3 units above the art. This
    reads the source png instead, at full resolution, and emits axis aligned boxes
    that Unity can turn into colliders. Run LevelCollisionBaker in the editor next.

    The png is read one row at a time: the full image is ~470 MB as pixels.

.EXAMPLE
    ./Tools/bake-level-collision.ps1
    ./Tools/bake-level-collision.ps1 -Cell 4 -Exclude '27,8,34,32'
#>
param(
    # Source art. Not the imported sprite: that one is downsampled.
    [string]$Png = "Assets/Environment/Sprites/Level 0/backdrop_1.png",

    # World space rect the sprite occupies, "minX,minY,maxX,maxY". Read this off the
    # SpriteRenderer bounds in the inspector.
    [string]$Bounds = "3.2,-23.26,156.8,53.54",

    # Source pixels per collision cell, across. Smaller is more faithful and produces more
    # boxes. 8 gives 0.08 world units, about 4 screen pixels at 48 PPU.
    [int]$Cell = 8,

    # Source pixels per collision cell, down. Kept finer than -Cell by default: the height a
    # character stands at is the error you actually see, while a wall being an eighth of a
    # unit off across is not noticeable. 0 follows -Cell.
    #
    # 4 lands the floor surface within 0.01 units of the art, which is where the measurement
    # bottoms out: going finer only adds boxes.
    [int]$CellY = 4,

    # A pixel counts as solid above this alpha.
    [int]$AlphaThreshold = 8,

    # Fraction of a cell that must be solid for the cell to be solid.
    [double]$Coverage = 0.5,

    # Boxes smaller than this many cells on either side are dropped as speckle.
    [int]$MinCells = 1,

    # World space rects to leave out entirely, semicolon separated: "minX,minY,maxX,maxY;...".
    [string]$Exclude = "",

    # World space rects to thin out rather than drop, same format. Inside these, anything
    # whose vertical thickness is at or below -DeclutterMaxThickness is removed. Handrails on
    # the spiral staircases measure 0.13 to 0.23 units thick while the treads and the central
    # column measure 0.63 and up, so the two separate cleanly and the stairs stay climbable
    # while the rails stop being walls.
    [string]$Declutter = "",
    [double]$DeclutterMaxThickness = 0.35,

    [string]$OutCsv = "level-collision.csv"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if (-not ("LevelCollisionMask" -as [type])) {
Add-Type -ReferencedAssemblies System.Drawing, System.Drawing.Primitives @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class LevelCollisionMask
{
    public static int GridW, GridH, SrcW, SrcH, SolidCells;

    public static bool[] Build(string path, int cellX, int cellY, double coverage, int alphaThreshold)
    {
        using (Bitmap bmp = new Bitmap(path))
        {
            SrcW = bmp.Width;
            SrcH = bmp.Height;
            GridW = (SrcW + cellX - 1) / cellX;
            GridH = (SrcH + cellY - 1) / cellY;

            int[] counts = new int[GridW * GridH];
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, SrcW, SrcH),
                                           ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] row = new byte[data.Stride];
                for (int y = 0; y < SrcH; y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, data.Stride);
                    int rowBase = (y / cellY) * GridW;
                    for (int x = 0; x < SrcW; x++)
                    {
                        if (row[x * 4 + 3] > alphaThreshold) counts[rowBase + (x / cellX)]++;
                    }
                }
            }
            finally { bmp.UnlockBits(data); }

            int need = (int)Math.Ceiling(cellX * cellY * coverage);
            bool[] mask = new bool[GridW * GridH];
            SolidCells = 0;
            for (int i = 0; i < mask.Length; i++)
            {
                if (counts[i] >= need) { mask[i] = true; SolidCells++; }
            }
            return mask;
        }
    }

    // Clears vertical runs no taller than maxRunCells whose middle falls inside one of the
    // regions. Regions arrive flattened as x0,y0,x1,y1 in grid coordinates. A run is measured
    // over the whole column so a rail crossing a region edge is still judged on its real
    // thickness rather than the sliver that happens to be inside.
    public static int RemoveThinRuns(bool[] mask, int gw, int gh, int[] regions, int maxRunCells)
    {
        int cleared = 0;
        for (int x = 0; x < gw; x++)
        {
            bool columnMatters = false;
            for (int r = 0; r < regions.Length; r += 4)
            {
                if (x >= regions[r] && x <= regions[r + 2]) { columnMatters = true; break; }
            }
            if (!columnMatters) continue;

            int y = 0;
            while (y < gh)
            {
                if (!mask[y * gw + x]) { y++; continue; }

                int start = y;
                while (y < gh && mask[y * gw + x]) y++;
                int length = y - start;
                if (length > maxRunCells) continue;

                int middle = (start + y - 1) / 2;
                bool inside = false;
                for (int r = 0; r < regions.Length; r += 4)
                {
                    if (x >= regions[r] && x <= regions[r + 2] &&
                        middle >= regions[r + 1] && middle <= regions[r + 3]) { inside = true; break; }
                }
                if (!inside) continue;

                for (int k = start; k < start + length; k++) { mask[k * gw + x] = false; cleared++; }
            }
        }
        return cleared;
    }

    // The same idea along rows, which clears upright bars: the balusters between a tread and
    // its handrail. Run separately from the vertical pass because the two use different cell
    // sizes, and because clearing the rails first leaves the balusters standing alone.
    public static int RemoveThinColumns(bool[] mask, int gw, int gh, int[] regions, int maxRunCells)
    {
        int cleared = 0;
        for (int y = 0; y < gh; y++)
        {
            bool rowMatters = false;
            for (int r = 0; r < regions.Length; r += 4)
            {
                if (y >= regions[r + 1] && y <= regions[r + 3]) { rowMatters = true; break; }
            }
            if (!rowMatters) continue;

            int rowBase = y * gw;
            int x = 0;
            while (x < gw)
            {
                if (!mask[rowBase + x]) { x++; continue; }

                int start = x;
                while (x < gw && mask[rowBase + x]) x++;
                int length = x - start;
                if (length > maxRunCells) continue;

                int middle = (start + x - 1) / 2;
                bool inside = false;
                for (int r = 0; r < regions.Length; r += 4)
                {
                    if (middle >= regions[r] && middle <= regions[r + 2] &&
                        y >= regions[r + 1] && y <= regions[r + 3]) { inside = true; break; }
                }
                if (!inside) continue;

                for (int k = start; k < start + length; k++) { mask[rowBase + k] = false; cleared++; }
            }
        }
        return cleared;
    }

    // Greedy maximal rectangles. Take the first uncovered solid cell, run right while the
    // row allows, then grow down while the whole width stays solid and uncovered.
    public static List<int[]> Decompose(bool[] mask, int gw, int gh, int minCells, out int dropped)
    {
        bool[] used = new bool[mask.Length];
        var rects = new List<int[]>();
        dropped = 0;

        for (int y = 0; y < gh; y++)
        {
            for (int x = 0; x < gw; x++)
            {
                int start = y * gw + x;
                if (!mask[start] || used[start]) continue;

                int w = 0;
                while (x + w < gw && mask[start + w] && !used[start + w]) w++;

                int h = 1;
                while (y + h < gh)
                {
                    bool full = true;
                    int rowBase = (y + h) * gw + x;
                    for (int k = 0; k < w; k++)
                    {
                        if (!mask[rowBase + k] || used[rowBase + k]) { full = false; break; }
                    }
                    if (!full) break;
                    h++;
                }

                for (int yy = y; yy < y + h; yy++)
                {
                    int rowBase = yy * gw + x;
                    for (int k = 0; k < w; k++) used[rowBase + k] = true;
                }

                if (w >= minCells && h >= minCells) rects.Add(new int[] { x, y, w, h });
                else dropped++;
            }
        }
        return rects;
    }
}
"@
}

function Parse-Rect([string]$text) {
    $p = $text.Split(',')
    if ($p.Count -ne 4) { throw "Expected 'minX,minY,maxX,maxY' but got '$text'." }
    return [double[]]@($p[0], $p[1], $p[2], $p[3])
}

$b = Parse-Rect $Bounds
$leftX = $b[0]; $bottomY = $b[1]; $rightX = $b[2]; $topY = $b[3]

$excludes = @()
if ($Exclude -ne "") {
    foreach ($chunk in $Exclude.Split(';')) {
        if ($chunk.Trim() -ne "") { $excludes += ,(Parse-Rect $chunk.Trim()) }
    }
}

$pngFull = if ([IO.Path]::IsPathRooted($Png)) { $Png } else { Join-Path (Get-Location) $Png }
if (-not (Test-Path $pngFull)) { throw "Source png not found: $pngFull" }

if ($CellY -le 0) { $CellY = $Cell }
$mask = [LevelCollisionMask]::Build($pngFull, $Cell, $CellY, $Coverage, $AlphaThreshold)
$gw = [LevelCollisionMask]::GridW
$gh = [LevelCollisionMask]::GridH
$srcW = [LevelCollisionMask]::SrcW
$srcH = [LevelCollisionMask]::SrcH

# The world rect and the pixel grid have to agree or every box lands offset.
$ppuX = $srcW / ($rightX - $leftX)
$ppuY = $srcH / ($topY - $bottomY)
if ([math]::Abs($ppuX - $ppuY) -gt 0.01) {
    Write-Warning ("Non uniform scale: {0:N3} px/unit across, {1:N3} down. Check -Bounds." -f $ppuX, $ppuY)
}

$declutterCells = 0
if ($Declutter -ne "") {
    $regions = New-Object System.Collections.Generic.List[int]
    foreach ($chunk in $Declutter.Split(';')) {
        if ($chunk.Trim() -eq "") { continue }
        $d = Parse-Rect $chunk.Trim()
        # World rect to grid cells. Y is flipped: grid row 0 is the top of the image.
        $regions.Add([int][math]::Floor((($d[0] - $leftX) * $ppuX) / $Cell))
        $regions.Add([int][math]::Floor((($topY - $d[3]) * $ppuY) / $CellY))
        $regions.Add([int][math]::Ceiling((($d[2] - $leftX) * $ppuX) / $Cell))
        $regions.Add([int][math]::Ceiling((($topY - $d[1]) * $ppuY) / $CellY))
    }
    $maxRunY = [int][math]::Floor(($DeclutterMaxThickness * $ppuY) / $CellY)
    $maxRunX = [int][math]::Floor(($DeclutterMaxThickness * $ppuX) / $Cell)
    $arr = $regions.ToArray()
    # Rails first, then the balusters they were resting on.
    $flat = [LevelCollisionMask]::RemoveThinRuns($mask, $gw, $gh, $arr, $maxRunY)
    $upright = [LevelCollisionMask]::RemoveThinColumns($mask, $gw, $gh, $arr, $maxRunX)
    $declutterCells = $flat + $upright
    Write-Output ("declutter  : {0} region(s) at <= {1:N3} units" -f ($regions.Count / 4), $DeclutterMaxThickness)
    Write-Output ("             flat bars  : {0} cells (runs <= {1} rows)" -f $flat, $maxRunY)
    Write-Output ("             upright bars: {0} cells (runs <= {1} cols)" -f $upright, $maxRunX)
}

$dropped = 0
$rects = [LevelCollisionMask]::Decompose($mask, $gw, $gh, $MinCells, [ref]$dropped)

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("centerX,centerY,sizeX,sizeY")
$kept = 0
$skipped = 0
foreach ($r in $rects) {
    $x0 = $leftX + ($r[0] * $Cell) / $ppuX
    $x1 = $leftX + (($r[0] + $r[2]) * $Cell) / $ppuX
    $y1 = $topY  - ($r[1] * $CellY) / $ppuY
    $y0 = $topY  - (($r[1] + $r[3]) * $CellY) / $ppuY

    $cx = ($x0 + $x1) / 2; $cy = ($y0 + $y1) / 2
    $inExcluded = $false
    foreach ($e in $excludes) {
        if ($cx -ge $e[0] -and $cx -le $e[2] -and $cy -ge $e[1] -and $cy -le $e[3]) { $inExcluded = $true; break }
    }
    if ($inExcluded) { $skipped++; continue }

    [void]$sb.AppendLine(("{0:F4},{1:F4},{2:F4},{3:F4}" -f $cx, $cy, ($x1 - $x0), ($y1 - $y0)))
    $kept++
}

$outFull = if ([IO.Path]::IsPathRooted($OutCsv)) { $OutCsv } else { Join-Path (Get-Location) $OutCsv }
[IO.File]::WriteAllText($outFull, $sb.ToString(), (New-Object Text.UTF8Encoding $true))

Write-Output ("source     : {0}  ({1}x{2})" -f $Png, $srcW, $srcH)
Write-Output ("world rect : {0} .. {1} across, {2} .. {3} down" -f $leftX, $rightX, $bottomY, $topY)
Write-Output ("cell       : {0}x{1} px = {2:N3}x{3:N3} units    grid {4}x{5}" -f `
              $Cell, $CellY, ($Cell / $ppuX), ($CellY / $ppuY), $gw, $gh)
Write-Output ("solid cells: {0}" -f [LevelCollisionMask]::SolidCells)
Write-Output ("boxes      : {0} kept, {1} excluded, {2} below {3} cells" -f $kept, $skipped, $dropped, $MinCells)
Write-Output ("csv        : {0}" -f $outFull)
