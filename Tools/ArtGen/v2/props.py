"""Village v2 props (pivot 14 px above bottom, centre) and looping overlay animations (centre pivot)."""
import math
import random
import sys
import json
import cairo
from PIL import Image
from kit import *

OUTDIR = sys.argv[1] if len(sys.argv) > 1 else 'out'
SS = 2
STONE = rgb('b5a693')
STONE_D = rgb('918271')
WOOD = rgb('b47c5c')
WOOD_D = rgb('8d5d48')
LEAF = rgb('7f9d5a')
LEAF_D = rgb('6b8a4c')
META = {}


def render(name, w, h, draw, pivot=True, meta=None):
    s, c = canvas(w * SS, h * SS)
    c.scale(SS, SS)
    if pivot:
        c.translate(w / 2, h - 14)
    else:
        c.translate(w / 2, h / 2)
    draw(c)
    s.flush()
    tmp = f'/tmp/p_{name}.png'
    s.write_to_png(tmp)
    Image.open(tmp).resize((w, h), Image.LANCZOS).save(f'{OUTDIR}/{name}.png')
    if meta:
        m = {}
        for k, v in meta.items():
            m[k] = [[round(w / 2 + p[0], 1), round(h - 14 + p[1], 1)] for p in v]
        META[name] = m


def P(*pts):
    def b(c):
        poly(c, pts)
    return b


def soft_shadow(c, rx, ry, dx=8):
    c.new_path(); ellipse(c, dx, 0, rx, ry)
    c.set_source_rgba(0.14, 0.12, 0.2, 0.26); c.fill()


# ---------------------------------------------------------------- fountain (360x260)
def fountain():
    def draw(c):
        soft_shadow(c, 165, 36)
        # outer basin: 3/4 ellipse ring with front wall
        rx, ry, hgt = 150, 62, 44
        def basin_wall(c):
            c.move_to(-rx, -hgt); c.line_to(-rx, -ry * 0.0)
            c.curve_to(-rx, ry * 0.75, rx, ry * 0.75, rx, 0)
            c.line_to(rx, -hgt); c.close_path()
        c.save(); c.translate(0, -ry * 0.55)
        shape(c, basin_wall, STONE, bottom=0.18, band=0.5, lw=LW)
        c.save(); c.new_path(); basin_wall(c); c.clip()
        for k in range(-7, 8):
            x = k * 22
            line(c, [(x, -hgt + 4), (x, ry * 0.6)], STONE_D, 2)
        line(c, [(-rx, -hgt * 0.45), (rx, -hgt * 0.45)], STONE_D, 2)
        c.restore()
        shape(c, lambda c: ellipse(c, 0, -hgt, rx, ry), lighter(STONE, 0.12), lw=LW)
        # water surface
        c.new_path(); ellipse(c, 0, -hgt + 4, rx - 16, ry - 12)
        g = cairo.LinearGradient(0, -hgt - ry, 0, -hgt + ry)
        g.add_color_stop_rgba(0, *rgb('3f7486'), 1)
        g.add_color_stop_rgba(1, *rgb('5e9aa6'), 1)
        c.set_source(g); c.fill_preserve(); stroke(c, OUT, 3)
        c.new_path()
        # central column + upper bowl
        shape(c, P((-16, -hgt), (16, -hgt), (12, -hgt - 70), (-12, -hgt - 70)), STONE, top=0.2, lw=3.6)
        shape(c, lambda c: ellipse(c, 0, -hgt - 74, 52, 18), STONE, bottom=0.2, band=0.5, lw=3.6)
        shape(c, lambda c: ellipse(c, 0, -hgt - 80, 46, 13), rgb('5e9aa6'), lw=3)
        shape(c, P((-7, -hgt - 82), (7, -hgt - 82), (5, -hgt - 112), (-5, -hgt - 112)), STONE, lw=3)
        shape(c, lambda c: ellipse(c, 0, -hgt - 114, 9, 6), lighter(STONE, 0.2), lw=3)
        c.restore()
    render('Prop_V2_Fountain', 360, 280, draw, meta={'water': [(0, -190)]})


