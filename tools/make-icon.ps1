# Derives app assets from the master logo (assets/logscope-logo.png):
#   - Assets/logo-full.png : the full vertical lockup (mark + wordmark) for the landing screen
#   - Assets/logo-mark.png : the stethoscope mark only, square, for the header bar
#   - Assets/app.ico       : multi-size icon built from the mark
# Run: pwsh tools/make-icon.ps1   (uses System.Drawing on Windows)
Add-Type -AssemblyName System.Drawing

$src    = Join-Path $PSScriptRoot "..\assets\logscope-logo.png"
$outDir = Join-Path $PSScriptRoot "..\src\LogScope.App\Assets"
New-Item -ItemType Directory -Force $outDir | Out-Null

$master = New-Object System.Drawing.Bitmap($src)
$W = $master.Width; $H = $master.Height

# --- Per-row opaque-pixel coverage (alpha > 32) ---
$rowCount = New-Object 'int[]' $H
for ($y = 0; $y -lt $H; $y++) {
    $c = 0
    for ($x = 0; $x -lt $W; $x += 2) {
        if ($master.GetPixel($x, $y).A -gt 32) { $c++ }
    }
    $rowCount[$y] = $c
}

# --- Find the gap between the mark (top) and the wordmark (bottom) ---
# Scan the middle band for the longest run of (near) empty rows.
$bandStart = [int]($H * 0.45); $bandEnd = [int]($H * 0.85)
$bestLen = 0; $bestMid = [int]($H * 0.70)
$runStart = -1
for ($y = $bandStart; $y -lt $bandEnd; $y++) {
    if ($rowCount[$y] -le 1) {
        if ($runStart -lt 0) { $runStart = $y }
    } else {
        if ($runStart -ge 0) {
            $len = $y - $runStart
            if ($len -gt $bestLen) { $bestLen = $len; $bestMid = [int](($runStart + $y) / 2) }
            $runStart = -1
        }
    }
}
$split = $bestMid
Write-Host "Mark/wordmark split row: $split (gap height $bestLen)"

function Get-ContentBounds([System.Drawing.Bitmap]$bmp, [int]$y0, [int]$y1) {
    $minX = $bmp.Width; $minY = $bmp.Height; $maxX = 0; $maxY = 0
    for ($y = $y0; $y -lt $y1; $y++) {
        for ($x = 0; $x -lt $bmp.Width; $x++) {
            if ($bmp.GetPixel($x, $y).A -gt 32) {
                if ($x -lt $minX) { $minX = $x }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }
    return @{ X = $minX; Y = $minY; W = ($maxX - $minX + 1); H = ($maxY - $minY + 1) }
}

# --- Mark bounding box (above the split), pasted centered onto a transparent square ---
# Paste the EXACT mark region (never sample beyond it) so the wordmark can't bleed in.
$b = Get-ContentBounds $master 0 $split
$pad = [int]([Math]::Max($b.W, $b.H) * 0.10)
$side = [Math]::Max($b.W, $b.H) + 2 * $pad
$destX = [int](($side - $b.W) / 2)
$destY = [int](($side - $b.H) / 2)

$mark = New-Object System.Drawing.Bitmap($side, $side)
$mg = [System.Drawing.Graphics]::FromImage($mark)
$mg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$mg.Clear([System.Drawing.Color]::Transparent)
$markDest = New-Object System.Drawing.Rectangle($destX, $destY, $b.W, $b.H)
$mg.DrawImage($master, $markDest, $b.X, $b.Y, $b.W, $b.H, [System.Drawing.GraphicsUnit]::Pixel)
$mg.Dispose()
$markPath = Join-Path $outDir "logo-mark.png"
$mark.Save($markPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Wrote $markPath ($side x $side)"

# --- Full lockup (copy master through, trimmed to overall content) ---
$fb = Get-ContentBounds $master 0 $H
$full = New-Object System.Drawing.Bitmap($fb.W, $fb.H)
$fg = [System.Drawing.Graphics]::FromImage($full)
$fg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$fg.Clear([System.Drawing.Color]::Transparent)
$fg.DrawImage($master, (New-Object System.Drawing.Rectangle(0, 0, $fb.W, $fb.H)), $fb.X, $fb.Y, $fb.W, $fb.H, [System.Drawing.GraphicsUnit]::Pixel)
$fg.Dispose()
$fullPath = Join-Path $outDir "logo-full.png"
$full.Save($fullPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Wrote $fullPath ($($fb.W) x $($fb.H))"

# --- Multi-size .ico built from the mark ---
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = @()
foreach ($sz in $sizes) {
    $b2 = New-Object System.Drawing.Bitmap($sz, $sz)
    $g2 = [System.Drawing.Graphics]::FromImage($b2)
    $g2.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g2.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g2.Clear([System.Drawing.Color]::Transparent)
    $g2.DrawImage($mark, 0, 0, $sz, $sz)
    $g2.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $b2.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames += ,@{ Size = $sz; Bytes = $ms.ToArray() }
    $ms.Dispose(); $b2.Dispose()
}

$icoPath = Join-Path $outDir "app.ico"
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$frames.Count)
$offset = 6 + (16 * $frames.Count)
foreach ($f in $frames) {
    $dim = if ($f.Size -ge 256) { 0 } else { $f.Size }
    $bw.Write([Byte]$dim); $bw.Write([Byte]$dim); $bw.Write([Byte]0); $bw.Write([Byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$f.Bytes.Length); $bw.Write([UInt32]$offset)
    $offset += $f.Bytes.Length
}
foreach ($f in $frames) { $bw.Write($f.Bytes) }
$bw.Flush(); $fs.Close()
Write-Host "Wrote $icoPath"

$mark.Dispose(); $full.Dispose(); $master.Dispose()
