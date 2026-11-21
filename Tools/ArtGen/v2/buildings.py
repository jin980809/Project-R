"""Village v2 buildings with an oblique 3/4 projection: front wall + right side wall + gable roof,
so they read as boxes with depth. Dusk palette, lit windows. Each sprite's pivot (14 px above the
bottom, horizontally centred) sits at the middle of the front wall's base.
Writes Prop_V2_<Name>.png and prints the meta used by TowerBootstrap (footprint, overlay pixel spots)."""
import math
import random
import sys
import json
import cairo
from PIL import Image
from kit import *

OUTDIR = sys.argv[1] if len(sys.argv) > 1 else 'out'
SS = 2
META = {}

DX, DY = 84, -60          # depth vector (to the back-right, upwards on screen)
WALL = rgb('ead7b6')
WALL_SIDE = rgb('c9b293')
TIMBER = rgb('9a6a55')
STONE = rgb('b5a693')
STONE_D = rgb('918271')
WIN = rgb('ffd87e')
WIN_HI = rgb('fff3c8')
WOOD = rgb('b47c5c')
WOOD_D = rgb('8d5d48')


class Canvas:
    """Draw in pivot-relative coords: (0,0) = front-base centre, +y down."""

    def __init__(self, name, left, right, top, bottom=40):
        half = max(left, right)
        self.w = int(math.ceil(half * 2))
        self.h = int(math.ceil(top + bottom))
        # pivot 14 px above bottom -> base line at h - 14; keep ground details below within 'bottom'
        self.h = int(math.ceil(top + 14 + max(0, bottom - 14)))
        self.ox = self.w / 2
        self.oy = self.h - 14 - max(0, bottom - 14)
        # make pivot exactly 14 px above bottom: shift so that oy = h - 14
        self.h = int(math.ceil(top)) + 14
        self.oy = self.h - 14
        self.name = name
        self.s = cairo.ImageSurface(cairo.FORMAT_ARGB32, self.w * SS, self.h * SS)
        self.c = cairo.Context(self.s)
        self.c.scale(SS, SS)
        self.c.translate(self.ox, self.oy)
        self.c.set_line_join(cairo.LINE_JOIN_ROUND)
        self.c.set_line_cap(cairo.LINE_CAP_ROUND)

    def save(self, meta=None):
        self.s.flush()
        tmp = f'/tmp/b_{self.name}.png'
        self.s.write_to_png(tmp)
        Image.open(tmp).resize((self.w, self.h), Image.LANCZOS).save(f'{OUTDIR}/{self.name}.png')
        m = {'w': self.w, 'h': self.h}
        if meta:
            # convert pivot-relative pixel spots to sprite pixel coords (top-left origin)
            for k, v in meta.items():
                if isinstance(v, list) and v and isinstance(v[0], (list, tuple)):
                    m[k] = [[round(self.ox + p[0], 1), round(self.oy + p[1], 1)] for p in v]
                else:
                    m[k] = v
        META[self.name] = m


def P(*pts):
    def b(c):
        poly(c, pts)
    return b


def ground_shadow(c, x0, x1, depth=DY, length=70):
    c.new_path()
    c.move_to(x0 - 10, 6)
    c.line_to(x1 + 14, 6)
    c.line_to(x1 + DX + length, depth * 0.3 + 4)
    c.line_to(x1 + DX + length * 0.6, depth + 2)
    c.line_to(x0 + DX * 0.5, depth + 2)
    c.close_path()
    c.set_source_rgba(0.14, 0.12, 0.2, 0.28)
    c.fill()


def lit_window(c, x, y, w, h, arch=False, frame=TIMBER, mullion=True):
    def frame_b(c):
        if arch:
            c.move_to(x - 4, y + h + 4); c.line_to(x - 4, y + w / 2)
            c.arc(x + w / 2, y + w / 2, w / 2 + 4, math.pi, 2 * math.pi)
            c.line_to(x + w + 4, y + h + 4); c.close_path()
        else:
            rrect(c, x - 4, y - 4, w + 8, h + 8, 4)
    shape(c, frame_b, frame, lw=3.2)

    def pane(c):
        if arch:
            c.move_to(x, y + h); c.line_to(x, y + w / 2)
            c.arc(x + w / 2, y + w / 2, w / 2, math.pi, 2 * math.pi)
            c.line_to(x + w, y + h); c.close_path()
        else:
            rrect(c, x, y, w, h, 2)
    c.new_path(); pane(c)
    g = cairo.LinearGradient(0, y, 0, y + h)
    g.add_color_stop_rgba(0, *WIN_HI, 1)
    g.add_color_stop_rgba(1, *WIN, 1)
    c.set_source(g); c.fill()
    if mullion:
        line(c, [(x + w / 2, y + 2), (x + w / 2, y + h)], frame, 3)
        line(c, [(x, y + h * 0.55), (x + w, y + h * 0.55)], frame, 3)
    # warm glow spilling outside the window
    c.save()
    g2 = cairo.RadialGradient(x + w / 2, y + h / 2, 2, x + w / 2, y + h / 2, max(w, h) * 1.1)
    g2.add_color_stop_rgba(0, 1, 0.85, 0.5, 0.0)
    g2.add_color_stop_rgba(0.55, 1, 0.85, 0.5, 0.18)
    g2.add_color_stop_rgba(1, 1, 0.85, 0.5, 0)
    c.set_source(g2)
    c.arc(x + w / 2, y + h / 2, max(w, h) * 1.1, 0, 2 * math.pi); c.fill()
    c.restore()


