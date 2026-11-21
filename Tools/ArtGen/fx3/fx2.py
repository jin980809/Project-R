"""Extra skill FX (value maps for the gradient shader, same style as fx.py).
Directional clips are drawn facing +x (right); the game rotates them to the aim."""
import math, random, sys, os
import cairo
import fx
from fx import (render, v, ink_fill, fill, radial, star4, polyb, crescent, ease_out, ease_in,
                INK, SHADE, MID, LIGHT, HI, S, H)
from kit import smooth, blob, ellipse

def blade(x0, y0, x1, y1, w):
    """Long lens-shaped slash streak from p0 to p1, widest at 60%."""
    dx, dy = x1 - x0, y1 - y0; L = math.hypot(dx, dy) or 1
    nx, ny = -dy / L, dx / L
    pts = []
    n = 24
    for i in range(n + 1):
        t = i / n
        ww = w * (math.sin(t * math.pi) ** 0.7) * (0.6 + 0.4 * t)
        pts.append((x0 + dx * t + nx * ww, y0 + dy * t + ny * ww))
    for i in range(n, -1, -1):
        t = i / n
        ww = w * 0.35 * (math.sin(t * math.pi) ** 0.7)
        pts.append((x0 + dx * t - nx * ww, y0 + dy * t - ny * ww))
    return polyb(pts)

def sparks(c, x, y, n, t, seed, size=10, spread=40, fade=1.0):
    rnd = random.Random(seed)
    for i in range(n):
        a = rnd.uniform(0, 2 * math.pi)
        d = spread * (0.3 + t) * rnd.uniform(0.6, 1.2)
        ink_fill(c, polyb(star4(x + math.cos(a) * d, y + math.sin(a) * d, size * (1 - t * 0.6), a)), HI, fade, lw=2.5)

# ---------------------------------------------------------------- CrossSlash (two cuts drawn one after another)
def crossslash(f, N=10):
    t = f / (N - 1)
    def draw(c):
        fade = 1 - max(0, (t - 0.7) / 0.3) * 0.9
        cuts = [((40, 40), (216, 216), 0.0), ((216, 40), (40, 216), 0.3)]
        for k, (p0, p1, start) in enumerate(cuts):
            p = (t - start) / 0.32
            if p <= 0: continue
            p = min(1, p)
            e = ease_out(p)
            x1 = p0[0] + (p1[0] - p0[0]) * e; y1 = p0[1] + (p1[1] - p0[1]) * e
            tail = max(0, e - 0.85)
            x0 = p0[0] + (p1[0] - p0[0]) * tail; y0 = p0[1] + (p1[1] - p0[1]) * tail
            w = 32 * (1 - 0.4 * max(0, t - 0.6))
            ink_fill(c, blade(x0, y0, x1, y1, w), MID, fade)
            fill(c, blade(x0, y0, x1, y1, w * 0.45), LIGHT, fade)
            fill(c, blade(x0 + 2, y0 + 2, x1, y1, w * 0.16), HI, fade)
            if p < 1:
                sparks(c, x1, y1, 3, p * 0.5, f + k * 7, 9, 18, fade)
        # impact flash at the crossing once both cuts are done
        if t > 0.6:
            q = (t - 0.6) / 0.4
            radial(c, H, H, 70 * (0.5 + q), [(0, HI, 0.9 * (1 - q)), (1, LIGHT, 0)])
            ink_fill(c, polyb(star4(H, H, 60 * (1 - q * 0.5), math.pi / 4, 0.18)), HI, 1 - q, lw=3)
            sparks(c, H, H, 6, q, 99, 11, 70, 1 - q)
    render(f'CrossSlash_{f:02d}', draw)

