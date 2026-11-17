# 로비 마을 스프라이트 생성기(잔디, 길, 탑 입구, 건물 3채, 우물, 가로등, 나무, 울타리)
# 실행: pwsh -File Tools\VillageGen\GenerateVillage.ps1
# 결과: Assets\4.Sprite\Village\*.png (100픽셀 = 1유닛). Prop_*은 아래에서 14픽셀이 피벗(발밑)이다.
# 길 그림(Ground_Paths)의 좌표는 마을 배치(TowerBootstrap.BuildLobbyScene)와 맞춰야 한다.
param(
    [string]$OutRoot = (Join-Path $PSScriptRoot "..\..\Assets\4.Sprite\Village")
)

Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $OutRoot | Out-Null

$Ink = [System.Drawing.Color]::FromArgb(255, 28, 28, 28)
$rng = New-Object System.Random 11

function C($r, $g, $b, $a = 255) { return [System.Drawing.Color]::FromArgb([int]$a, [int][Math]::Max(0, [Math]::Min(255, $r)), [int][Math]::Max(0, [Math]::Min(255, $g)), [int][Math]::Max(0, [Math]::Min(255, $b))) }
function Br($c) { return New-Object System.Drawing.SolidBrush($c) }
function InkPen([double]$w) { $p = New-Object System.Drawing.Pen($Ink, [single]$w); $p.LineJoin = 'Round'; $p.StartCap = 'Round'; $p.EndCap = 'Round'; return $p }
function ColorPen($c, [double]$w) { $p = New-Object System.Drawing.Pen($c, [single]$w); $p.LineJoin = 'Round'; $p.StartCap = 'Round'; $p.EndCap = 'Round'; return $p }
function P([double]$x, [double]$y) { return [System.Drawing.PointF]::new([single]$x, [single]$y) }

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

function FillInk($g, $path, $color, [double]$w = 5) {
    $g.FillPath((Br $color), $path)
    $g.DrawPath((InkPen $w), $path)
}

function Poly([double[]]$pts) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $list = New-Object 'System.Collections.Generic.List[System.Drawing.PointF]'
    for ($i = 0; $i -lt $pts.Count; $i += 2) { $list.Add((P $pts[$i] $pts[$i + 1])) }
    $path.AddPolygon($list.ToArray())
    return $path
}

function Shadow($g, [double]$cx, [double]$by, [double]$w) {
    $g.FillEllipse((Br (C 0 0 0 55)), [single]($cx - $w / 2), [single]($by - 14), [single]$w, 26)
}

# 짧은 풀 포기(패키지 맵처럼 세 가닥)
function Tuft($g, [double]$x, [double]$y, $color) {
    $pen = ColorPen $color 5
    $g.DrawLine($pen, [single]$x, [single]$y, [single]($x - 4), [single]($y - 12))
    $g.DrawLine($pen, [single]($x + 7), [single]$y, [single]($x + 7), [single]($y - 15))
    $g.DrawLine($pen, [single]($x + 14), [single]$y, [single]($x + 18), [single]($y - 12))
}

# ------------------------------------------------------------ 잔디 타일 256x256
$W = 256; $H = 256
$cv = NewCanvas $W $H
$cv.g.Clear((C 100 134 64))
for ($i = 0; $i -lt 7; $i++) {
    $x = $rng.Next(0, $W); $y = $rng.Next(0, $H); $rw = $rng.Next(60, 120); $rh = $rng.Next(40, 80)
    foreach ($ox in @(-$W, 0, $W)) { foreach ($oy in @(-$H, 0, $H)) {
        $cv.g.FillEllipse((Br (C 106 142 68)), [single]($x + $ox - $rw / 2), [single]($y + $oy - $rh / 2), [single]$rw, [single]$rh)
    } }
}
for ($i = 0; $i -lt 5; $i++) {
    $x = $rng.Next(20, $W - 40); $y = $rng.Next(30, $H - 10)
    Tuft $cv.g $x $y (C 78 110 50)
}
Save $cv "Ground_Grass"