def side_window(c, x, y, w, h):
    """Window on the side wall, skewed along the depth vector."""
    k = DY / DX
    pts = [(x, y), (x + w, y + w * k), (x + w, y + w * k + h), (x, y + h)]
    shape(c, P(*[(p[0] - 3, p[1] - 3) if i in (0, 1) else (p[0] - 3, p[1] + 3) for i, p in enumerate(pts)]), TIMBER, lw=3)
    c.new_path(); poly(c, pts)
    c.set_source_rgba(*mix(WIN, WALL_SIDE, 0.25), 1); c.fill()
    line(c, [(x + w / 2, y + w * k / 2), (x + w / 2, y + w * k / 2 + h)], TIMBER, 2.6)


def door(c, cx, w, h, color=WOOD, lit=True):
    x = cx - w / 2

    def arch(c, pad=0):
        c.move_to(x - pad, 0); c.line_to(x - pad, -h + w / 2)
        c.arc(cx, -h + w / 2, w / 2 + pad, math.pi, 2 * math.pi)
        c.line_to(x + w + pad, 0); c.close_path()
    shape(c, lambda c: arch(c, 6), STONE, lw=3.4)
    shape(c, arch, color, bottom=0.18, band=0.3, lw=3.4)
    c.save(); c.new_path(); arch(c); c.clip()
    for i in range(1, 3):
        line(c, [(x + w * i / 3, -h), (x + w * i / 3, 0)], darker(color, 0.35), 2.2)
    c.restore()
    shape(c, lambda c: ellipse(c, x + w - 8, -h * 0.42, 3, 3), rgb('e8c35f'), lw=1.8)
    if lit:
        # small lit transom
        shape(c, lambda c: ellipse(c, cx, -h + w / 2 - 2, w * 0.28, w * 0.2), WIN, lw=2.4)
    shape(c, lambda c: rrect(c, x - 14, -4, w + 28, 10, 3), STONE, top=0.25, lw=3)


def box_walls(c, x0, x1, h, wall=WALL, side=WALL_SIDE, timber=True, stone_base=True, seed=0):
    """Front wall [x0,x1] from y=-h to 0, plus side wall to the back-right."""
    # side wall
    side_pts = [(x1, 0), (x1 + DX, DY), (x1 + DX, DY - h), (x1, -h)]
    shape(c, P(*side_pts), side, lw=LW)
    if timber:
        c.save(); c.new_path(); poly(c, side_pts); c.clip()
        line(c, [(x1, -h + 4), (x1 + DX, DY - h + 4)], darker(TIMBER, 0.1), 7)
        line(c, [(x1 + DX - 4, DY), (x1 + DX - 4, DY - h)], darker(TIMBER, 0.1), 7)
        c.restore()
        c.new_path(); poly(c, side_pts); stroke(c); c.new_path()
    # front wall
    def front(c):
        poly(c, wobble([(x0, 0), (x0, -h), (x1, -h), (x1, 0)], 0.6, seed, 14))
    shape(c, front, wall, outline=False, bottom=0.1, band=0.45)
    if timber:
        c.save(); c.new_path(); front(c); c.clip()
        for bx in (x0 + 1, x1 - 9):
            c.new_path(); c.rectangle(bx, -h, 8, h); c.set_source_rgba(*TIMBER, 1); c.fill()
        c.new_path(); c.rectangle(x0, -h, x1 - x0, 9); c.set_source_rgba(*TIMBER, 1); c.fill()
        if h > 220:
            c.new_path(); c.rectangle(x0, -h * 0.52, x1 - x0, 8); c.set_source_rgba(*TIMBER, 1); c.fill()
        rnd = random.Random(seed)
        for _ in range(5):
            px_, py_ = rnd.uniform(x0 + 20, x1 - 20), rnd.uniform(-h + 20, -30)
            line(c, [(px_, py_), (px_ + 7, py_ + 2)], darker(wall, 0.22), 1.6)
        c.restore()
    c.new_path(); front(c); stroke(c); c.new_path()
    if stone_base:
        shape(c, P((x0 - 4, 2), (x0 - 4, -16), (x1 + 4, -16), (x1 + 4, 2)), STONE, top=0.2, band=0.3, lw=3.4)
        shape(c, P((x1 + 4, 2), (x1 + 4 + DX, DY + 2), (x1 + 4 + DX, DY - 16), (x1 + 4, -16)), STONE_D, lw=3.4)
        rnd = random.Random(seed + 9)
        xx = x0
        while xx < x1 - 10:
            xx += rnd.uniform(18, 30)
            line(c, [(xx, -15), (xx, 0)], STONE_D, 2)


