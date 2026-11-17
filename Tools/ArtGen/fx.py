"""Skill FX frames as grayscale VALUE maps.
In game a gradient-map shader (Tower/SkillFxGradient) turns value into colour with the skill tint:
0 = dark outline tone, 0.5 = tint, 1 = near white. Without the shader the old multiply tint still works.
Frames: 256x256 (circle fills the square), Wind 256x96. 8 frames each, 18 fps."""
import math
import random
import sys
import cairo
from PIL import Image
from kit import smooth, blob, ellipse, wobble

SS = 2
N = 8
S = 256
H = S / 2
OUTDIR = sys.argv[1] if len(sys.argv) > 1 else 'fx'

INK = 0.06     # outline value
SHADE = 0.32
MID = 0.52
LIGHT = 0.74
HI = 0.97
OLW = 5.0      # outline width


def ease_out(t):
    return 1 - (1 - t) ** 2


def ease_in(t):
    return t * t


def render(name, draw, w=S, h=S):
    s = cairo.ImageSurface(cairo.FORMAT_ARGB32, w * SS, h * SS)
    c = cairo.Context(s)
    c.scale(SS, SS)
    c.set_line_join(cairo.LINE_JOIN_ROUND)
    c.set_line_cap(cairo.LINE_CAP_ROUND)
    draw(c)
    s.flush()
    tmp = f'/tmp/fx_{name}.png'
    s.write_to_png(tmp)
    Image.open(tmp).resize((w, h), Image.LANCZOS).save(f'{OUTDIR}/{name}.png')


def v(c, val, a=1.0):
    c.set_source_rgba(val, val, val, a)


def ink_fill(c, build, val, a=1.0, lw=OLW, ink=INK):
    """Outline first (wide), then fill on top -> outline only shows outside. Package style."""
    c.new_path(); build(c)
    v(c, ink, a); c.set_line_width(lw * 2); c.stroke_preserve()
    v(c, val, a); c.fill()
    c.new_path()


def fill(c, build, val, a=1.0):
    c.new_path(); build(c); v(c, val, a); c.fill(); c.new_path()


def clip_fill(c, clip_build, build, val, a=1.0):
    c.save(); c.new_path(); clip_build(c); c.clip()
    fill(c, build, val, a)
    c.restore()


def radial(c, cx, cy, r, stops, sy=1.0):
    c.save()
    c.translate(cx, cy); c.scale(1, sy)
    g = cairo.RadialGradient(0, 0, 0, 0, 0, r)
    for o, val, a in stops:
        g.add_color_stop_rgba(o, val, val, val, a)
    c.set_source(g)
    c.arc(0, 0, r, 0, 2 * math.pi)
    c.fill()
    c.restore()


def star4(cx, cy, r, rot=0.0, thin=0.28):
    pts = []
    for i in range(8):
        a = rot + i * math.pi / 4
        rr = r if i % 2 == 0 else r * thin
        pts.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr))
    return pts


def polyb(pts):
    def b(c):
        c.move_to(*pts[0])
        for p in pts[1:]:
            c.line_to(*p)
        c.close_path()
    return b


def crescent(cx, cy, r, a0, sweep, thick, taper=True, inner_shift=0.0):
    """Blade-like crescent: thick in the middle, pointed at both ends."""
    n = 40
    outer, inner = [], []
    for i in range(n + 1):
        t = i / n
        a = a0 + sweep * t
        w = thick * (math.sin(t * math.pi) ** 0.8 if taper else 1)
        outer.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
        inner.append((cx + math.cos(a) * (r - w), cy + math.sin(a) * (r - w)))
    return polyb(outer + inner[::-1])