# ---------------------------------------------------------------- Whirl (spinning blades around the caster)
def whirl(f, N=10):
    t = f / (N - 1)
    def draw(c):
        fade = 1 - max(0, (t - 0.65) / 0.35) * 0.9
        rot = -math.pi / 2 + t * math.pi * 3.2
        # faint full motion ring
        def ring(c, r=96, th=30):
            c.new_sub_path(); c.arc(H, H, r, 0, 2 * math.pi)
            c.new_sub_path(); c.arc_negative(H, H, r - th, 2 * math.pi, 0)
        fill(c, ring, LIGHT, 0.16 * fade)
        for k in range(2):
            a0 = rot + k * math.pi
            sweep = math.radians(150)
            ink_fill(c, crescent(H, H, 112, a0 - sweep, sweep, 30), MID, fade)
            fill(c, crescent(H, H, 110, a0 - sweep * 0.7, sweep * 0.7, 13), LIGHT, fade)
            fill(c, crescent(H, H, 109, a0 - sweep * 0.35, sweep * 0.35, 5), HI, fade)
            # inner afterimage
            ink_fill(c, crescent(H, H, 74, a0 - sweep * 0.9 - 0.3, sweep * 0.8, 14), SHADE + 0.2, 0.6 * fade, lw=3)
        # wind lines
        for i in range(5):
            a = rot * 1.2 + i * 1.25
            c.new_path(); c.arc(H, H, 50 + i * 9, a, a + 0.8)
            v(c, INK, 0.5 * fade); c.set_line_width(6); c.stroke_preserve()
            v(c, LIGHT, 0.8 * fade); c.set_line_width(2.5); c.stroke()
    render(f'Whirl_{f:02d}', draw)

# ---------------------------------------------------------------- Sweep (wide arc in front, facing +x)
def sweep(f, N=8):
    t = f / (N - 1)
    def draw(c):
        fade = 1 - max(0, (t - 0.55) / 0.45) * 0.9
        span = math.radians(200)
        start = -span / 2
        prog = ease_out(min(1, t * 1.6))
        sw = span * max(0.15, prog)
        a0 = start - math.pi * 0 + 0  # arc centred on +x
        cx, cy = H, H
        ink_fill(c, crescent(cx, cy, 116, -span / 2, sw, 40 * (1 - 0.4 * t)), MID, fade)
        fill(c, crescent(cx, cy, 114, -span / 2 + sw * 0.3, sw * 0.7, 18 * (1 - 0.4 * t)), LIGHT, fade)
        fill(c, crescent(cx, cy, 113, -span / 2 + sw * 0.6, sw * 0.4, 6), HI, fade)
        # dust kicked along the arc
        rnd = random.Random(4)
        for i in range(6):
            a = -span / 2 + sw * (i + 0.5) / 6
            d = 100 + rnd.uniform(-6, 6)
            x, y = cx + math.cos(a) * d, cy + math.sin(a) * d
            r = 9 + 6 * t
            ink_fill(c, lambda c, x=x, y=y, r=r, i=i: smooth(c, blob(x, y, r, r * 0.8, 7, 0.15, i), tension=0.6), LIGHT, 0.6 * fade * (1 - t * 0.5), lw=2.5, ink=SHADE)
        hx, hy = cx + math.cos(-span / 2 + sw) * 100, cy + math.sin(-span / 2 + sw) * 100
        if t < 0.7: sparks(c, hx, hy, 3, t, f, 9, 22, fade)
    render(f'Sweep_{f:02d}', draw)