def fountain_water(f, n=6):
    """Falling water + ripples overlay, drawn relative to the fountain sprite (same size, same pivot)."""
    t = f / n

    def draw(c):
        c.translate(0, -62 * 0.55)
        hgt = 44
        top_y = -hgt - 114
        # spout jet
        for k, (dx, a) in enumerate(((0, 0.9), (-3, 0.6), (3, 0.6))):
            c.new_path(); c.move_to(dx, top_y)
            c.curve_to(dx, top_y - 20, dx * 6, top_y - 26, dx * 10, top_y - 4)
            c.set_source_rgba(0.85, 0.95, 1, a); c.set_line_width(5); c.stroke()
        # falling sheets from the upper bowl to the basin (animated dashes)
        for side in (-1, 1):
            for k in range(3):
                x0 = side * (40 + k * 4)
                c.new_path(); c.move_to(x0, -hgt - 74)
                c.curve_to(x0 + side * 14, -hgt - 64, x0 + side * 22, -hgt - 40, x0 + side * 26, -hgt - 6)
                c.set_dash([10, 8], -t * 18 * 3 - k * 6)
                c.set_source_rgba(0.85, 0.95, 1, 0.85); c.set_line_width(4 - k); c.stroke()
                c.set_dash([])
        # ripples in the basin
        for k in range(3):
            ph = (t + k / 3) % 1
            for side in (-1, 1):
                cx = side * 66
                c.new_path(); ellipse(c, cx, -hgt + 2, 8 + ph * 26, 3 + ph * 8)
                c.set_source_rgba(0.9, 0.97, 1, 0.8 * (1 - ph)); c.set_line_width(2.4); c.stroke()
        # sparkle
        sx = math.cos(t * 2 * math.pi) * 60
        c.new_path(); c.move_to(sx, -hgt - 6); c.line_to(sx + 3, -hgt - 2); c.line_to(sx, -hgt + 2); c.line_to(sx - 3, -hgt - 2); c.close_path()
        c.set_source_rgba(1, 1, 1, 0.9); c.fill()
    render(f'Anim_V2_Fountain_{f:02d}', 360, 280, draw)


# ---------------------------------------------------------------- market stall (260x250)
def stall(name, canopy, goods):
    def draw(c):
        soft_shadow(c, 120, 22)
        # counter box (3/4)
        DX, DY = 40, -30
        x0, x1, h = -100, 100, 64
        shape(c, P((x1, 0), (x1 + DX, DY), (x1 + DX, DY - h), (x1, -h)), WOOD_D, lw=3.6)
        shape(c, P((x0, 0), (x0, -h), (x1, -h), (x1, 0)), WOOD, bottom=0.15, band=0.4, lw=3.6)
        for xx in range(int(x0) + 25, int(x1), 25):
            line(c, [(xx, -h + 6), (xx, -4)], darker(WOOD, 0.3), 2)
        shape(c, P((x0 - 6, -h), (x1 + 6, -h), (x1 + 6 + DX, -h + DY), (x0 - 6 + DX, -h + DY)), lighter(WOOD, 0.2), lw=3.4)
        goods(c, x0, x1, -h, DX, DY)
        # posts
        for xx in (x0 + 2, x1 - 2, x1 + DX - 2):
            yy = 0 if xx < x1 + 10 else DY
            line(c, [(xx, yy - h), (xx, yy - 170)], OUT, 8)
            line(c, [(xx, yy - h), (xx, yy - 170)], WOOD_D, 4)
        # striped canopy (slanted)
        cp = [(x0 - 20, -150), (x1 + 20, -150), (x1 + 20 + DX, -186 + DY * 0.3), (x0 - 20 + DX, -186 + DY * 0.3)]
        shape(c, P(*cp), rgb('f3ead8'), lw=3.6)
        c.save(); c.new_path(); poly(c, cp); c.clip()
        for i in range(12):
            xx = x0 - 40 + i * 28
            c.new_path(); c.move_to(xx, -140); c.line_to(xx + 14, -140); c.line_to(xx + 14 + DX, -200); c.line_to(xx + DX, -200); c.close_path()
            c.set_source_rgba(*canopy, 1); c.fill()
        c.restore(); c.new_path(); poly(c, cp); stroke(c); c.new_path()
        # scalloped front edge
        for i in range(8):
            xx = x0 - 20 + i * 30
            c.new_path(); c.move_to(xx, -150); c.curve_to(xx + 4, -136, xx + 26, -136, xx + 30, -150)
            c.set_source_rgba(*(canopy if i % 2 == 0 else rgb('f3ead8')), 1); c.fill_preserve(); stroke(c, OUT, 3); c.new_path()
    render(name, 300, 260, draw)