# ================================================================ Slash
def slash(f):
    t = f / (N - 1)

    def draw(c):
        sweep = math.radians(70 + 250 * ease_out(min(1, t * 1.5)))
        head = math.radians(-100) + sweep
        tail_a = head - sweep
        fade = 1 - max(0, (t - 0.55) / 0.45) * 0.8
        r = 112
        thick = 34 * (1 - 0.55 * t)
        # trailing afterimage (soft)
        ink_fill(c, crescent(H, H, r - 6, tail_a - 0.25, sweep * 0.9, thick * 0.55), SHADE + 0.1, 0.55 * fade, lw=3)
        # main blade
        ink_fill(c, crescent(H, H, r, tail_a, sweep, thick), MID, fade)
        # light edge along the outer rim
        ink_fill(c, crescent(H, H, r - 2, tail_a + sweep * 0.25, sweep * 0.75, thick * 0.45), LIGHT, fade, lw=0.01)
        ink_fill(c, crescent(H, H, r - 3, tail_a + sweep * 0.5, sweep * 0.5, thick * 0.18), HI, fade, lw=0.01)
        # speed lines inside
        rnd = random.Random(f)
        for i in range(2):
            rr = r - thick - 12 - i * 14
            a1 = head - 0.35 - i * 0.3
            a2 = a1 - sweep * (0.3 + 0.1 * i)
            c.new_path(); c.arc(H, H, rr, a2, a1)
            v(c, INK, 0.7 * fade); c.set_line_width(7); c.stroke_preserve()
            v(c, LIGHT, 0.9 * fade); c.set_line_width(3); c.stroke()
        # sparks at the blade tip
        tx, ty = H + math.cos(head) * (r - thick / 2), H + math.sin(head) * (r - thick / 2)
        for i in range(4):
            a = head + math.pi / 2 + (i - 1.5) * 0.5 + rnd.uniform(-0.2, 0.2)
            d = 8 + 24 * t + rnd.uniform(0, 10)
            ink_fill(c, polyb(star4(tx + math.cos(a) * d, ty + math.sin(a) * d, 9 * (1 - t * 0.6), a)), HI, fade, lw=2.5)
    render(f'Slash_{f:02d}', draw)


# ================================================================ Shockwave (ground)
def shockwave(f):
    t = f / (N - 1)

    def draw(c):
        e = ease_out(t)
        fade = 1 - max(0, (t - 0.45) / 0.55) * 0.8
        rnd = random.Random(3)
        # cracks in the ground (stay, fade late)
        for i in range(8):
            a = i * math.pi / 4 + rnd.uniform(-0.25, 0.25)
            pts = [(H, H)]
            d = 0
            length = (52 + rnd.uniform(0, 34)) * min(1, t * 3 + 0.3)
            while d < length:
                d += 14
                aa = a + rnd.uniform(-0.35, 0.35)
                pts.append((H + math.cos(aa) * d, H + math.sin(aa) * d))
            c.new_path(); c.move_to(*pts[0])
            for p in pts[1:]:
                c.line_to(*p)
            v(c, INK, 0.85 * fade); c.set_line_width(6 - i % 2 * 2); c.stroke()
        # impact disc
        radial(c, H, H, 60 * (1 - 0.5 * t), [(0, HI, 0.9 * (1 - t)), (1, LIGHT, 0)])
        # expanding ring (thick band with light top edge)
        r = 26 + 92 * e
        th = 26 * (1 - 0.65 * t) + 5

        def ring(c):
            c.new_sub_path(); c.arc(H, H, r, 0, 2 * math.pi)
            c.new_sub_path(); c.arc_negative(H, H, max(1, r - th), 2 * math.pi, 0)
        ink_fill(c, ring, MID, fade)

        def ring_hi(c):
            c.new_sub_path(); c.arc(H, H, r - 2, 0, 2 * math.pi)
            c.new_sub_path(); c.arc_negative(H, H, max(1, r - th * 0.45), 2 * math.pi, 0)
        fill(c, ring_hi, LIGHT, fade)
        # dust puffs on the ring
        if t > 0.15:
            for i in range(6):
                a = i * 2 * math.pi / 6 + 0.5
                px, py = H + math.cos(a) * (r + 4), H + math.sin(a) * (r + 4)
                pr = (9 + (i % 3) * 3) * (0.5 + 0.8 * t)
                ink_fill(c, lambda c, px=px, py=py, pr=pr, i=i: smooth(c, blob(px, py, pr * 1.3, pr, 7, 0.15, i), tension=0.6), LIGHT, 0.75 * fade * (1 - t * 0.5), lw=2.5, ink=SHADE)
        # flying rocks
        for i in range(6):
            a = i * math.pi / 3 + 0.4
            d = r * 0.8
            x = H + math.cos(a) * d
            y = H + math.sin(a) * d - 34 * math.sin(t * math.pi)
            sz = 10 - 4 * t
            rock = polyb([(x - sz, y + 2), (x - sz * 0.4, y - sz), (x + sz, y - sz * 0.3), (x + sz * 0.6, y + sz)])
            ink_fill(c, rock, SHADE + 0.08, fade, lw=3.5)
            fill(c, polyb([(x - sz * 0.6, y), (x - sz * 0.3, y - sz * 0.7), (x + sz * 0.6, y - sz * 0.2)]), LIGHT, fade)
    render(f'Shockwave_{f:02d}', draw)


