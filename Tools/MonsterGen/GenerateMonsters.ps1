# 몬스터 스프라이트 프레임 생성기
# 실행: pwsh -File Tools\MonsterGen\GenerateMonsters.ps1
# 결과: Assets\4.Sprite\Monster\<이름>\<상태>_<번호>.png, Assets\4.Sprite\Fx\*.png
# 좌표계: 모델 원점(0,0)은 발밑 중앙, x는 오른쪽(+), y는 위쪽이 음수. 기본 방향은 오른쪽을 봄.
param(
    [string]$OutRoot = (Join-Path $PSScriptRoot "..\..\Assets\4.Sprite"),
    [string]$PreviewPath = ""
)

Add-Type -AssemblyName System.Drawing

$script:S = 4
$script:OutlineW = 4.0
$script:Baseline = 8
$script:Ink = [System.Drawing.Color]::FromArgb(255, 28, 28, 28)
$script:G = $null
$script:CX = 0.0
$script:GY = 0.0
$script:P = $null
$script:Sheet = New-Object System.Collections.ArrayList

function NewBrush($r, $g, $b, $a = 255) {
    return New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb([int]$a, [int]$r, [int]$g, [int]$b))
}

function NewPen($w) {
    $pen = New-Object System.Drawing.Pen($script:Ink, [single]$w)
    $pen.LineJoin = 'Round'
    $pen.StartCap = 'Round'
    $pen.EndCap = 'Round'
    return $pen
}

function Pose([double]$sx = 1.0, [double]$sy = 1.0, [double]$shear = 0.0, [double]$rot = 0.0, [double]$dx = 0.0, [double]$dy = 0.0, [double]$py = 0.0) {
    return @{ sx = $sx; sy = $sy; shear = $shear; rot = $rot; dx = $dx; dy = $dy; py = $py }
}

function Lerp([double]$a, [double]$b, [double]$k) { return $a + ($b - $a) * $k }
function Clamp01([double]$k) { return [Math]::Max(0.0, [Math]::Min(1.0, $k)) }
function Smooth([double]$k) { $c = Clamp01 $k; return $c * $c * (3 - 2 * $c) }
function Wave([double]$t) { return [Math]::Sin(2 * [Math]::PI * $t) }
function Arc([double]$k) { return [Math]::Sin([Math]::PI * (Clamp01 $k)) }

# 모델 좌표 배열을 현재 포즈로 변환해 캔버스 좌표 PointF[]로 반환
function Xf([double[]]$a) {
    $p = $script:P
    $rad = $p.rot * [Math]::PI / 180.0
    $cos = [Math]::Cos($rad)
    $sin = [Math]::Sin($rad)
    $list = New-Object 'System.Collections.Generic.List[System.Drawing.PointF]'
    for ($i = 0; $i -lt $a.Count; $i += 2) {
        $y = $a[$i + 1] * $p.sy
        $x = $a[$i] * $p.sx + $p.shear * (-$y)
        $yp = $y - $p.py
        $rx = $x * $cos - $yp * $sin
        $ry = $x * $sin + $yp * $cos + $p.py
        $list.Add([System.Drawing.PointF]::new([single]($script:CX + $rx + $p.dx), [single]($script:GY + $ry + $p.dy)))
    }
    return , $list.ToArray()
}

function Blob([double[]]$pts, $brush, [double]$tension = 0.5) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddClosedCurve((Xf $pts), [single]$tension)
    $script:G.FillPath($brush, $path)
    $script:G.DrawPath((NewPen $script:OutlineW), $path)
    return $path
}

function Poly([double[]]$pts, $brush) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddPolygon((Xf $pts))
    $script:G.FillPath($brush, $path)
    $script:G.DrawPath((NewPen $script:OutlineW), $path)
}

function Dot([double]$x, [double]$y, [double]$rx, [double]$ry, $brush) {
    $c = (Xf @($x, $y))[0]
    $script:G.FillEllipse($brush, [single]($c.X - $rx), [single]($c.Y - $ry), [single](2 * $rx), [single](2 * $ry))
}

function Stroke([double[]]$pts, [double]$w = 2.5) {
    $script:G.DrawLines((NewPen $w), (Xf $pts))
}

