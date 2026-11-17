"""Small cairo toolkit that imitates the Layer Lab 2D Minimal map style:
warm dark-brown outlines, muted flat fills, a lighter top band and darker bottom band
inside each shape, slightly wobbly hand-drawn edges, soft flat ground shadows."""
import math
import random
import cairo

OUT = (0.36, 0.27, 0.24)        # outline (92, 68, 60)
LW = 4.0                        # outline width in px (100 px = 1 unit)


def rgb(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def mix(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def darker(c, t=0.18):
    return mix(c, (0.25, 0.16, 0.18), t)


def lighter(c, t=0.22):
    return mix(c, (1.0, 0.97, 0.88), t)


def canvas(w, h):
    s = cairo.ImageSurface(cairo.FORMAT_ARGB32, w, h)
    c = cairo.Context(s)
    c.set_line_join(cairo.LINE_JOIN_ROUND)
    c.set_line_cap(cairo.LINE_CAP_ROUND)
    return s, c


def save(s, path):
    s.flush()
    s.write_to_png(path)


# ---------------------------------------------------------------- paths
def wobble(points, amp=1.2, seed=0, step=10, closed=True):
    """Subdivide a polyline and push points sideways by smooth noise."""
    rnd = random.Random(seed)
    pts = list(points) + ([points[0]] if closed else [])
    out = []
    phase = rnd.random() * 10
    for i in range(len(pts) - 1):
        (x0, y0), (x1, y1) = pts[i], pts[i + 1]
        d = math.hypot(x1 - x0, y1 - y0)
        n = max(1, int(d / step))
        nx, ny = (-(y1 - y0) / d, (x1 - x0) / d) if d > 0 else (0, 0)
        for k in range(n):
            t = k / n
            w = math.sin(phase + (i * 7 + k) * 0.9) * amp * (0.5 + rnd.random() * 0.5)
            if k == 0:
                w = 0
            out.append((x0 + (x1 - x0) * t + nx * w, y0 + (y1 - y0) * t + ny * w))
    if not closed:
        out.append(pts[-1])
    return out


def poly(c, pts):
    c.move_to(*pts[0])
    for p in pts[1:]:
        c.line_to(*p)
    c.close_path()


def smooth(c, pts, closed=True, tension=0.5):
    """Catmull-Rom through points."""
    n = len(pts)
    if closed:
        P = [pts[-1]] + list(pts) + [pts[0], pts[1]]
    else:
        P = [pts[0]] + list(pts) + [pts[-1]]
    c.move_to(*P[1])
    rng = n if closed else n - 1
    for i in range(1, rng + 1):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        c1 = (p1[0] + (p2[0] - p0[0]) * tension / 3, p1[1] + (p2[1] - p0[1]) * tension / 3)
        c2 = (p2[0] - (p3[0] - p1[0]) * tension / 3, p2[1] - (p3[1] - p1[1]) * tension / 3)
        c.curve_to(*c1, *c2, *p2)
    if closed:
        c.close_path()


def rrect(c, x, y, w, h, r):
    r = min(r, w / 2, h / 2)
    c.new_sub_path()
    c.arc(x + w - r, y + r, r, -math.pi / 2, 0)
    c.arc(x + w - r, y + h - r, r, 0, math.pi / 2)
    c.arc(x + r, y + h - r, r, math.pi / 2, math.pi)
    c.arc(x + r, y + r, r, math.pi, 1.5 * math.pi)
    c.close_path()


def ellipse(c, cx, cy, rx, ry):
    c.save()
    c.translate(cx, cy)
    c.scale(rx, ry)
    c.new_sub_path()
    c.arc(0, 0, 1, 0, 2 * math.pi)
    c.restore()
    c.close_path()


def blob(cx, cy, rx, ry, n=14, amp=0.08, seed=0):
    rnd = random.Random(seed)
    return [(cx + math.cos(a) * rx * (1 + (rnd.random() - 0.5) * amp * 2),
             cy + math.sin(a) * ry * (1 + (rnd.random() - 0.5) * amp * 2))
            for a in [i * 2 * math.pi / n for i in range(n)]]


# ---------------------------------------------------------------- painting
def fill(c, color, a=1.0):
    c.set_source_rgba(*color, a)
    c.fill_preserve()


def stroke(c, color=OUT, w=LW, a=1.0):
    c.set_source_rgba(*color, a)
    c.set_line_width(w)
    c.stroke_preserve()


def shape(c, build, color, outline=True, top=0.0, bottom=0.0, band=0.3, lw=LW, ocolor=OUT, bbox=None, soft=False):
    """Fill a shape with base color; optional lighter top band / darker bottom band
    (clipped to the shape), then outline."""
    c.new_path()
    build(c)
    x0, y0, x1, y1 = c.fill_extents() if bbox is None else bbox
    fill(c, color)
    if soft and (top or bottom):
        c.save()
        c.clip_preserve()
        g = cairo.LinearGradient(0, y0, 0, y1)
        g.add_color_stop_rgba(0, *lighter(color, top), 1)
        g.add_color_stop_rgba(0.45, *color, 1)
        g.add_color_stop_rgba(1, *darker(color, bottom), 1)
        c.set_source(g)
        c.paint()
        c.restore()
        c.new_path()
        build(c)
    elif top or bottom:
        c.save()
        c.clip_preserve()
        h = y1 - y0
        if top:
            c.rectangle(x0 - 50, y0 - 50, x1 - x0 + 100, 50 + h * band)
            c.set_source_rgba(*lighter(color, top), 1)
            c.fill()
        if bottom:
            c.rectangle(x0 - 50, y1 - h * band, x1 - x0 + 100, h * band + 50)
            c.set_source_rgba(*darker(color, bottom), 1)
            c.fill()
        c.restore()
        c.new_path()
        build(c)
    if outline:
        stroke(c, ocolor, lw)
    c.new_path()


def line(c, pts, color=OUT, w=LW, a=1.0):
    c.new_path()
    c.move_to(*pts[0])
    for p in pts[1:]:
        c.line_to(*p)
    c.set_source_rgba(*color, a)
    c.set_line_width(w)
    c.stroke()


def curve(c, pts, color=OUT, w=LW, a=1.0):
    c.new_path()
    smooth(c, pts, closed=False)
    c.set_source_rgba(*color, a)
    c.set_line_width(w)
    c.stroke()


def shadow(c, cx, cy, rx, ry, a=0.22):
    c.new_path()
    ellipse(c, cx, cy, rx, ry)
    c.set_source_rgba(0.13, 0.16, 0.08, a)
    c.fill()


def tuft(c, x, y, s=1.0, color=(0.33, 0.45, 0.27)):
    for dx, ang in ((-4, -1.9), (0, -1.6), (4, -1.3)):
        line(c, [(x + dx * s, y), (x + dx * s + math.cos(ang) * 9 * s, y + math.sin(ang) * 9 * s)], color, 2.6 * s)
