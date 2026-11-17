import math
import random
import sys
from PIL import Image
from kit import *

SS = 2  # supersample
OUTDIR = sys.argv[1] if len(sys.argv) > 1 else 'out'


def render(name, w, h, draw):
    s, c = canvas(w * SS, h * SS)
    c.scale(SS, SS)
    draw(c)
    s.flush()
    tmp = f'/tmp/{name}_ss.png'
    s.write_to_png(tmp)
    Image.open(tmp).resize((w, h), Image.LANCZOS).save(f'{OUTDIR}/{name}.png')


# palette (muted, like the package sample map)
PLASTER = rgb('ecdcbc')
TIMBER = rgb('a0705a')
WOOD = rgb('b9805f')
WOOD_D = rgb('94604c')
STONE = rgb('b3aa9b')
STONE_D = rgb('958c80')
GLASS = rgb('8fc3cf')
GOLD = rgb('e3bb5f')
LEAF = rgb('7f9d5a')
LEAF_D = rgb('6b8a4c')
POT = rgb('c07a62')
FLOWERS = [rgb('e9a0a6'), rgb('f2d27a'), rgb('9fd0e6'), rgb('f4f1e4')]


def window(c, x, y, w, h, seed, box=True, shutter=None):
    # shutters
    if shutter:
        for sx in (x - 13, x + w + 1):
            shape(c, lambda c, sx=sx: rrect(c, sx, y - 1, 12, h + 2, 2), shutter, top=0.15, lw=3)
            line(c, [(sx + 3, y + h * 0.35), (sx + 9, y + h * 0.35)], darker(shutter, 0.3), 2)
            line(c, [(sx + 3, y + h * 0.65), (sx + 9, y + h * 0.65)], darker(shutter, 0.3), 2)
    shape(c, lambda c: rrect(c, x - 3, y - 3, w + 6, h + 6, 5), TIMBER, lw=3.4)
    shape(c, lambda c: rrect(c, x, y, w, h, 3), GLASS, outline=False, bottom=0.25, band=0.45)
    # reflection
    line(c, [(x + 5, y + h * 0.55), (x + w * 0.45, y + 5)], (1, 1, 1), 3, 0.45)
    line(c, [(x + w / 2, y), (x + w / 2, y + h)], TIMBER, 3)
    line(c, [(x, y + h / 2), (x + w, y + h / 2)], TIMBER, 3)
    c.new_path(); rrect(c, x - 3, y - 3, w + 6, h + 6, 5); stroke(c, OUT, 3.2); c.new_path()
    if box:
        rnd = random.Random(seed)
        for i in range(6):
            fx = x - 2 + i * (w + 4) / 5.5 + 3
            col = FLOWERS[rnd.randrange(len(FLOWERS))]
            shape(c, lambda c, fx=fx: ellipse(c, fx, y + h + 2, 5, 4.5), LEAF if i % 2 else LEAF_D, lw=2.2)
            shape(c, lambda c, fx=fx: ellipse(c, fx + 1, y + h, 2.8, 2.8), col, lw=1.6)
        shape(c, lambda c: rrect(c, x - 6, y + h + 3, w + 12, 10, 3), POT, top=0.2, lw=3)


def door(c, cx, base, w, h, color=WOOD):
    x = cx - w / 2

    def arch(c):
        c.move_to(x, base)
        c.line_to(x, base - h + w / 2)
        c.arc(cx, base - h + w / 2, w / 2, math.pi, 2 * math.pi)
        c.line_to(x + w, base)
        c.close_path()
    shape(c, lambda c: (c.move_to(x - 5, base), c.line_to(x - 5, base - h + w / 2),
                        c.arc(cx, base - h + w / 2, w / 2 + 5, math.pi, 2 * math.pi),
                        c.line_to(x + w + 5, base), c.close_path()), STONE, lw=3.4)
    shape(c, arch, color, top=0.12, bottom=0.15, band=0.25, lw=3.4)
    c.save(); c.new_path(); arch(c); c.clip()
    for i in range(1, 3):
        line(c, [(x + w * i / 3, base - h), (x + w * i / 3, base)], darker(color, 0.35), 2.2)
    c.restore()
    line(c, [(x + 3, base - h * 0.62), (x + w - 3, base - h * 0.62)], darker(color, 0.4), 3)
    shape(c, lambda c: ellipse(c, x + w - 7, base - h * 0.42, 2.6, 2.6), GOLD, lw=1.6)
    # step
    shape(c, lambda c: rrect(c, x - 10, base - 3, w + 20, 9, 3), STONE, top=0.2, lw=3)