# ---------------------------------------------------------------- Pierce (thrust beam, 256x96, facing +x)
def pierce(f, N=8):
    t = f / (N - 1)
    W2, H2 = 256, 96
    def draw(c):
        fade = 1 - max(0, (t - 0.5) / 0.5) * 0.9
        e = ease_out(min(1, t * 1.8))
        tipx = 30 + 220 * e
        y = H2 / 2
        # speed streaks
        rnd = random.Random(2)
        for i in range(6):
            yy = y + rnd.uniform(-30, 30); x0 = rnd.uniform(0, 80) + 60 * t; x1 = x0 + 60 + rnd.uniform(0, 70)
            x1 = min(x1, tipx - 10)
            if x1 <= x0: continue
            c.new_path(); c.move_to(x0, yy); c.line_to(x1, yy)
            v(c, INK, 0.6 * fade); c.set_line_width(6); c.stroke_preserve(); v(c, LIGHT, 0.9 * fade); c.set_line_width(2.5); c.stroke()
        # main lance: long wedge from left to tip
        body = polyb([(4, y - 6), (tipx - 40, y - 15), (tipx, y), (tipx - 40, y + 15), (4, y + 6)])
        ink_fill(c, body, MID, fade)
        fill(c, polyb([(20, y - 3), (tipx - 40, y - 7), (tipx - 4, y), (tipx - 40, y + 2), (20, y + 2)]), LIGHT, fade)
        fill(c, polyb([(tipx - 60, y - 2), (tipx - 6, y), (tipx - 60, y + 1)]), HI, fade)
        # shock rings at the tip
        for k in range(2):
            rr = 10 + 16 * e + k * 10
            c.new_path(); c.save(); c.translate(tipx - 6 - k * 14, y); c.scale(0.45, 1); c.arc(0, 0, rr, 0, 2 * math.pi); c.restore()
            v(c, INK, 0.7 * fade); c.set_line_width(6); c.stroke_preserve(); v(c, HI, 0.9 * fade); c.set_line_width(2.5); c.stroke()
    render(f'Pierce_{f:02d}', draw, W2, H2)

# ---------------------------------------------------------------- Cleave (huge downward slash in front + crack)
def cleave(f, N=8):
    t = f / (N - 1)
    def draw(c):
        fade = 1 - max(0, (t - 0.55) / 0.45) * 0.9
        e = ease_in(min(1, t * 2.0))
        cx, cy = 96, H
        a_start = -math.pi / 2 - 0.2; span = math.pi * 0.95
        sw = span * max(0.1, e)
        ink_fill(c, crescent(cx, cy, 120, a_start, sw, 46 * (1 - 0.3 * t)), MID, fade)
        fill(c, crescent(cx, cy, 118, a_start + sw * 0.35, sw * 0.65, 20), LIGHT, fade)
        fill(c, crescent(cx, cy, 117, a_start + sw * 0.65, sw * 0.35, 7), HI, fade)
        if t >= 0.45:
            q = (t - 0.45) / 0.55
            ix, iy = cx + math.cos(a_start + span) * 100, cy + math.sin(a_start + span) * 100
            rnd = random.Random(7)
            for i in range(5):
                a = -0.6 + i * 0.3 + rnd.uniform(-0.1, 0.1)
                pts = [(ix, iy)]; d = 0; L = (30 + rnd.uniform(0, 30)) * min(1, q * 2.5)
                while d < L:
                    d += 12; aa = a + rnd.uniform(-0.3, 0.3); pts.append((ix + math.cos(aa) * d, iy + math.sin(aa) * d * 0.5 + 6))
                c.new_path(); c.move_to(*pts[0]); [c.line_to(*p) for p in pts[1:]]
                v(c, INK, 0.9 * fade); c.set_line_width(5); c.stroke()
            radial(c, ix, iy, 46 * (0.6 + q), [(0, HI, 0.9 * (1 - q)), (1, LIGHT, 0)])
            sparks(c, ix, iy, 6, q, 3, 10, 60, 1 - q)
    render(f'Cleave_{f:02d}', draw)