# ================================================================ Fire (explosion; also loops as a fire zone)
def fireball_blob(cx, cy, r, seed, lumps=11, amp=0.18):
    rnd = random.Random(seed)
    pts = []
    for i in range(lumps * 2):
        a = i * math.pi / lumps
        rr = r * (1 + (amp if i % 2 == 0 else -amp * 0.4) + rnd.uniform(-0.05, 0.05))
        pts.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr))
    return pts


def fire(f):
    t = f / (N - 1)

    def draw(c):
        g = 0.45 + 0.55 * ease_out(min(1, t * 1.4))
        fade = 1 - max(0, (t - 0.5) / 0.5) * 0.85
        # flame tongues around
        rnd = random.Random(5)
        for i in range(7):
            a = -math.pi / 2 + (i - 3) * 0.62 + math.sin(f * 0.9 + i) * 0.1
            ln = (40 + 20 * math.sin(f * 1.7 + i * 2.1)) * g * (1.2 - abs(i - 3) * 0.12)
            root = 50 * g
            bx, by = H + math.cos(a) * root, H + math.sin(a) * root
            w = 24 * g
            tip = (H + math.cos(a) * (root + ln) + math.sin(f + i) * 6, H + math.sin(a) * (root + ln) - 16 * g)
            pts = [(bx + math.cos(a + 1.57) * w, by + math.sin(a + 1.57) * w),
                   ((bx + tip[0]) / 2 + math.cos(a + 1.9) * w * 0.5, (by + tip[1]) / 2 + math.sin(a + 1.9) * w * 0.5),
                   tip,
                   ((bx + tip[0]) / 2 + math.cos(a - 1.2) * w * 0.4, (by + tip[1]) / 2 + math.sin(a - 1.2) * w * 0.4),
                   (bx + math.cos(a - 1.57) * w, by + math.sin(a - 1.57) * w)]
            ink_fill(c, lambda c, pts=pts: smooth(c, pts, tension=0.75), MID, fade)
        # body: three nested lumpy layers
        layers = [(66, MID + 0.04, 0), (50, LIGHT, 1), (30, HI, 2)]
        for r, val, k in layers:
            pts = fireball_blob(H + (k * 3), H + 6 - k * 6, r * g, f * 3 + k, 7 + k, 0.1)
            if k == 0:
                ink_fill(c, lambda c, pts=pts: smooth(c, pts, tension=0.6), val, fade)
            else:
                fill(c, lambda c, pts=pts: smooth(c, pts, tension=0.6), val, fade)
        # smoke puffs late
        if t > 0.35:
            st = (t - 0.35) / 0.65
            for i in range(5):
                a = -math.pi / 2 + (i - 2) * 0.55
                d = 60 + 40 * st
                x, y = H + math.cos(a) * d, H + math.sin(a) * d - 20 * st
                r = 14 + 10 * st
                ink_fill(c, lambda c, x=x, y=y, r=r, i=i: smooth(c, blob(x, y, r, r * 0.9, 8, 0.15, i + 9), tension=0.6), SHADE, 0.85 * (1 - st), lw=3.5)
        # embers
        for i in range(8):
            a = rnd.uniform(0, 2 * math.pi)
            d = (40 + rnd.uniform(0, 30)) + 70 * t
            x, y = H + math.cos(a) * d, H + math.sin(a) * d - 20 * t
            ink_fill(c, lambda c, x=x, y=y: ellipse(c, x, y, 4, 4), HI, fade * (1 - t * 0.5), lw=2)
    render(f'Fire_{f:02d}', draw)