def roof(c, x0, x1, y_eave, y_ridge, inset, color, seed):
    """Front face of a roof seen from a 3/4 view, with scalloped shingle rows."""
    pts = wobble([(x0, y_eave), (x1, y_eave), (x1 - inset, y_ridge), (x0 + inset, y_ridge)], 1.0, seed, 14)

    def face(c):
        smooth(c, pts, tension=0.15)
    shape(c, face, color, outline=False)
    c.save(); c.new_path(); face(c); c.clip()
    rows = 5
    for r in range(rows):
        t = (r + 1) / (rows + 0.6)
        y = y_ridge + (y_eave - y_ridge) * t
        shade = lighter(color, 0.16) if r == 0 else (darker(color, 0.05 * r))
        # band under each row line
        c.new_path(); c.rectangle(x0 - 10, y, x1 - x0 + 20, (y_eave - y_ridge) / rows + 2)
        c.set_source_rgba(*shade, 1); c.fill()
        # scallops
        step = 18
        off = (r % 2) * step / 2
        c.new_path()
        xx = x0 - 20 + off
        c.move_to(xx, y)
        while xx < x1 + 20:
            c.curve_to(xx + 3, y + 7, xx + step - 3, y + 7, xx + step, y)
            xx += step
        c.set_source_rgba(*darker(color, 0.32), 1); c.set_line_width(2.2); c.stroke()
    # top highlight
    c.new_path(); c.rectangle(x0, y_ridge - 5, x1 - x0, 12)
    c.set_source_rgba(*lighter(color, 0.3), 1); c.fill()
    c.restore()
    # eave fascia (thickness)
    shape(c, lambda c: rrect(c, x0 - 4, y_eave - 4, x1 - x0 + 8, 11, 5), darker(color, 0.28), lw=3.6)
    c.new_path(); face(c); stroke(c, OUT, LW); c.new_path()
    # ridge cap
    shape(c, lambda c: rrect(c, x0 + inset - 6, y_ridge - 6, x1 - x0 - inset * 2 + 12, 11, 5), darker(color, 0.12), top=0.25, lw=3.4)


def chimney(c, x, y, w, h):
    shape(c, lambda c: poly(c, wobble([(x, y + h), (x, y), (x + w, y), (x + w, y + h)], 0.6, 3, 10)), STONE, top=0.12, bottom=0.12, lw=3.6)
    for yy in range(int(y + 12), int(y + h), 12):
        line(c, [(x + 2, yy), (x + w - 2, yy)], STONE_D, 2)
    shape(c, lambda c: rrect(c, x - 4, y - 6, w + 8, 10, 3), STONE_D, top=0.2, lw=3.4)


def timber_wall(c, x0, x1, top, base, seed):
    def wall(c):
        poly(c, wobble([(x0, base), (x0, top), (x1, top), (x1, base)], 0.7, seed, 12))
    shape(c, wall, PLASTER, outline=False, bottom=0.12, band=0.5)
    c.save(); c.new_path(); wall(c); c.clip()
    # beams
    for bx in (x0 + 2, x1 - 10):
        c.new_path(); c.rectangle(bx, top, 8, base - top); c.set_source_rgba(*TIMBER, 1); c.fill()
    c.new_path(); c.rectangle(x0, top, x1 - x0, 8); c.set_source_rgba(*TIMBER, 1); c.fill()
    # plaster cracks / texture
    rnd = random.Random(seed)
    for _ in range(4):
        px, py = rnd.uniform(x0 + 20, x1 - 20), rnd.uniform(top + 20, base - 30)
        line(c, [(px, py), (px + 6, py + 2)], darker(PLASTER, 0.25), 1.6)
    c.restore()
    # stone footing
    def foot(c):
        poly(c, wobble([(x0 - 4, base + 2), (x0 - 4, base - 14), (x1 + 4, base - 14), (x1 + 4, base + 2)], 0.8, seed + 1, 10))
    shape(c, foot, STONE, top=0.18, band=0.3, lw=3.4)
    rnd = random.Random(seed + 5)
    xx = x0
    while xx < x1 - 8:
        xx += rnd.uniform(14, 24)
        line(c, [(xx, base - 13), (xx, base)], STONE_D, 2)
    c.new_path(); wall(c); stroke(c, OUT, LW); c.new_path()


