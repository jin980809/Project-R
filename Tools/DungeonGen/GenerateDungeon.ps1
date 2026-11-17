# 던전 방 스프라이트 생성기(바닥, 벽, 문, 횃불, 장애물)
# 실행: pwsh -File Tools\DungeonGen\GenerateDungeon.ps1
# 결과: Assets\4.Sprite\Dungeon\*.png (100픽셀 = 1유닛)
# 스타일: 패키지와 같은 굵은 어두운 외곽선(28,28,28)과 단색 면. 바닥 타일은 외곽선 대신 어두운 줄눈을 쓴다.
param(
    [string]$OutRoot = (Join-Path $PSScriptRoot "..\..\Assets\4.Sprite\Dungeon")
)

Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $OutRoot | Out-Null

$Ink = [System.Drawing.Color]::FromArgb(255, 28, 28, 28)
$rng = New-Object System.Random 7

function C($r, $g, $b, $a = 255) { return [System.Drawing.Color]::FromArgb([int]$a, [int][Math]::Max(0, [Math]::Min(255, $r)), [int][Math]::Max(0, [Math]::Min(255, $g)), [int][Math]::Max(0, [Math]::Min(255, $b))) }
function Br($c) { return New-Object System.Drawing.SolidBrush($c) }
function InkPen([double]$w) { $p = New-Object System.Drawing.Pen($Ink, [single]$w); $p.LineJoin = 'Round'; $p.StartCap = 'Round'; $p.EndCap = 'Round'; return $p }
function ColorPen($c, [double]$w) { $p = New-Object System.Drawing.Pen($c, [single]$w); $p.LineJoin = 'Round'; $p.StartCap = 'Round'; $p.EndCap = 'Round'; return $p }

function NewCanvas([int]$w, [int]$h) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    return @{ bmp = $bmp; g = $g }
}

function Save($canvas, [string]$name) {
    $canvas.g.Dispose()
    $canvas.bmp.Save((Join-Path $OutRoot ($name + ".png")), [System.Drawing.Imaging.ImageFormat]::Png)
    $canvas.bmp.Dispose()
}