# ================================================================ Arcane (magic circle)
def arcane(f):
    t = f / (N - 1)

    def draw(c):
        e = ease_out(min(1, t * 1.6))
        fade = 1 - max(0, (t - 0.55) / 0.45) * 0.8
        rot = t * 1.2
        R = 64 + 52 * e
        radial(c, H, H, R + 6, [(0, LIGHT, 0.55 * fade), (0.7, MID, 0.25 * fade), (1, MID, 0)])

        def circle(c, r, w, val, a=1.0):
            c.new_path(); c.arc(H, H, r, 0, 2 * math.pi)
            v(c, INK, a * fade); c.set_line_width(w + 5); c.stroke_preserve()
            v(c, val, a * fade); c.set_line_width(w); c.stroke()
        circle(c, R, 6, HI)
        circle(c, R - 18, 3.5, LIGHT)
        # rune ticks between circles
        for i in range(24):
            a = rot + i * 2 * math.pi / 24
            r0, r1 = R - 14, R - 4
            if i % 3 == 0:
                c.new_path(); c.arc(H + math.cos(a) * (R - 9), H + math.sin(a) * (R - 9), 3.2, 0, 2 * math.pi)
                v(c, HI, fade); c.fill()
            else:
                c.new_path(); c.move_to(H + math.cos(a) * r0, H + math.sin(a) * r0); c.line_to(H + math.cos(a) * r1, H + math.sin(a) * r1)
                v(c, LIGHT, fade); c.set_line_width(2.5); c.stroke()
        # hexagram rotating the other way
        for k in range(2):
            pts = [(H + math.cos(-rot * 1.4 + k * math.pi / 3 + i * 2 * math.pi / 3) * (R - 22),
                    H + math.sin(-rot * 1.4 + k * math.pi / 3 + i * 2 * math.pi / 3) * (R - 22)) for i in range(3)]
            c.new_path(); polyb(pts)(c)
            v(c, INK, 0.8 * fade); c.set_line_width(7); c.stroke_preserve()
            v(c, LIGHT, fade); c.set_line_width(3); c.stroke()
        # orbiting stars
        for i in range(4):
            a = rot * 2 + i * math.pi / 2
            ink_fill(c, polyb(star4(H + math.cos(a) * R, H + math.sin(a) * R, 15, a)), HI, fade, lw=3)
        # central burst
        cs = 58 * (1 - 0.55 * t) * (0.6 + 0.4 * e)
        ink_fill(c, polyb(star4(H, H, cs, rot * 0.6, 0.22)), LIGHT, fade)
        fill(c, polyb(star4(H, H, cs * 0.6, rot * 0.6, 0.22)), HI, fade)
    render(f'Arcane_{f:02d}', draw)


# ================================================================ Holy (light pillar; also loops as sanctum)
def holy(f):
    t = f / (N - 1)

    def draw(c):
        fade = 1 - max(0, (t - 0.6) / 0.4) * 0.7
        gy = H + 44
        ring_r = 104 * (0.7 + 0.3 * ease_out(t))
        # ground halo (ellipse)
        c.save(); c.translate(H, gy); c.scale(1, 0.5)
        g = cairo.RadialGradient(0, 0, 0, 0, 0, ring_r)
        g.add_color_stop_rgba(0, HI, HI, HI, 0.7 * fade)
        g.add_color_stop_rgba(0.75, LIGHT, LIGHT, LIGHT, 0.45 * fade)
        g.add_color_stop_rgba(1, MID, MID, MID, 0)
        c.set_source(g); c.arc(0, 0, ring_r, 0, 2 * math.pi); c.fill()
        for rr, w, val in ((ring_r * 0.92, 5, HI), (ring_r * 0.7, 3, LIGHT)):
            c.new_path(); c.arc(0, 0, rr, 0, 2 * math.pi)
            c.restore(); c.save(); c.translate(H, gy); c.scale(1, 0.5)
            c.new_path(); c.arc(0, 0, rr, 0, 2 * math.pi)
            c.restore()
            v(c, INK, 0.6 * fade); c.set_line_width(w + 4); c.stroke_preserve()
            v(c, val, fade); c.set_line_width(w); c.stroke()
            c.save(); c.translate(H, gy); c.scale(1, 0.5)
        c.restore()
        # pillar
        w = (34 + 14 * math.sin(t * math.pi)) * (1 - 0.35 * t)
        top = -10

        def pillar(c, ww):
            c.move_to(H - ww * 0.75, top); c.line_to(H + ww * 0.75, top)
            c.line_to(H + ww, gy); c.curve_to(H + ww, gy + ww * 0.5, H - ww, gy + ww * 0.5, H - ww, gy)
            c.close_path()
        ink_fill(c, lambda c: pillar(c, w), LIGHT, 0.95 * fade)
        fill(c, lambda c: pillar(c, w * 0.5), HI, fade)
        # light rays streaming down
        for i in range(3):
            y = (t * 3 + i / 3) % 1 * (gy - top) + top
            c.new_path(); c.move_to(H - w * 0.3 + i * w * 0.3, y); c.line_to(H - w * 0.3 + i * w * 0.3, y + 26)
            v(c, 1.0, 0.9 * fade); c.set_line_width(3); c.stroke()
        # rising sparkles / feathers
        rnd = random.Random(2)
        for i in range(7):
            ph = (t + i / 7) % 1
            x = H + rnd.uniform(-90, 90)
            y = gy + 10 - ph * 120
            s = 11 * math.sin(ph * math.pi) + 3
            ink_fill(c, polyb(star4(x, y, s, 0, 0.3)), HI, fade * math.sin(ph * math.pi), lw=2.5)
    render(f'Holy_{f:02d}', draw)


