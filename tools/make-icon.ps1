# Reconstructs the .logscope logo (stethoscope ring around log lines with a heartbeat
# pulse and a chestpiece) and emits a PNG plus a multi-size .ico.
# Run with Windows PowerShell (System.Drawing): powershell.exe -File tools\make-icon.ps1
Add-Type -AssemblyName System.Drawing

$navy   = [System.Drawing.Color]::FromArgb(255, 30, 58, 95)    # #1E3A5F
$teal   = [System.Drawing.Color]::FromArgb(255, 20, 184, 150)  # #14B8A6
$blue   = [System.Drawing.Color]::FromArgb(255, 43, 140, 230)  # #2B8CE6
$ltblue = [System.Drawing.Color]::FromArgb(255, 74, 163, 224)  # #4AA3E0

function New-LogoBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # Work in a 256-unit design space, then scale.
    $s = $size / 256.0
    $g.ScaleTransform($s, $s)

    $navyPen = New-Object System.Drawing.Pen($navy, 14)
    $navyPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $navyPen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round

    $navyBrush   = New-Object System.Drawing.SolidBrush($navy)
    $tealBrush   = New-Object System.Drawing.SolidBrush($teal)
    $blueBrush   = New-Object System.Drawing.SolidBrush($blue)
    $ltblueBrush = New-Object System.Drawing.SolidBrush($ltblue)

    # --- Stethoscope ring (binaural tubing) -- near-full circle, gap at lower-right ---
    # Ellipse bounds for the ring centered around (118,120), radius ~88
    $rx = 30; $ry = 32; $rw = 176; $rh = 176
    $g.DrawArc($navyPen, $rx, $ry, $rw, $rh, 130, 285)

    # --- Ear tips at the top ---
    $g.FillEllipse($navyBrush, 70, 16, 22, 22)
    $g.FillEllipse($navyBrush, 134, 16, 22, 22)

    # --- Log lines inside the ring ---
    $lineBrush = $navyBrush
    function FillRound($g, $brush, $x, $y, $w, $h) {
        $r = $h
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $path.AddArc($x, $y, $r, $r, 90, 180)
        $path.AddArc($x + $w - $r, $y, $r, $r, 270, 180)
        $path.CloseFigure()
        $g.FillPath($brush, $path)
        $path.Dispose()
    }
    FillRound $g $lineBrush 96 84  86 11
    FillRound $g $lineBrush 96 134 70 11

    # bullet dots to the left of lines 1 and 3
    $g.FillEllipse($tealBrush, 72, 82, 14, 14)
    $g.FillEllipse($blueBrush, 72, 132, 14, 14)

    # --- Heartbeat pulse across the middle line ---
    $pulsePen = New-Object System.Drawing.Pen($teal, 7)
    $pulsePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pulsePen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    $pulsePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    [System.Drawing.Point[]]$pts = @(
        (New-Object System.Drawing.Point(74, 114)),
        (New-Object System.Drawing.Point(104, 114)),
        (New-Object System.Drawing.Point(116, 96)),
        (New-Object System.Drawing.Point(128, 130)),
        (New-Object System.Drawing.Point(140, 108)),
        (New-Object System.Drawing.Point(150, 114)),
        (New-Object System.Drawing.Point(182, 114))
    )
    $g.DrawLines($pulsePen, $pts)

    # --- Chestpiece at lower-right ---
    $tubePen = New-Object System.Drawing.Pen($navy, 11)
    $tubePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $tubePen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($tubePen, 168, 188, 196, 196)
    $chestPen = New-Object System.Drawing.Pen($navy, 9)
    $g.DrawEllipse($chestPen, 188, 178, 44, 44)
    $g.FillEllipse($tealBrush, 200, 190, 20, 20)

    $g.Dispose()
    return $bmp
}

$outDir = Join-Path $PSScriptRoot "..\src\LogScope.App\Assets"
New-Item -ItemType Directory -Force $outDir | Out-Null

# High-res PNG for the in-app header
$png = New-LogoBitmap 256
$pngPath = Join-Path $outDir "logo.png"
$png.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Wrote $pngPath"

# Build a multi-size .ico with PNG-compressed frames
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = @()
foreach ($sz in $sizes) {
    $b = New-LogoBitmap $sz
    $ms = New-Object System.IO.MemoryStream
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames += ,@{ Size = $sz; Bytes = $ms.ToArray() }
    $ms.Dispose(); $b.Dispose()
}

$icoPath = Join-Path $outDir "app.ico"
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0)            # reserved
$bw.Write([UInt16]1)            # type = icon
$bw.Write([UInt16]$frames.Count)
$offset = 6 + (16 * $frames.Count)
foreach ($f in $frames) {
    $dim = if ($f.Size -ge 256) { 0 } else { $f.Size }
    $bw.Write([Byte]$dim)       # width
    $bw.Write([Byte]$dim)       # height
    $bw.Write([Byte]0)          # palette
    $bw.Write([Byte]0)          # reserved
    $bw.Write([UInt16]1)        # planes
    $bw.Write([UInt16]32)       # bpp
    $bw.Write([UInt32]$f.Bytes.Length)
    $bw.Write([UInt32]$offset)
    $offset += $f.Bytes.Length
}
foreach ($f in $frames) { $bw.Write($f.Bytes) }
$bw.Flush(); $fs.Close()
Write-Host "Wrote $icoPath"
