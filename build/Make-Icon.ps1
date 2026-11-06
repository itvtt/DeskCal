param([string]$OutIco, [string]$OutPreview, [string]$Date)
# 사용: powershell -NoProfile -STA -ExecutionPolicy Bypass -File build\Make-Icon.ps1 -OutIco cal.ico [-OutPreview icon-preview.png]
# DeskCal 아이콘: 아이폰 캘린더처럼 흰 타일 + 주황 요일 + 큰 날짜. 앱은 AppIcon.cs 로 오늘 날짜를 그리고, 이 파일(cal.ico)은 고정 날짜(-Date, 기본 일요일 31)
# 크기마다 픽셀에 맞춘 배치를 따로 써서 16px 에서도 또렷하게. 프레임은 모두 32비트 BMP (.NET Framework 설치 프로그램의 Icon 이 PNG 프레임을 못 읽음)
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase, System.Drawing

function C([string]$hex) { [System.Windows.Media.ColorConverter]::ConvertFromString($hex) }
function Solid([string]$hex) { $b = New-Object System.Windows.Media.SolidColorBrush (C $hex); $b.Freeze(); $b }
function Lin([double]$x1, [double]$y1, [double]$x2, [double]$y2, [object[]]$stops) {
  $g = New-Object System.Windows.Media.LinearGradientBrush
  $g.StartPoint = New-Object System.Windows.Point $x1, $y1; $g.EndPoint = New-Object System.Windows.Point $x2, $y2
  for ($i = 0; $i -lt $stops.Count; $i += 2) { $g.GradientStops.Add((New-Object System.Windows.Media.GradientStop (C $stops[$i]), $stops[$i + 1])) }
  $g.Freeze(); $g
}
function NewRect($x, $y, $w, $h) { New-Object System.Windows.Rect $x, $y, $w, $h }
function RRect($x, $y, $w, $h, $r) { New-Object System.Windows.Media.RectangleGeometry((NewRect $x $y $w $h), $r, $r) }