# style: open, blink, hurt, dead, angry
function Eyes([string]$style, [double]$x1, [double]$x2, [double]$y, [double]$rx = 3.0, [double]$ry = 5.0) {
    $ink = NewBrush 28 28 28
    foreach ($x in @($x1, $x2)) {
        if ($style -eq 'open') {
            Dot $x $y $rx $ry $ink
        }
        elseif ($style -eq 'angry') {
            Dot $x $y $rx $ry $ink
            $dir = 1
            if ($x -lt (($x1 + $x2) / 2)) { $dir = -1 }
            Stroke @(($x + $dir * 5), ($y - $ry - 4), ($x - $dir * 4), ($y - $ry + 1)) 2.5
        }
        elseif ($style -eq 'blink') {
            Stroke @(($x - $rx - 1), $y, ($x + $rx + 1), $y) 2.5
        }
        elseif ($style -eq 'hurt') {
            $dir = 1
            if ($x -lt (($x1 + $x2) / 2)) { $dir = -1 }
            Stroke @(($x + $dir * 4), ($y - 4), ($x - $dir * 3), $y, ($x + $dir * 4), ($y + 4)) 2.5
        }
        else {
            Stroke @(($x - 4), ($y - 4), ($x + 4), ($y + 4)) 2.5
            Stroke @(($x - 4), ($y + 4), ($x + 4), ($y - 4)) 2.5
        }
    }
}

# 한 프레임을 그려 PNG로 저장. $draw 블록 안에서 $script:P 포즈와 그리기 함수를 사용
function Frame($dir, $name, $w, $h, $alpha, [scriptblock]$draw) {
    $big = New-Object System.Drawing.Bitmap(($w * $script:S), ($h * $script:S))
    $script:G = [System.Drawing.Graphics]::FromImage($big)
    $script:G.SmoothingMode = 'AntiAlias'
    $script:G.ScaleTransform($script:S, $script:S)
    $script:CX = $w / 2.0
    $script:GY = $h - $script:Baseline
    & $draw
    $script:G.Dispose()

    $small = New-Object System.Drawing.Bitmap($w, $h)
    $g2 = [System.Drawing.Graphics]::FromImage($small)
    $g2.InterpolationMode = 'HighQualityBicubic'
    $g2.PixelOffsetMode = 'HighQuality'
    $attr = New-Object System.Drawing.Imaging.ImageAttributes
    $matrix = New-Object System.Drawing.Imaging.ColorMatrix
    $matrix.Matrix33 = [single]$alpha
    $attr.SetColorMatrix($matrix)
    $attr.SetWrapMode('TileFlipXY')
    $rect = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
    $g2.DrawImage($big, $rect, 0, 0, $big.Width, $big.Height, [System.Drawing.GraphicsUnit]::Pixel, $attr)
    $g2.Dispose()
    $big.Dispose()

    New-Item -ItemType Directory -Force $dir | Out-Null
    $path = Join-Path $dir "$name.png"
    $small.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $small.Dispose()
    [void]$script:Sheet.Add($path)
}

function Clip($monster, $state, $count, $w, $h, [scriptblock]$draw) {
    $dir = Join-Path $OutRoot "Monster\$monster"
    for ($i = 0; $i -lt $count; $i++) {
        $script:T = $i / [double]$count
        $script:Alpha = 1.0
        $script:P = Pose
        $script:FrameDraw = $draw
        Frame $dir ("{0}_{1:00}" -f $state, $i) $w $h 1.0 {
            & $script:FrameDraw
        }
        if ($script:Alpha -lt 1.0) {
            # 알파가 바뀐 프레임은 같은 그림을 알파 적용해서 다시 저장
            $script:Sheet.RemoveAt($script:Sheet.Count - 1)
            Frame $dir ("{0}_{1:00}" -f $state, $i) $w $h $script:Alpha {
                & $script:FrameDraw
            }
        }
    }
}

# ---------------------------------------------------------------- Slime
function DrawSlime($eye, $mouth) {
    $body = NewBrush 126 204 96
    [void](Blob @(-32, -3, -29, -22, -16, -40, 0, -46, 16, -40, 29, -22, 32, -3, 0, 2) $body 0.5)
    Dot -13 -33 6 3.5 (NewBrush 255 255 255 170)
    Eyes $eye -1 15 -20
    if ($mouth -eq 'smile') {
        Stroke @(3, -11, 7, -8, 11, -11) 2.5
    }
    elseif ($mouth -eq 'open') {
        Dot 7 -10 4.5 5 (NewBrush 28 28 28)
    }
    else {
        Stroke @(3, -9, 11, -9) 2.5
    }
}