# ------------------------------------------------------------ 흙길 1840x1000 (마을 전체, 방 크기 18.4x10)
# 세계 좌표 (wx, wy) -> 그림 좌표 (920 + 100wx, 500 - 100wy)
function WX([double]$wx) { return 920 + 100 * $wx }
function WY([double]$wy) { return 500 - 100 * $wy }
$cv = NewCanvas 1840 1000
$g = $cv.g
$edge = C 168 132 88
$dirt = C 198 164 114
$plaza = @((WX 0), (WY -0.6))
$targets = @(
    @(0, 2.4),      # 탑 입구
    @(-6.0, -0.2),  # 해금 상점
    @(6.0, -0.2),   # 도서관
    @(5.8, -4.1),   # 미용실
    @(-5.6, -3.9)   # 우물
)
foreach ($pass in 0..1) {
    $w = 150; $col = $edge
    if ($pass -eq 1) { $w = 120; $col = $dirt }
    foreach ($t in $targets) {
        $tx = WX $t[0]; $ty = WY $t[1]
        $mx = ($plaza[0] + $tx) / 2; $my = ($plaza[1] + $ty) / 2 + 30
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $path.AddCurve([System.Drawing.PointF[]]@((P $plaza[0] $plaza[1]), (P $mx $my), (P $tx $ty)), 0.5)
        $g.DrawPath((ColorPen $col $w), $path)
        $g.FillEllipse((Br $col), [single]($tx - $w * 0.75), [single]($ty - $w * 0.5), [single]($w * 1.5), [single]$w)
    }
    $pw = 620 + 30 * (1 - $pass); $ph = 330 + 30 * (1 - $pass)
    $g.FillEllipse((Br $col), [single]($plaza[0] - $pw / 2), [single]($plaza[1] - $ph / 2), [single]$pw, [single]$ph)
}
# 길 위 자갈
for ($i = 0; $i -lt 70; $i++) {
    $t = $targets[$rng.Next(0, $targets.Count)]
    $k = $rng.NextDouble()
    $x = $plaza[0] + ((WX $t[0]) - $plaza[0]) * $k + $rng.Next(-40, 41)
    $y = $plaza[1] + ((WY $t[1]) - $plaza[1]) * $k + $rng.Next(-30, 31)
    $s = $rng.Next(8, 16)
    $g.FillEllipse((Br (C 176 142 98)), [single]$x, [single]$y, [single]$s, [single]($s * 0.7))
}
Save $cv "Ground_Paths"

# ------------------------------------------------------------ 탑 입구 400x290
$cv = NewCanvas 400 290
$g = $cv.g
Shadow $g 200 276 380
$stone = C 168 160 156
$stoneDark = C 140 132 130
# 몸체(위로 갈수록 좁아지는 탑 밑동)
$body = Poly @(30, 276, 50, 70, 350, 70, 370, 276)
FillInk $g $body $stone
$old = $g.Clip; $g.SetClip($body)
for ($row = 0; $row -lt 8; $row++) {
    $y = 76 + $row * 26
    $off = 0; if ($row % 2 -eq 1) { $off = 30 }
    for ($bx = 20 + $off; $bx -lt 380; $bx += 60) {
        $g.DrawRectangle((ColorPen $stoneDark 3), [single]$bx, [single]$y, 56, 22)
    }
}
$g.Clip = $old
$g.DrawPath((InkPen 5), $body)
# 성가퀴
for ($mx = 46; $mx -lt 350; $mx += 52) {
    $merlon = RoundRect $mx 30 36 46 4
    FillInk $g $merlon (C 180 172 168)
}
$g.FillRectangle((Br (C 150 142 140)), 44, 64, 312, 14)
$g.DrawRectangle((InkPen 5), 44, 64, 312, 14)
# 깃발
foreach ($fx in @(70, 290)) {
    $banner = Poly @($fx, 92, ($fx + 40), 92, ($fx + 40), 196, ($fx + 20), 180, $fx, 196)
    FillInk $g $banner (C 170 60 70)
    $g.FillEllipse((Br (C 240 200 90)), [single]($fx + 10), 118, 20, 20)
    $g.DrawEllipse((InkPen 3), [single]($fx + 10), 118, 20, 20)
    $g.FillRectangle((Br (C 96 70 50)), [single]($fx - 4), 86, 48, 8)
    $g.DrawRectangle((InkPen 3), [single]($fx - 4), 86, 48, 8)
}
# 아치 문(안쪽에 보라빛)
$arch = New-Object System.Drawing.Drawing2D.GraphicsPath
$arch.AddArc(132, 108, 136, 120, 180, 180)
$arch.AddLine(268, 168, 268, 276)
$arch.AddLine(268, 276, 132, 276)
$arch.CloseFigure()
$g.FillPath((Br (C 38 30 52)), $arch)
$old = $g.Clip; $g.SetClip($arch)
$g.FillEllipse((Br (C 110 70 170 150)), 150, 200, 100, 110)
$g.FillEllipse((Br (C 170 130 230 120)), 172, 230, 56, 60)
$g.Clip = $old
$g.DrawPath((InkPen 6), $arch)
$frame = New-Object System.Drawing.Drawing2D.GraphicsPath
$frame.AddArc(116, 92, 168, 152, 180, 180)
$g.DrawPath((ColorPen (C 120 112 110) 10), $frame)
$g.DrawPath((InkPen 4), $frame)
# 계단
$step1 = RoundRect 110 262 180 18 4
FillInk $g $step1 (C 150 142 140) 4
Save $cv "Prop_TowerGate"