def fruit_goods(c, x0, x1, top, DX, DY):
    rnd = random.Random(3)
    for b, (bx, col) in enumerate([(-70, rgb('e0705e')), (-10, rgb('f2b84c')), (50, rgb('9cc25a'))]):
        shape(c, P((bx - 26, top + 2), (bx + 26, top + 2), (bx + 22, top - 18), (bx - 22, top - 18)), rgb('c69a6b'), lw=3)
        for k in range(6):
            fx, fy = bx + rnd.uniform(-16, 16), top - 20 - rnd.uniform(0, 12)
            shape(c, lambda c, fx=fx, fy=fy, col=col: ellipse(c, fx, fy, 8, 8), col, top=0.25, band=0.4, lw=2.4)


def bread_goods(c, x0, x1, top, DX, DY):
    for k, bx in enumerate((-70, -30, 10, 50)):
        col = rgb('d9a35e') if k % 2 else rgb('c98c4e')
        shape(c, lambda c, bx=bx: ellipse(c, bx, top - 12, 18, 10), col, top=0.3, band=0.4, lw=2.8)
        for j in range(3):
            line(c, [(bx - 8 + j * 7, top - 18), (bx - 4 + j * 7, top - 8)], darker(col, 0.35), 2)
    shape(c, lambda c: rrect(c, 66, top - 30, 20, 30, 4), rgb('d6b896'), lw=2.6)


# ---------------------------------------------------------------- lamp (dusk, lit) 80x230
def lamp():
    def draw(c):
        soft_shadow(c, 20, 6)
        shape(c, lambda c: rrect(c, -12, -16, 24, 18, 4), STONE, top=0.25, lw=3.2)
        shape(c, lambda c: rrect(c, -5, -170, 10, 158, 3), rgb('5d5560'), top=0.2, lw=3)
        line(c, [(0, -160), (16, -172), (16, -180)], OUT, 4)
        g = cairo.RadialGradient(0, -196, 4, 0, -196, 40)
        g.add_color_stop_rgba(0, 1, 0.85, 0.5, 0.45)
        g.add_color_stop_rgba(1, 1, 0.85, 0.5, 0)
        c.set_source(g); c.arc(0, -196, 40, 0, 2 * math.pi); c.fill()
        shape(c, P((-14, -214), (14, -214), (10, -176), (-10, -176)), rgb('ffe39a'), top=0.3, band=0.4, lw=3.2)
        line(c, [(0, -214), (0, -176)], rgb('5d5560'), 2.4)
        shape(c, P((-20, -214), (0, -228), (20, -214)), rgb('5d5560'), top=0.25, lw=3.2)
        shape(c, lambda c: rrect(c, -14, -178, 28, 6, 2), rgb('5d5560'), lw=3)
    render('Prop_V2_Lamp', 90, 250, draw, meta={'glow': [(0, -196)]})


