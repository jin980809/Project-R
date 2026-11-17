# 스킬 이펙트 스프라이트 생성기(프레임 애니메이션 + 스킬 아이콘)
# 실행: pwsh -File Tools\SkillFxGen\GenerateSkillFx.ps1
# 결과: Assets\4.Sprite\Fx\Skill\<이름>_00.png ~ _07.png, Icon_<이름>.png (100픽셀 = 1유닛)
# 스타일: 패키지와 같은 굵은 어두운 외곽선(28,28,28)과 흰색·밝은 회색 면. 게임에서 스킬 색(fxColor)을 곱해 물들인다.
# 프레임은 정사각형 256x256이고 그림의 원이 칸을 꽉 채운다(SkillFxPlayer가 반경에 맞춰 늘린다). Wind만 256x96.
param(
    [string]$OutRoot = (Join-Path $PSScriptRoot "..\..\Assets\4.Sprite\Fx\Skill")
)

Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $OutRoot | Out-Null

$Ink = [System.Drawing.Color]::FromArgb(255, 28, 28, 28)
$Frames = 8
$Size = 256
$Half = 128

function C($r, $g, $b, $a = 255) { return [System.Drawing.Color]::FromArgb([int][Math]::Max(0, [Math]::Min(255, $a)), [int][Math]::Max(0, [Math]::Min(255, $r)), [int][Math]::Max(0, [Math]::Min(255, $g)), [int][Math]::Max(0, [Math]::Min(255, $b))) }
function W([double]$a) { return C 255 255 255 ([int](255 * $a)) }
function L([double]$a) { return C 205 205 210 ([int](255 * $a)) }
function InkA([double]$a) { return C 28 28 28 ([int](255 * $a)) }
function Br($c) { return New-Object System.Drawing.SolidBrush($c) }
function Pen($c, [double]$w) { $p = New-Object System.Drawing.Pen($c, [single]$w); $p.LineJoin = 'Round'; $p.StartCap = 'Round'; $p.EndCap = 'Round'; return $p }

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

# 외곽선을 먼저 굵게, 그 위에 면을 칠한다(패키지 스타일).
function InkFill($g, $path, $fill, [double]$ink, [double]$alpha) {
    $g.DrawPath((Pen (InkA $alpha) $ink), $path)
    $g.FillPath((Br $fill), $path)
}

function Ellipse([double]$cx, [double]$cy, [double]$rx, [double]$ry) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddEllipse([single]($cx - $rx), [single]($cy - $ry), [single](2 * $rx), [single](2 * $ry))
    return $p
}

function Poly($points) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pts = New-Object 'System.Drawing.PointF[]' ($points.Count / 2)
    for ($i = 0; $i -lt $pts.Length; $i++) { $pts[$i] = New-Object System.Drawing.PointF([single]$points[2 * $i], [single]$points[2 * $i + 1]) }
    $p.AddPolygon($pts)
    return $p
}

# 가운데 (cx, cy)에서 각도 a(도), 거리 r인 점
function PX([double]$cx, [double]$a, [double]$r) { return $cx + [Math]::Cos($a * [Math]::PI / 180) * $r }
function PY([double]$cy, [double]$a, [double]$r) { return $cy + [Math]::Sin($a * [Math]::PI / 180) * $r }

# 두께 있는 고리 띠(각도 a0부터 sweep만큼)
function ArcBand([double]$cx, [double]$cy, [double]$rOut, [double]$rIn, [double]$a0, [double]$sweep) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc([single]($cx - $rOut), [single]($cy - $rOut), [single](2 * $rOut), [single](2 * $rOut), [single]$a0, [single]$sweep)
    $p.AddArc([single]($cx - $rIn), [single]($cy - $rIn), [single](2 * $rIn), [single](2 * $rIn), [single]($a0 + $sweep), [single](-$sweep))
    $p.CloseFigure()
    return $p
}