def gable_roof(c, x0, x1, h, rh, color, seed, overhang=20, rows=5):
    """Gable roof: ridge parallel to the front wall at half depth."""
    ye = -h + 4
    L = (x0 - overhang, ye + 8)
    R = (x1 + overhang, ye + 8)
    RL = (x0 - overhang + DX * 0.5, ye + DY * 0.5 - rh)
    RR = (x1 + overhang + DX * 0.5, ye + DY * 0.5 - rh)
    BR = (x1 + overhang + DX, ye + 8 + DY)
    # gable end wall (triangle under the roof on the right side)
    shape(c, P((x1, ye - 4), (x1 + DX, ye + DY - 4), (x1 + DX * 0.5, ye + DY * 0.5 - rh + 14)), darker(WALL_SIDE, 0.06), lw=LW)
    # back-slope edge strip seen on the right (ridge -> back eave)
    shape(c, P(RR, BR, (BR[0] + 2, BR[1] + 14), (RR[0] + 2, RR[1] + 12)), darker(color, 0.3), lw=3.6)
    # front slope with shingle rows
    face = wobble([L, R, RR, RL], 1.0, seed, 16)

    def f(c):
        smooth(c, face, tension=0.12)
    shape(c, f, color, outline=False)
    c.save(); c.new_path(); f(c); c.clip()
    for r in range(rows):
        t = (r + 1) / (rows + 0.5)
        yl = RL[1] + (L[1] - RL[1]) * t
        xl = RL[0] + (L[0] - RL[0]) * t
        xr = RR[0] + (R[0] - RR[0]) * t
        shade = lighter(color, 0.16) if r == 0 else darker(color, 0.045 * r)
        c.new_path(); c.rectangle(xl - 40, yl, xr - xl + 80, 200)
        c.set_source_rgba(*shade, 1); c.fill()
        step = 20
        c.new_path()
        xx = xl - 30 + (r % 2) * step / 2
        c.move_to(xx, yl)
        while xx < xr + 30:
            c.curve_to(xx + 3, yl + 8, xx + step - 3, yl + 8, xx + step, yl)
            xx += step
        c.set_source_rgba(*darker(color, 0.32), 1); c.set_line_width(2.2); c.stroke()
    c.restore()
    c.new_path(); f(c); stroke(c); c.new_path()
    # eave fascia and ridge cap
    shape(c, P((L[0] - 4, L[1] - 2), (R[0] + 4, R[1] - 2), (R[0] + 4, R[1] + 8), (L[0] - 4, L[1] + 8)), darker(color, 0.3), lw=3.4)
    shape(c, P((RL[0] - 6, RL[1] - 5), (RR[0] + 6, RR[1] - 5), (RR[0] + 6, RR[1] + 5), (RL[0] - 6, RL[1] + 5)), lighter(color, 0.12), lw=3.4)
    # right roof edge (front slope)
    line(c, [R, RR], OUT, 4)
    return RL, RR


def chimney(c, x, base_y, w, h):
    shape(c, P((x, base_y), (x, base_y - h), (x + w, base_y - h), (x + w, base_y)), STONE, top=0.12, lw=3.6)
    shape(c, P((x + w, base_y), (x + w + 12, base_y - 9), (x + w + 12, base_y - h - 9), (x + w, base_y - h)), STONE_D, lw=3.4)
    for yy in range(int(base_y - h + 12), int(base_y), 12):
        line(c, [(x + 2, yy), (x + w - 2, yy)], STONE_D, 2)
    shape(c, P((x - 4, base_y - h - 2), (x - 4, base_y - h - 10), (x + w + 16, base_y - h - 18), (x + w + 16, base_y - h - 8)), lighter(STONE, 0.1), lw=3.2)
    return (x + w / 2 + 4, base_y - h - 14)


def hanging_sign(c, wall_x, y, w, h, icon, outward=-1, col=rgb('caa077')):
    end = wall_x + outward * (w + 18)
    line(c, [(wall_x, y - 14), (end, y - 14)], OUT, 5)
    line(c, [(wall_x, y - 22), (wall_x, y - 6)], OUT, 6)
    sx = min(end, wall_x + outward * 10) if outward < 0 else wall_x + 10
    sx = end if outward < 0 else wall_x + 8
    line(c, [(sx + 5, y - 14), (sx + 5, y)], OUT, 2.4)
    line(c, [(sx + w - 5, y - 14), (sx + w - 5, y)], OUT, 2.4)
    shape(c, lambda c: rrect(c, sx, y, w, h, 6), col, top=0.25, band=0.3, lw=3.4)
    icon(c, sx + w / 2, y + h / 2)


# ---------------------------------------------------------------- icons
def gem_icon(c, cx, cy, s=1.0):
    col = rgb('a786d6')
    shape(c, P((cx, cy - 11 * s), (cx + 10 * s, cy - 3 * s), (cx, cy + 12 * s), (cx - 10 * s, cy - 3 * s)), col, lw=2.8)
    shape(c, P((cx, cy - 11 * s), (cx + 10 * s, cy - 3 * s), (cx, cy - 1 * s), (cx - 10 * s, cy - 3 * s)), lighter(col, 0.4), lw=2.2)


def book_icon(c, cx, cy):
    page = rgb('f6efdc')
    shape(c, P((cx - 15, cy - 8), (cx, cy - 5), (cx + 15, cy - 8), (cx + 15, cy + 9), (cx, cy + 12), (cx - 15, cy + 9)), rgb('7f8fc4'), lw=2.6)
    shape(c, P((cx - 13, cy - 10), (cx, cy - 6), (cx, cy + 9), (cx - 13, cy + 6)), page, lw=2.2)
    shape(c, P((cx + 13, cy - 10), (cx, cy - 6), (cx, cy + 9), (cx + 13, cy + 6)), page, lw=2.2)


def scissors_icon(c, cx, cy):
    for sgn in (-1, 1):
        line(c, [(cx - 12 * sgn, cy - 10), (cx + 8 * sgn, cy + 6)], OUT, 6.5)
        line(c, [(cx - 12 * sgn, cy - 10), (cx + 8 * sgn, cy + 6)], rgb('dfe3e8'), 3.2)
    for sgn in (-1, 1):
        shape(c, lambda c, sgn=sgn: ellipse(c, cx + 10 * sgn, cy + 8, 5, 5), rgb('d96f6f'), lw=2.6)