# ---------------------------------------------------------------- Quake (ground: cracks + rock spikes erupting)
def quake(f, N=10):
    t = f / (N - 1)
    def draw(c):
        fade = 1 - max(0, (t - 0.6) / 0.4) * 0.85
        rnd = random.Random(11)
        # cracks
        for i in range(10):
            a = i * 2 * math.pi / 10 + rnd.uniform(-0.2, 0.2)
            pts = [(H, H)]; d = 0; L = (60 + rnd.uniform(0, 50)) * min(1, t * 3 + 0.2)
            while d < L:
                d += 13; aa = a + rnd.uniform(-0.35, 0.35); pts.append((H + math.cos(aa) * d, H + math.sin(aa) * d))
            c.new_path(); c.move_to(*pts[0]); [c.line_to(*p) for p in pts[1:]]
            v(c, INK, 0.9 * fade); c.set_line_width(7 - (i % 2) * 2); c.stroke_preserve()
            v(c, LIGHT, 0.5 * fade); c.set_line_width(2); c.stroke()
        # dust ring
        r = 28 + 76 * ease_out(t)
        for i in range(10):
            a = i * 2 * math.pi / 10 + 0.3
            px, py = H + math.cos(a) * r, H + math.sin(a) * r
            pr = (10 + (i % 3) * 4) * (0.6 + 0.6 * t)
            ink_fill(c, lambda c, px=px, py=py, pr=pr, i=i: smooth(c, blob(px, py, pr * 1.3, pr, 7, 0.15, i), tension=0.6), LIGHT, 0.7 * fade * (1 - t * 0.5), lw=2.5, ink=SHADE)
        # rock spikes erupting along a ring (rise then sink)
        rise = math.sin(min(1, t * 1.4) * math.pi) if t < 0.72 else max(0, 1 - (t - 0.5) * 2)
        spikes = []
        for i in range(8):
            a = i * 2 * math.pi / 8 + rnd.uniform(-0.2, 0.2)
            d = 58 + rnd.uniform(-8, 14)
            spikes.append((math.sin(a), a, d))
        spikes.sort()
        for _, a, d in spikes:
            bx, by = H + math.cos(a) * d, H + math.sin(a) * d * 0.9
            hgt = (34 + 18 * abs(math.sin(a * 3))) * rise
            if hgt < 3: continue
            w = 13
            ink_fill(c, polyb([(bx - w, by + 4), (bx - 3, by - hgt), (bx + 5, by - hgt * 0.8), (bx + w, by + 4)]), SHADE + 0.12, fade)
            fill(c, polyb([(bx - w + 3, by + 2), (bx - 3, by - hgt + 3), (bx, by + 2)]), LIGHT, fade)
        radial(c, H, H, 50 * (1 - 0.5 * t), [(0, HI, 0.8 * (1 - t)), (1, LIGHT, 0)])
    render(f'Quake_{f:02d}', draw)

# ---------------------------------------------------------------- IceNova (ice shards bursting outward)
def icenova(f, N=8):
    t = f / (N - 1)
    def draw(c):
        fade = 1 - max(0, (t - 0.55) / 0.45) * 0.9
        e = ease_out(t)
        radial(c, H, H, 120, [(0, LIGHT, 0.5 * fade), (0.7, MID, 0.25 * fade), (1, MID, 0)])
        # frost ring
        r = 30 + 85 * e
        def ring(c):
            c.new_sub_path(); c.arc(H, H, r, 0, 2 * math.pi); c.new_sub_path(); c.arc_negative(H, H, max(1, r - 12), 2 * math.pi, 0)
        ink_fill(c, ring, LIGHT, fade * 0.9, lw=3)
        rnd = random.Random(5)
        for i in range(12):
            a = i * 2 * math.pi / 12 + rnd.uniform(-0.15, 0.15)
            d = 20 + 92 * e * rnd.uniform(0.8, 1.05)
            L = 26 + rnd.uniform(0, 14); w = 8
            bx, by = H + math.cos(a) * d, H + math.sin(a) * d
            tip = (bx + math.cos(a) * L, by + math.sin(a) * L)
            back = (bx - math.cos(a) * L * 0.4, by - math.sin(a) * L * 0.4)
            l = (bx + math.cos(a + 1.57) * w, by + math.sin(a + 1.57) * w); rr = (bx + math.cos(a - 1.57) * w, by + math.sin(a - 1.57) * w)
            ink_fill(c, polyb([back, l, tip, rr]), MID + 0.08, fade, lw=3.5)
            fill(c, polyb([back, l, tip]), LIGHT + 0.1, fade)
        # snowflake core
        sr = 34 * (1 - 0.5 * t)
        for k in range(6):
            a = t + k * math.pi / 3
            for lw, val in ((11, INK), (5, HI)):
                c.new_path(); c.move_to(H, H); c.line_to(H + math.cos(a) * sr, H + math.sin(a) * sr)
                v(c, val, fade); c.set_line_width(lw); c.stroke()
    render(f'IceNova_{f:02d}', draw)