# 아이폰 캘린더 모양 (AppIcon.cs 와 같은 배치). 흰 타일 + 주황 요일 + 큰 날짜, 32px 이하는 요일 대신 주황 막대 + 굵은 날짜
$Weekdays = @('일요일', '월요일', '화요일', '수요일', '목요일', '금요일', '토요일')
$FontDir = (Resolve-Path (Join-Path $PSScriptRoot '..\Fonts')).Path.Replace('\', '/')
$Pretendard = New-Object System.Windows.Media.FontFamily((New-Object System.Uri("file:///$FontDir/")), './#Pretendard')
$SegoeUi = New-Object System.Windows.Media.FontFamily('Segoe UI')
$IconDate = if ($Date) { [DateTime]::Parse($Date) } else { [DateTime]::new(2026, 5, 31) } # 고정 아이콘은 '일요일 31'

function DrawFitted($dc, [string]$text, $font, [string]$weight, [double]$height, $x, $y, $w, $h, $brush, [bool]$snap) {
  $tf = New-Object System.Windows.Media.Typeface($font, [System.Windows.FontStyles]::Normal, [System.Windows.FontWeights]::$weight, [System.Windows.FontStretches]::Normal)
  $probe = New-Object System.Windows.Media.FormattedText($text, [System.Globalization.CultureInfo]::InvariantCulture, [System.Windows.FlowDirection]::LeftToRight, $tf, 100, $brush, 1.0)
  $ph = $probe.BuildGeometry((New-Object System.Windows.Point 0, 0)).Bounds.Height; if ($ph -le 0) { return }
  $ft = New-Object System.Windows.Media.FormattedText($text, [System.Globalization.CultureInfo]::InvariantCulture, [System.Windows.FlowDirection]::LeftToRight, $tf, (100 * $height / $ph), $brush, 1.0)
  $geo = $ft.BuildGeometry((New-Object System.Windows.Point 0, 0)); $bb = $geo.Bounds
  $dx = $x + ($w - $bb.Width) / 2 - $bb.X; $dy = $y + ($h - $bb.Height) / 2 - $bb.Y
  if ($snap) { $dx = [Math]::Round($dx); $dy = [Math]::Round($dy) }
  $geo.Transform = New-Object System.Windows.Media.TranslateTransform($dx, $dy)
  $dc.DrawGeometry($brush, $null, $geo)
}

function Render([int]$s) {
  $dv = New-Object System.Windows.Media.DrawingVisual
  $dc = $dv.RenderOpen()
  $accent = Solid '#FFFF9500'; $ink = Solid '#FF1D1D1F'
  $day = [string]$IconDate.Day
  if ($s -le 32) {
    $inset = if ($s -ge 32) { 1 } else { 0 }; $r = [Math]::Round($s * 0.22, 1)
    $dc.DrawRoundedRectangle((Lin 0 0 0 1 @('#FFFFFFFF', 0.0, '#FFF0F0F2', 1.0)), (New-Object System.Windows.Media.Pen((Solid '#FFC4C4C9'), 1)), (NewRect ($inset + 0.5) ($inset + 0.5) ($s - 2 * $inset - 1) ($s - 2 * $inset - 1)), $r, $r)
    $barW = [Math]::Round($s * 0.40); $barH = if ($s -ge 24) { 3 } else { 2 }; $barY = [Math]::Round($s * 0.12)
    $dc.DrawRoundedRectangle($accent, $null, (NewRect ([Math]::Round(($s - $barW) / 2)) $barY $barW $barH), ($barH / 2), ($barH / 2))
    $top = $barY + $barH + 1; $bottom = $s - [Math]::Max(2, [Math]::Round($s * 0.1))
    $weight = if ($s -ge 24) { 'SemiBold' } else { 'Bold' }
    $nh = [Math]::Round($s * $(if ($day.Length -eq 1) { 0.50 } else { 0.44 }))
    DrawFitted $dc $day $SegoeUi $weight $nh 0 $top $s ($bottom - $top) $ink $true
  } else {
    $k = $s / 256.0; $inset = 10 * $k; $r = 54 * $k
    if ($s -ge 64) { for ($g = 6; $g -ge 1; $g--) { $o = $g * 1.5 * $k; $dc.DrawRoundedRectangle((New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.Color]::FromArgb([byte]8, 0, 0, 0))), $null, (NewRect ($inset - $o * 0.2) ($inset + $o * 0.9) ($s - 2 * $inset + $o * 0.4) ($s - 2 * $inset)), ($r + $o * 0.5), ($r + $o * 0.5)) } }
    $edgeHex = if ($s -ge 64) { '#1A000000' } else { '#FFC8C8CD' }
    $dc.DrawRoundedRectangle((Lin 0 0 0 1 @('#FFFFFFFF', 0.0, '#FFF1F1F3', 1.0)), (New-Object System.Windows.Media.Pen((Solid $edgeHex), [Math]::Max(1, 1.5 * $k))), (NewRect $inset $inset ($s - 2 * $inset) ($s - 2 * $inset)), $r, $r)
    DrawFitted $dc $Weekdays[[int]$IconDate.DayOfWeek] $Pretendard 'SemiBold' (27 * $k) 0 (32 * $k) $s (40 * $k) $accent $false
    $nw = if ($s -ge 96) { 'Light' } elseif ($s -ge 48) { 'Normal' } else { 'SemiBold' }
    DrawFitted $dc $day $SegoeUi $nw ($(if ($day.Length -eq 1) { 118 } else { 112 }) * $k) 0 (82 * $k) $s (150 * $k) $ink $false
  }
  $dc.Close()
  $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap $s, $s, 96, 96, ([System.Windows.Media.PixelFormats]::Pbgra32)
  $rtb.Render($dv)
  return New-Object System.Windows.Media.Imaging.FormatConvertedBitmap($rtb, [System.Windows.Media.PixelFormats]::Bgra32, $null, 0)
}

function PngBytes($bmp) {
  $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
  $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bmp))
  $ms = New-Object System.IO.MemoryStream; $enc.Save($ms); return ,($ms.ToArray())
}