def mug_icon(c, cx, cy):
    shape(c, lambda c: rrect(c, cx - 10, cy - 9, 18, 20, 3), rgb('d9a54e'), top=0.2, lw=2.6)
    shape(c, lambda c: ellipse(c, cx - 1, cy - 10, 10, 5), rgb('fbf4e2'), lw=2.4)
    c.new_path(); c.arc(cx + 9, cy + 1, 6, -1.4, 1.4); c.set_source_rgba(*OUT, 1); c.set_line_width(3); c.stroke()


def anvil_icon(c, cx, cy):
    shape(c, P((cx - 14, cy - 6), (cx + 14, cy - 6), (cx + 8, cy + 1), (cx + 5, cy + 1), (cx + 7, cy + 9), (cx - 7, cy + 9), (cx - 5, cy + 1), (cx - 10, cy + 1)), rgb('6f7380'), top=0.3, lw=2.6)


# ---------------------------------------------------------------- props shared
def barrel(c, cx, base, r=14, h=28, col=rgb('c2857a')):
    shape(c, lambda c: rrect(c, cx - r, base - h, 2 * r, h, 6), col, bottom=0.18, band=0.35, lw=3.2)
    for yy in (base - h * 0.28, base - h * 0.72):
        line(c, [(cx - r + 2, yy), (cx + r - 2, yy)], darker(col, 0.35), 2.4)
    shape(c, lambda c: ellipse(c, cx, base - h, r - 1, 5), lighter(col, 0.45), lw=3)


def crate(c, x, base, s=26, col=rgb('c69a6b')):
    shape(c, lambda c: rrect(c, x, base - s, s, s, 3), col, bottom=0.18, band=0.3, lw=3.2)
    line(c, [(x + 3, base - 3), (x + s - 3, base - s + 3)], darker(col, 0.35), 2.4)
    shape(c, P((x, base - s), (x + 8, base - s - 6), (x + s + 8, base - s - 6), (x + s, base - s)), lighter(col, 0.3), lw=3)
    shape(c, P((x + s, base), (x + s + 8, base - 6), (x + s + 8, base - s - 6), (x + s, base - s)), darker(col, 0.15), lw=3)


def flower_box(c, x, y, w, seed):
    rnd = random.Random(seed)
    cols = [rgb('f2a3a8'), rgb('f4d37c'), rgb('a8c8f0'), rgb('f6f0e0')]
    for i in range(int(w / 9)):
        fx = x + 4 + i * 9
        shape(c, lambda c, fx=fx: ellipse(c, fx, y - 2, 5.5, 5), rgb('7f9d5a') if i % 2 else rgb('6b8a4c'), lw=2.2)
        shape(c, lambda c, fx=fx: ellipse(c, fx + 1, y - 4, 2.8, 2.8), cols[rnd.randrange(4)], lw=1.6)
    shape(c, lambda c: rrect(c, x - 2, y, w + 4, 10, 3), rgb('c07a62'), top=0.2, lw=3)


def potted(c, cx, base, seed=0):
    for i, (dx, dy, r) in enumerate([(-8, -28, 10), (7, -30, 11), (0, -40, 10)]):
        shape(c, lambda c, dx=dx, dy=dy, r=r, i=i: smooth(c, blob(cx + dx, base + dy, r, r, 9, 0.12, seed + i)), rgb('7f9d5a') if i != 1 else rgb('6b8a4c'), lw=3)
    shape(c, P((cx - 12, base - 20), (cx + 12, base - 20), (cx + 9, base), (cx - 9, base)), rgb('c07a62'), top=0.2, lw=3.2)


# ================================================================ buildings
def standard_house(name, W, H, RH, roof_col, seed, extra=None, chimney_x=None, door_col=WOOD, windows=2, upper=False):
    x0, x1 = -W / 2, W / 2
    top = H + RH + (-DY) * 0.5 + 60 + (60 if chimney_x is not None else 0)
    cv = Canvas(name, W / 2 + 40, W / 2 + DX + 110, top, 40)
    c = cv.c
    ground_shadow(c, x0, x1)
    smoke = None
    if chimney_x is not None:
        smoke = chimney(c, chimney_x, -H + DY * 0.25 - RH * 0.4, 26, RH * 0.65 + 30)
    box_walls(c, x0, x1, H, seed=seed)
    # side windows
    side_window(c, x1 + 14, -H * 0.62, 26, 34)
    # front windows
    win_y = -H * 0.62 if not upper else -H * 0.42
    door_w = 44
    door(c, 0, door_w, 78, door_col)
    spots = []
    if windows >= 2:
        for wx in (x0 + W * 0.2, x1 - W * 0.2 - 40):
            lit_window(c, wx, win_y, 40, 38)
            flower_box(c, wx - 4, win_y + 44, 48, seed + int(wx))
            spots.append((wx + 20, win_y + 19))
    if upper:
        for wx in (x0 + W * 0.18, -20, x1 - W * 0.18 - 40):
            lit_window(c, wx, -H * 0.86, 40, 34)
            spots.append((wx + 20, -H * 0.86 + 17))
    RL, RR = gable_roof(c, x0, x1, H, RH, roof_col, seed)
    if extra:
        extra(c, x0, x1, H)
    meta = {'footprint': [W / 100, -DY / 100], 'top': top / 100, 'windows': spots, 'door': [[0, -10]]}
    if smoke:
        meta['smoke'] = [smoke]
    cv.save(meta)