def bench():
    def draw(c):
        soft_shadow(c, 70, 10)
        for xx in (-50, 50):
            line(c, [(xx, -4), (xx, -30)], OUT, 9); line(c, [(xx, -4), (xx, -30)], WOOD_D, 5)
        shape(c, P((-64, -30), (64, -30), (72, -40), (-56, -40)), WOOD, top=0.3, lw=3.2)
        shape(c, lambda c: rrect(c, -62, -76, 124, 12, 3), WOOD, top=0.3, lw=3.2)
        shape(c, lambda c: rrect(c, -62, -60, 124, 10, 3), WOOD, top=0.3, lw=3.2)
        for xx in (-54, 54):
            line(c, [(xx, -36), (xx, -78)], OUT, 8); line(c, [(xx, -36), (xx, -78)], WOOD_D, 4)
    render('Prop_V2_Bench', 170, 110, draw)


def notice_board():
    def draw(c):
        soft_shadow(c, 50, 9)
        for xx in (-36, 36):
            line(c, [(xx, -2), (xx, -120)], OUT, 10); line(c, [(xx, -2), (xx, -120)], WOOD_D, 6)
        shape(c, lambda c: rrect(c, -50, -118, 100, 70, 5), WOOD, top=0.2, lw=3.6)
        shape(c, lambda c: rrect(c, -42, -110, 84, 54, 3), rgb('d8b98d'), lw=2.6)
        for i, (x, y, w, h, col) in enumerate([(-36, -104, 26, 30, rgb('f6efdc')), (-4, -106, 30, 22, rgb('f3d9a8')), (10, -80, 26, 20, rgb('f6efdc')), (-30, -72, 30, 14, rgb('e8c9c9'))]):
            shape(c, lambda c, x=x, y=y, w=w, h=h: rrect(c, x, y, w, h, 2), col, lw=2)
            for k in range(2):
                line(c, [(x + 4, y + 6 + k * 6), (x + w - 5, y + 6 + k * 6)], darker(col, 0.4), 1.4)
            shape(c, lambda c, x=x, y=y, w=w: ellipse(c, x + w / 2, y + 2, 2.5, 2.5), rgb('d96f6f'), lw=1.2)
        shape(c, P((-58, -118), (58, -118), (40, -136), (-40, -136)), rgb('9c6b55'), top=0.25, lw=3.4)
    render('Prop_V2_Board', 140, 160, draw)


def signpost():
    def draw(c):
        soft_shadow(c, 18, 6)
        line(c, [(0, -2), (0, -130)], OUT, 11); line(c, [(0, -2), (0, -130)], WOOD_D, 6)
        shape(c, P((-6, -126), (54, -126), (66, -114), (54, -102), (-6, -102)), WOOD, top=0.25, lw=3.2)
        shape(c, P((6, -96), (-54, -96), (-66, -84), (-54, -72), (6, -72)), WOOD, top=0.25, lw=3.2)
        for y in (-114, -84):
            line(c, [(-40 if y > -100 else 6, y), (-10 if y > -100 else 46, y)], darker(WOOD, 0.4), 2.4)
    render('Prop_V2_Signpost', 150, 160, draw)


