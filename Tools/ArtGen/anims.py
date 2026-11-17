"""Looping overlay frames for the lobby village (center pivot, placed as children of the props)."""
import math, random, sys
from PIL import Image
from kit import *

SS = 2
OUTDIR = sys.argv[1] if len(sys.argv) > 1 else 'out'


def render(name, w, h, draw):
    s, c = canvas(w * SS, h * SS)
    c.scale(SS, SS)
    draw(c)
    s.flush()
    tmp = f'/tmp/{name}_ss.png'
    s.write_to_png(tmp)
    Image.open(tmp).resize((w, h), Image.LANCZOS).save(f'{OUTDIR}/{name}.png')


# ---- portal: swirling purple vortex clipped to the gate arch (arch inner 128 wide, top y 76, base y 270 in gate pixels)
PW, PH = 128, 170   # gate pixels x 136..264, y 76..246 -> center (200, 161)


def portal(f, n=8):
    t = f / n

    def draw(c):
        aw = 128
        def arch(c):
            c.move_to(0, PH)
            c.line_to(0, aw / 2 + 14)
            c.arc(aw / 2, aw / 2 + 14, aw / 2, math.pi, 2 * math.pi)
            c.line_to(aw, PH)
            c.close_path()
        c.save(); arch(c); c.clip()
        cx, cy = PW / 2, 104
        g = cairo.RadialGradient(cx, cy, 4, cx, cy, 110)
        g.add_color_stop_rgba(0, *rgb('f3e6ff'), 1)
        g.add_color_stop_rgba(0.18, *rgb('c6a3f0'), 1)
        g.add_color_stop_rgba(0.55, *rgb('7a56b8'), 1)
        g.add_color_stop_rgba(1, *rgb('3b2f45'), 1)
        c.set_source(g); c.paint()
        # spiral arms
        for arm in range(4):
            c.new_path()
            for k in range(60):
                r = 6 + k * 1.6
                a = arm * math.pi / 2 + t * 2 * math.pi + k * 0.09
                x, y = cx + math.cos(a) * r, cy + math.sin(a) * r * 1.15
                (c.move_to if k == 0 else c.line_to)(x, y)
            c.set_source_rgba(*rgb('e7d6ff'), 0.55)
            c.set_line_width(5); c.stroke()
            c.new_path()
            for k in range(60):
                r = 6 + k * 1.6
                a = arm * math.pi / 2 + math.pi / 4 + t * 2 * math.pi + k * 0.09
                x, y = cx + math.cos(a) * r, cy + math.sin(a) * r * 1.15
                (c.move_to if k == 0 else c.line_to)(x, y)
            c.set_source_rgba(*rgb('5a3f8a'), 0.5)
            c.set_line_width(4); c.stroke()
        # sparkles drifting inward
        rnd = random.Random(1)
        for i in range(10):
            ph = (rnd.random() + t) % 1.0
            a = rnd.random() * 2 * math.pi + ph * 2
            r = 70 * (1 - ph)
            x, y = cx + math.cos(a) * r, cy + math.sin(a) * r
            s = 2.5 + 2 * (1 - ph)
            c.new_path(); c.move_to(x, y - s * 1.8); c.line_to(x + s * 0.5, y); c.line_to(x, y + s * 1.8); c.line_to(x - s * 0.5, y); c.close_path()
            c.set_source_rgba(1, 1, 1, 0.9 * math.sin(ph * math.pi)); c.fill()
        c.restore()
        # inner rim shadow so it sits in the arch
        c.new_path(); arch(c); c.set_source_rgba(0.12, 0.08, 0.16, 0.5); c.set_line_width(10); c.stroke()
    render(f'Anim_Portal_{f:02d}', PW, PH, draw)