def barrel(c, cx, base, r=13, h=26, col=rgb('c2857a')):
    shape(c, lambda c: rrect(c, cx - r, base - h, 2 * r, h, 6), col, top=0.0, bottom=0.18, band=0.35, lw=3.2)
    for yy in (base - h * 0.28, base - h * 0.72):
        line(c, [(cx - r + 2, yy), (cx + r - 2, yy)], darker(col, 0.35), 2.4)
    shape(c, lambda c: ellipse(c, cx, base - h, r - 1, 5), lighter(col, 0.45), lw=3)


def crate(c, x, base, s=24, col=rgb('c69a6b')):
    shape(c, lambda c: rrect(c, x, base - s, s, s, 3), col, bottom=0.18, band=0.3, lw=3.2)
    line(c, [(x + 3, base - 3), (x + s - 3, base - s + 3)], darker(col, 0.35), 2.4)
    shape(c, lambda c: rrect(c, x, base - s - 6, s, 7, 2), lighter(col, 0.3), lw=3)


def potted_plant(c, cx, base, seed=0):
    for i, (dx, dy, r) in enumerate([(-7, -26, 9), (6, -28, 10), (0, -36, 9)]):
        shape(c, lambda c, dx=dx, dy=dy, r=r: smooth(c, blob(cx + dx, base + dy, r, r, 9, 0.12, seed + i)), LEAF if i != 1 else LEAF_D, lw=3)
    shape(c, lambda c: poly(c, [(cx - 11, base - 18), (cx + 11, base - 18), (cx + 8, base), (cx - 8, base)]), POT, top=0.2, lw=3.2)
    shape(c, lambda c: ellipse(c, cx + 4, base - 34, 3, 3), FLOWERS[1], lw=1.6)


def hanging_sign(c, x, y, w, h, draw_icon, col=rgb('caa077'), wall_x=None):
    # bracket from the wall corner
    if wall_x is None:
        wall_x = x + w + 8
    line(c, [(wall_x, y - 16), (x - 2 if wall_x > x else x + w + 2, y - 16)], OUT, 5)
    line(c, [(wall_x, y - 22), (wall_x, y - 8)], OUT, 6)
    line(c, [(x + 4, y - 16), (x + 4, y)], OUT, 2.4)
    line(c, [(x + w - 4, y - 16), (x + w - 4, y)], OUT, 2.4)
    shape(c, lambda c: rrect(c, x, y, w, h, 6), col, top=0.25, band=0.3, lw=3.4)
    draw_icon(c, x + w / 2, y + h / 2)


# ---------------------------------------------------------------- icons on signs
def gem_icon(c, cx, cy, s=1.0):
    col = rgb('a786d6')
    pts = [(cx, cy - 11 * s), (cx + 10 * s, cy - 3 * s), (cx, cy + 12 * s), (cx - 10 * s, cy - 3 * s)]
    shape(c, lambda c: poly(c, pts), col, lw=2.8)
    shape(c, lambda c: poly(c, [(cx, cy - 11 * s), (cx + 10 * s, cy - 3 * s), (cx, cy - 1 * s), (cx - 10 * s, cy - 3 * s)]), lighter(col, 0.4), lw=2.2)
    line(c, [(cx - 3 * s, cy - 6 * s), (cx - 1 * s, cy - 8 * s)], (1, 1, 1), 2, 0.8)


def book_icon(c, cx, cy):
    page = rgb('f6efdc')
    shape(c, lambda c: poly(c, [(cx - 15, cy - 8), (cx, cy - 5), (cx + 15, cy - 8), (cx + 15, cy + 9), (cx, cy + 12), (cx - 15, cy + 9)]), rgb('7f8fc4'), lw=2.6)
    shape(c, lambda c: poly(c, [(cx - 13, cy - 10), (cx, cy - 6), (cx, cy + 9), (cx - 13, cy + 6)]), page, lw=2.2)
    shape(c, lambda c: poly(c, [(cx + 13, cy - 10), (cx, cy - 6), (cx, cy + 9), (cx + 13, cy + 6)]), page, lw=2.2)
    for k in range(3):
        line(c, [(cx - 10, cy - 4 + k * 4), (cx - 3, cy - 2 + k * 4)], darker(page, 0.4), 1.4)
        line(c, [(cx + 3, cy - 2 + k * 4), (cx + 10, cy - 4 + k * 4)], darker(page, 0.4), 1.4)