# 불꽃 한 줄기(뿌리 (x, y)에서 각도 a 쪽으로 길이 len, 폭 w)
function Flame([double]$x, [double]$y, [double]$a, [double]$len, [double]$w) {
    $tipX = PX $x $a $len; $tipY = PY $y $a $len
    $lx = PX $x ($a - 90) $w; $ly = PY $y ($a - 90) $w
    $rx = PX $x ($a + 90) $w; $ry = PY $y ($a + 90) $w
    $bx = PX $x ($a + 180) ($w * 0.9); $by = PY $y ($a + 180) ($w * 0.9)
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pts = New-Object 'System.Drawing.PointF[]' 4
    $pts[0] = New-Object System.Drawing.PointF([single]$tipX, [single]$tipY)
    $pts[1] = New-Object System.Drawing.PointF([single]$rx, [single]$ry)
    $pts[2] = New-Object System.Drawing.PointF([single]$bx, [single]$by)
    $pts[3] = New-Object System.Drawing.PointF([single]$lx, [single]$ly)
    $p.AddClosedCurve($pts, [single]0.6)
    return $p
}

# 네 갈래 별(반짝임)
function Star4([double]$cx, [double]$cy, [double]$r, [double]$rot) {
    $pts = @()
    for ($i = 0; $i -lt 8; $i++) {
        $rr = $r; if ($i % 2 -eq 1) { $rr = $r * 0.3 }
        $a = $rot + 45 * $i
        $pts += (PX $cx $a $rr); $pts += (PY $cy $a $rr)
    }
    return Poly $pts
}

# 번개 줄기(지그재그)
function Bolt($g, [double]$x0, [double]$y0, [double]$a, [double]$len, [int]$seed, [double]$alpha, [double]$width) {
    $rng = New-Object System.Random $seed
    $pts = New-Object 'System.Drawing.PointF[]' 6
    for ($i = 0; $i -lt 6; $i++) {
        $d = $len * $i / 5
        $side = 0; if ($i -gt 0 -and $i -lt 5) { $side = ($rng.NextDouble() - 0.5) * $len * 0.35 }
        $pts[$i] = New-Object System.Drawing.PointF([single]((PX $x0 $a $d) + (PX 0 ($a + 90) $side)), [single]((PY $y0 $a $d) + (PY 0 ($a + 90) $side)))
    }
    $g.DrawLines((Pen (InkA $alpha) ($width + 8)), $pts)
    $g.DrawLines((Pen (W $alpha) $width), $pts)
}

# 0~1 진행도
function T([int]$f) { return $f / ($Frames - 1) }
function EaseOut([double]$t) { return 1 - (1 - $t) * (1 - $t) }

# ------------------------------------------------------------ Slash: 원을 따라 휘도는 베기 고리
for ($f = 0; $f -lt $Frames; $f++) {
    $t = T $f; $cv = NewCanvas $Size $Size; $g = $cv.g
    $sweep = 40 + 300 * (EaseOut $t)
    $a0 = -90 + 120 * $t
    $thick = 30 * (1 - 0.6 * $t)
    $alpha = 1 - 0.5 * $t
    $band = ArcBand $Half $Half 118 (118 - $thick) $a0 $sweep
    InkFill $g $band (W (0.95 * $alpha)) 7 $alpha
    # 안쪽 잔상
    $trail = ArcBand $Half $Half (112 - $thick) (104 - $thick) ($a0 + 20) ($sweep - 30)
    if ($sweep -gt 60) { $g.FillPath((Br (L (0.6 * $alpha))), $trail) }
    Save $cv ("Slash_{0:00}" -f $f)
}

# ------------------------------------------------------------ Shockwave: 퍼지는 충격파 고리, 금 간 바닥, 튀는 돌 조각
for ($f = 0; $f -lt $Frames; $f++) {
    $t = T $f; $cv = NewCanvas $Size $Size; $g = $cv.g
    $alpha = 1 - 0.55 * $t
    $r = 30 + 92 * (EaseOut $t)
    $th = 22 * (1 - 0.7 * $t) + 4
    # 금(가운데에서 뻗는 짧은 선)
    for ($i = 0; $i -lt 7; $i++) {
        $a = $i * 51 + 10
        $len = 40 + 25 * (($i * 37) % 3)
        $g.DrawLine((Pen (InkA (0.8 * $alpha)) 5), [single]$Half, [single]$Half, [single](PX $Half $a $len), [single](PY $Half $a $len))
    }
    $ring = ArcBand $Half $Half $r ([Math]::Max(1, $r - $th)) 0 359.9
    InkFill $g $ring (L (0.9 * $alpha)) 6 $alpha
    # 돌 조각
    for ($i = 0; $i -lt 6; $i++) {
        $a = $i * 60 + 25
        $d = $r * 0.85
        $x = PX $Half $a $d; $y = (PY $Half $a $d) - 30 * [Math]::Sin($t * [Math]::PI)
        $sz = 9 - 4 * $t
        $rock = Poly @(($x - $sz), $y, $x, ($y - $sz), ($x + $sz), ($y - 2), ($x + 2), ($y + $sz))
        InkFill $g $rock (C 170 160 150 ([int](255 * $alpha))) 4 $alpha
    }
    Save $cv ("Shockwave_{0:00}" -f $f)
}