function GenSlime {
    $w = 104; $h = 80
    Clip Slime Idle 20 $w $h {
        $b = Wave $script:T
        $script:P = Pose (1 - 0.03 * $b) (1 + 0.05 * $b)
        $eye = 'open'
        if ($script:T -ge 0.45 -and $script:T -lt 0.55) { $eye = 'blink' }
        DrawSlime $eye 'smile'
    }
    Clip Slime Walk 12 $w $h {
        $t = $script:T
        if ($t -lt 0.35) {
            $k = Arc ($t / 0.35)
            $script:P = Pose (1 + 0.18 * $k) (1 - 0.22 * $k)
        }
        else {
            $k = Arc (($t - 0.35) / 0.65)
            $script:P = Pose (1 - 0.08 * $k) (1 + 0.14 * $k) (0.08 * $k) 0 0 (-12 * $k)
        }
        DrawSlime 'open' 'smile'
    }
    Clip Slime Attack 16 $w $h {
        $t = $script:T
        if ($t -lt 0.45) {
            $k = Smooth ($t / 0.45)
            $script:P = Pose (Lerp 1 1.28 $k) (Lerp 1 0.66 $k) (-0.1 * $k) 0 (-5 * $k) 0
            DrawSlime 'angry' 'flat'
        }
        elseif ($t -lt 0.62) {
            $k = Smooth (($t - 0.45) / 0.17)
            $script:P = Pose (Lerp 1.28 0.88 $k) (Lerp 0.66 1.2 $k) (Lerp -0.1 0.35 $k) 0 (Lerp -5 16 $k) (-6 * (Arc $k))
            DrawSlime 'angry' 'open'
        }
        else {
            $k = Smooth (($t - 0.62) / 0.38)
            $script:P = Pose (Lerp 0.88 1 $k) (Lerp 1.2 1 $k) (Lerp 0.35 0 $k) 0 (Lerp 16 0 $k) 0
            DrawSlime 'open' 'flat'
        }
    }
    Clip Slime Hit 6 $w $h {
        $k = 1 - (Smooth $script:T)
        $script:P = Pose (1 + 0.16 * $k) (1 - 0.14 * $k) (-0.3 * $k) 0 (-4 * $k) 0
        DrawSlime 'hurt' 'open'
    }
    Clip Slime Dead 14 $w $h {
        $k = Smooth ($script:T / 0.6)
        $script:P = Pose (Lerp 1 1.5 $k) (Lerp 1 0.22 $k)
        $script:Alpha = 1 - (Clamp01 (($script:T - 0.55) / 0.45))
        DrawSlime 'dead' 'flat'
    }
}

# ---------------------------------------------------------------- Bat
# flap: 1 = 날개 위, -1 = 날개 아래
function DrawBat([double]$flap, [string]$eye, [string]$mouth) {
    $body = NewBrush 150 118 200
    $wy = 14 * $flap
    Poly @(-12, -44, -44, (-52 - $wy), -47, (-34 - $wy * 0.6), -37, (-39 - $wy * 0.5), -32, (-28 - $wy * 0.3), -24, (-35 - $wy * 0.2), -12, -30) $body
    Poly @(12, -44, 44, (-52 - $wy), 47, (-34 - $wy * 0.6), 37, (-39 - $wy * 0.5), 32, (-28 - $wy * 0.3), 24, (-35 - $wy * 0.2), 12, -30) $body
    Poly @(-13, -50, -12, -66, -3, -56) $body
    Poly @(13, -50, 12, -66, 3, -56) $body
    [void](Blob @(-18, -40, -13, -54, 0, -59, 13, -54, 18, -40, 13, -26, 0, -21, -13, -26) $body 0.5)
    if ($eye -eq 'open' -or $eye -eq 'angry') {
        Dot -2 -43 4.5 5.5 (NewBrush 255 255 255)
        Dot 10 -43 4.5 5.5 (NewBrush 255 255 255)
        Eyes $eye 0 11 -42 2 3
    }
    else {
        Eyes $eye -2 10 -43 3 4
    }
    if ($mouth -eq 'open') {
        Dot 4 -31 5 4.5 (NewBrush 28 28 28)
        Poly @(0, -34, 2, -29, 4, -34) (NewBrush 255 255 255)
        Poly @(5, -34, 7, -29, 9, -34) (NewBrush 255 255 255)
    }
    else {
        Poly @(0, -33, 2, -28, 4, -33) (NewBrush 255 255 255)
        Poly @(5, -33, 7, -28, 9, -33) (NewBrush 255 255 255)
    }
}