# ================================================================ Heal (ground)
def plus(cx, cy, s):
    k = s / 3
    return polyb([(cx - k, cy - s), (cx + k, cy - s), (cx + k, cy - k), (cx + s, cy - k), (cx + s, cy + k), (cx + k, cy + k),
                  (cx + k, cy + s), (cx - k, cy + s), (cx - k, cy + k), (cx - s, cy + k), (cx - s, cy - k), (cx - k, cy - k)])


def heal(f):
    t = f / (N - 1)

    def draw(c):
        fade = 1 - max(0, (t - 0.55) / 0.45) * 0.8
        R = 116
        radial(c, H, H, R, [(0, LIGHT, 0.45 * fade), (0.8, MID, 0.3 * fade), (1, MID, 0.0)])
        # ring with leaf-like dashes
        c.new_path(); c.arc(H, H, R - 6, 0, 2 * math.pi)
        v(c, INK, 0.7 * fade); c.set_line_width(10); c.stroke_preserve()
        v(c, LIGHT, fade); c.set_line_width(5); c.stroke()
        for i in range(12):
            a = i * math.pi / 6 + t * 0.6
            x, y = H + math.cos(a) * (R - 6), H + math.sin(a) * (R - 6)
            leaf = lambda c, x=x, y=y, a=a: (c.save(), c.translate(x, y), c.rotate(a + math.pi / 2), ellipse(c, 0, 0, 9, 4.5), c.restore())
            ink_fill(c, leaf, HI if i % 2 else LIGHT, fade, lw=2.5)
        # inner expanding wave
        r2 = 30 + 80 * ease_out(t)
        c.new_path(); c.arc(H, H, r2, 0, 2 * math.pi)
        v(c, HI, 0.6 * (1 - t)); c.set_line_width(6); c.stroke()
        # rising crosses
        rnd = random.Random(4)
        for i in range(7):
            ph = (t * 0.9 + i / 7) % 1
            a = rnd.uniform(0, 2 * math.pi)
            d = rnd.uniform(10, 80)
            x = H + math.cos(a) * d
            y = H + math.sin(a) * d * 0.6 + 30 - ph * 90
            s = 6 + 9 * math.sin(ph * math.pi)
            al = fade * math.sin(ph * math.pi) ** 0.6
            ink_fill(c, plus(x, y, s), LIGHT, al, lw=3)
            fill(c, plus(x - 1, y - 1, s * 0.55), HI, al)
    render(f'Heal_{f:02d}', draw)