def shop():
    def extra(c, x0, x1, H):
        # striped awning over the right window
        aw = [(x1 - 140, -H * 0.62 - 22), (x1 - 20, -H * 0.62 - 22), (x1 - 8, -H * 0.62 + 6), (x1 - 152, -H * 0.62 + 6)]
        shape(c, P(*aw), rgb('f2ead6'), lw=3.2)
        c.save(); c.new_path(); poly(c, aw); c.clip()
        for i in range(8):
            c.new_path(); c.rectangle(x1 - 156 + i * 20, -H, 10, H); c.set_source_rgba(*rgb('a786d6'), 1); c.fill()
        c.restore(); c.new_path(); poly(c, aw); stroke(c, OUT, 3.2); c.new_path()
        crate(c, x1 - 70, 6, 30)
        gem_icon(c, x1 - 52, -32, 0.8)
        gem_icon(c, x1 - 62, -30, 0.55)
        crate(c, x1 - 32, 8, 24, rgb('b88b62'))
        barrel(c, x0 + 26, 8)
        barrel(c, x0 + 56, 10, 12, 24, rgb('a8705c'))
        hanging_sign(c, x0, -H * 0.72, 40, 34, lambda c, x, y: gem_icon(c, x, y, 0.85), outward=-1)
    standard_house('Prop_V2_Shop', 420, 210, 150, rgb('9b84bf'), 11, extra, chimney_x=60, door_col=rgb('9c7bb0'))


def barber():
    def extra(c, x0, x1, H):
        def pole(c):
            rrect(c, x0 - 34, -96, 16, 74, 7)
        shape(c, pole, rgb('f4f1e4'), lw=3.2)
        c.save(); c.new_path(); pole(c); c.clip()
        for i in range(-2, 10):
            c.new_path(); c.move_to(x0 - 34, -96 + i * 14); c.line_to(x0 - 18, -106 + i * 14)
            c.set_source_rgba(*rgb('d96f6f'), 1); c.set_line_width(6); c.stroke()
        c.restore(); c.new_path(); pole(c); stroke(c, OUT, 3.2); c.new_path()
        for yy in (-100, -22):
            shape(c, lambda c, yy=yy: rrect(c, x0 - 38, yy - 3, 24, 9, 3), STONE, top=0.25, lw=3)
        shape(c, lambda c: ellipse(c, x0 - 26, -110, 8, 8), rgb('e8c35f'), top=0.3, lw=3)
        potted(c, x1 - 26, 8, 9)
        hanging_sign(c, x1, -H * 0.78, 40, 34, scissors_icon, outward=1)
    standard_house('Prop_V2_Barber', 320, 190, 130, rgb('86b08a'), 37, extra, chimney_x=40)


def house(name, W, H, RH, roof, seed, chim=True):
    def extra(c, x0, x1, H):
        potted(c, x0 + 22, 8, seed)
        if seed % 2:
            barrel(c, x1 - 24, 8, 12, 24)
    standard_house(name, W, H, RH, roof, seed, extra, chimney_x=(W * 0.15 if chim else None))


def tavern():
    def extra(c, x0, x1, H):
        # balcony along the upper floor
        by = -H * 0.6
        shape(c, P((x0 + 30, by), (x1 - 30, by), (x1 - 30, by + 10), (x0 + 30, by + 10)), WOOD_D, lw=3.2)
        for xx in range(int(x0 + 36), int(x1 - 30), 18):
            line(c, [(xx, by), (xx, by - 30)], OUT, 5)
            line(c, [(xx, by), (xx, by - 30)], WOOD, 2.5)
        line(c, [(x0 + 30, by - 30), (x1 - 30, by - 30)], OUT, 7)
        line(c, [(x0 + 30, by - 30), (x1 - 30, by - 30)], WOOD, 4)
        for xx in (x0 + 40, x1 - 40):
            line(c, [(xx, by + 10), (xx, -16)], OUT, 9)
            line(c, [(xx, by + 10), (xx, -16)], WOOD_D, 5)
        barrel(c, x0 + 22, 8); barrel(c, x0 + 50, 10, 12, 24, rgb('a8705c'))
        crate(c, x1 - 50, 8, 26)
        # bench
        shape(c, lambda c: rrect(c, x1 - 140, -26, 70, 10, 3), WOOD, top=0.3, lw=3)
        for xx in (x1 - 134, x1 - 80):
            line(c, [(xx, -16), (xx, 2)], OUT, 5)
        hanging_sign(c, x0, -H * 0.42, 40, 34, mug_icon, outward=-1)
    standard_house('Prop_V2_Tavern', 460, 260, 150, rgb('c0705c'), 51, extra, chimney_x=-150, upper=True, windows=2)