# ------------------------------------------------------------ 집 280x270 (지붕 색과 간판 그림만 다르다)
function DrawHouse([string]$name, $roof, $roofDark, [scriptblock]$sign, [bool]$pole) {
    $cv = NewCanvas 280 270
    $g = $cv.g
    Shadow $g 140 256 260
    # 벽
    $wall = RoundRect 34 118 212 138 6
    FillInk $g $wall (C 236 220 190)
    $beam = C 130 92 62
    $g.FillRectangle((Br $beam), 34, 118, 212, 10)
    foreach ($bx in @(34, 134, 236)) { $g.FillRectangle((Br $beam), [single]$bx, 118, 10, 138) }
    $g.DrawPath((InkPen 5), $wall)
    # 창문
    foreach ($wx in @(54, 186)) {
        $win = RoundRect $wx 150 40 40 5
        FillInk $g $win (C 170 210 240) 4
        $g.DrawLine((InkPen 3), [single]($wx + 20), 152, [single]($wx + 20), 188)
        $g.DrawLine((InkPen 3), [single]($wx + 2), 170, [single]($wx + 38), 170)
    }
    # 문
    $door = New-Object System.Drawing.Drawing2D.GraphicsPath
    $door.AddArc(116, 176, 48, 40, 180, 180)
    $door.AddLine(164, 196, 164, 256)
    $door.AddLine(164, 256, 116, 256)
    $door.CloseFigure()
    FillInk $g $door (C 150 98 62) 4
    $g.DrawLine((ColorPen (C 118 76 48) 3), 140, 182, 140, 254)
    $g.FillEllipse((Br (C 240 200 90)), 152, 218, 7, 7)
    # 지붕
    $roofPath = Poly @(12, 132, 140, 22, 268, 132)
    FillInk $g $roofPath $roof
    $old = $g.Clip; $g.SetClip($roofPath)
    for ($ry = 50; $ry -lt 132; $ry += 22) { $g.DrawLine((ColorPen $roofDark 4), 0, [single]$ry, 280, [single]$ry) }
    $g.Clip = $old
    $g.DrawPath((InkPen 5), $roofPath)
    $chimney = RoundRect 196 40 26 46 3
    FillInk $g $chimney (C 160 150 146) 4
    # 간판(문 위)
    $board = RoundRect 98 98 84 48 8
    FillInk $g $board (C 196 150 100) 4
    & $sign $g
    if ($pole) {
        # 이발소 기둥(빨강·흰 줄무늬)
        $poleRect = RoundRect 246 150 22 96 10
        $g.FillPath((Br (C 245 245 245)), $poleRect)
        $old = $g.Clip; $g.SetClip($poleRect)
        for ($sy = 130; $sy -lt 260; $sy += 24) {
            $stripe = Poly @(246, $sy, 268, ($sy - 14), 268, ($sy - 4), 246, ($sy + 10))
            $g.FillPath((Br (C 220 60 60)), $stripe)
        }
        $g.Clip = $old
        $g.DrawPath((InkPen 4), $poleRect)
        $g.FillEllipse((Br (C 120 140 170)), 245, 140, 24, 16)
        $g.DrawEllipse((InkPen 3), 245, 140, 24, 16)
    }
    Save $cv $name
}

# 해금 상점: 보라 지붕, 영혼석(보석) 간판
DrawHouse "Prop_HouseShop" (C 130 90 180) (C 110 72 156) {
    param($g)
    $gem = Poly @(140, 104, 158, 118, 140, 140, 122, 118)
    FillInk $g $gem (C 190 130 255) 3
    $g.FillPolygon((Br (C 230 200 255)), [System.Drawing.PointF[]]@((P 140 108), (P 150 117), (P 140 120), (P 130 117)))
} $false

# 도서관: 파란 지붕, 펼친 책 간판
DrawHouse "Prop_HouseLibrary" (C 70 120 190) (C 56 100 166) {
    param($g)
    $left = Poly @(112, 108, 140, 114, 140, 138, 112, 132)
    $right = Poly @(140, 114, 168, 108, 168, 132, 140, 138)
    FillInk $g $left (C 245 240 225) 3
    FillInk $g $right (C 245 240 225) 3
    foreach ($ly in @(118, 124)) {
        $g.DrawLine((ColorPen (C 150 140 130) 2), 117, [single]$ly, 135, [single]($ly + 3))
        $g.DrawLine((ColorPen (C 150 140 130) 2), 145, [single]($ly + 3), 163, [single]$ly)
    }
} $false