# ---------------------------------------------------------------- ArrowRain (looping zone: arrows falling)
def arrowrain(f, N=8):
    def draw(c):
        radial(c, H, H, 122, [(0, MID, 0.35), (0.8, SHADE + 0.1, 0.3), (1, SHADE, 0)])
        def ring(c):
            c.new_sub_path(); c.arc(H, H, 118, 0, 2 * math.pi); c.new_sub_path(); c.arc_negative(H, H, 110, 2 * math.pi, 0)
        fill(c, ring, MID, 0.6)
        rnd = random.Random(31)
        drops = [(rnd.uniform(-90, 90), rnd.uniform(-70, 80), rnd.uniform(0, 1)) for _ in range(14)]
        for (x, y, ph) in drops:
            p = (f / N + ph) % 1.0
            gx, gy = H + x, H + y
            if p < 0.6:
                q = p / 0.6
                ay = gy - 90 * (1 - q)
                L = 30
                c.new_path(); c.move_to(gx + 8 * (1 - q), ay - L); c.line_to(gx, ay)
                v(c, INK, 0.9); c.set_line_width(6); c.stroke_preserve(); v(c, LIGHT, 1); c.set_line_width(2.5); c.stroke()
                ink_fill(c, polyb([(gx - 5, ay - 6), (gx, ay + 4), (gx + 5, ay - 6)]), HI, 1, lw=2)
            else:
                q = (p - 0.6) / 0.4
                # stuck arrow + splash
                c.new_path(); c.move_to(gx + 6, gy - 18); c.line_to(gx, gy)
                v(c, INK, 1 - q); c.set_line_width(5); c.stroke_preserve(); v(c, MID, 1 - q); c.set_line_width(2); c.stroke()
                c.new_path(); c.save(); c.translate(gx, gy); c.scale(1, 0.45); c.arc(0, 0, 6 + 16 * q, 0, 2 * math.pi); c.restore()
                v(c, INK, 0.7 * (1 - q)); c.set_line_width(4); c.stroke_preserve(); v(c, LIGHT, 0.9 * (1 - q)); c.set_line_width(2); c.stroke()
        # bubbles of poison
        for i in range(5):
            a = rnd.uniform(0, 6.28); d = rnd.uniform(10, 90)
            yy = H + math.sin(a) * d - ((f * 7 + i * 13) % 30)
            ink_fill(c, lambda c, a=a, d=d, yy=yy: ellipse(c, H + math.cos(a) * d, yy, 5, 5), LIGHT, 0.8, lw=2)
    render(f'ArrowRain_{f:02d}', draw)

# ---------------------------------------------------------------- Hex (curse sigil with skull)
def hexsigil(f, N=8):
    t = f / (N - 1)
    def draw(c):
        fade = 1 - max(0, (t - 0.65) / 0.35) * 0.9
        g = ease_out(min(1, t * 1.6))
        rot = t * 1.2
        # outer circle + inner circle
        for rr, lw in ((112 * g, 9), (86 * g, 6)):
            c.new_path(); c.arc(H, H, max(1, rr), 0, 2 * math.pi)
            v(c, INK, fade); c.set_line_width(lw + 5); c.stroke_preserve(); v(c, MID + 0.1, fade); c.set_line_width(lw); c.stroke()
        # pentagram
        pts = [(H + math.cos(rot - math.pi / 2 + k * 4 * math.pi / 5) * 86 * g, H + math.sin(rot - math.pi / 2 + k * 4 * math.pi / 5) * 86 * g) for k in range(5)]
        c.new_path(); c.move_to(*pts[0]); [c.line_to(*p) for p in pts[1:]]; c.close_path()
        v(c, INK, fade); c.set_line_width(9); c.stroke_preserve(); v(c, LIGHT, fade); c.set_line_width(4); c.stroke()
        # runes on the ring
        for k in range(8):
            a = -rot + k * math.pi / 4
            x, y = H + math.cos(a) * 99 * g, H + math.sin(a) * 99 * g
            ink_fill(c, polyb(star4(x, y, 7, a, 0.35)), HI, fade, lw=2)
        # skull rising
        sy = H + 10 - 30 * ease_out(min(1, t * 1.3))
        s = 0.6 + 0.4 * g
        ink_fill(c, lambda c: ellipse(c, H, sy - 6 * s, 30 * s, 27 * s), LIGHT, fade)
        ink_fill(c, lambda c: c.rectangle(H - 16 * s, sy + 12 * s, 32 * s, 14 * s), LIGHT, fade)
        fill(c, lambda c: ellipse(c, H - 11 * s, sy - 4 * s, 8 * s, 9 * s), INK, fade)
        fill(c, lambda c: ellipse(c, H + 11 * s, sy - 4 * s, 8 * s, 9 * s), INK, fade)
        fill(c, polyb([(H, sy + 4 * s), (H - 4 * s, sy + 11 * s), (H + 4 * s, sy + 11 * s)]), INK, fade)
        for k in range(3):
            c.new_path(); c.move_to(H - 10 * s + k * 10 * s, sy + 15 * s); c.line_to(H - 10 * s + k * 10 * s, sy + 25 * s)
            v(c, INK, fade); c.set_line_width(3); c.stroke()
        if t > 0.5:
            q = (t - 0.5) / 0.5
            radial(c, H, H, 120 * q + 20, [(0, HI, 0.6 * (1 - q)), (1, MID, 0)])
    render(f'Hex_{f:02d}', draw)