def oak(name, seed, scale=1.0):
    W, H = int(240 * scale), int(300 * scale)

    def draw(c):
        c.scale(scale, scale)
        soft_shadow(c, 80, 16, dx=18)
        shape(c, lambda c: smooth(c, [(-14, -150), (14, -150), (16, -40), (30, 0), (12, -6), (0, 2), (-12, -6), (-30, 0), (-16, -40)], tension=0.4),
              rgb('a8705c'), bottom=0.18, band=0.4, lw=3.8)
        rnd = random.Random(seed)
        # three overlapping lumpy clusters for volume (back, sides, front)
        clusters = [(0, -196, 78, 66, darker(rgb('8ba05e'), 0.1)), (-46, -160, 56, 48, rgb('8ba05e')), (46, -158, 56, 48, rgb('86995a')), (0, -150, 62, 46, lighter(rgb('8ba05e'), 0.06))]
        for i, (x, y, rx, ry, col) in enumerate(clusters):
            pts = []
            n = 16
            for k in range(n):
                a = k * 2 * math.pi / n
                b = 1 + (0.07 if k % 2 else -0.02) + rnd.uniform(-0.03, 0.03)
                pts.append((x + math.cos(a) * rx * b, y + math.sin(a) * ry * b))
            shape(c, lambda c, pts=pts: smooth(c, pts, tension=0.6), col, top=0.14, bottom=0.16, lw=3.8, soft=True)
        # sunset rim light on the left
        c.save()
        c.new_path(); smooth(c, [(x + math.cos(a) * 70, -190 + math.sin(a) * 60) for a in [k * 0.4 for k in range(16)]], tension=0.6)
        c.restore()
        for (x, y) in [(-70, -150), (-40, -230), (-10, -250)]:
            c.new_path(); c.arc(x, y, 10, math.pi * 0.9, math.pi * 1.6)
            c.set_source_rgba(1, 0.86, 0.6, 0.8); c.set_line_width(4); c.stroke()
    render(name, W, H, draw)


def pine(name, seed):
    def draw(c):
        soft_shadow(c, 56, 12, dx=14)
        shape(c, lambda c: rrect(c, -10, -60, 20, 62, 4), rgb('9c6650'), lw=3.4)
        col = rgb('5f8a62')
        for k, (y, w) in enumerate([(-60, 74), (-112, 62), (-158, 48), (-198, 34)]):
            pts = [(-w, y), (0, y - 78), (w, y)]
            c.new_path()
            shape(c, lambda c, pts=pts, y=y, w=w: smooth(c, [(-w, y), (-w * 0.5, y - 30), (0, y - 80), (w * 0.5, y - 30), (w, y), (0, y + 10)], tension=0.35),
                  lighter(col, 0.04 * k), top=0.12, bottom=0.18, lw=3.6, soft=True)
        c.new_path(); c.arc(-30, -110, 8, math.pi * 1.0, math.pi * 1.6)
        c.set_source_rgba(1, 0.86, 0.6, 0.7); c.set_line_width(3.5); c.stroke()
    render(name, 180, 300, draw)


def bush(name, seed, flowers=False):
    def draw(c):
        soft_shadow(c, 46, 10)
        rnd = random.Random(seed)
        for i, (x, y, r) in enumerate([(-22, -22, 24), (20, -22, 24), (0, -38, 28)]):
            shape(c, lambda c, x=x, y=y, r=r, i=i: smooth(c, blob(x, y, r, r * 0.85, 10, 0.12, seed + i), tension=0.6),
                  LEAF if i == 2 else LEAF_D, top=0.15, bottom=0.15, lw=3.4, soft=True)
        if flowers:
            cols = [rgb('f2a3a8'), rgb('f4d37c'), rgb('f6f0e0')]
            for k in range(6):
                x, y = rnd.uniform(-34, 34), rnd.uniform(-50, -14)
                shape(c, lambda c, x=x, y=y: ellipse(c, x, y, 4, 4), cols[k % 3], lw=1.8)
    render(name, 120, 90, draw)


def fence():
    """Rail segment 128 px wide that tiles horizontally."""
    def draw(c):
        c.translate(-64, 0)
        for y in (-44, -24):
            shape(c, lambda c, y=y: c.rectangle(-6, y, 140, 9), WOOD, top=0.25, band=0.4, lw=3.2)
        for px_ in (20, 84):
            shape(c, P((px_, -6), (px_, -58), (px_ + 12, -66), (px_ + 24, -58), (px_ + 24, -6)), WOOD_D, top=0.2, bottom=0.12, lw=3.4)
    render('Prop_FenceV2', 128, 84, draw)