def scissors_icon(c, cx, cy):
    blade = rgb('dfe3e8')
    for sgn in (-1, 1):
        line(c, [(cx - 12 * sgn, cy - 10), (cx + 8 * sgn, cy + 6)], OUT, 6.5)
        line(c, [(cx - 12 * sgn, cy - 10), (cx + 8 * sgn, cy + 6)], blade, 3.2)
    for sgn in (-1, 1):
        shape(c, lambda c, sgn=sgn: ellipse(c, cx + 10 * sgn, cy + 8, 5, 5), rgb('d96f6f'), lw=2.6)
        shape(c, lambda c, sgn=sgn: ellipse(c, cx + 10 * sgn, cy + 8, 2, 2), rgb('caa077'), lw=1.4)
    shape(c, lambda c: ellipse(c, cx, cy - 1, 2, 2), GOLD, lw=1.4)


# ---------------------------------------------------------------- houses (280x270, pivot 14 px from bottom)
def house(kind):
    W, H = 280, 270
    base = 252
    roof_col = {'shop': rgb('9b84bf'), 'library': rgb('7d9ccb'), 'barber': rgb('86b08a')}[kind]
    seed = {'shop': 11, 'library': 23, 'barber': 37}[kind]

    def draw(c):
        shadow(c, 140, 255, 124, 15)
        chimney(c, 188, 26, 24, 50)
        timber_wall(c, 50, 230, 128, base, seed)
        roof(c, 26, 254, 140, 56, 26, roof_col, seed)
        if kind == 'library':
            # round dormer window with books visible
            shape(c, lambda c: ellipse(c, 140, 96, 20, 20), darker(roof_col, 0.2), lw=3.6)
            shape(c, lambda c: ellipse(c, 140, 96, 13, 13), GLASS, bottom=0.2, band=0.5, lw=3)
            line(c, [(127, 96), (153, 96)], TIMBER, 2.6)
            line(c, [(140, 83), (140, 109)], TIMBER, 2.6)
        door(c, 140, base - 1, 42, 74, WOOD if kind != 'shop' else rgb('9c7bb0'))
        window(c, 72, 164, 38, 36, seed, shutter=darker(roof_col, 0.1))
        window(c, 170, 164, 38, 36, seed + 1, shutter=darker(roof_col, 0.1))
        if kind == 'shop':
            # striped awning over right window, crates of crystals
            def awn(c):
                poly(c, [(164, 150), (214, 150), (222, 170), (156, 170)])
            shape(c, awn, rgb('f2ead6'), lw=3.2)
            c.save(); c.new_path(); awn(c); c.clip()
            for i in range(4):
                c.new_path(); c.rectangle(156 + i * 17, 145, 8.5, 30); c.set_source_rgba(*rgb('a786d6'), 1); c.fill()
            c.restore(); c.new_path(); awn(c); stroke(c, OUT, 3.2); c.new_path()
            crate(c, 226, base + 3, 26)
            gem_icon(c, 240, base - 30, 0.7)
            gem_icon(c, 231, base - 28, 0.55)
            barrel(c, 34, base + 3)
            hanging_sign(c, 4, 168, 36, 32, lambda c, x, y: gem_icon(c, x, y, 0.85), wall_x=50)
        elif kind == 'library':
            # stacked books by the door, lantern
            for i, col in enumerate([rgb('c98a7a'), rgb('7f8fc4'), rgb('d9b46a')]):
                shape(c, lambda c, i=i, col=col: rrect(c, 236 + (i % 2) * 3, base - 6 - i * 8, 28, 8, 2), col, top=0.25, lw=2.6)
            potted_plant(c, 34, base + 2, 4)
            hanging_sign(c, 238, 168, 40, 32, book_icon, wall_x=230)
        else:
            # barber pole and potted plant
            def pole(c):
                rrect(c, 240, 172, 14, 62, 6)
            shape(c, pole, rgb('f4f1e4'), lw=3.2)
            c.save(); c.new_path(); pole(c); c.clip()
            for i in range(-2, 8):
                c.new_path(); c.move_to(240, 172 + i * 14); c.line_to(254, 162 + i * 14)
                c.set_source_rgba(*rgb('d96f6f'), 1); c.set_line_width(6); c.stroke()
            c.restore(); c.new_path(); pole(c); stroke(c, OUT, 3.2); c.new_path()
            for yy in (168, 236):
                shape(c, lambda c, yy=yy: rrect(c, 237, yy - 3, 20, 8, 3), rgb('b3aa9b'), top=0.25, lw=3)
            shape(c, lambda c: ellipse(c, 247, 160, 7, 7), GOLD, top=0.3, lw=3)
            potted_plant(c, 30, base + 2, 9)
            hanging_sign(c, 4, 168, 38, 32, scissors_icon, wall_x=50)
        tuft(c, 30, 262, 1.0)
        tuft(c, 250, 264, 0.9)
    render({'shop': 'Prop_HouseShop', 'library': 'Prop_HouseLibrary', 'barber': 'Prop_HouseBarber'}[kind], W, H, draw)