function GenBat {
    $w = 156; $h = 112
    Clip Bat Idle 12 $w $h {
        $f = Wave $script:T
        $script:P = Pose 1 1 0 0 0 (-4 - 3 * $f)
        DrawBat $f 'open' 'closed'
    }
    Clip Bat Walk 8 $w $h {
        $f = Wave $script:T
        $script:P = Pose 1 1 0.08 6 0 (-4 - 3 * $f)
        DrawBat $f 'open' 'closed'
    }
    Clip Bat Attack 16 $w $h {
        $t = $script:T
        if ($t -lt 0.4) {
            $k = Smooth ($t / 0.4)
            $script:P = Pose 1 1 0 (-14 * $k) (-8 * $k) (-4 - 10 * $k)
            DrawBat (Lerp 0 1 $k) 'angry' 'closed'
        }
        elseif ($t -lt 0.62) {
            $k = Smooth (($t - 0.4) / 0.22)
            $script:P = Pose 1 1 0 (Lerp -14 22 $k) (Lerp -8 18 $k) (Lerp -14 10 $k)
            DrawBat (Lerp 1 -1 $k) 'angry' 'open'
        }
        else {
            $k = Smooth (($t - 0.62) / 0.38)
            $script:P = Pose 1 1 0 (Lerp 22 0 $k) (Lerp 18 0 $k) (Lerp 10 -4 $k)
            DrawBat (Wave ($k * 1.5)) 'open' 'closed'
        }
    }
    Clip Bat Hit 6 $w $h {
        $k = 1 - (Smooth $script:T)
        $script:P = Pose 1 1 0 (-18 * $k) (-6 * $k) (-4 - 4 * $k) -40
        DrawBat (-0.6) 'hurt' 'open'
    }
    Clip Bat Dead 14 $w $h {
        $k = Smooth ($script:T / 0.55)
        $script:P = Pose 1 1 0 (Lerp 0 150 $k) 0 (Lerp -4 16 ($k * $k)) -40
        $script:Alpha = 1 - (Clamp01 (($script:T - 0.6) / 0.4))
        DrawBat (Lerp 0.4 -1 $k) 'dead' 'open'
    }
}

# ---------------------------------------------------------------- Mushroom
# capLift: 갓이 위로 뜨는 정도(px), capSquash: 갓 납작해짐
function DrawMushroom([double]$capLift, [double]$capSquash, [string]$eye, [bool]$cheek) {
    [void](Blob @(-12, -34, 12, -34, 15, -8, 8, -1, -8, -1, -15, -8) (NewBrush 244 230 200) 0.4)
    Eyes $eye 0 10 -18 2.5 4
    if ($cheek) {
        Dot 7 -8 4 4 (NewBrush 28 28 28)
    }
    $cy = -40 - $capLift
    $ch = 34 * (1 - $capSquash)
    $cw = 32 * (1 + $capSquash * 0.6)
    $saved = $script:P
    $path = Blob @((-$cw), ($cy + 6), (-$cw * 0.82), ($cy - $ch * 0.6), 0, ($cy - $ch), ($cw * 0.82), ($cy - $ch * 0.6), $cw, ($cy + 6), 0, ($cy + 12)) (NewBrush 224 84 72) 0.5
    $script:G.SetClip($path)
    $white = NewBrush 255 244 232
    Dot (-$cw * 0.5) ($cy - $ch * 0.45) 6.5 5.5 $white
    Dot ($cw * 0.2) ($cy - $ch * 0.78) 8 6.5 $white
    Dot ($cw * 0.62) ($cy - $ch * 0.2) 5 4 $white
    $script:G.ResetClip()
    $script:G.DrawPath((NewPen $script:OutlineW), $path)
    $script:P = $saved
}

