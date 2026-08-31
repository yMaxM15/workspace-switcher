Add-Type -AssemblyName System.Drawing

function New-WorkspaceSwitcherIcon {
    param(
        [string]$OutputPath = "src/WorkspaceSwitcher.UI/app.ico"
    )

    $sizes = @(16, 32, 48, 64, 128, 256)
    $bitmaps = @()

    foreach ($size in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        # Transparent clear
        $g.Clear([System.Drawing.Color]::Transparent)

        # Background rounded dark indigo box
        $radius = [Math]::Max(2, [int]($size * 0.22))
        $rect = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
        
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $d = $radius * 2
        $path.AddArc(0, 0, $d, $d, 180, 90)
        $path.AddArc($size - $d, 0, $d, $d, 270, 90)
        $path.AddArc($size - $d, $size - $d, $d, $d, 0, 90)
        $path.AddArc(0, $size - $d, $d, $d, 90, 90)
        $path.CloseFigure()

        # Gradient Brush (Indigo to Violet)
        $gradBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            (New-Object System.Drawing.PointF(0, 0)),
            (New-Object System.Drawing.PointF($size, $size)),
            [System.Drawing.Color]::FromArgb(255, 99, 102, 241),
            [System.Drawing.Color]::FromArgb(255, 124, 58, 237)
        )
        $g.FillPath($gradBrush, $path)

        # Inner dark frame
        $pad = [Math]::Max(1, [int]($size * 0.08))
        $innerSize = $size - (2 * $pad)
        $innerRadius = [Math]::Max(2, [int]($innerSize * 0.18))
        $inD = $innerRadius * 2
        $inPath = New-Object System.Drawing.Drawing2D.GraphicsPath
        $inPath.AddArc($pad, $pad, $inD, $inD, 180, 90)
        $inPath.AddArc($pad + $innerSize - $inD, $pad, $inD, $inD, 270, 90)
        $inPath.AddArc($pad + $innerSize - $inD, $pad + $innerSize - $inD, $inD, $inD, 0, 90)
        $inPath.AddArc($pad, $pad + $innerSize - $inD, $inD, $inD, 90, 90)
        $inPath.CloseFigure()

        $innerBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 10, 14, 26))
        $g.FillPath($innerBrush, $inPath)

        # Draw 4 window tiles inside
        $tilePad = [Math]::Max(2, [int]($size * 0.20))
        $tileGap = [Math]::Max(1, [int]($size * 0.06))
        $tileW = [int](($size - (2 * $tilePad) - $tileGap) / 2)
        $tileH = $tileW

        $x1 = $tilePad
        $y1 = $tilePad
        $x2 = $tilePad + $tileW + $tileGap
        $y2 = $tilePad + $tileH + $tileGap

        # Tile 1: Top-Left (Indigo)
        $b1 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 99, 102, 241))
        $g.FillRectangle($b1, $x1, $y1, $tileW, $tileH)

        # Tile 2: Top-Right (Cyan)
        $b2 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 96, 205, 255))
        $g.FillRectangle($b2, $x2, $y1, $tileW, $tileH)

        # Tile 3: Bottom-Left (Purple)
        $b3 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 168, 85, 247))
        $g.FillRectangle($b3, $x1, $y2, $tileW, $tileH)

        # Tile 4: Bottom-Right (Emerald)
        $b4 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 16, 185, 129))
        $g.FillRectangle($b4, $x2, $y2, $tileW, $tileH)

        $g.Dispose()
        $bitmaps += $bmp
    }

    # Save as multi-resolution ICO file using binary stream
    $fs = [System.IO.File]::OpenWrite($OutputPath)
    $bw = New-Object System.IO.BinaryWriter($fs)

    # ICONDIR header: Reserved (0), Type (1 for icon), Count
    $bw.Write([UInt16]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]$bitmaps.Count)

    $pngStreams = @()
    foreach ($bmp in $bitmaps) {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngStreams += $ms
    }

    # Offset to image data = 6 (header) + 16 * count
    $offset = 6 + (16 * $bitmaps.Count)

    for ($i = 0; $i - $bitmaps.Count; $i++) {
        $sz = $sizes[$i]
        $w = if ($sz -ge 256) { 0 } else { [byte]$sz }
        $h = if ($sz -ge 256) { 0 } else { [byte]$sz }
        $bytesCount = [UInt32]$pngStreams[$i].Length

        # ICONDIRENTRY: Width, Height, ColorCount, Reserved, Planes, BitCount, BytesInRes, ImageOffset
        $bw.Write([byte]$w)
        $bw.Write([byte]$h)
        $bw.Write([byte]0)   # ColorCount
        $bw.Write([byte]0)   # Reserved
        $bw.Write([UInt16]1) # Planes
        $bw.Write([UInt16]32)# BitCount
        $bw.Write($bytesCount)
        $bw.Write([UInt32]$offset)

        $offset += $bytesCount
    }

    # Write PNG payloads
    foreach ($ms in $pngStreams) {
        $data = $ms.ToArray()
        $bw.Write($data)
        $ms.Dispose()
    }

    $bw.Flush()
    $fs.Close()
    $fs.Dispose()

    foreach ($bmp in $bitmaps) { $bmp.Dispose() }
    Write-Output "Successfully created $OutputPath"
}

New-WorkspaceSwitcherIcon -OutputPath "src/WorkspaceSwitcher.UI/app.ico"