# ---------------------------------------------------------------- tower gate 400x290 (pivot 14 from bottom)
def tower_gate():
    W, H = 400, 290
    base = 270

    def draw(c):
        shadow(c, 200, 274, 186, 18)
        rnd = random.Random(5)
        # side towers (round-ish), center wall
        def body(c):
            poly(c, wobble([(30, base), (44, 40), (356, 40), (370, base)], 0.8, 2, 14))
        shape(c, body, STONE, outline=False, top=0.14, bottom=0.1, band=0.25)
        c.save(); c.new_path(); body(c); c.clip()
        # irregular bricks
        y = 46
        row = 0
        while y < base:
            hgt = rnd.uniform(18, 24)
            x = 20 - (row % 2) * 18
            while x < 380:
                wdt = rnd.uniform(30, 48)
                col = mix(STONE, STONE_D, rnd.uniform(0, 0.55))
                c.new_path(); rrect(c, x + 2, y + 2, wdt - 4, hgt - 4, 4)
                c.set_source_rgba(*col, 1); c.fill_preserve()
                c.set_source_rgba(*darker(STONE, 0.42), 1); c.set_line_width(1.8); c.stroke()
                if rnd.random() < 0.25:
                    line(c, [(x + 6, y + 6), (x + 12, y + 8)], lighter(col, 0.4), 2)
                x += wdt
            y += hgt
            row += 1
        c.restore()
        c.new_path(); body(c); stroke(c, OUT, LW); c.new_path()
        # crenellations
        for i in range(7):
            bx = 46 + i * 46
            shape(c, lambda c, bx=bx: rrect(c, bx, 12, 32, 34, 5), STONE, top=0.2, bottom=0.12, lw=3.6)
        shape(c, lambda c: rrect(c, 36, 38, 328, 14, 5), STONE_D, top=0.2, lw=3.6)
        # ivy on left
        for i, (x, y, r) in enumerate([(46, 70, 13), (40, 92, 15), (52, 116, 12), (44, 140, 14), (58, 58, 10), (360, 190, 13), (352, 212, 15), (364, 236, 12)]):
            shape(c, lambda c, x=x, y=y, r=r, i=i: smooth(c, blob(x, y, r, r * 0.9, 9, 0.15, i + 40)), LEAF if i % 2 else LEAF_D, lw=3)
        # big arch
        ax, aw, atop = 200, 128, 76

        def arch_outer(c):
            c.move_to(ax - aw / 2 - 14, base)
            c.line_to(ax - aw / 2 - 14, atop + aw / 2 + 14)
            c.arc(ax, atop + aw / 2 + 14, aw / 2 + 14, math.pi, 2 * math.pi)
            c.line_to(ax + aw / 2 + 14, base)
            c.close_path()

        def arch_inner(c):
            c.move_to(ax - aw / 2, base)
            c.line_to(ax - aw / 2, atop + aw / 2 + 14)
            c.arc(ax, atop + aw / 2 + 14, aw / 2, math.pi, 2 * math.pi)
            c.line_to(ax + aw / 2, base)
            c.close_path()
        shape(c, arch_outer, lighter(STONE, 0.12), lw=3.8)
        # voussoir lines
        for k in range(11):
            a = math.pi + k * math.pi / 10
            r0, r1 = aw / 2, aw / 2 + 14
            cy = atop + aw / 2 + 14
            line(c, [(ax + math.cos(a) * r0, cy + math.sin(a) * r0), (ax + math.cos(a) * r1, cy + math.sin(a) * r1)], STONE_D, 2.2)
        shape(c, arch_inner, rgb('3b2f45'), lw=3.8)
        # keystone
        shape(c, lambda c: poly(c, [(ax - 11, atop - 4), (ax + 11, atop - 4), (ax + 8, atop + 18), (ax - 8, atop + 18)]), lighter(STONE, 0.3), lw=3.2)
        gem_icon(c, ax, atop + 6, 0.5)
        # banners
        for bx in (80, 290):
            line(c, [(bx - 6, 70), (bx + 36, 70)], OUT, 5)
            shape(c, lambda c, bx=bx: ellipse(c, bx - 6, 70, 4, 4), GOLD, lw=2.4)
            shape(c, lambda c, bx=bx: ellipse(c, bx + 36, 70, 4, 4), GOLD, lw=2.4)
        # steps
        shape(c, lambda c: rrect(c, ax - aw / 2 - 26, base - 12, aw + 52, 16, 5), STONE, top=0.25, lw=3.6)
        shape(c, lambda c: rrect(c, ax - aw / 2 - 12, base - 24, aw + 24, 14, 5), lighter(STONE, 0.1), top=0.25, lw=3.6)
        # torch brackets (flames are animated separately)
        for tx in (ax - aw / 2 - 30, ax + aw / 2 + 30):
            line(c, [(tx, 206), (tx, 188)], OUT, 5)
            shape(c, lambda c, tx=tx: poly(c, [(tx - 9, 178), (tx + 9, 178), (tx + 5, 192), (tx - 5, 192)]), WOOD_D, top=0.2, lw=3)
        tuft(c, 24, 282)
        tuft(c, 378, 280, 0.9)
    render('Prop_TowerGate', W, H, draw)


