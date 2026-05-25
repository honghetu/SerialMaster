# Generate SerialMaster app icon as a multi-resolution .ico (PNG-encoded entries).
# Design: dark rounded square (#1E1E2E) with accent-blue stylized "S" + signal wave underline.
# Sizes: 16, 32, 48, 64, 128, 256.

Add-Type -AssemblyName System.Drawing

$sizes = @(16, 32, 48, 64, 128, 256)
$outIco = Join-Path $PSScriptRoot '..\src\SerialMaster.App\Assets\app.ico'
$outDir = Split-Path $outIco
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

function New-IconBitmap([int]$size) {
    $bmp = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    $bgDark   = [System.Drawing.Color]::FromArgb(255, 30, 30, 46)
    $bgLight  = [System.Drawing.Color]::FromArgb(255, 49, 50, 68)
    $accent   = [System.Drawing.Color]::FromArgb(255, 137, 180, 250)
    $green    = [System.Drawing.Color]::FromArgb(255, 166, 227, 161)

    $r = [Math]::Max(2, [int]($size / 6))
    $rw = $size - 1
    $rh = $size - 1
    $diam = $r * 2

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc(0, 0, $diam, $diam, 180, 90)
    $path.AddArc(($rw - $diam), 0, $diam, $diam, 270, 90)
    $path.AddArc(($rw - $diam), ($rh - $diam), $diam, $diam, 0, 90)
    $path.AddArc(0, ($rh - $diam), $diam, $diam, 90, 90)
    $path.CloseFigure()

    $p0 = [System.Drawing.PointF]::new(0, 0)
    $p1 = [System.Drawing.PointF]::new(0, [float]$size)
    $brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new($p0, $p1, $bgLight, $bgDark)
    $g.FillPath($brush, $path)
    $brush.Dispose()

    $strokeW = [Math]::Max(1, [int]($size / 10))
    $pen = [System.Drawing.Pen]::new($accent, [float]$strokeW)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round

    $padding = [int]($size * 0.22)
    $sw = $size - 2 * $padding
    $sh = [int]($size * 0.50)
    $sx = $padding
    $sy = $padding

    # Upper half-ellipse of S
    $g.DrawArc($pen, $sx, $sy, $sw, [int]($sh * 0.6), 200, 250)
    # Lower half-ellipse of S
    $g.DrawArc($pen, $sx, ($sy + [int]($sh * 0.45)), $sw, [int]($sh * 0.6), 20, 250)

    if ($size -ge 32) {
        $waveStroke = [Math]::Max(1, [int]($size / 32))
        $wavePen = [System.Drawing.Pen]::new($green, [float]$waveStroke)
        $wavePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $wavePen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round

        $by = [int]($size * 0.83)
        $bx0 = [int]($size * 0.20)
        $bx1 = [int]($size * 0.80)
        $bMid = [int](($bx0 + $bx1) / 2)
        $bAmp = [int]($size * 0.05)

        $g.DrawLine($wavePen, $bx0, $by, ($bMid - $bAmp), $by)
        $g.DrawLine($wavePen, ($bMid - $bAmp), $by, $bMid, ($by - $bAmp * 2))
        $g.DrawLine($wavePen, $bMid, ($by - $bAmp * 2), ($bMid + $bAmp), $by)
        $g.DrawLine($wavePen, ($bMid + $bAmp), $by, $bx1, $by)
        $wavePen.Dispose()
    }

    $pen.Dispose()
    $path.Dispose()
    $g.Dispose()
    return $bmp
}

$pngBlobs = @()
foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    $ms  = [System.IO.MemoryStream]::new()
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBlobs += , @($s, $ms.ToArray())
    $ms.Dispose()
    $bmp.Dispose()
}

$out = [System.IO.MemoryStream]::new()
$bw  = [System.IO.BinaryWriter]::new($out)

$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$pngBlobs.Count)

$dataOffset = 6 + (16 * $pngBlobs.Count)

foreach ($entry in $pngBlobs) {
    $s     = $entry[0]
    $bytes = $entry[1]
    $byteW = if ($s -ge 256) { [byte]0 } else { [byte]$s }
    $byteH = $byteW

    $bw.Write($byteW)
    $bw.Write($byteH)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$bytes.Length)
    $bw.Write([uint32]$dataOffset)

    $dataOffset += $bytes.Length
}

foreach ($entry in $pngBlobs) {
    $bw.Write($entry[1])
}

$bw.Flush()
[System.IO.File]::WriteAllBytes($outIco, $out.ToArray())
$bw.Dispose()
$out.Dispose()

"OK: $outIco ($((Get-Item $outIco).Length) bytes, $($pngBlobs.Count) resolutions)"
