"""Village v2 ground: 3600x2000 px painted once, saved as four 1800x1000 tiles (Ground_V2_00..03:
00 = top-left, 01 = top-right, 02 = bottom-left, 03 = bottom-right). Dusk palette."""
import math
import random
import sys
import cairo
from PIL import Image
from kit import rgb, mix, darker, lighter, smooth, blob, ellipse, wobble, poly
from layout import *

OUTDIR = sys.argv[1] if len(sys.argv) > 1 else 'out'
W, H = 3600, 2000

GRASS = rgb('86984f')
GRASS_L = rgb('97a75a')
GRASS_D = rgb('76884a')
TERRACE = rgb('8e9e55')
PATH = rgb('caa06f')
PATH_L = rgb('d8b282')
PATH_EDGE = rgb('7c5a46')
STONE = rgb('b9a991')
STONE_D = rgb('94836f')
CLIFF = rgb('a39280')
CLIFF_D = rgb('7c6c60')
OUT = rgb('5c4540')
WATER = rgb('4e8796')
WATER_D = rgb('3d6f80')


def P(x, y):
    return px(x, y)


def set_c(c, col, a=1.0):
    c.set_source_rgba(*col, a)


def stroke_poly(c, pts, width, col, cap=True):
    c.new_path()
    c.move_to(*pts[0])
    for p in pts[1:]:
        c.line_to(*p)
    c.set_line_width(width)
    set_c(c, col)
    c.stroke()