# ------------------------------------------------------------ Fire: 사방으로 솟구치는 불꽃과 밝은 속
for ($f = 0; $f -lt $Frames; $f++) {
    $t = T $f; $cv = NewCanvas $Size $Size; $g = $cv.g
    $alpha = 1 - 0.4 * $t
    $grow = 0.45 + 0.55 * (EaseOut $t)
    for ($i = 0; $i -lt 9; $i++) {
        $a = $i * 40 + 15 * [Math]::Sin(($f + $i) * 1.3)
        $len = (70 + 25 * [Math]::Sin(($f * 2 + $i) * 0.9)) * $grow
        $root = 28 * $grow
        $fl = Flame (PX $Half $a $root) (PY $Half $a $root) $a $len (18 * $grow)
        InkFill $g $fl (L (0.85 * $alpha)) 6 $alpha
    }
    $core = Ellipse $Half $Half (46 * $grow) (46 * $grow)
    InkFill $g $core (W $alpha) 6 $alpha
    $hot = Ellipse $Half $Half (24 * $grow) (24 * $grow)
    $g.FillPath((Br (C 255 255 235 ([int](255 * $alpha)))), $hot)
    Save $cv ("Fire_{0:00}" -f $f)
}

# ------------------------------------------------------------ Arcane: 도는 마법진 고리와 네 갈래 별
for ($f = 0; $f -lt $Frames; $f++) {
    $t = T $f; $cv = NewCanvas $Size $Size; $g = $cv.g
    $alpha = 1 - 0.45 * $t
    $r = 60 + 55 * (EaseOut $t)
    $ring = ArcBand $Half $Half $r ($r - 10) 0 359.9
    InkFill $g $ring (L (0.85 * $alpha)) 5 $alpha
    # 고리 위의 룬 점
    for ($i = 0; $i -lt 6; $i++) {
        $a = $i * 60 + 45 * $t
        $dot = Star4 (PX $Half $a ($r - 5)) (PY $Half $a ($r - 5)) 14 ($a)
        InkFill $g $dot (W $alpha) 4 $alpha
    }
    $big = Star4 $Half $Half (70 * (1 - 0.5 * $t)) (30 * $t)
    InkFill $g $big (W $alpha) 6 $alpha
    Save $cv ("Arcane_{0:00}" -f $f)
}

# ------------------------------------------------------------ Holy: 내리꽂히는 빛기둥과 바닥의 빛 고리, 떠오르는 반짝임
for ($f = 0; $f -lt $Frames; $f++) {
    $t = T $f; $cv = NewCanvas $Size $Size; $g = $cv.g
    $alpha = 1 - 0.4 * $t
    $base = Ellipse $Half ($Half + 40) (110 * (0.6 + 0.4 * (EaseOut $t))) (56 * (0.6 + 0.4 * (EaseOut $t)))
    InkFill $g $base (L (0.55 * $alpha)) 5 $alpha
    $w = 40 * (1 - 0.5 * $t) + 8
    $pillar = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pillar.AddRectangle((New-Object System.Drawing.RectangleF([single]($Half - $w / 2), 0, [single]$w, [single]($Half + 40))))
    InkFill $g $pillar (W (0.9 * $alpha)) 6 $alpha
    $foot = Ellipse $Half ($Half + 40) ($w * 0.9) ($w * 0.45)
    $g.FillPath((Br (W $alpha)), $foot)
    for ($i = 0; $i -lt 5; $i++) {
        $x = $Half + ($i - 2) * 38
        $y = $Half + 40 - 90 * (($t + $i * 0.2) % 1)
        $sp = Star4 $x $y 12 0
        InkFill $g $sp (W $alpha) 4 $alpha
    }
    Save $cv ("Holy_{0:00}" -f $f)
}