function RoundRect([double]$x, [double]$y, [double]$w, [double]$h, [double]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = [single](2 * $r)
    $path.AddArc([single]$x, [single]$y, $d, $d, 180, 90)
    $path.AddArc([single]($x + $w - $d), [single]$y, $d, $d, 270, 90)
    $path.AddArc([single]($x + $w - $d), [single]($y + $h - $d), $d, $d, 0, 90)
    $path.AddArc([single]$x, [single]($y + $h - $d), $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

# 화면 밖으로 넘친 도형을 반대편에도 그려 이음새 없는 타일을 만든다.
function WrapFill($g, $brush, $path, [int]$w, [int]$h) {
    foreach ($ox in @(-$w, 0, $w)) {
        foreach ($oy in @(-$h, 0, $h)) {
            $copy = $path.Clone()
            $m = New-Object System.Drawing.Drawing2D.Matrix
            $m.Translate([single]$ox, [single]$oy)
            $copy.Transform($m)
            $g.FillPath($brush, $copy)
        }
    }
}

# ------------------------------------------------------------ 바닥 타일 256x256 (2.56유닛)
# 줄눈(어두운 바탕) 위에 줄마다 폭이 다른 판석을 깐다. 판석은 위쪽이 밝고 아래쪽이 어둡다.
$W = 256; $H = 256
$cv = NewCanvas $W $H
$grout = C 78 74 76
$cv.g.Clear($grout)
$rowH = 64
for ($row = 0; $row -lt 4; $row++) {
    $x = ($row * 37) % 64
    $y = $row * $rowH
    $widths = @(96, 80, 80)
    if ($row % 2 -eq 1) { $widths = @(64, 112, 80) }
    foreach ($bw in $widths) {
        $shade = $rng.Next(-10, 11)
        $base = C (126 + $shade) (120 + $shade) (118 + $shade)
        $light = C (138 + $shade) (132 + $shade) (130 + $shade)
        $dark = C (108 + $shade) (102 + $shade) (102 + $shade)
        $stone = RoundRect ($x + 3) ($y + 3) ($bw - 6) ($rowH - 6) 8
        WrapFill $cv.g (Br $dark) $stone $W $H
        $top = RoundRect ($x + 3) ($y + 3) ($bw - 6) ($rowH - 12) 8
        WrapFill $cv.g (Br $base) $top $W $H
        $hl = RoundRect ($x + 9) ($y + 7) ($bw - 18) 6 3
        WrapFill $cv.g (Br $light) $hl $W $H
        # 가끔 금이 간 판석
        if ($rng.Next(0, 7) -eq 0) {
            $cx = $x + $bw * 0.3; $cy = $y + 20
            $crack = New-Object System.Drawing.Drawing2D.GraphicsPath
            $crack.AddLines([System.Drawing.PointF[]]@([System.Drawing.PointF]::new($cx, $cy), [System.Drawing.PointF]::new($cx + 10, $cy + 9), [System.Drawing.PointF]::new($cx + 6, $cy + 18), [System.Drawing.PointF]::new($cx + 16, $cy + 26)))
            $pen = ColorPen (C 96 90 90) 2.5
            foreach ($ox in @(-$W, 0, $W)) { foreach ($oy in @(-$H, 0, $H)) { $c2 = $crack.Clone(); $m = New-Object System.Drawing.Drawing2D.Matrix; $m.Translate($ox, $oy); $c2.Transform($m); $cv.g.DrawPath($pen, $c2) } }
        }
        $x += $bw
    }
}
Save $cv "Floor"

# ------------------------------------------------------------ 북쪽 벽 정면 128x140 (가로로 이어 붙임)
# 위 30픽셀은 벽 윗면(위에서 본 돌), 아래는 벽돌 정면, 맨 아래는 바닥에 지는 그림자.
$W = 128; $H = 140
$cv = NewCanvas $W $H
$cv.g.Clear((C 104 96 92))
$capH = 30
$cv.g.FillRectangle((Br (C 92 86 84)), 0, 0, $W, $capH)
for ($i = 0; $i -lt 2; $i++) {
    $cap = RoundRect ($i * 64 + 3) 4 58 ($capH - 10) 6
    $cv.g.FillPath((Br (C 124 116 112)), $cap)
}
$cv.g.FillRectangle((Br $Ink), 0, $capH - 3, $W, 5)
$brickH = 26
for ($row = 0; $row -lt 4; $row++) {
    $y = $capH + 2 + $row * $brickH
    $offset = 0
    if ($row % 2 -eq 1) { $offset = -32 }
    for ($bx = $offset; $bx -lt $W; $bx += 64) {
        $shade = $rng.Next(-8, 9)
        $brick = RoundRect ($bx + 3) ($y + 3) 58 ($brickH - 6) 5
        WrapFill $cv.g (Br (C (146 + $shade) (134 + $shade) (124 + $shade))) $brick $W 10000
        $hl = RoundRect ($bx + 8) ($y + 5) 46 4 2
        WrapFill $cv.g (Br (C (162 + $shade) (150 + $shade) (140 + $shade))) $hl $W 10000
    }
}
$shadowTop = $capH + 2 + 4 * $brickH
$cv.g.FillRectangle((Br (C 28 28 28 200)), 0, $shadowTop, $W, $H - $shadowTop)
Save $cv "WallFront"

# ------------------------------------------------------------ 옆·아래 벽 윗면 128x70 (가로로 이어 붙임, 옆벽은 90도 돌려 쓴다)
# 방 쪽(아래 가장자리)에 굵은 외곽선.
$W = 128; $H = 70
$cv = NewCanvas $W $H
$cv.g.Clear((C 92 86 84))
for ($i = 0; $i -lt 2; $i++) {
    $shade = $rng.Next(-6, 7)
    $cap = RoundRect ($i * 64 + 4) 6 56 ($H - 18) 8
    $cv.g.FillPath((Br (C (124 + $shade) (116 + $shade) (112 + $shade))), $cap)
    $hl = RoundRect ($i * 64 + 10) 10 44 6 3
    $cv.g.FillPath((Br (C (140 + $shade) (132 + $shade) (128 + $shade))), $hl)
}
$cv.g.FillRectangle((Br $Ink), 0, $H - 6, $W, 6)
Save $cv "WallRim"

# ------------------------------------------------------------ 북쪽 문(아치) 220x200, 열림/닫힘
function DrawArch([bool]$closed) {
    $W = 220; $H = 200
    $cv = NewCanvas $W $H
    $g = $cv.g
    # 바깥 돌 틀
    $outer = New-Object System.Drawing.Drawing2D.GraphicsPath
    $outer.AddArc(14, 10, 192, 180, 180, 180)
    $outer.AddLine(206, 100, 206, 196)
    $outer.AddLine(206, 196, 14, 196)
    $outer.CloseFigure()
    $g.FillPath((Br (C 150 140 132)), $outer)
    $g.DrawPath((InkPen 5), $outer)
    # 틀의 돌 이음
    $pen = ColorPen (C 112 104 98) 3
    foreach ($a in @(200, 230, 270, 310, 340)) {
        $rad = $a * [Math]::PI / 180
        $g.DrawLine($pen, [single](110 + 96 * [Math]::Cos($rad)), [single](100 + 90 * [Math]::Sin($rad)), [single](110 + 68 * [Math]::Cos($rad)), [single](100 + 64 * [Math]::Sin($rad)))
    }
    # 안쪽 통로(어둠)
    $inner = New-Object System.Drawing.Drawing2D.GraphicsPath
    $inner.AddArc(42, 36, 136, 128, 180, 180)
    $inner.AddLine(178, 100, 178, 196)
    $inner.AddLine(178, 196, 42, 196)
    $inner.CloseFigure()
    $g.FillPath((Br (C 34 30 38)), $inner)
    $g.FillRectangle((Br (C 54 48 58)), 42, 150, 136, 46)
    if ($closed) {
        # 쇠창살 내리닫이
        $bar = Br (C 96 98 110)
        $old = $g.Clip
        $g.SetClip($inner)
        for ($bx = 54; $bx -lt 178; $bx += 26) {
            $g.FillRectangle($bar, $bx, 30, 10, 170)
            $g.DrawRectangle((InkPen 3), $bx, 30, 10, 170)
        }
        foreach ($by in @(80, 140)) {
            $g.FillRectangle($bar, 42, $by, 136, 10)
            $g.DrawRectangle((InkPen 3), 42, $by, 136, 10)
        }
        $g.Clip = $old
    }
    $g.DrawPath((InkPen 5), $inner)
    return $cv
}
Save (DrawArch $false) "GateOpen"
Save (DrawArch $true) "GateClosed"

# ------------------------------------------------------------ 동·서·남쪽 문(위에서 본 문턱) 220x90, 열림/닫힘
# 가로 방향 기준(남쪽). 동서쪽은 90도 돌려 쓴다. 위쪽이 방 안쪽이다.
function DrawThreshold([bool]$closed) {
    $W = 220; $H = 90
    $cv = NewCanvas $W $H
    $g = $cv.g
    $gap = RoundRect 30 8 160 74 6
    $g.FillPath((Br (C 54 48 58)), $gap)
    $g.FillRectangle((Br (C 34 30 38)), 34, 44, 152, 38)
    $g.DrawPath((InkPen 4), $gap)
    if ($closed) {
        $bar = Br (C 96 98 110)
        for ($bx = 44; $bx -lt 186; $bx += 24) {
            $g.FillRectangle($bar, $bx, 14, 10, 62)
            $g.DrawRectangle((InkPen 3), $bx, 14, 10, 62)
        }
    }
    # 양쪽 기둥(위에서 본 사각 돌)
    foreach ($px in @(4, 176)) {
        $post = RoundRect $px 4 40 82 8
        $g.FillPath((Br (C 150 140 132)), $post)
        $top = RoundRect ($px + 6) 10 28 30 5
        $g.FillPath((Br (C 170 160 152)), $top)
        $g.DrawPath((InkPen 5), $post)
    }
    return $cv
}
Save (DrawThreshold $false) "DoorOpen"
Save (DrawThreshold $true) "DoorClosed"

# ------------------------------------------------------------ 벽 횃불 64x112, 4프레임(불꽃이 흔들린다)
for ($f = 0; $f -lt 4; $f++) {
    $cv = NewCanvas 64 112
    $g = $cv.g
    # 받침
    $g.FillRectangle((Br (C 96 70 50)), 26, 56, 12, 50)
    $g.DrawRectangle((InkPen 4), 26, 56, 12, 50)
    $cup = New-Object System.Drawing.Drawing2D.GraphicsPath
    $cup.AddPolygon([System.Drawing.PointF[]]@([System.Drawing.PointF]::new(14, 48), [System.Drawing.PointF]::new(50, 48), [System.Drawing.PointF]::new(42, 64), [System.Drawing.PointF]::new(22, 64)))
    $g.FillPath((Br (C 110 112 124)), $cup)
    $g.DrawPath((InkPen 4), $cup)
    # 불꽃: 프레임마다 끝이 좌우로 흔들리고 높이가 바뀐다.
    $sway = @(0, 5, 0, -5)[$f]
    $tall = @(0, 4, 8, 3)[$f]
    $outer = New-Object System.Drawing.Drawing2D.GraphicsPath
    $outer.AddClosedCurve([System.Drawing.PointF[]]@([System.Drawing.PointF]::new(32 + $sway, 4 - $tall + 8), [System.Drawing.PointF]::new(48, 34), [System.Drawing.PointF]::new(42, 48), [System.Drawing.PointF]::new(22, 48), [System.Drawing.PointF]::new(16, 34)), 0.6)
    $g.FillPath((Br (C 245 140 50)), $outer)
    $g.DrawPath((InkPen 4), $outer)
    $inner = New-Object System.Drawing.Drawing2D.GraphicsPath
    $inner.AddClosedCurve([System.Drawing.PointF[]]@([System.Drawing.PointF]::new(32 + $sway * 0.6, 20 - $tall * 0.6 + 4), [System.Drawing.PointF]::new(40, 38), [System.Drawing.PointF]::new(32, 46), [System.Drawing.PointF]::new(24, 38)), 0.6)
    $g.FillPath((Br (C 255 226 120)), $inner)
    Save $cv ("Torch_" + $f.ToString("00"))
}

# ------------------------------------------------------------ 장애물(발밑 8픽셀이 피벗)
# 돌기둥 112x210
$cv = NewCanvas 112 210
$g = $cv.g
$g.FillEllipse((Br (C 0 0 0 60)), 8, 180, 96, 26)
$g.FillRectangle((Br (C 150 140 132)), 22, 34, 68, 156)
$g.FillRectangle((Br (C 170 160 152)), 30, 34, 14, 156)
$g.FillRectangle((Br (C 128 118 112)), 72, 34, 18, 156)
$g.DrawRectangle((InkPen 5), 22, 34, 68, 156)
foreach ($ly in @(84, 136)) { $g.DrawLine((ColorPen (C 112 104 98) 3), 24, $ly, 88, $ly) }
$cap = RoundRect 10 10 92 30 8
$g.FillPath((Br (C 160 150 142)), $cap); $g.DrawPath((InkPen 5), $cap)
$foot = RoundRect 12 178 88 22 6
$g.FillPath((Br (C 140 130 124)), $foot); $g.DrawPath((InkPen 5), $foot)
Save $cv "Prop_Pillar"

# 바위 140x112
$cv = NewCanvas 140 112
$g = $cv.g
$g.FillEllipse((Br (C 0 0 0 60)), 10, 84, 120, 24)
$rock = New-Object System.Drawing.Drawing2D.GraphicsPath
$rock.AddClosedCurve([System.Drawing.PointF[]]@([System.Drawing.PointF]::new(16, 96), [System.Drawing.PointF]::new(14, 60), [System.Drawing.PointF]::new(40, 24), [System.Drawing.PointF]::new(80, 14), [System.Drawing.PointF]::new(116, 36), [System.Drawing.PointF]::new(126, 80), [System.Drawing.PointF]::new(110, 100), [System.Drawing.PointF]::new(60, 104)), 0.45)
$g.FillPath((Br (C 132 128 136)), $rock)
$old = $g.Clip; $g.SetClip($rock)
$g.FillEllipse((Br (C 156 152 160)), 30, 18, 70, 40)
$g.FillEllipse((Br (C 112 108 118)), 60, 66, 90, 60)
$g.Clip = $old
$g.DrawPath((InkPen 5), $rock)
$g.DrawLine((ColorPen (C 96 92 102) 3), 70, 40, 82, 62)
Save $cv "Prop_Rock"

# 나무 상자 120x128
$cv = NewCanvas 120 128
$g = $cv.g
$g.FillEllipse((Br (C 0 0 0 60)), 6, 106, 108, 20)
$g.FillRectangle((Br (C 186 132 80)), 12, 14, 96, 30)
$g.DrawRectangle((InkPen 5), 12, 14, 96, 30)
$g.FillRectangle((Br (C 160 108 62)), 12, 44, 96, 70)
foreach ($ly in @(66, 90)) { $g.DrawLine((ColorPen (C 120 80 46) 3), 14, $ly, 106, $ly) }
$g.DrawLine((InkPen 4), 16, 48, 104, 110)
$g.DrawRectangle((InkPen 5), 12, 44, 96, 70)
Save $cv "Prop_Crate"

# 나무통 100x128
$cv = NewCanvas 100 128
$g = $cv.g
$g.FillEllipse((Br (C 0 0 0 60)), 6, 106, 88, 20)
$body = RoundRect 12 22 76 94 18
$g.FillPath((Br (C 170 104 70)), $body)
$old = $g.Clip; $g.SetClip($body)
$g.FillRectangle((Br (C 190 124 86)), 22, 22, 14, 94)
$g.FillRectangle((Br (C 110 112 124)), 12, 42, 76, 8)
$g.FillRectangle((Br (C 110 112 124)), 12, 88, 76, 8)
$g.Clip = $old
$g.DrawPath((InkPen 5), $body)
$g.FillEllipse((Br (C 140 84 56)), 14, 10, 72, 26)
$g.FillEllipse((Br (C 120 72 48)), 24, 15, 52, 16)
$g.DrawEllipse((InkPen 5), 14, 10, 72, 26)
Save $cv "Prop_Barrel"

"dungeon sprites -> $OutRoot"