def draw(c):
    rnd = random.Random(42)
    c.set_line_join(cairo.LINE_JOIN_ROUND)
    c.set_line_cap(cairo.LINE_CAP_ROUND)

    # ---------------------------------------------------------------- grass base + soft patches
    set_c(c, GRASS); c.paint()
    for i in range(160):
        x, y = rnd.uniform(0, W), rnd.uniform(0, H)
        r = rnd.uniform(60, 190)
        c.new_path(); smooth(c, blob(x, y, r, r * 0.62, 10, 0.12, i), tension=0.6)
        set_c(c, GRASS_L if i % 2 else GRASS_D, 0.55); c.fill()

    # ---------------------------------------------------------------- terrace (upper level)
    tx0, ty = P(-PLATEAU_X, CLIFF_TOP)
    tx1, _ = P(PLATEAU_X, CLIFF_TOP)
    _, cliff_base = P(0, CLIFF_BASE)
    lip = 18
    terrace_pts = wobble([(tx0, -40), (tx1, -40), (tx1, ty), (tx0, ty)], 3, 5, 24)

    # cliff face (front, below the edge) drawn first, terrace top drawn over its upper lip
    def cliff_face(c):
        pts = wobble([(tx0, ty - 4), (tx1, ty - 4), (tx1, cliff_base), (tx0, cliff_base)], 4, 9, 30)
        smooth(c, pts, tension=0.15)
    c.new_path(); cliff_face(c)
    set_c(c, OUT); c.set_line_width(10); c.stroke_preserve()
    g = cairo.LinearGradient(0, ty, 0, cliff_base)
    g.add_color_stop_rgba(0, *lighter(CLIFF, 0.12), 1)
    g.add_color_stop_rgba(1, *CLIFF_D, 1)
    c.set_source(g); c.fill()
    # stones on the cliff face
    c.save(); c.new_path(); cliff_face(c); c.clip()
    yy = ty + 10
    row = 0
    while yy < cliff_base:
        hgt = rnd.uniform(26, 34)
        xx = tx0 - (row % 2) * 30
        while xx < tx1:
            wdt = rnd.uniform(46, 80)
            col = mix(CLIFF, CLIFF_D, rnd.uniform(0.0, 0.6) + row * 0.08)
            c.new_path(); c.rectangle(xx + 3, yy + 3, wdt - 6, hgt - 6)
            set_c(c, col); c.fill_preserve()
            set_c(c, darker(CLIFF_D, 0.25)); c.set_line_width(2.5); c.stroke()
            if rnd.random() < 0.3:
                stroke_poly(c, [(xx + 8, yy + 8), (xx + 20, yy + 10)], 3, lighter(col, 0.35))
            xx += wdt
        yy += hgt
        row += 1
    c.restore()
    # contact shadow at the foot of the cliff (on lower grass)
    for k in range(3):
        c.new_path(); c.rectangle(tx0 + 10, cliff_base + 4 + k * 6, tx1 - tx0 - 20, 8)
        set_c(c, (0.18, 0.2, 0.1), 0.18 - k * 0.05); c.fill()

    # side faces of the terrace (seen at an angle, a strip on each outer side)
    for sx, sgn in ((tx0, -1), (tx1, 1)):
        c.new_path(); c.rectangle(min(sx, sx + sgn * 34), -20, 34, ty + 20)
        set_c(c, OUT); c.set_line_width(8); c.stroke_preserve()
        g2 = cairo.LinearGradient(sx, 0, sx + sgn * 34, 0)
        g2.add_color_stop_rgba(0, *CLIFF, 1); g2.add_color_stop_rgba(1, *CLIFF_D, 1)
        c.set_source(g2); c.fill()
    # terrace top
    def terrace(c):
        smooth(c, terrace_pts, tension=0.1)
    c.new_path(); terrace(c)
    set_c(c, OUT); c.set_line_width(10); c.stroke_preserve()
    set_c(c, TERRACE); c.fill()
    c.save(); c.new_path(); terrace(c); c.clip()
    for i in range(60):
        x, y = rnd.uniform(tx0, tx1), rnd.uniform(0, ty)
        r = rnd.uniform(60, 150)
        c.new_path(); smooth(c, blob(x, y, r, r * 0.6, 10, 0.12, 500 + i), tension=0.6)
        set_c(c, lighter(TERRACE, 0.1) if i % 2 else darker(TERRACE, 0.08), 0.5); c.fill()
    # grass lip highlight along the edge
    c.new_path(); c.rectangle(tx0, ty - 26, tx1 - tx0, 20)
    set_c(c, lighter(TERRACE, 0.22), 0.8); c.fill()
    c.restore()
    # grass tufts hanging over the edge
    xx = tx0 + 20
    while xx < tx1 - 20:
        if not (P(-STAIR_HALF - 0.2, 0)[0] < xx < P(STAIR_HALF + 0.2, 0)[0]):
            w = rnd.uniform(18, 34)
            c.new_path(); c.move_to(xx, ty - 6); c.curve_to(xx + w * 0.3, ty + 14, xx + w * 0.7, ty + 14, xx + w, ty - 6)
            set_c(c, OUT); c.set_line_width(6); c.stroke_preserve()
            set_c(c, lighter(TERRACE, 0.15)); c.fill()
        xx += rnd.uniform(40, 90)

    # ---------------------------------------------------------------- dirt roads (outline pass, then fill pass)
    roads = [(wobble([P(*p) for p in pts], 7, i + 3, 40, closed=False), w * 100) for i, (pts, w) in enumerate(ROADS)]
    tpts, tw = TERRACE_PATH
    terrace_road = ([P(*p) for p in tpts], tw * 100)
    for pts, w in roads + [terrace_road]:
        stroke_poly(c, pts, w + 14, PATH_EDGE)
    # gate platform (stone) on the terrace
    gx, gy = P(GATE[0], GATE[1])
    c.new_path(); smooth(c, blob(gx, gy + 70, 470, 150, 16, 0.04, 77), tension=0.5)
    set_c(c, OUT); c.set_line_width(12); c.stroke_preserve()
    set_c(c, STONE); c.fill()
    for pts, w in roads + [terrace_road]:
        stroke_poly(c, pts, w, PATH)
    # light centre stripe + pebbles
    for pts, w in roads + [terrace_road]:
        stroke_poly(c, pts, w * 0.45, PATH_L)
    for pts, w in roads:
        for i in range(len(pts) - 1):
            (x0, y0), (x1, y1) = pts[i], pts[i + 1]
            n = int(math.hypot(x1 - x0, y1 - y0) / 45)
            for k in range(n):
                t = rnd.random()
                ox, oy = rnd.uniform(-w * 0.35, w * 0.35), rnd.uniform(-w * 0.35, w * 0.35)
                x, y = x0 + (x1 - x0) * t + ox, y0 + (y1 - y0) * t + oy
                c.new_path(); ellipse(c, x, y, rnd.uniform(4, 9), rnd.uniform(3, 6))
                set_c(c, darker(PATH, 0.25), 0.8); c.fill()
    # stone platform tiles
    c.save(); c.new_path(); smooth(c, blob(gx, gy + 70, 466, 146, 16, 0.04, 77), tension=0.5); c.clip()
    for yy in range(int(gy - 100), int(gy + 240), 46):
        off = (yy // 46) % 2 * 40
        for xx in range(int(gx - 520) + off, int(gx + 520), 80):
            c.new_path(); c.rectangle(xx + 3, yy + 3, 74, 40)
            set_c(c, mix(STONE, STONE_D, rnd.uniform(0, 0.4))); c.fill_preserve()
            set_c(c, darker(STONE_D, 0.2)); c.set_line_width(2.5); c.stroke()
    c.restore()

    # ---------------------------------------------------------------- stairs
    sx0, _ = P(-STAIR_HALF, 0)
    sx1, _ = P(STAIR_HALF, 0)
    steps = 5
    for k in range(steps):
        y0 = ty - 8 + (cliff_base - ty + 8) * k / steps
        y1 = ty - 8 + (cliff_base - ty + 8) * (k + 1) / steps
        c.new_path(); c.rectangle(sx0, y0, sx1 - sx0, y1 - y0)
        set_c(c, OUT); c.set_line_width(7); c.stroke_preserve()
        set_c(c, lighter(STONE, 0.1)); c.fill()
        c.new_path(); c.rectangle(sx0 + 4, y1 - (y1 - y0) * 0.35, sx1 - sx0 - 8, (y1 - y0) * 0.35)
        set_c(c, STONE_D); c.fill()
    # stair side walls
    for x in (sx0 - 22, sx1):
        c.new_path(); c.rectangle(x, ty - 30, 22, cliff_base - ty + 34)
        set_c(c, OUT); c.set_line_width(7); c.stroke_preserve()
        set_c(c, STONE); c.fill()
        c.new_path(); c.rectangle(x, ty - 30, 22, 12)
        set_c(c, lighter(STONE, 0.25)); c.fill()

    # ---------------------------------------------------------------- plaza (cobblestone disc)
    cx, cy = P(*PLAZA)
    r = PLAZA_R * 100
    c.new_path(); ellipse(c, cx, cy, r + 8, r * 0.82 + 8)
    set_c(c, OUT); c.set_line_width(12); c.stroke_preserve()
    set_c(c, STONE); c.fill()
    c.save(); c.new_path(); ellipse(c, cx, cy, r, r * 0.82); c.clip()
    for ring in range(1, 12):
        rr = ring * 38
        n = max(6, int(2 * math.pi * rr / 70))
        for k in range(n):
            a0 = k * 2 * math.pi / n + ring * 0.3
            a1 = a0 + 2 * math.pi / n * 0.9
            c.new_path()
            c.save(); c.translate(cx, cy); c.scale(1, 0.82)
            c.arc(0, 0, rr, a0, a1); c.arc_negative(0, 0, rr - 34, a1, a0); c.close_path()
            c.restore()
            set_c(c, mix(STONE, STONE_D, rnd.uniform(0, 0.45))); c.fill_preserve()
            set_c(c, darker(STONE_D, 0.25)); c.set_line_width(2.4); c.stroke()
    c.restore()

    # ---------------------------------------------------------------- pond
    pcx, pcy = P(*POND)
    prx, pry = POND_R[0] * 100, POND_R[1] * 100
    pond_pts = blob(pcx, pcy, prx, pry, 14, 0.08, 31)
    c.new_path(); smooth(c, blob(pcx, pcy, prx + 26, pry + 22, 14, 0.08, 31), tension=0.6)
    set_c(c, OUT); c.set_line_width(8); c.stroke_preserve()
    set_c(c, rgb('b4a58b')); c.fill()   # muddy shore
    c.new_path(); smooth(c, pond_pts, tension=0.6)
    set_c(c, OUT); c.set_line_width(8); c.stroke_preserve()
    g = cairo.LinearGradient(0, pcy - pry, 0, pcy + pry)
    g.add_color_stop_rgba(0, *WATER_D, 1)
    g.add_color_stop_rgba(1, *WATER, 1)
    c.set_source(g); c.fill()
    c.save(); c.new_path(); smooth(c, pond_pts, tension=0.6); c.clip()
    # inner bank shadow
    c.new_path(); smooth(c, blob(pcx, pcy - 20, prx, pry, 14, 0.08, 31), tension=0.6)
    set_c(c, darker(WATER_D, 0.3), 0.5); c.set_line_width(24); c.stroke()
    # sunset reflection streaks
    for i in range(7):
        y = pcy - pry * 0.4 + i * 22
        x = pcx + rnd.uniform(-prx * 0.5, prx * 0.4)
        stroke_poly(c, [(x, y), (x + rnd.uniform(50, 130), y)], 5, rgb('f1b98a') if i % 2 else rgb('a9d3d6'))
    c.restore()
    # shore stones and reeds
    for i in range(16):
        a = i * 2 * math.pi / 16 + rnd.uniform(-0.1, 0.1)
        x, y = pcx + math.cos(a) * (prx + 14), pcy + math.sin(a) * (pry + 12)
        if rnd.random() < 0.6:
            c.new_path(); ellipse(c, x, y, rnd.uniform(12, 20), rnd.uniform(9, 13))
            set_c(c, OUT); c.set_line_width(5); c.stroke_preserve()
            set_c(c, mix(STONE, STONE_D, rnd.random())); c.fill()
    for i in range(10):
        a = math.pi * (0.1 + 0.8 * rnd.random())
        x, y = pcx + math.cos(a) * (prx - 10), pcy - math.sin(a) * (pry - 6)
        for k in range(3):
            stroke_poly(c, [(x + k * 6, y), (x + k * 6 + rnd.uniform(-4, 4), y - rnd.uniform(26, 40))], 6, OUT)
            stroke_poly(c, [(x + k * 6, y), (x + k * 6 + rnd.uniform(-4, 4), y - rnd.uniform(24, 36))], 3, rgb('7f9a4f'))
    # lily pads
    for i in range(5):
        x, y = pcx + rnd.uniform(-prx * 0.6, prx * 0.6), pcy + rnd.uniform(-pry * 0.3, pry * 0.5)
        c.new_path(); c.move_to(x, y); c.arc(x, y, 16, 0.3, 2 * math.pi - 0.3); c.close_path()
        set_c(c, OUT); c.set_line_width(4); c.stroke_preserve()
        set_c(c, rgb('7da35a')); c.fill()

    # ---------------------------------------------------------------- flower patches and tufts
    flowers = [rgb('f2a3a8'), rgb('f4d37c'), rgb('a8c8f0'), rgb('f6f0e0'), rgb('d8a2e6')]
    for i in range(70):
        x, y = rnd.uniform(40, W - 40), rnd.uniform(40, H - 40)
        if not grass_free(x, y):
            continue
        for k in range(rnd.randint(4, 8)):
            fx, fy = x + rnd.uniform(-30, 30), y + rnd.uniform(-18, 18)
            c.new_path(); ellipse(c, fx, fy, 5, 4.5)
            set_c(c, OUT); c.set_line_width(3); c.stroke_preserve()
            set_c(c, flowers[(i + k) % len(flowers)]); c.fill()
    for i in range(420):
        x, y = rnd.uniform(20, W - 20), rnd.uniform(20, H - 20)
        if not grass_free(x, y):
            continue
        col = darker(GRASS, 0.3) if i % 3 else lighter(GRASS, 0.18)
        for dx, ang in ((-5, -1.9), (0, -1.6), (5, -1.3)):
            stroke_poly(c, [(x + dx, y), (x + dx + math.cos(ang) * 12, y + math.sin(ang) * 12)], 3.4, col)


def grass_free(x, y):
    """True when (pixel) point is on plain grass (not road, plaza, pond, cliff, stairs)."""
    wx, wy = x / 100 - HALF_W, HALF_H - y / 100
    if CLIFF_BASE - 0.4 < wy < CLIFF_TOP + 0.3 and abs(wx) < PLATEAU_X + 0.3:
        return False
    if (wx - PLAZA[0]) ** 2 / (PLAZA_R + 0.4) ** 2 + (wy - PLAZA[1]) ** 2 / ((PLAZA_R * 0.82 + 0.4) ** 2) < 1:
        return False
    if (wx - POND[0]) ** 2 / (POND_R[0] + 0.5) ** 2 + (wy - POND[1]) ** 2 / (POND_R[1] + 0.5) ** 2 < 1:
        return False
    for pts, w in ROADS + [TERRACE_PATH]:
        for i in range(len(pts) - 1):
            if seg_dist(wx, wy, pts[i], pts[i + 1]) < w * 0.5 + 0.25:
                return False
    if abs(wx - GATE[0]) < 5.2 and GATE[1] - 1.8 < wy < GATE[1] + 1.5:
        return False
    return True


def seg_dist(x, y, a, b):
    ax, ay = a; bx, by = b
    dx, dy = bx - ax, by - ay
    L = dx * dx + dy * dy
    t = 0 if L == 0 else max(0, min(1, ((x - ax) * dx + (y - ay) * dy) / L))
    return math.hypot(x - ax - t * dx, y - ay - t * dy)


if __name__ == '__main__':
    import os
    os.makedirs(OUTDIR, exist_ok=True)
    s = cairo.ImageSurface(cairo.FORMAT_ARGB32, W, H)
    c = cairo.Context(s)
    draw(c)
    s.flush()
    s.write_to_png('/tmp/ground_full.png')
    im = Image.open('/tmp/ground_full.png').convert('RGB')
    im.resize((W // 4, H // 4), Image.LANCZOS).save(f'{OUTDIR}/_ground_preview.png')
    k = 0
    for ty in (0, 1000):
        for tx in (0, 1800):
            im.crop((tx, ty, tx + 1800, ty + 1000)).save(f'{OUTDIR}/Ground_V2_{k:02d}.png')
            k += 1