# ---------------------------------------------------------------- Buff casts
def buffpower(f, N=8):
    t = f / (N - 1)
    def draw(c):
        fade = 1 - max(0, (t - 0.55) / 0.45) * 0.9
        g = ease_out(min(1, t * 1.5))
        radial(c, H, H + 30, 90 * g + 10, [(0, LIGHT, 0.5 * fade), (1, MID, 0)], sy=0.5)
        for i in range(7):
            x = H + (i - 3) * 26
            h = (60 + 50 * math.sin(i * 1.7 + f)) * g
            base = H + 50 - abs(i - 3) * 6
            w = 16
            pts = [(x - w, base), (x - w * 0.6, base - h * 0.5), (x + 3 * math.sin(f + i), base - h), (x + w * 0.6, base - h * 0.45), (x + w, base)]
            ink_fill(c, lambda c, pts=pts: smooth(c, pts, tension=0.7), MID, fade)
            pts2 = [(x - w * 0.5, base), (x, base - h * 0.7), (x + w * 0.5, base)]
            fill(c, lambda c, pts2=pts2: smooth(c, pts2, tension=0.7), LIGHT, fade)
        # up arrows
        for k in range(2):
            y = H - 10 - 50 * t - k * 40
            ink_fill(c, polyb([(H - 22, y + 10), (H, y - 14), (H + 22, y + 10), (H + 9, y + 10), (H + 9, y + 26), (H - 9, y + 26), (H - 9, y + 10)]), HI, fade * (1 - k * 0.3), lw=3)
    render(f'BuffPower_{f:02d}', draw)

def buffguard(f, N=8):
    t = f / (N - 1)
    def draw(c):
        fade = 1 - max(0, (t - 0.6) / 0.4) * 0.9
        g = ease_out(min(1, t * 1.7))
        R = 108 * g + 4
        radial(c, H, H, R, [(0, MID, 0.15 * fade), (0.85, LIGHT, 0.35 * fade), (1, HI, 0.6 * fade)])
        # hexagon cells
        for row in range(-4, 5):
            for col in range(-4, 5):
                cx = H + col * 30 + (row % 2) * 15; cy = H + row * 26
                if math.hypot(cx - H, cy - H) > R - 14: continue
                pts = [(cx + math.cos(k * math.pi / 3 + math.pi / 6) * 13, cy + math.sin(k * math.pi / 3 + math.pi / 6) * 13) for k in range(6)]
                c.new_path(); c.move_to(*pts[0]); [c.line_to(*p) for p in pts[1:]]; c.close_path()
                v(c, LIGHT, 0.55 * fade); c.set_line_width(2.5); c.stroke()
        c.new_path(); c.arc(H, H, R, 0, 2 * math.pi)
        v(c, INK, fade); c.set_line_width(9); c.stroke_preserve(); v(c, HI, fade); c.set_line_width(4); c.stroke()
        # glint sweeping
        a = -2.4 + t * 2.0
        c.new_path(); c.arc(H, H, R - 10, a, a + 0.6); v(c, HI, fade); c.set_line_width(5); c.stroke()
        # shield emblem
        s = 0.6 + 0.4 * g
        shield = polyb([(H - 26 * s, H - 30 * s), (H + 26 * s, H - 30 * s), (H + 24 * s, H + 6 * s), (H, H + 32 * s), (H - 24 * s, H + 6 * s)])
        ink_fill(c, shield, LIGHT, fade)
        fill(c, polyb([(H - 18 * s, H - 24 * s), (H, H - 24 * s), (H, H + 22 * s), (H - 17 * s, H + 3 * s)]), HI, fade)
    render(f'BuffGuard_{f:02d}', draw)