# ------------------------------------------------------------ Heal: 바닥의 부드러운 고리와 떠오르는 십자
for ($f = 0; $f -lt $Frames; $f++) {
    $t = T $f; $cv = NewCanvas $Size $Size; $g = $cv.g
    $alpha = 1 - 0.35 * $t
    $ring = ArcBand $Half $Half 120 108 0 359.9
    InkFill $g $ring (L (0.7 * $alpha)) 5 $alpha
    $g.FillPath((Br (W (0.18 * $alpha))), (Ellipse $Half $Half 108 108))
    for ($i = 0; $i -lt 6; $i++) {
        $a = $i * 60 + 20
        $d = 30 + 45 * (($i * 7) % 3) / 2
        $x = PX $Half $a $d
        $y = (PY $Half $a $d) + 30 - 70 * (($t + $i * 0.17) % 1)
        $pl = 14
        $plus = Poly @(($x - $pl / 3), ($y - $pl), ($x + $pl / 3), ($y - $pl), ($x + $pl / 3), ($y - $pl / 3), ($x + $pl), ($y - $pl / 3), ($x + $pl), ($y + $pl / 3), ($x + $pl / 3), ($y + $pl / 3), ($x + $pl / 3), ($y + $pl), ($x - $pl / 3), ($y + $pl), ($x - $pl / 3), ($y + $pl / 3), ($x - $pl), ($y + $pl / 3), ($x - $pl), ($y - $pl / 3), ($x - $pl / 3), ($y - $pl / 3))
        InkFill $g $plus (W $alpha) 5 $alpha
    }
    Save $cv ("Heal_{0:00}" -f $f)
}

# ------------------------------------------------------------ Aura: 발밑 타원 고리와 위로 흐르는 기운(반복 재생)
for ($f = 0; $f -lt $Frames; $f++) {
    $t = $f / $Frames; $cv = NewCanvas $Size $Size; $g = $cv.g
    $ring = ArcBand $Half ($Half + 60) 100 90 0 359.9
    $m = New-Object System.Drawing.Drawing2D.Matrix
    $m.Translate(0, [single](($Half + 60) * 0.5))
    $m.Scale(1, 0.5)
    $ring.Transform($m)
    InkFill $g $ring (L 0.8) 4 0.9
    for ($i = 0; $i -lt 6; $i++) {
        $x = $Half + [Math]::Cos($i * 1.05) * 80
        $p = ($t + $i / 6) % 1
        $y0 = $Half + 70 - 150 * $p
        $a = [Math]::Sin($p * [Math]::PI)
        $g.DrawLine((Pen (InkA (0.8 * $a)) 12), [single]$x, [single]$y0, [single]$x, [single]($y0 + 30))
        $g.DrawLine((Pen (W (0.9 * $a)) 6), [single]$x, [single]$y0, [single]$x, [single]($y0 + 30))
    }
    Save $cv ("Aura_{0:00}" -f $f)
}

# ------------------------------------------------------------ Poison: 부글거리는 독 구름과 터지는 거품
for ($f = 0; $f -lt $Frames; $f++) {
    $t = T $f; $cv = NewCanvas $Size $Size; $g = $cv.g
    $alpha = 1 - 0.3 * $t
    $grow = 0.6 + 0.4 * (EaseOut $t)
    $cloud = New-Object System.Drawing.Drawing2D.GraphicsPath
    for ($i = 0; $i -lt 8; $i++) {
        $a = $i * 45 + 10 * $f
        $d = 62 * $grow
        $r = (40 + 8 * [Math]::Sin($f + $i)) * $grow
        $cloud.AddEllipse([single]((PX $Half $a $d) - $r), [single]((PY $Half $a $d) - $r), [single](2 * $r), [single](2 * $r))
    }
    $cloud.AddEllipse([single]($Half - 60 * $grow), [single]($Half - 60 * $grow), [single](120 * $grow), [single](120 * $grow))
    $cloud.FillMode = 'Winding'
    # 겉 테두리만 남도록 외곽선을 먼저 모두 그리고, 면을 한 번에 칠한다(불투명하게 칠한 뒤 전체 투명도는 게임에서).
    $g.DrawPath((Pen (InkA $alpha) 7), $cloud)
    $g.FillPath((Br (C 205 205 210 255)), $cloud)
    for ($i = 0; $i -lt 5; $i++) {
        $p = ($t + $i * 0.23) % 1
        $x = $Half + ($i - 2) * 30
        $y = $Half + 30 - 80 * $p
        $r = 8 + 6 * $p
        $bub = Ellipse $x $y $r $r
        InkFill $g $bub (W (0.9 * $alpha * (1 - $p))) 4 ($alpha * (1 - $p))
    }
    Save $cv ("Poison_{0:00}" -f $f)
}

