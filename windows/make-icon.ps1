$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function New-RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconBitmap([int]$S) {
    $bmp = New-Object System.Drawing.Bitmap $S, $S, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $pad = [float]($S * 0.035)
    $rect = New-Object System.Drawing.RectangleF $pad, $pad, ($S - 2 * $pad), ($S - 2 * $pad)
    $radius = [float]($S * 0.23)
    $tile = New-RoundedPath $rect.X $rect.Y $rect.Width $rect.Height $radius

    $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 24, 32, 54)), ([System.Drawing.Color]::FromArgb(255, 6, 9, 16)), 115
    $g.FillPath($bgBrush, $tile)

    $edgePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(80, 130, 200, 255)), ([float][Math]::Max(1, $S * 0.007))
    $g.DrawPath($edgePen, $tile)

    $cx = $S / 2.0
    $cy = $S / 2.0

    if ($S -ge 48) {
        $outerPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(70, 120, 200, 255)), ([float][Math]::Max(1, $S * 0.010))
        $outerR = [float]($S * 0.355)
        $g.DrawEllipse($outerPen, $cx - $outerR, $cy - $outerR, $outerR * 2, $outerR * 2)
        $outerPen.Dispose()
    }

    $ringPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(185, 120, 210, 255)), ([float][Math]::Max(1, $S * 0.014))
    $ringR = [float]($S * 0.295)
    $g.DrawEllipse($ringPen, $cx - $ringR, $cy - $ringR, $ringR * 2, $ringR * 2)
    $ringPen.Dispose()

    $scale = [float]($S * 0.56 / 36.0)
    $tipX = [float]($S * 0.305)
    $tipY = [float]($S * 0.215)

    $raw = @(
        @(2.6, 1.6), @(2.6, 27.6), @(9.1, 21.2), @(13.5, 31.7),
        @(17.7, 29.9), @(13.3, 19.6), @(22.6, 19.6)
    )
    $pts = New-Object 'System.Drawing.PointF[]' $raw.Count
    for ($i = 0; $i -lt $raw.Count; $i++) {
        $pts[$i] = New-Object System.Drawing.PointF (($tipX + ($raw[$i][0] - 2.6) * $scale)), (($tipY + ($raw[$i][1] - 1.6) * $scale))
    }

    $arrow = New-Object System.Drawing.Drawing2D.GraphicsPath
    $arrow.AddPolygon($pts)

    $shadow = [System.Drawing.Drawing2D.GraphicsPath]$arrow.Clone()
    $matrix = New-Object System.Drawing.Drawing2D.Matrix
    $matrix.Translate(0, [float]($S * 0.012))
    $shadow.Transform($matrix)
    $shadowBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(95, 0, 0, 0))
    $g.FillPath($shadowBrush, $shadow)
    $shadowBrush.Dispose()
    $matrix.Dispose()
    $shadow.Dispose()

    $fill = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 230, 55, 65))
    $g.FillPath($fill, $arrow)
    $fill.Dispose()

    $outline = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 45, 18, 22)), ([float][Math]::Max(1, $S * 0.012))
    $outline.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($outline, $arrow)
    $outline.Dispose()
    $arrow.Dispose()

    $g.Dispose()
    return $bmp
}

$icoPath = Join-Path $PSScriptRoot "FindMyCursor.ico"
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = New-Object System.Collections.ArrayList

foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    [void]$images.Add([pscustomobject]@{ Size = $s; Data = $ms.ToArray() })
    $ms.Dispose()
    $bmp.Dispose()
}

$preview = New-IconBitmap 256
$preview.Save((Join-Path $PSScriptRoot "FindMyCursor-preview.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$preview.Dispose()

$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$images.Count)

$offset = 6 + 16 * $images.Count
foreach ($img in $images) {
    $dim = if ($img.Size -ge 256) { 0 } else { $img.Size }
    $bw.Write([Byte]$dim)
    $bw.Write([Byte]$dim)
    $bw.Write([Byte]0)
    $bw.Write([Byte]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $bw.Write([UInt32]$img.Data.Length)
    $bw.Write([UInt32]$offset)
    $offset += $img.Data.Length
}
foreach ($img in $images) { $bw.Write($img.Data) }
$bw.Close()
$fs.Close()

Write-Host ("Icon written: " + $icoPath)