def buffspeed(f, N=8):
    t = f / (N - 1)
    def draw(c):
        fade = 1 - max(0, (t - 0.6) / 0.4) * 0.9
        for k in range(3):
            rot = t * 5 + k * 2.1
            rr = 40 + k * 26
            for seg in range(2):
                a0 = rot + seg * math.pi
                ink_fill(c, crescent(H, H + 10, rr, a0, 1.6, 12 - k * 2), LIGHT if k else HI, fade * (1 - k * 0.2), lw=3)
        # streaks going up
        rnd = random.Random(8)
        for i in range(7):
            x = H + rnd.uniform(-90, 90); y = H + 70 - ((t * 160 + i * 37) % 160)
            c.new_path(); c.move_to(x, y); c.line_to(x, y + 30)
            v(c, INK, 0.7 * fade); c.set_line_width(6); c.stroke_preserve(); v(c, HI, fade); c.set_line_width(2.5); c.stroke()
    render(f'BuffSpeed_{f:02d}', draw)

def bufffocus(f, N=8):
    t = f / (N - 1)
    def draw(c):
        fade = 1 - max(0, (t - 0.65) / 0.35) * 0.9
        e = ease_out(min(1, t * 1.4))
        R = 118 - 60 * e
        rot = (1 - e) * 1.2
        c.new_path(); c.arc(H, H, R, 0, 2 * math.pi)
        v(c, INK, fade); c.set_line_width(10); c.stroke_preserve(); v(c, LIGHT, fade); c.set_line_width(5); c.stroke()
        for k in range(4):
            a = rot + k * math.pi / 2
            x0, y0 = H + math.cos(a) * (R - 22), H + math.sin(a) * (R - 22)
            x1, y1 = H + math.cos(a) * (R + 18), H + math.sin(a) * (R + 18)
            c.new_path(); c.move_to(x0, y0); c.line_to(x1, y1)
            v(c, INK, fade); c.set_line_width(11); c.stroke_preserve(); v(c, HI, fade); c.set_line_width(5); c.stroke()
        # inner diamond
        d = 16 + 6 * math.sin(t * 6)
        ink_fill(c, polyb([(H, H - d), (H + d, H), (H, H + d), (H - d, H)]), HI, fade, lw=3)
        if t > 0.5:
            sparks(c, H, H, 5, (t - 0.5) * 2, 4, 10, 50, fade)
    render(f'BuffFocus_{f:02d}', draw)

CLIPS = dict(crossslash=(crossslash, 10), whirl=(whirl, 10), sweep=(sweep, 8), pierce=(pierce, 8), cleave=(cleave, 8),
             quake=(quake, 10), icenova=(icenova, 8), arrowrain=(arrowrain, 8), hexsigil=(hexsigil, 8),
             buffpower=(buffpower, 8), buffguard=(buffguard, 8), buffspeed=(buffspeed, 8), bufffocus=(bufffocus, 8))

if __name__ == '__main__':
    fx.OUTDIR = sys.argv[1] if len(sys.argv) > 1 else 'fx2'
    os.makedirs(fx.OUTDIR, exist_ok=True)
    only = sys.argv[2:] or list(CLIPS)
    for name in only:
        fn, n = CLIPS[name]
        for f in range(n):
            fn(f, n)