# ------------------------------------------------------------ Lightning: 가운데 섬광과 사방으로 뻗는 번개
for ($f = 0; $f -lt $Frames; $f++) {
    $t = T $f; $cv = NewCanvas $Size $Size; $g = $cv.g
    $alpha = 1 - 0.5 * $t
    $flash = Ellipse $Half $Half (50 * (1 - 0.6 * $t)) (50 * (1 - 0.6 * $t))
    InkFill $g $flash (W $alpha) 6 $alpha
    for ($i = 0; $i -lt 6; $i++) {
        $a = $i * 60 + 20 * ($f % 3)
        Bolt $g $Half $Half $a (80 + 40 * (EaseOut $t)) ($f * 13 + $i) $alpha (10 - 5 * $t)
    }
    Save $cv ("Lightning_{0:00}" -f $f)
}

# ------------------------------------------------------------ Wind: 가로로 긴 바람 자국(돌진 방향 = 오른쪽), 256x96
for ($f = 0; $f -lt $Frames; $f++) {
    $t = T $f; $cv = NewCanvas $Size 96; $g = $cv.g
    $alpha = 1 - 0.6 * $t
    for ($i = 0; $i -lt 4; $i++) {
        $y = 20 + $i * 18
        $x0 = 10 + 50 * $t + ($i % 2) * 30
        $x1 = $x0 + 150 - 40 * $t - ($i % 2) * 40
        $g.DrawLine((Pen (InkA (0.8 * $alpha)) 10), [single]$x0, [single]$y, [single]$x1, [single]$y)
        $g.DrawLine((Pen (W (0.9 * $alpha)) 5), [single]$x0, [single]$y, [single]$x1, [single]$y)
    }
    # 앞쪽 초승달 바람
    $tip = ArcBand (190 + 30 * $t) 48 40 30 -70 140
    InkFill $g $tip (W $alpha) 5 $alpha
    Save $cv ("Wind_{0:00}" -f $f)
}

# ------------------------------------------------------------ Frost: 바닥에서 솟는 얼음 가시와 눈꽃
for ($f = 0; $f -lt $Frames; $f++) {
    $t = T $f; $cv = NewCanvas $Size $Size; $g = $cv.g
    $alpha = 1 - 0.4 * $t
    $grow = EaseOut ([Math]::Min([double]1.0, [double]($t * 1.6)))
    $g.FillPath((Br (W (0.2 * $alpha))), (Ellipse $Half $Half 112 112))
    for ($i = 0; $i -lt 8; $i++) {
        $a = $i * 45 + 22
        $d0 = 40; $len = (70 + 15 * ($i % 2)) * $grow + 4
        $bx = PX $Half $a $d0; $by = PY $Half $a $d0
        $shard = Poly @((PX $bx ($a - 90) 12), (PY $by ($a - 90) 12), (PX $bx $a $len), (PY $by $a $len), (PX $bx ($a + 90) 12), (PY $by ($a + 90) 12))
        InkFill $g $shard (L (0.9 * $alpha)) 5 $alpha
    }
    $snow = Star4 $Half $Half (44 * (1 - 0.3 * $t)) 0
    InkFill $g $snow (W $alpha) 5 $alpha
    $snow2 = Star4 $Half $Half (30 * (1 - 0.3 * $t)) 45
    InkFill $g $snow2 (W $alpha) 4 $alpha
    Save $cv ("Frost_{0:00}" -f $f)
}

# ------------------------------------------------------------ 아이콘 128x128: 흰 기호 + 굵은 외곽선(게임에서 스킬 색으로 물들인다)
$IconSize = 128; $IH = 64
function Icon([string]$name, [scriptblock]$draw) {
    $cv = NewCanvas $IconSize $IconSize
    & $draw $cv.g
    Save $cv ("Icon_" + $name)
}