def library():
    W, H, RH = 400, 270, 150
    x0, x1 = -W / 2, W / 2
    tw, th = 130, 420
    tx0 = x0 - 60
    top = th + 170 + 40
    cv = Canvas('Prop_V2_Library', W / 2 + 120, W / 2 + DX + 110, top, 40)
    c = cv.c
    ground_shadow(c, tx0, x1)
    box_walls(c, x0, x1, H, wall=rgb('e4dcca'), side=rgb('c2b8a4'), timber=False, seed=23)
    # stone quoins
    for yy in range(-H, 0, 30):
        for xx in (x0, x1 - 22):
            shape(c, lambda c, xx=xx, yy=yy: rrect(c, xx, yy + 2, 22, 26, 2), STONE, lw=2.4)
    side_window(c, x1 + 14, -H * 0.66, 26, 46)
    for wx in (x0 + 70, x1 - 110):
        lit_window(c, wx, -H * 0.72, 44, 70, arch=True)
        spots_dummy = None
    door(c, 30, 54, 92, rgb('7d6a9c'))
    gable_roof(c, x0, x1, H, RH, rgb('6f8fc4'), 23)
    # clock tower on the left (taller box, in front-left)
    box_walls(c, tx0, tx0 + tw, th, wall=rgb('e4dcca'), side=rgb('c2b8a4'), timber=False, seed=24)
    for yy in range(-th, 0, 30):
        for xx in (tx0, tx0 + tw - 22):
            shape(c, lambda c, xx=xx, yy=yy: rrect(c, xx, yy + 2, 22, 26, 2), STONE, lw=2.4)
    lit_window(c, tx0 + 43, -th * 0.45, 44, 64, arch=True)
    # clock face
    cxk, cyk = tx0 + tw / 2, -th + 70
    shape(c, lambda c: ellipse(c, cxk, cyk, 40, 40), rgb('caa077'), lw=4)
    shape(c, lambda c: ellipse(c, cxk, cyk, 31, 31), rgb('fbf3dc'), lw=3)
    for k in range(12):
        a = k * math.pi / 6
        line(c, [(cxk + math.cos(a) * 24, cyk + math.sin(a) * 24), (cxk + math.cos(a) * 28, cyk + math.sin(a) * 28)], OUT, 2.4)
    line(c, [(cxk, cyk), (cxk + 2, cyk - 20)], OUT, 4)
    line(c, [(cxk, cyk), (cxk + 15, cyk + 6)], OUT, 4)
    # pointed tower roof (pyramid seen 3/4)
    ye = -th + 4
    apex = (tx0 + tw / 2 + DX * 0.5, ye + DY * 0.5 - 170)
    shape(c, P((tx0 + tw + 14, ye + 8), (tx0 + tw + 14 + DX, ye + 8 + DY), apex), darker(rgb('6f8fc4'), 0.3), lw=LW)
    face = [(tx0 - 14, ye + 8), (tx0 + tw + 14, ye + 8), apex]
    shape(c, P(*face), rgb('6f8fc4'), top=0.15, bottom=0.12, band=0.35, lw=LW)
    c.save(); c.new_path(); poly(c, face); c.clip()
    for r in range(1, 6):
        yy = apex[1] + (ye + 8 - apex[1]) * r / 6
        line(c, [(tx0 - 30, yy), (tx0 + tw + 40, yy)], darker(rgb('6f8fc4'), 0.3), 2.2)
    c.restore()
    shape(c, P((tx0 - 18, ye + 4), (tx0 + tw + 18, ye + 4), (tx0 + tw + 18, ye + 14), (tx0 - 18, ye + 14)), darker(rgb('6f8fc4'), 0.3), lw=3.4)
    line(c, [apex, (apex[0], apex[1] - 26)], OUT, 4)
    shape(c, lambda c: ellipse(c, apex[0], apex[1] - 28, 6, 6), rgb('e8c35f'), lw=2.4)
    # book stack + sign
    for i, col in enumerate([rgb('c98a7a'), rgb('7f8fc4'), rgb('d9b46a')]):
        shape(c, lambda c, i=i, col=col: rrect(c, x1 - 60 + (i % 2) * 3, -6 - i * 9, 32, 9, 2), col, top=0.25, lw=2.6)
    hanging_sign(c, x1, -H * 0.5, 44, 34, book_icon, outward=1)
    spots = [(x0 + 92, -H * 0.72 + 35), (x1 - 88, -H * 0.72 + 35), (tx0 + 65, -th * 0.45 + 32)]
    cv.save({'footprint': [(x1 - tx0) / 100, -DY / 100], 'footprint_offset': [((x1 + tx0) / 2) / 100, 0], 'top': top / 100,
             'windows': spots, 'door': [[30, -10]]})