# ---------------------------------------------------------------- well 140x150
def well():
    W, H = 140, 150
    base = 136

    def draw(c):
        shadow(c, 70, 138, 60, 11)
        # posts and roof
        for px in (26, 108):
            shape(c, lambda c, px=px: rrect(c, px, 34, 9, 80, 3), WOOD_D, top=0.15, lw=3.2)
        line(c, [(30, 58), (112, 58)], OUT, 5)
        line(c, [(30, 58), (112, 58)], WOOD, 2.5)
        line(c, [(70, 58), (70, 86)], OUT, 1.8)
        shape(c, lambda c: rrect(c, 63, 84, 14, 14, 3), rgb('b88b62'), top=0.2, lw=2.6)
        roof(c, 10, 130, 44, 14, 16, rgb('c46a6a'), 8)
        # stone ring (3/4 view: ellipse top + cylinder)
        def ring(c):
            c.move_to(18, 104); c.line_to(18, base - 6)
            c.curve_to(18, base + 6, 122, base + 6, 122, base - 6)
            c.line_to(122, 104); c.close_path()
        shape(c, ring, STONE, outline=False, bottom=0.18, band=0.4)
        c.save(); c.new_path(); ring(c); c.clip()
        rnd = random.Random(3)
        for r, y in enumerate((110, 122)):
            x = 14 + (r % 2) * 10
            while x < 126:
                wdt = rnd.uniform(16, 24)
                line(c, [(x, y), (x, y + 12)], STONE_D, 2)
                x += wdt
            line(c, [(18, y + 12), (122, y + 12)], STONE_D, 2)
        c.restore(); c.new_path(); ring(c); stroke(c, OUT, LW); c.new_path()
        shape(c, lambda c: ellipse(c, 70, 104, 52, 14), lighter(STONE, 0.2), lw=3.6)
        shape(c, lambda c: ellipse(c, 70, 104, 40, 9), rgb('4f7f99'), lw=3)
        line(c, [(52, 102), (66, 100)], (1, 1, 1), 2, 0.5)
        tuft(c, 14, 142, 0.9)
    render('Prop_Well', W, H, draw)