# 미용실: 초록 지붕, 가위 간판, 이발소 기둥
DrawHouse "Prop_HouseBarber" (C 90 160 110) (C 72 136 92) {
    param($g)
    $blade = InkPen 5
    $g.DrawLine($blade, 124, 108, 156, 134)
    $g.DrawLine($blade, 156, 108, 124, 134)
    $g.DrawLine((ColorPen (C 210 215 225) 3), 124, 108, 156, 134)
    $g.DrawLine((ColorPen (C 210 215 225) 3), 156, 108, 124, 134)
    $g.FillEllipse((Br (C 220 70 70)), 112, 128, 16, 14); $g.DrawEllipse((InkPen 3), 112, 128, 16, 14)
    $g.FillEllipse((Br (C 220 70 70)), 152, 128, 16, 14); $g.DrawEllipse((InkPen 3), 152, 128, 16, 14)
} $true

# ------------------------------------------------------------ 우물 140x150
$cv = NewCanvas 140 150
$g = $cv.g
Shadow $g 70 136 130
$base = RoundRect 16 86 108 50 12
FillInk $g $base (C 160 152 148)
$g.FillEllipse((Br (C 40 60 90)), 26, 80, 88, 22)
$g.DrawEllipse((InkPen 4), 26, 80, 88, 22)
foreach ($px in @(22, 110)) { $g.FillRectangle((Br (C 130 92 62)), [single]$px, 26, 10, 64); $g.DrawRectangle((InkPen 4), [single]$px, 26, 10, 64) }
$wellRoof = Poly @(8, 34, 70, 6, 132, 34, 120, 42, 20, 42)
FillInk $g $wellRoof (C 170 70 60)
$g.DrawLine((InkPen 3), 70, 42, 70, 66)
$bucket = RoundRect 60 64 20 18 3
FillInk $g $bucket (C 150 100 66) 3
Save $cv "Prop_Well"

# ------------------------------------------------------------ 가로등 64x170
$cv = NewCanvas 64 170
$g = $cv.g
Shadow $g 32 158 46
$g.FillEllipse((Br (C 255 230 140 70)), 2, 6, 60, 60)
$g.FillRectangle((Br (C 60 60 70)), 28, 52, 9, 102)
$g.DrawRectangle((InkPen 4), 28, 52, 9, 102)
$foot = RoundRect 18 146 28 12 3
FillInk $g $foot (C 70 70 80) 4
$lantern = RoundRect 18 22 28 32 5
FillInk $g $lantern (C 255 220 120) 4
$cap = Poly @(12, 24, 32, 10, 52, 24)
FillInk $g $cap (C 60 60 70) 4
Save $cv "Prop_Lamp"

# ------------------------------------------------------------ 나무 180x220
$cv = NewCanvas 180 220
$g = $cv.g
Shadow $g 90 206 120
$trunk = Poly @(78, 206, 82, 130, 98, 130, 104, 206)
FillInk $g $trunk (C 130 90 60)
$canopy = New-Object System.Drawing.Drawing2D.GraphicsPath
$canopy.AddClosedCurve([System.Drawing.PointF[]]@((P 22 120), (P 18 70), (P 56 26), (P 100 14), (P 146 40), (P 164 88), (P 150 134), (P 96 150), (P 46 146)), 0.5)
$g.FillPath((Br (C 84 140 70)), $canopy)
$old = $g.Clip; $g.SetClip($canopy)
$g.FillEllipse((Br (C 104 164 84)), 40, 22, 90, 70)
$g.FillEllipse((Br (C 70 120 60)), 70, 96, 110, 70)
$g.Clip = $old
$g.DrawPath((InkPen 5), $canopy)
Save $cv "Prop_Tree"

# ------------------------------------------------------------ 울타리 128x80 (가로로 이어 붙임)
$cv = NewCanvas 128 80
$g = $cv.g
$wood = C 176 128 84
foreach ($ry in @(30, 52)) {
    $g.FillRectangle((Br $wood), 0, [single]$ry, 128, 10)
    $g.DrawLine((InkPen 3), 0, [single]$ry, 128, [single]$ry)
    $g.DrawLine((InkPen 3), 0, [single]($ry + 10), 128, [single]($ry + 10))
}
foreach ($px in @(10, 74)) {
    $post = Poly @($px, 72, $px, 18, ($px + 11), 8, ($px + 22), 18, ($px + 22), 72)
    FillInk $g $post (C 160 112 72) 4
}
Save $cv "Prop_Fence"

"village sprites -> $OutRoot"