# ---- torch flame (36x48), bottom of flame sits on the cup
def flame(f, n=6):
    t = f / n

    def draw(c):
        sway = math.sin(t * 2 * math.pi) * 3
        h = 34 + math.sin(t * 4 * math.pi) * 3
        def outer(c):
            smooth(c, [(18, 46), (6, 34), (9, 22), (18 + sway, 46 - h), (27, 22), (30, 34)], tension=0.55)
        g = cairo.RadialGradient(18, 46, 2, 18, 46, 60)
        c.save(); outer(c); c.set_source_rgba(*rgb('e8835a'), 1); c.fill_preserve(); c.set_source_rgba(*OUT, 1); c.set_line_width(3); c.stroke(); c.restore()
        c.new_path(); smooth(c, [(18, 44), (11, 35), (13, 26), (18 + sway * 0.7, 46 - h * 0.72), (23, 26), (25, 35)], tension=0.55)
        c.set_source_rgba(*rgb('f2c46a'), 1); c.fill()
        c.new_path(); smooth(c, [(18, 43), (15, 37), (18 + sway * 0.4, 46 - h * 0.45), (21, 37)], tension=0.55)
        c.set_source_rgba(*rgb('fff3cf'), 1); c.fill()
        # ember
        ey = 18 - (t * 30) % 16
        c.new_path(); ellipse(c, 18 - sway, ey, 1.8, 1.8); c.set_source_rgba(*rgb('f2c46a'), 1 - ((t * 30) % 16) / 16); c.fill()
    render(f'Anim_Flame_{f:02d}', 36, 48, draw)


# ---- chimney smoke (56x80), puffs rise from the bottom center
def smoke(f, n=8):
    t = f / n

    def draw(c):
        for i in range(3):
            ph = (t + i / 3) % 1.0
            y = 72 - ph * 62
            x = 22 + math.sin(ph * 4 + i) * 6 + ph * 10
            r = 7 + ph * 9
            a = 0.85 * (1 - ph) ** 0.8
            c.new_path(); smooth(c, blob(x, y, r, r * 0.85, 8, 0.12, i), tension=0.6)
            c.set_source_rgba(*rgb('ece6df'), a); c.fill_preserve()
            c.set_source_rgba(*rgb('a69c96'), a); c.set_line_width(2.6); c.stroke()
    render(f'Anim_Smoke_{f:02d}', 56, 80, draw)


# ---- lamp glow (80x80) soft pulsing halo
def glow(f, n=6):
    t = f / n

    def draw(c):
        p = 0.5 + 0.5 * math.sin(t * 2 * math.pi)
        r = 30 + p * 6
        g = cairo.RadialGradient(40, 40, 4, 40, 40, r)
        g.add_color_stop_rgba(0, *rgb('fff1bf'), 0.55 + 0.15 * p)
        g.add_color_stop_rgba(0.5, *rgb('ffd986'), 0.22 + 0.08 * p)
        g.add_color_stop_rgba(1, *rgb('ffd986'), 0)
        c.set_source(g); c.paint()
    render(f'Anim_LampGlow_{f:02d}', 80, 80, draw)


# ---- waving banner (40x72), hangs from the top edge (top-center is the rod)
def banner(f, n=6):
    t = f / n

    def draw(c):
        def wave(y):
            return math.sin(t * 2 * math.pi + y * 0.07) * (y / 64) * 4
        left = [(5 + wave(y), y) for y in range(4, 68, 8)]
        right = [(35 + wave(y), y) for y in range(4, 68, 8)]
        tip = (20 + wave(58), 58)
        pts = [(5, 4), (35, 4)] + right[1:] + [tip] + left[1:][::-1]
        def ban(c):
            c.move_to(*pts[0])
            for p in pts[1:]:
                c.line_to(*p)
            c.close_path()
        col = rgb('c46a6a')
        shape(c, ban, col, top=0.2, bottom=0.15, band=0.25, lw=3.4)
        y1 = 14
        line(c, [(9 + wave(y1), y1), (31 + wave(y1), y1)], rgb('e3bb5f'), 3)
        shape(c, lambda c: ellipse(c, 20 + wave(32), 32, 7, 7), rgb('e3bb5f'), lw=2.6)
        line(c, [(18 + wave(30), 30), (19 + wave(29), 28)], (1, 1, 1), 1.6, 0.7)
    render(f'Anim_Banner_{f:02d}', 40, 72, draw)


if __name__ == '__main__':
    import os
    os.makedirs(OUTDIR, exist_ok=True)
    for f in range(8):
        portal(f); smoke(f)
    for f in range(6):
        flame(f); glow(f); banner(f)