# ================================================================ Aura (loop, under the character)
def aura(f):
    t = f / N  # loop

    def draw(c):
        cy = H + 62
        rx, ry = 100, 46
        # back half of the ring
        def ring(c, a0, a1, rr=1.0):
            c.save(); c.translate(H, cy); c.scale(1, ry / rx); c.new_path(); c.arc(0, 0, rx * rr, a0, a1); c.restore()
        c.new_path(); ring(c, math.pi, 2 * math.pi)
        v(c, INK, 0.7); c.set_line_width(14); c.stroke_preserve(); v(c, MID, 1); c.set_line_width(8); c.stroke()
        # glow disc
        c.save(); c.translate(H, cy); c.scale(1, ry / rx)
        g = cairo.RadialGradient(0, 0, 0, 0, 0, rx)
        g.add_color_stop_rgba(0, LIGHT, LIGHT, LIGHT, 0.55)
        g.add_color_stop_rgba(1, MID, MID, MID, 0.05)
        c.set_source(g); c.arc(0, 0, rx, 0, 2 * math.pi); c.fill(); c.restore()
        # rising flame wisps around the rim
        for i in range(7):
            a = i * 2 * math.pi / 7 + 0.2
            ph = (t + i * 0.37) % 1
            bx = H + math.cos(a) * rx * 0.86
            by = cy + math.sin(a) * ry * 0.86
            hgt = 34 + 26 * math.sin(ph * math.pi)
            y0 = by - ph * 60
            al = math.sin(ph * math.pi)
            w = 15 * (1 - ph * 0.35)
            sway = math.sin(ph * 6 + i) * 7
            pts = [(bx, y0 + w * 0.6), (bx - w, y0 - w * 0.2), (bx - w * 0.55 + sway * 0.4, y0 - hgt * 0.55), (bx + sway, y0 - hgt), (bx + w * 0.55 + sway * 0.4, y0 - hgt * 0.55), (bx + w, y0 - w * 0.2)]
            ink_fill(c, lambda c, pts=pts: smooth(c, pts, tension=0.8), LIGHT, al * 0.95, lw=3.5)
            pts2 = [(bx, y0 + 2), (bx - w * 0.45, y0 - w * 0.3), (bx + sway * 0.7, y0 - hgt * 0.7), (bx + w * 0.45, y0 - w * 0.3)]
            fill(c, lambda c, pts2=pts2: smooth(c, pts2, tension=0.8), HI, al)
        # front half of the ring + sparks
        c.new_path(); ring(c, 0, math.pi)
        v(c, INK, 0.8); c.set_line_width(14); c.stroke_preserve(); v(c, LIGHT, 1); c.set_line_width(8); c.stroke()
        c.new_path(); ring(c, 0.3, math.pi - 0.3, 0.98)
        v(c, HI, 1); c.set_line_width(3); c.stroke()
        for i in range(4):
            ph = (t * 2 + i / 4) % 1
            a = i * 1.7 + 0.5
            x = H + math.cos(a) * rx * 0.6
            y = cy - 20 - ph * 110
            ink_fill(c, polyb(star4(x, y, 8 * math.sin(ph * math.pi) + 2)), HI, math.sin(ph * math.pi), lw=2.5)
    render(f'Aura_{f:02d}', draw)


# ================================================================ Poison (cloud; loops as a zone)
def poison(f):
    t = f / (N - 1)
    lt = f / N

    def draw(c):
        g = 0.62 + 0.38 * ease_out(min(1, t * 1.5))
        rnd = random.Random(6)
        # cloud: union of lumps, outline only around the union
        lumps = []
        for i in range(7):
            a = i * 2 * math.pi / 7 + 0.2 + 0.2
            d = 60 * g
            r = (38 + 7 * math.sin(lt * 2 * math.pi + i * 1.3)) * g
            lumps.append((H + math.cos(a) * d, H + math.sin(a) * d * 0.85 + 6, r))
        lumps.append((H, H, 64 * g))

        def union(c):
            for x, y, r in lumps:
                c.new_sub_path(); c.arc(x, y, r, 0, 2 * math.pi)
        c.new_path(); union(c); v(c, INK, 1); c.set_line_width(OLW * 2); c.stroke()
        c.new_path(); union(c); v(c, MID - 0.06, 1); c.fill()
        # top-light on each lump
        for x, y, r in lumps:
            fill(c, lambda c, x=x, y=y, r=r: ellipse(c, x - r * 0.15, y - r * 0.3, r * 0.65, r * 0.5), MID + 0.12, 1)
        # dark spots / swirls
        for i in range(5):
            a = rnd.uniform(0, 2 * math.pi); d = rnd.uniform(10, 60) * g
            x, y = H + math.cos(a) * d, H + math.sin(a) * d
            fill(c, lambda c, x=x, y=y, i=i: ellipse(c, x, y, 7 + i % 3 * 2, 5 + i % 2 * 2), SHADE, 0.9)
        # bubbles rising and popping (looping)
        brnd = random.Random(11)
        for i in range(6):
            ph = (lt + brnd.random()) % 1
            x = H + brnd.uniform(-70, 70) + math.sin(ph * 6 + i) * 4
            y = H + brnd.uniform(0, 50) - ph * 90
            r = 6 + 8 * ph
            if ph < 0.85:
                ink_fill(c, lambda c, x=x, y=y, r=r: ellipse(c, x, y, r, r), LIGHT, 1, lw=3)
                fill(c, lambda c, x=x, y=y, r=r: ellipse(c, x - r * 0.35, y - r * 0.35, r * 0.3, r * 0.3), HI)
            else:
                for k in range(5):
                    a = k * 2 * math.pi / 5
                    c.new_path(); c.move_to(x + math.cos(a) * r * 0.8, y + math.sin(a) * r * 0.8)
                    c.line_to(x + math.cos(a) * r * 1.4, y + math.sin(a) * r * 1.4)
                    v(c, LIGHT, 1); c.set_line_width(3); c.stroke()
    render(f'Poison_{f:02d}', draw)