Icon "Slash" { param($g)
    $band = ArcBand 40 88 78 58 -80 95
    InkFill $g $band (W 1) 8 1
    $blade = Poly @(30, 98, 98, 30, 108, 20, 104, 38, 38, 104)
    InkFill $g $blade (L 1) 7 1
}
Icon "Shockwave" { param($g)
    $ring = ArcBand $IH 84 52 40 180 180
    InkFill $g $ring (L 1) 7 1
    $head = Poly @(40, 22, 88, 22, 88, 52, 40, 52)
    InkFill $g $head (W 1) 7 1
    $handle = Poly @(60, 52, 68, 52, 68, 84, 60, 84)
    InkFill $g $handle (L 1) 6 1
}
Icon "Fire" { param($g)
    $fl = Flame $IH 104 -90 88 30
    InkFill $g $fl (L 1) 8 1
    $in = Flame $IH 104 -90 52 16
    $g.FillPath((Br (W 1)), $in)
}
Icon "Arcane" { param($g)
    $ring = ArcBand $IH $IH 54 44 0 359.9
    InkFill $g $ring (L 1) 6 1
    $st = Star4 $IH $IH 40 0
    InkFill $g $st (W 1) 7 1
}
Icon "Holy" { param($g)
    $cross = Poly @(54, 14, 74, 14, 74, 42, 102, 42, 102, 62, 74, 62, 74, 114, 54, 114, 54, 62, 26, 62, 26, 42, 54, 42)
    InkFill $g $cross (W 1) 8 1
}
Icon "Heal" { param($g)
    $heart = New-Object System.Drawing.Drawing2D.GraphicsPath
    $heart.AddEllipse(18, 22, 50, 50); $heart.AddEllipse(60, 22, 50, 50)
    $heart.AddPolygon([System.Drawing.PointF[]]@((New-Object System.Drawing.PointF(22, 58)), (New-Object System.Drawing.PointF(106, 58)), (New-Object System.Drawing.PointF(64, 110))))
    $heart.FillMode = 'Winding'
    $g.DrawPath((Pen $Ink 8), $heart); $g.FillPath((Br (L 1)), $heart)
    $plus = Poly @(56, 36, 72, 36, 72, 52, 88, 52, 88, 66, 72, 66, 72, 82, 56, 82, 56, 66, 40, 66, 40, 52, 56, 52)
    $g.FillPath((Br (W 1)), $plus)
}
Icon "Aura" { param($g)
    $shield = Poly @(64, 12, 106, 28, 100, 76, 64, 116, 28, 76, 22, 28)
    InkFill $g $shield (L 1) 8 1
    $arrow = Poly @(64, 30, 88, 60, 74, 60, 74, 92, 54, 92, 54, 60, 40, 60)
    InkFill $g $arrow (W 1) 5 1
}
Icon "Poison" { param($g)
    $drop = Flame $IH 92 -90 78 30
    InkFill $g $drop (L 1) 8 1
    InkFill $g (Ellipse 96 96 12 12) (W 1) 5 1
    InkFill $g (Ellipse 30 100 9 9) (W 1) 5 1
    $g.FillPath((Br (W 1)), (Ellipse 54 82 10 10))
}
Icon "Lightning" { param($g)
    $bolt = Poly @(74, 8, 30, 70, 60, 70, 46, 120, 98, 52, 66, 52, 84, 8)
    InkFill $g $bolt (W 1) 8 1
}
Icon "Wind" { param($g)
    foreach ($y in @(36, 64, 92)) {
        $g.DrawLine((Pen $Ink 16), 18, [single]$y, [single](80 + ($y % 3) * 8), [single]$y)
        $g.DrawLine((Pen (W 1) 8), 18, [single]$y, [single](80 + ($y % 3) * 8), [single]$y)
    }
    $tip = ArcBand 92 64 30 20 -80 160
    InkFill $g $tip (L 1) 6 1
}
Icon "Frost" { param($g)
    for ($i = 0; $i -lt 3; $i++) {
        $a = $i * 60
        $g.DrawLine((Pen $Ink 18), [single](PX $IH $a 50), [single](PY $IH $a 50), [single](PX $IH ($a + 180) 50), [single](PY $IH ($a + 180) 50))
    }
    for ($i = 0; $i -lt 3; $i++) {
        $a = $i * 60
        $g.DrawLine((Pen (W 1) 8), [single](PX $IH $a 50), [single](PY $IH $a 50), [single](PX $IH ($a + 180) 50), [single](PY $IH ($a + 180) 50))
    }
    InkFill $g (Star4 $IH $IH 22 45) (L 1) 5 1
}

Write-Output ("스킬 이펙트 생성 완료: " + (Get-ChildItem $OutRoot -Filter *.png).Count + "장 -> " + $OutRoot)