function GenMushroom {
    $w = 152; $h = 104
    Clip Mushroom Idle 20 $w $h {
        $b = Wave $script:T
        $script:P = Pose (1 - 0.02 * $b) (1 + 0.03 * $b)
        $eye = 'open'
        if ($script:T -ge 0.6 -and $script:T -lt 0.7) { $eye = 'blink' }
        DrawMushroom (1.5 * (Wave ($script:T - 0.12))) 0 $eye $false
    }
    Clip Mushroom Walk 12 $w $h {
        $b = Wave $script:T
        $hop = [Math]::Abs((Wave ($script:T)))
        $script:P = Pose 1 (1 - 0.05 * $hop) 0 (7 * $b) 0 (-3 * $hop)
        DrawMushroom (2 * $hop) 0 'open' $false
    }
    Clip Mushroom Attack 16 $w $h {
        $t = $script:T
        if ($t -lt 0.5) {
            $k = Smooth ($t / 0.5)
            $script:P = Pose (Lerp 1 1.12 $k) (Lerp 1 0.78 $k)
            DrawMushroom (-3 * $k) (0.22 * $k) 'angry' $true
        }
        elseif ($t -lt 0.65) {
            $k = Smooth (($t - 0.5) / 0.15)
            $script:P = Pose (Lerp 1.12 0.92 $k) (Lerp 0.78 1.14 $k)
            DrawMushroom (Lerp -3 10 $k) (Lerp 0.22 -0.08 $k) 'angry' $true
        }
        else {
            $k = Smooth (($t - 0.65) / 0.35)
            $script:P = Pose (Lerp 0.92 1 $k) (Lerp 1.14 1 $k)
            DrawMushroom (Lerp 10 0 $k) (Lerp -0.08 0 $k) 'open' $false
        }
    }
    Clip Mushroom Hit 6 $w $h {
        $k = 1 - (Smooth $script:T)
        $script:P = Pose 1 (1 - 0.08 * $k) 0 (-14 * $k) (-3 * $k) 0
        DrawMushroom (3 * $k) (0.1 * $k) 'hurt' $true
    }
    Clip Mushroom Dead 14 $w $h {
        $k = Smooth ($script:T / 0.55)
        $script:P = Pose 1 1 0 (Lerp 0 -84 $k) (Lerp 0 14 $k) (Lerp 0 -12 $k)
        $script:Alpha = 1 - (Clamp01 (($script:T - 0.6) / 0.4))
        DrawMushroom 0 0 'dead' $false
    }
}

# ---------------------------------------------------------------- Fx
function GenFx {
    $dir = Join-Path $OutRoot "Fx"
    $script:P = Pose
    # 포자 투사체
    Frame $dir "Spore" 28 28 1.0 {
        $script:GY = 14
        [void](Blob @(-8, 0, -6, -6, 0, -8, 6, -6, 8, 0, 6, 6, 0, 8, -6, 6) (NewBrush 214 226 110) 0.6)
        Dot -2 -3 2.5 2 (NewBrush 255 255 255 190)
    }
    # 흰 원(범위 표시, 그림자 등 색을 입혀 사용)
    Frame $dir "Circle" 64 64 1.0 {
        $script:G.FillEllipse((NewBrush 255 255 255), 1, 1, 62, 62)
    }
    # 흰 사각형(체력바 등)
    Frame $dir "Pixel" 4 4 1.0 {
        $script:G.FillRectangle((NewBrush 255 255 255), 0, 0, 4, 4)
    }
    # 근접 공격 궤적(초승달). 오른쪽을 향함
    Frame $dir "Slash" 96 128 1.0 {
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $path.AddArc(-70, 4, 150, 120, -70, 140)
        $path.AddArc(-96, 14, 150, 100, 62, -124)
        $path.CloseFigure()
        $script:G.FillPath((NewBrush 255 255 255), $path)
    }
}

GenSlime
GenBat
GenMushroom
GenFx
"frames: $($script:Sheet.Count)"

# 확인용 시트(선택)
if ($PreviewPath -ne "") {
    $cell = 132
    $cols = 20
    $monsterFrames = @($script:Sheet | Where-Object { $_ -match '\\Monster\\' })
    $rows = [Math]::Ceiling($monsterFrames.Count / $cols)
    $pv = New-Object System.Drawing.Bitmap(($cols * $cell), ([int]$rows * $cell))
    $g = [System.Drawing.Graphics]::FromImage($pv)
    $g.Clear([System.Drawing.Color]::FromArgb(96, 138, 78))
    for ($i = 0; $i -lt $monsterFrames.Count; $i++) {
        $im = [System.Drawing.Image]::FromFile($monsterFrames[$i])
        $x = ($i % $cols) * $cell + ($cell - $im.Width) / 2
        $y = [Math]::Floor($i / $cols) * $cell + ($cell - $im.Height)
        $g.DrawImage($im, [single]$x, [single]$y, [single]$im.Width, [single]$im.Height)
        $im.Dispose()
    }
    $g.Dispose()
    $pv.Save($PreviewPath)
    $pv.Dispose()
}