# ---------------------------------------------------------------- lamp 64x170 (glow animated separately)
def lamp():
    W, H = 64, 170
    base = 156

    def draw(c):
        shadow(c, 32, 157, 18, 6)
        shape(c, lambda c: rrect(c, 22, base - 12, 20, 14, 4), STONE, top=0.25, lw=3.2)
        shape(c, lambda c: rrect(c, 28, 50, 8, base - 60, 3), rgb('5d5560'), top=0.2, lw=3)
        line(c, [(32, 58), (46, 50), (46, 44)], OUT, 4)
        # lantern
        shape(c, lambda c: poly(c, [(18, 22), (46, 22), (42, 52), (22, 52)]), rgb('f3d98a'), top=0.3, band=0.4, lw=3.2)
        line(c, [(32, 22), (32, 52)], rgb('5d5560'), 2.4)
        shape(c, lambda c: poly(c, [(12, 22), (32, 8), (52, 22)]), rgb('5d5560'), top=0.25, lw=3.2)
        shape(c, lambda c: rrect(c, 18, 50, 28, 6, 2), rgb('5d5560'), lw=3)
        shape(c, lambda c: ellipse(c, 32, 6, 4, 4), rgb('5d5560'), lw=2.4)
    render('Prop_Lamp', W, H, draw)


# ---------------------------------------------------------------- tree 180x220 (package-like fluffy canopy)
def tree():
    W, H = 180, 220
    base = 206

    def draw(c):
        shadow(c, 90, 207, 62, 12)
        # trunk with flared roots
        shape(c, lambda c: smooth(c, [(78, 120), (102, 120), (104, 186), (114, base), (98, base - 4), (90, base + 2), (80, base - 4), (66, base), (76, 186)], tension=0.4),
              rgb('a8705c'), top=0.0, bottom=0.15, band=0.3, lw=3.6)
        line(c, [(86, 150), (88, 170)], darker(rgb('a8705c'), 0.35), 2)
        # canopy: lumpy cloud outline like package trees
        pts = []
        rnd = random.Random(7)
        n = 22
        for i in range(n):
            a = -math.pi / 2 + i * 2 * math.pi / n
            rx, ry = 78, 70
            bump = 1 + (0.07 if i % 2 else -0.02) + rnd.uniform(-0.03, 0.03)
            pts.append((90 + math.cos(a) * rx * bump, 84 + math.sin(a) * ry * bump))
        col = rgb('8ba05e')
        shape(c, lambda c: smooth(c, pts, tension=0.6), col, top=0.14, bottom=0.2, lw=3.8, soft=True)
        # little curl marks near the edge like the package trees
        for (x, y, a0, a1) in [(24, 92, 0.75, 1.35), (152, 70, 1.7, 2.25)]:
            c.new_path(); c.arc(x, y, 8, math.pi * a0, math.pi * a1)
            c.set_source_rgba(*OUT, 1); c.set_line_width(2.6); c.stroke()
        # leaf clusters (slightly darker lumps) for depth
        for i, (x, y, r) in enumerate([(62, 104, 20), (118, 112, 18)]):
            c.new_path(); smooth(c, blob(x, y, r, r * 0.8, 9, 0.12, i + 3), tension=0.6)
            c.set_source_rgba(*darker(col, 0.12), 1); c.fill()
        tuft(c, 40, 210, 0.9)
        tuft(c, 136, 212, 0.8)
    render('Prop_Tree', W, H, draw)


# ---------------------------------------------------------------- fence 128x80 (tiles horizontally)
def fence():
    W, H = 128, 80

    def draw(c):
        for y in (34, 54):
            shape(c, lambda c, y=y: c.rectangle(-6, y, W + 12, 9), WOOD, top=0.25, band=0.4, lw=3.2)
        for px in (20, 84):
            shape(c, lambda c, px=px: poly(c, [(px, 72), (px, 20), (px + 12, 12), (px + 24, 20), (px + 24, 72)]), WOOD_D, top=0.2, bottom=0.12, lw=3.4)
            line(c, [(px + 8, 32), (px + 9, 46)], darker(WOOD_D, 0.3), 2)
    render('Prop_Fence', W, H, draw)


if __name__ == '__main__':
    import os
    os.makedirs(OUTDIR, exist_ok=True)
    for k in ('shop', 'library', 'barber'):
        house(k)
    tower_gate()
    well()
    lamp()
    tree()
    fence()