# ================================================================ Lightning
def bolt_path(x0, y0, a, length, seed, segs=7):
    rnd = random.Random(seed)
    pts = [(x0, y0)]
    for i in range(1, segs + 1):
        d = length * i / segs
        off = rnd.uniform(-14, 14) if i < segs else 0
        pts.append((x0 + math.cos(a) * d + math.cos(a + 1.57) * off, y0 + math.sin(a) * d + math.sin(a + 1.57) * off))
    return pts


def lightning(f):
    t = f / (N - 1)

    def draw(c):
        fade = 1 - max(0, (t - 0.4) / 0.6) * 0.8
        flick = 1.0 if f % 2 == 0 else 0.8
        radial(c, H, H, 120, [(0, HI, 0.65 * fade * flick), (0.4, LIGHT, 0.3 * fade), (1, MID, 0)])
        for i in range(6):
            a = i * math.pi / 3 + (f % 3) * 0.25 + 0.2
            length = (96 + 22 * ease_out(t)) * (0.85 + 0.15 * ((i * 7 + f) % 3) / 2)
            pts = bolt_path(H, H, a, length, f * 31 + i)
            w = (8 - 4 * t)
            for lw, val, al in ((w + 8, INK, 0.95), (w, LIGHT, 1), (w * 0.4, HI, 1)):
                c.new_path(); c.move_to(*pts[0])
                for p in pts[1:]:
                    c.line_to(*p)
                v(c, val, al * fade); c.set_line_width(lw); c.stroke()
            # branch
            bp = pts[3]
            br = bolt_path(bp[0], bp[1], a + (0.6 if i % 2 else -0.6), length * 0.4, f * 17 + i, 3)
            for lw, val, al in ((w * 0.6 + 6, INK, 0.9), (w * 0.6, LIGHT, 1)):
                c.new_path(); c.move_to(*br[0])
                for p in br[1:]:
                    c.line_to(*p)
                v(c, val, al * fade); c.set_line_width(lw); c.stroke()
        # core flash: jagged burst
        cr = 46 * (1 - 0.55 * t) * flick
        burst = [(H + math.cos(i * math.pi / 6) * cr * (1 if i % 2 == 0 else 0.6), H + math.sin(i * math.pi / 6) * cr * (1 if i % 2 == 0 else 0.6)) for i in range(12)]
        ink_fill(c, polyb(burst), LIGHT, fade)
        fill(c, lambda c: ellipse(c, H, H, cr * 0.42, cr * 0.42), HI, fade)
        # spark dots
        rnd = random.Random(f)
        for i in range(6):
            a = rnd.uniform(0, 2 * math.pi); d = rnd.uniform(60, 115)
            ink_fill(c, polyb(star4(H + math.cos(a) * d, H + math.sin(a) * d, 7, a)), HI, fade, lw=2.5)
    render(f'Lightning_{f:02d}', draw)


# ================================================================ Wind (256x96, dash streak, direction = +x)
def wind(f):
    t = f / (N - 1)
    W2, H2 = 256, 96

    def draw(c):
        fade = 1 - max(0, (t - 0.35) / 0.65) * 0.8
        rnd = random.Random(8)
        # streaks with curly ends
        for i in range(5):
            y = 16 + i * 16
            x0 = 8 + 70 * t + (i % 2) * 26
            x1 = x0 + 150 - 60 * t - (i % 3) * 22
            wv = 5 - (i % 2) * 1.5
            dy = math.sin(i + t * 3) * 3
            streak = polyb([(x0, y), ((x0 + x1) * 0.55, y - wv + dy), (x1, y), ((x0 + x1) * 0.55, y + wv + dy)])
            c.new_path(); smooth(c, [(x0, y), ((x0 + x1) * 0.55, y - wv + dy), (x1, y), ((x0 + x1) * 0.55, y + wv + dy)], tension=0.4)
            v(c, INK, 0.85 * fade); c.set_line_width(5); c.stroke_preserve()
            v(c, LIGHT if i % 2 else HI, fade); c.fill()
        # front crescent gust
        fx = 196 + 30 * t
        ink_fill(c, crescent(fx - 30, 48, 40, -1.25, 2.5, 14), LIGHT, fade)
        fill(c, crescent(fx - 31, 48, 39, -0.9, 1.8, 5), HI, fade)
        # leaves / bits
        for i in range(4):
            x = (40 + i * 52 + 160 * t) % 250
            y = 20 + rnd.uniform(0, 56) + math.sin(t * 6 + i) * 6
            leaf = lambda c, x=x, y=y, i=i: (c.save(), c.translate(x, y), c.rotate(t * 8 + i), ellipse(c, 0, 0, 6, 3), c.restore())
            ink_fill(c, leaf, MID + 0.1, fade, lw=2.2)
    render(f'Wind_{f:02d}', draw, W2, H2)