def cart():
    def draw(c):
        soft_shadow(c, 80, 14)
        shape(c, P((-70, -40), (60, -40), (70, -80), (-80, -80)), WOOD, top=0.2, bottom=0.15, lw=3.6)
        for xx in range(-60, 60, 22):
            line(c, [(xx, -76), (xx + 2, -44)], darker(WOOD, 0.3), 2)
        for i, (x, y, r, col) in enumerate([(-40, -92, 16, rgb('e0705e')), (-12, -96, 18, rgb('f2b84c')), (20, -92, 16, rgb('d9a35e'))]):
            shape(c, lambda c, x=x, y=y, r=r: ellipse(c, x, y, r, r * 0.8), col, top=0.25, lw=3)
        line(c, [(60, -50), (110, -30)], OUT, 8); line(c, [(60, -50), (110, -30)], WOOD_D, 4)
        for wx in (-44, 34):
            shape(c, lambda c, wx=wx: ellipse(c, wx, -22, 22, 22), WOOD_D, lw=3.6)
            shape(c, lambda c, wx=wx: ellipse(c, wx, -22, 6, 6), rgb('6f7380'), lw=2.4)
    render('Prop_V2_Cart', 260, 150, draw)


def well():
    def draw(c):
        soft_shadow(c, 66, 13)
        for px_ in (-48, 40):
            shape(c, lambda c, px_=px_: rrect(c, px_, -110, 9, 82, 3), WOOD_D, top=0.15, lw=3.2)
        line(c, [(-44, -86), (44, -86)], OUT, 5)
        shape(c, P((-62, -100), (62, -100), (44, -134), (-44, -134)), rgb('c0705c'), top=0.2, bottom=0.15, lw=3.6)
        def ring(c):
            c.move_to(-52, -50); c.line_to(-52, -14)
            c.curve_to(-52, 0, 52, 0, 52, -14); c.line_to(52, -50); c.close_path()
        shape(c, ring, STONE, bottom=0.2, band=0.45, lw=3.6)
        shape(c, lambda c: ellipse(c, 0, -50, 52, 15), lighter(STONE, 0.2), lw=3.6)
        shape(c, lambda c: ellipse(c, 0, -50, 40, 9), rgb('4f7f99'), lw=3)
    render('Prop_V2_Well', 150, 160, draw)


# ---------------------------------------------------------------- overlays
def portal(f, aw=180, ah=240, n=8):
    t = f / n

    def draw(c):
        def arch(c):
            c.move_to(-aw / 2, ah / 2); c.line_to(-aw / 2, -ah / 2 + aw / 2)
            c.arc(0, -ah / 2 + aw / 2, aw / 2, math.pi, 2 * math.pi)
            c.line_to(aw / 2, ah / 2); c.close_path()
        c.save(); arch(c); c.clip()
        cx, cy = 0, -10
        g = cairo.RadialGradient(cx, cy, 4, cx, cy, 160)
        g.add_color_stop_rgba(0, *rgb('f6e8ff'), 1)
        g.add_color_stop_rgba(0.18, *rgb('c9a6f2'), 1)
        g.add_color_stop_rgba(0.55, *rgb('7a56b8'), 1)
        g.add_color_stop_rgba(1, *rgb('34283f'), 1)
        c.set_source(g); c.paint()
        for arm in range(5):
            for shift, col, a, w in ((0, rgb('ead9ff'), 0.55, 6), (math.pi / 5, rgb('5a3f8a'), 0.5, 5)):
                c.new_path()
                for k in range(70):
                    r = 6 + k * 2.0
                    ang = arm * 2 * math.pi / 5 + shift + t * 2 * math.pi + k * 0.08
                    x, y = cx + math.cos(ang) * r, cy + math.sin(ang) * r * 1.15
                    (c.move_to if k == 0 else c.line_to)(x, y)
                c.set_source_rgba(*col, a); c.set_line_width(w); c.stroke()
        rnd = random.Random(1)
        for i in range(14):
            ph = (rnd.random() + t) % 1.0
            a = rnd.random() * 2 * math.pi + ph * 2
            r = 110 * (1 - ph)
            x, y = cx + math.cos(a) * r, cy + math.sin(a) * r
            s = 3 + 2.5 * (1 - ph)
            c.new_path(); c.move_to(x, y - s * 1.8); c.line_to(x + s * 0.5, y); c.line_to(x, y + s * 1.8); c.line_to(x - s * 0.5, y); c.close_path()
            c.set_source_rgba(1, 1, 1, 0.9 * math.sin(ph * math.pi)); c.fill()
        c.restore()
        c.new_path(); arch(c); c.set_source_rgba(0.12, 0.08, 0.16, 0.5); c.set_line_width(14); c.stroke()
    render(f'Anim_V2_Portal_{f:02d}', aw, ah, draw, pivot=False)