function DibBytes($bmp, [int]$s) {
  $stride = $s * 4; $px = New-Object byte[] ($stride * $s); $bmp.CopyPixels($px, $stride, 0)
  $maskStride = [int]([Math]::Ceiling($s / 32.0) * 4)
  $ms = New-Object System.IO.MemoryStream; $w = New-Object System.IO.BinaryWriter $ms
  $w.Write([int]40); $w.Write([int]$s); $w.Write([int]($s * 2)); $w.Write([int16]1); $w.Write([int16]32)
  $w.Write([int]0); $w.Write([int]($stride * $s + $maskStride * $s)); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0)
  for ($y = $s - 1; $y -ge 0; $y--) { $w.Write($px, $y * $stride, $stride) }  # 아래에서 위로
  for ($y = $s - 1; $y -ge 0; $y--) {
    $row = New-Object byte[] $maskStride
    for ($x = 0; $x -lt $s; $x++) { if ($px[$y * $stride + $x * 4 + 3] -eq 0) { $row[[int][Math]::Floor($x / 8)] = $row[[int][Math]::Floor($x / 8)] -bor (0x80 -shr ($x % 8)) } }
    $w.Write($row)
  }
  $w.Flush(); return ,($ms.ToArray())
}

function Zoom($bmp, [int]$s) {
  $f = [Math]::Max(1, [int][Math]::Floor(96 / $s)); $stride = $s * 4; $px = New-Object byte[] ($stride * $s); $bmp.CopyPixels($px, $stride, 0)
  $S2 = $s * $f; $out = New-Object byte[] ($S2 * $S2 * 4)
  for ($y = 0; $y -lt $S2; $y++) { for ($x = 0; $x -lt $S2; $x++) {
    $si = [int]([Math]::Floor($y / $f) * $stride + [Math]::Floor($x / $f) * 4); $di = ($y * $S2 + $x) * 4
    $out[$di] = $px[$si]; $out[$di+1] = $px[$si+1]; $out[$di+2] = $px[$si+2]; $out[$di+3] = $px[$si+3] } }
  [System.Windows.Media.Imaging.BitmapSource]::Create($S2, $S2, 96, 96, [System.Windows.Media.PixelFormats]::Bgra32, $null, $out, $S2 * 4)
}

$sizes = 256, 128, 64, 48, 40, 32, 24, 20, 16
$frames = @{}
foreach ($s in $sizes) { $frames[$s] = Render $s }

if ($OutIco) {
  $images = New-Object 'System.Collections.Generic.List[byte[]]'
  foreach ($s in $sizes) { $images.Add([byte[]](DibBytes $frames[$s] $s)) }
  $fs = [System.IO.File]::Create($OutIco); $w = New-Object System.IO.BinaryWriter $fs
  $w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$sizes.Count)
  $offset = 6 + 16 * $sizes.Count
  for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $images[$i].Length
    $w.Write([byte]($(if ($s -ge 256) { 0 } else { $s }))); $w.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $w.Write([byte]0); $w.Write([byte]0); $w.Write([int16]1); $w.Write([int16]32); $w.Write([int]$len); $w.Write([int]$offset)
    $offset += $len
  }
  foreach ($img in $images) { $w.Write([byte[]]$img, 0, $img.Length) }
  $w.Close(); "ico: $OutIco ($((Get-Item $OutIco).Length) bytes)"
}

if ($OutPreview) {
  # 어두운 / 밝은 바탕 위에서 실제 크기 + 16·24·32 확대
  $W = 900; $H = 560
  $dv = New-Object System.Windows.Media.DrawingVisual; $dc = $dv.RenderOpen()
  $dc.DrawRectangle((Solid '#FF1C1F2A'), $null, (NewRect 0 0 $W ($H / 2)))
  $dc.DrawRectangle((Solid '#FFF3F4F7'), $null, (NewRect 0 ($H / 2) $W ($H / 2)))
  foreach ($band in 0, 1) {
    $y0 = $band * $H / 2 + 12; $x = 12
    foreach ($s in 256, 128, 64, 48, 32, 24, 16) { $dc.DrawImage($frames[$s], (NewRect $x ($y0 + (256 - $s)) $s $s)); $x += $s + 14 }
    $zx = $W - 330
    foreach ($z in 16, 24, 32) { $dc.DrawImage((Zoom $frames[$z] $z), (NewRect $zx $y0 96 96)); $zx += 106 }
  }
  $dc.Close()
  $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap $W, $H, 96, 96, ([System.Windows.Media.PixelFormats]::Pbgra32)
  $rtb.Render($dv)
  [System.IO.File]::WriteAllBytes($OutPreview, (PngBytes $rtb)); "preview: $OutPreview"
}