def smithy():
    """Open-front forge shed: roof on posts, glowing forge inside, anvil."""
    W, H, RH = 340, 170, 110
    x0, x1 = -W / 2, W / 2
    top = H + RH + 140
    cv = Canvas('Prop_V2_Smithy', W / 2 + 40, W / 2 + DX + 110, top, 40)
    c = cv.c
    ground_shadow(c, x0, x1)
    # back wall (dark interior) + side wall
    shape(c, P((x0 + DX * 0.6, DY * 0.6), (x1 + DX * 0.6, DY * 0.6), (x1 + DX * 0.6, DY * 0.6 - H), (x0 + DX * 0.6, DY * 0.6 - H)), rgb('6e5a58'), lw=LW)
    shape(c, P((x1, 0), (x1 + DX, DY), (x1 + DX, DY - H), (x1, -H)), rgb('8b7466'), lw=LW)
    # floor
    shape(c, P((x0, 0), (x1, 0), (x1 + DX * 0.6, DY * 0.6), (x0 + DX * 0.6, DY * 0.6)), rgb('8f8072'), lw=3.4)
    # forge (stone block with glowing mouth) at back-left
    fx = x0 + 70
    shape(c, P((fx - 50, -10), (fx + 50, -10), (fx + 50, -110), (fx - 50, -110)), STONE, top=0.15, lw=LW)
    shape(c, P((fx + 50, -10), (fx + 64, -22), (fx + 64, -122), (fx + 50, -110)), STONE_D, lw=3.4)
    c.new_path(); c.move_to(fx - 30, -40); c.line_to(fx - 30, -70); c.arc(fx, -70, 30, math.pi, 0); c.line_to(fx + 30, -40); c.close_path()
    c.set_source_rgba(*rgb('2f2228'), 1); c.fill_preserve(); c.set_source_rgba(*OUT, 1); c.set_line_width(3.4); c.stroke()
    shape(c, P((fx - 20, -110), (fx + 20, -110), (fx + 14, -190), (fx - 14, -190)), STONE_D, lw=3.4)   # chimney stack
    # anvil
    ax = x0 + 200
    shape(c, P((ax - 34, -50), (ax + 34, -50), (ax + 20, -36), (ax + 12, -36), (ax + 16, -14), (ax - 16, -14), (ax - 12, -36), (ax - 24, -36)), rgb('6f7380'), top=0.3, lw=3.4)
    shape(c, lambda c: rrect(c, ax - 22, -16, 44, 16, 3), WOOD_D, lw=3)
    # tools on wall
    for i, xx in enumerate((x1 - 70, x1 - 44)):
        line(c, [(xx + DX * 0.5, -H * 0.8 + DY * 0.5), (xx + DX * 0.5, -H * 0.45 + DY * 0.5)], OUT, 5)
        shape(c, lambda c, xx=xx: rrect(c, xx + DX * 0.5 - 8, -H * 0.8 + DY * 0.5 - 6, 16, 10, 2), rgb('8c909a'), lw=2.4)
    # posts
    for xx in (x0 + 4, x1 - 4):
        shape(c, lambda c, xx=xx: rrect(c, xx - 7, -H, 14, H + 4, 3), WOOD, lw=3.4)
    gable_roof(c, x0, x1, H, RH, rgb('7c7a86'), 61)
    barrel(c, x1 - 30, 10, 14, 30, rgb('8a8f9c'))
    hanging_sign(c, x0, -H * 0.55, 40, 34, anvil_icon, outward=-1)
    cv.save({'footprint': [W / 100, -DY / 100], 'top': top / 100, 'forge': [(fx, -55)], 'smoke': [(fx + 2, -200)],
             'npc': [[ax - 50, -6]]})