def forge_fire(f, n=6):
    t = f / n

    def draw(c):
        g = cairo.RadialGradient(0, 8, 2, 0, 8, 40)
        g.add_color_stop_rgba(0, 1, 0.75, 0.3, 0.9)
        g.add_color_stop_rgba(1, 1, 0.5, 0.2, 0)
        c.set_source(g); c.arc(0, 8, 40, 0, 2 * math.pi); c.fill()
        for k in range(3):
            sway = math.sin(t * 2 * math.pi + k * 2) * 4
            h = 22 + 8 * math.sin(t * 4 * math.pi + k)
            x = (k - 1) * 12
            c.new_path(); smooth(c, [(x - 8, 18), (x - 6, 6), (x + sway, 18 - h), (x + 6, 6), (x + 8, 18)], tension=0.6)
            c.set_source_rgba(*rgb('f29a4a'), 1); c.fill()
            c.new_path(); smooth(c, [(x - 4, 18), (x + sway * 0.6, 18 - h * 0.6), (x + 4, 18)], tension=0.6)
            c.set_source_rgba(*rgb('ffe39a'), 1); c.fill()
    render(f'Anim_V2_Forge_{f:02d}', 80, 60, draw, pivot=False)


def smoke(f, n=8):
    t = f / n

    def draw(c):
        for i in range(3):
            ph = (t + i / 3) % 1.0
            y = 46 - ph * 80
            x = -6 + math.sin(ph * 4 + i) * 7 + ph * 16
            r = 8 + ph * 11
            a = 0.85 * (1 - ph) ** 0.8
            c.new_path(); smooth(c, blob(x, y, r, r * 0.85, 8, 0.12, i), tension=0.6)
            c.set_source_rgba(*rgb('e9e1e6'), a); c.fill_preserve()
            c.set_source_rgba(*rgb('a39aa6'), a); c.set_line_width(2.6); c.stroke()
    render(f'Anim_V2_Smoke_{f:02d}', 70, 110, draw, pivot=False)


if __name__ == '__main__':
    import os
    os.makedirs(OUTDIR, exist_ok=True)
    fountain()
    for f in range(6):
        fountain_water(f)
        forge_fire(f)
    for f in range(8):
        portal(f)
        smoke(f)
    stall('Prop_V2_StallFruit', rgb('d9705e'), fruit_goods)
    stall('Prop_V2_StallBread', rgb('6f9fa6'), bread_goods)
    lamp(); bench(); notice_board(); signpost(); fence(); cart(); well()
    oak('Prop_V2_Oak', 1); oak('Prop_V2_OakSmall', 2, 0.8)
    pine('Prop_V2_Pine', 3)
    bush('Prop_V2_Bush', 4); bush('Prop_V2_BushFlower', 5, True)
    json.dump(META, open(f'{OUTDIR}/props_meta.json', 'w'), indent=1)
