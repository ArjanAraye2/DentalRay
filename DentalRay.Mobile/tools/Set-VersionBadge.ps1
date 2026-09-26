# Generates the version badge that sits on the Dentix launcher icon.
# Run this after changing versionName in app/build.gradle.kts:
#   powershell -File tools/Set-VersionBadge.ps1
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$gradle = Join-Path $root "app\build.gradle.kts"
$out = Join-Path $root "app\src\main\res\drawable\ic_version_badge.png"

$version = "0.0"
foreach ($line in [System.IO.File]::ReadAllLines($gradle)) {
    if ($line -match 'versionName\s*=\s*"([^"]+)"') { $version = $Matches[1]; break }
}

Add-Type -AssemblyName System.Drawing

# 6x the 46x17dp slot so the badge stays crisp on high-density screens.
$w = 276; $h = 102; $radius = 51
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

$g.Clear([System.Drawing.Color]::Transparent)

$brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 14, 127, 149))
$g.FillEllipse($brush, 0, 0, $h - 1, $h - 1)
$g.FillEllipse($brush, $w - $h + 1, 0, $h - 1, $h - 1)
$g.FillRectangle($brush, [int]($h / 2), 0, [int]($w - $h), $h)

$font = New-Object System.Drawing.Font("Segoe UI", 52, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$white = [System.Drawing.Brushes]::White
$fmt = New-Object System.Drawing.StringFormat
$fmt.Alignment = [System.Drawing.StringAlignment]::Center
$fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
$rect = New-Object System.Drawing.RectangleF 0, 0, $w, $h
$g.DrawString($version, $font, $white, $rect, $fmt)

$g.Dispose()
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

Write-Output ("version=" + $version + "  ->  " + $out)