def tower_gate():
    """Stone keep: arched portal wall between two taller flanking towers, on the terrace."""
    W, H = 420, 270
    TW, TH = 140, 345
    x0, x1 = -W / 2, W / 2
    lt0, rt0 = x0 - TW + 30, x1 - 30
    top = TH + 70
    cv = Canvas('Prop_V2_TowerGate', -lt0 + 40, rt0 + TW + DX + 80, top, 40)
    c = cv.c
    rnd = random.Random(5)
    ground_shadow(c, lt0, rt0 + TW, length=40)

    def bricks(c, bx0, bx1, h, warm):
        y = -h
        row = 0
        while y < 0:
            hgt = rnd.uniform(22, 28)
            x = bx0 - 20 - (row % 2) * 22
            while x < bx1 + 20:
                wdt = rnd.uniform(36, 56)
                col = mix(STONE, STONE_D, rnd.uniform(0, 0.45) + (y + h) / h * 0.25)
                c.new_path(); rrect(c, x + 2, y + 2, wdt - 4, hgt - 4, 4)
                c.set_source_rgba(*col, 1); c.fill_preserve()
                c.set_source_rgba(*darker(STONE, 0.45), 1); c.set_line_width(1.8); c.stroke()
                if rnd.random() < 0.2:
                    line(c, [(x + 8, y + 7), (x + 16, y + 9)], lighter(col, 0.4), 2)
                x += wdt
            y += hgt
            row += 1
        if warm:
            g = cairo.LinearGradient(bx0, 0, bx1, 0)
            g.add_color_stop_rgba(0, 1, 0.75, 0.45, 0.22)
            g.add_color_stop_rgba(1, 1, 0.75, 0.45, 0.0)
            c.set_source(g); c.paint()

    def block(bx0, bx1, h, seed, warm=True, merlons=True):
        shape(c, P((bx1, 0), (bx1 + DX, DY), (bx1 + DX, DY - h), (bx1, -h)), STONE_D, lw=LW)
        c.save(); c.new_path(); poly(c, [(bx1, 0), (bx1 + DX, DY), (bx1 + DX, DY - h), (bx1, -h)]); c.clip()
        for yy in range(int(-h), 0, 26):
            line(c, [(bx1, yy), (bx1 + DX, yy + DY)], darker(STONE_D, 0.25), 1.8)
        c.restore()
        pts = wobble([(bx0, 0), (bx0, -h), (bx1, -h), (bx1, 0)], 0.8, seed, 16)
        c.save(); c.new_path(); poly(c, pts); c.clip(); bricks(c, bx0, bx1, h, warm); c.restore()
        c.new_path(); poly(c, pts); stroke(c); c.new_path()
        # top surface (seen from above) + merlons
        shape(c, P((bx0 - 8, -h), (bx1 + 8, -h), (bx1 + 8 + DX, -h + DY), (bx0 - 8 + DX, -h + DY)), lighter(STONE, 0.08), lw=3.6)
        shape(c, P((bx0 + 6 + DX * 0.2, -h + DY * 0.2), (bx1 - 6 + DX * 0.2, -h + DY * 0.2), (bx1 - 6 + DX * 0.8, -h + DY * 0.8), (bx0 + 6 + DX * 0.8, -h + DY * 0.8)), darker(STONE_D, 0.2), lw=2.6)
        if merlons:
            n = max(2, int((bx1 - bx0) / 62))
            for i in range(n):
                mx = bx0 - 4 + i * (bx1 - bx0 + 8 - 38) / max(1, n - 1)
                shape(c, lambda c, mx=mx: rrect(c, mx, -h - 34, 38, 38, 5), STONE, top=0.2, bottom=0.12, lw=3.4)
            for k in range(2):
                t = 0.3 + k * 0.45
                mx, my = bx1 + DX * t, -h + DY * t
                shape(c, P((mx - 6, my + 4), (mx + 14, my - 10), (mx + 14, my - 44), (mx - 6, my - 30)), STONE_D, lw=3.2)

    # center wall first (it sits back between the towers), then the towers in front
    block(x0, x1, H, 2)
    aw, atop = 180, -240

    def outer(c):
        c.move_to(-aw / 2 - 22, 0); c.line_to(-aw / 2 - 22, atop + aw / 2)
        c.arc(0, atop + aw / 2, aw / 2 + 22, math.pi, 2 * math.pi)
        c.line_to(aw / 2 + 22, 0); c.close_path()

    def inner(c):
        c.move_to(-aw / 2, 0); c.line_to(-aw / 2, atop + aw / 2)
        c.arc(0, atop + aw / 2, aw / 2, math.pi, 2 * math.pi)
        c.line_to(aw / 2, 0); c.close_path()
    shape(c, outer, lighter(STONE, 0.15), lw=4)
    for k in range(13):
        a = math.pi + k * math.pi / 12
        r0, r1 = aw / 2, aw / 2 + 22
        cy = atop + aw / 2
        line(c, [(math.cos(a) * r0, cy + math.sin(a) * r0), (math.cos(a) * r1, cy + math.sin(a) * r1)], STONE_D, 2.4)
    shape(c, inner, rgb('34283f'), lw=4)
    shape(c, P((-16, atop - 6), (16, atop - 6), (12, atop + 24), (-12, atop + 24)), lighter(STONE, 0.3), lw=3.2)
    gem_icon(c, 0, atop + 8, 0.6)
    # banner rods on the center wall
    banners = []
    for bx in (x0 + 34, x1 - 80):
        line(c, [(bx - 6, -H + 40), (bx + 46, -H + 40)], OUT, 5)
        shape(c, lambda c, bx=bx: ellipse(c, bx - 6, -H + 40, 4, 4), rgb('e8c35f'), lw=2.4)
        shape(c, lambda c, bx=bx: ellipse(c, bx + 46, -H + 40, 4, 4), rgb('e8c35f'), lw=2.4)
        banners.append((bx + 20, -H + 40 + 32))
    # flanking towers
    block(lt0, lt0 + TW, TH, 7)
    block(rt0, rt0 + TW, TH, 8, warm=False)
    for tx in (lt0, rt0):
        lit_window(c, tx + TW / 2 - 15, -TH + 90, 30, 56, arch=True, frame=STONE_D, mullion=False)
        lit_window(c, tx + TW / 2 - 15, -TH + 220, 30, 56, arch=True, frame=STONE_D, mullion=False)
    # ivy
    for i, (x, yy, r) in enumerate([(lt0 + 14, -TH + 150, 16), (lt0 + 8, -TH + 180, 18), (lt0 + 20, -TH + 212, 15), (lt0 + 12, -TH + 240, 17),
                                    (rt0 + TW - 14, -120, 16), (rt0 + TW - 20, -90, 18), (rt0 + TW - 8, -60, 15)]):
        shape(c, lambda c, x=x, yy=yy, r=r, i=i: smooth(c, blob(x, yy, r, r * 0.9, 9, 0.15, i + 40)), rgb('7f9d5a') if i % 2 else rgb('6b8a4c'), lw=3)
    # torch brackets on the towers, beside the arch
    torches = []
    for tx in (lt0 + TW - 26, rt0 + 26):
        line(c, [(tx, -128), (tx, -108)], OUT, 6)
        shape(c, P((tx - 11, -140), (tx + 11, -140), (tx + 6, -124), (tx - 6, -124)), WOOD_D, top=0.2, lw=3)
        torches.append((tx, -162))
    portal_center = (0, (atop + 0) / 2 + 14)
    cv.save({'footprint': [(rt0 + TW - lt0) / 100, -DY / 100], 'top': top / 100,
             'portal': [portal_center], 'portal_size': [aw, -atop - 0], 'torches': torches, 'banners': banners})


if __name__ == '__main__':
    import os
    os.makedirs(OUTDIR, exist_ok=True)
    shop(); barber(); library(); tavern(); smithy(); tower_gate()
    house('Prop_V2_HouseA', 300, 170, 120, rgb('b9705e'), 71)
    house('Prop_V2_HouseB', 280, 160, 115, rgb('6f9fa6'), 82)
    house('Prop_V2_HouseC', 270, 160, 115, rgb('d0925c'), 93)
    json.dump(META, open(f'{OUTDIR}/buildings_meta.json', 'w'), indent=1)