# ================================================================ Frost (ground; ice spikes)
def frost(f):
    t = f / (N - 1)

    def draw(c):
        g = ease_out(min(1, t * 1.7))
        fade = 1 - max(0, (t - 0.6) / 0.4) * 0.8
        radial(c, H, H, 118, [(0, LIGHT, 0.4 * fade), (0.85, MID, 0.25 * fade), (1, MID, 0)])
        # frost cracks on the ground ring
        rnd = random.Random(9)
        for i in range(6):
            a = i * 1.05 + 0.4; d = 80
            fill(c, lambda c, a=a, d=d, i=i: smooth(c, blob(H + math.cos(a) * d, H + math.sin(a) * d, 26, 16, 8, 0.2, i), tension=0.6), LIGHT + 0.1, 0.22 * fade)
        shards = []
        for i in range(9):
            a = i * 2 * math.pi / 9 + rnd.uniform(-0.25, 0.25)
            d0 = 30 + rnd.uniform(0, 20)
            ln = (42 + rnd.uniform(0, 46)) * g + 4
            w = 10 + rnd.uniform(0, 8)
            shards.append((a, d0, ln, w))
        # draw back shards (upper half) first so lower ones overlap: sort by y
        shards.sort(key=lambda s: math.sin(s[0]))
        for a, d0, ln, w in shards:
            bx, by = H + math.cos(a) * d0, H + math.sin(a) * d0
            tip = (bx + math.cos(a) * ln, by + math.sin(a) * ln)
            l = (bx + math.cos(a - 1.57) * w, by + math.sin(a - 1.57) * w)
            r = (bx + math.cos(a + 1.57) * w, by + math.sin(a + 1.57) * w)
            ink_fill(c, polyb([l, tip, r]), MID + 0.06, fade)
            # light facet
            m = (bx + math.cos(a) * ln * 0.25, by + math.sin(a) * ln * 0.25)
            fill(c, polyb([l, tip, m]), LIGHT + 0.08, fade)
            c.new_path(); c.move_to(*m); c.line_to(*tip); v(c, HI, fade); c.set_line_width(2.5); c.stroke()
        # central snowflake
        sr = 40 * (1 - 0.25 * t) * (0.5 + 0.5 * g)
        rot = t * 0.5
        for k in range(6):
            a = rot + k * math.pi / 3
            x1, y1 = H + math.cos(a) * sr, H + math.sin(a) * sr
            for lw, val in ((13, INK), (6, HI)):
                c.new_path(); c.move_to(H, H); c.line_to(x1, y1)
                for side in (-0.6, 0.6):
                    mx, my = H + math.cos(a) * sr * 0.6, H + math.sin(a) * sr * 0.6
                    c.move_to(mx, my); c.line_to(mx + math.cos(a + side) * sr * 0.35, my + math.sin(a + side) * sr * 0.35)
                v(c, val, fade); c.set_line_width(lw); c.stroke()
        ink_fill(c, lambda c: ellipse(c, H, H, 9, 9), HI, fade, lw=3)
        # snow motes
        for i in range(8):
            a = rnd.uniform(0, 2 * math.pi); d = rnd.uniform(40, 110) + 20 * t
            fill(c, lambda c, a=a, d=d: ellipse(c, H + math.cos(a) * d, H + math.sin(a) * d - 15 * t, 3.5, 3.5), HI, fade)
    render(f'Frost_{f:02d}', draw)


if __name__ == '__main__':
    import os
    os.makedirs(OUTDIR, exist_ok=True)
    only = sys.argv[2:] or ['slash', 'shockwave', 'fire', 'arcane', 'holy', 'heal', 'aura', 'poison', 'lightning', 'wind', 'frost']
    for name in only:
        for f in range(N):
            globals()[name](f)
