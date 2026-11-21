"""Room doors in three looks: normal, elite (purple runes + chains), boss (horned skull, spikes, red glow).
North gate: 72x80, bottom-centre pivot, bars rise into the arch.
Side door (east; west uses flipX): 28x80, centre pivot, an iron-bound slab slides into the north post.
South door: 80x44, centre pivot, drawn over the south wall top; the slab slides into the west post."""
import math
import numpy as np
from eng import Scene, save, BAYER

OUT = 'out'
KINDS = ('Normal', 'Elite', 'Boss')
GLOW = {'Elite': 'violet', 'Boss': 'blood'}


def thr(sc):
    return BAYER[(np.arange(sc.h)[:, None] % 4), (np.arange(sc.w)[None, :] % 4)]


def inner_glow(sc, hole, kind, o, bottom_up=True, axis='y'):
    """coloured light spilling out of the opening (elite/boss), stronger when open."""
    if kind == 'Normal':
        return
    t = thr(sc)
    if axis == 'y':
        lo, hi = sc.yy[hole].min(), sc.yy[hole].max()
        f = (sc.yy - lo) / max(1, hi - lo)
    else:
        lo, hi = sc.xx[hole].min(), sc.xx[hole].max()
        f = 1 - (sc.xx - lo) / max(1, hi - lo)
    strength = 0.25 + 0.35 * o
    m = hole & (f > 1 - strength) & (sc.M == sc.mat('coal'))
    sc.emit(m, GLOW[kind], np.clip(0.1 + (f - (1 - strength)) * 0.9, 0, 0.55))


def skull(sc, cx, cy, s=1.0, eyes='blood'):
    sc.dome(cx, cy, 5 * s, 4.6 * s, 'bone', tilt=0.35)
    sc.paint(sc.rect(cx - 3 * s, cy + 2 * s, cx + 3 * s, cy + 5 * s), 'bone', (0, -0.3, 1), -0.2)
    for x in (-1.5, 0.5):
        sc.paint(sc.rect(cx + x * s * 1.1, cy + 3.5 * s, cx + (x + 1) * s * 1.1, cy + 5 * s), 'coal', (0, 0, 1))
    sc.emit(sc.rect(cx - 3.2 * s, cy - 0.5 * s, cx - 1 * s, cy + 1.5 * s) | sc.rect(cx + 1 * s, cy - 0.5 * s, cx + 3.2 * s, cy + 1.5 * s), eyes, 0.9)


def horns(sc, cx, cy, s=1.0):
    for d in (-1, 1):
        pts = [(cx + d * 4 * s, cy - 2 * s), (cx + d * 11 * s, cy - 9 * s), (cx + d * 13 * s, cy - 15 * s),
               (cx + d * 9 * s, cy - 8 * s), (cx + d * 3 * s, cy + 1 * s)]
        sc.wallface(sc.poly(pts), 'bone', detail=0.2 * d)


def rune_band(sc, m, kind, strength=0.7):
    t = thr(sc)
    marks = m & (((np.floor(sc.yy / 3) + np.floor(sc.xx / 3)) % 2) == 0) & (t < 0.75)
    sc.emit(marks, GLOW[kind], strength)


# ------------------------------------------------------------------ north gate
def gate(o, kind='Normal'):
    sc = Scene(72, 80, 101)
    cx = 36
    spring = 30
    arch_out = sc.rect(2, spring, 70, 80) | sc.ell(cx, spring + 2, 34, 26)
    hole = sc.rect(11, spring, 61, 80) | sc.ell(cx, spring + 2, 25, 19)
    stone = 'slate' if kind == 'Boss' else 'stone'

    def arch():
        sc.wallface(arch_out & ~hole, stone, bump=0.06, detail=0.25)
        for k in range(11):
            a = math.pi + k / 10 * math.pi
            sc.add_detail(sc.line_mask([(cx + math.cos(a) * 25, spring + 2 + math.sin(a) * 19),
                                        (cx + math.cos(a) * 34, spring + 2 + math.sin(a) * 26)], 1) & arch_out & ~hole, -1.2)
        if kind == 'Elite':
            ring = arch_out & ~hole & ~sc.ell(cx, spring + 2, 30, 23) & (sc.yy < spring + 4)
            rune_band(sc, ring, kind, 0.65)
        if kind == 'Boss':
            # bone spikes along the outer arch
            for k in range(9):
                a = math.pi + (k + 0.5) / 9 * math.pi
                bx, by = cx + math.cos(a) * 34, spring + 2 + math.sin(a) * 26
                tx, ty = cx + math.cos(a) * 42, spring + 2 + math.sin(a) * 33
                nx, ny = -math.sin(a) * 2.2, math.cos(a) * 2.2
                sc.wallface(sc.poly([(bx + nx, by + ny), (tx, ty), (bx - nx, by - ny)]), 'bone', detail=0.3)

    def keystone():
        if kind == 'Boss':
            horns(sc, cx, 12, 1.0)
            skull(sc, cx, 10, 1.2)
        else:
            sc.wallface(sc.poly([(cx - 5, 1), (cx + 5, 1), (cx + 4, 12), (cx - 4, 12)]), stone, detail=0.7)
            sc.emit(sc.ell(cx, 6, 1.6, 2), 'violet' if kind == 'Elite' else 'blood', 0.75 if kind == 'Elite' else 0.55)

    arch()
    keystone()
    sc.wallface(hole, 'coal', detail=-1)
    sc.LM[hole] -= 0.2 * np.clip(1 - (sc.yy[hole] - 12) / 60, 0, 1)
    inner_glow(sc, hole & (sc.yy > spring - 4), kind, o)
    lift = o * 62
    bar_mat = 'rust' if kind == 'Boss' else 'iron'
    for x in range(15, 60, 6):
        sc.cyl(x - 1.3, x + 1.3, 12 - lift, 78 - lift, bar_mat)
        sc.paint(sc.poly([(x - 1.6, 78 - lift), (x + 1.6, 78 - lift), (x, 81 - lift)]), bar_mat, (0, -0.3, 1), 0.8)
    for y in (34, 56):
        sc.hcyl(11, 61, y - lift, y - lift + 3, bar_mat)
    iron = sc.M == sc.mat(bar_mat)
    sc.M[iron & ~hole] = -1
    sc.M[(sc.yy < spring - 16) & hole & (sc.M == sc.mat(bar_mat))] = sc.mat('coal')
    arch()
    keystone()
    if kind == 'Elite':
        # chains slung across the arch, they fall away as the gate opens
        if o < 0.5:
            for (a, b) in (((13, 40), (59, 66)), ((59, 40), (13, 66))):
                pts = []
                for k in range(12):
                    u = k / 11
                    pts.append((a[0] + (b[0] - a[0]) * u, a[1] + (b[1] - a[1]) * u + math.sin(u * math.pi) * 4 + o * 30))
                for k, (x, y) in enumerate(pts):
                    if k % 2 == 0:
                        m = sc.ell(x, y, 2.2, 1.6) & ~sc.ell(x, y, 0.9, 0.6)
                    else:
                        m = sc.ell(x, y, 1.4, 2.2) & ~sc.ell(x, y, 0.5, 1.0)
                    sc.paint(m & (sc.yy < 79), 'iron', (0.2, 0.4, 1), 0.3)
            sc.box(cx - 5, 50 + o * 30, cx + 5, 60 + o * 30, 2, 'gold', bevel=0.5)       # padlock
            sc.paint(sc.rect(cx - 1, 54 + o * 30, cx + 1, 57 + o * 30), 'coal', (0, 0, 1))
    for x in (2, 63):
        sc.cyl(x, x + 7, spring + 4, 80, stone, bump=0.05)
        sc.box(x - 1, spring, x + 8, spring + 5, 2, stone, bevel=1)
        if kind == 'Elite':
            rune_band(sc, sc.rect(x + 2, spring + 10, x + 5, 76), kind, 0.6)
        if kind == 'Boss':
            skull(sc, x + 3.5, spring - 2, 0.7)
    return sc.render(halo_alpha=0.55)


# ------------------------------------------------------------------ side door (east)
def post(sc, x0, y0, x1, y1, top_h, kind, mat='stone'):
    """a door post seen from the top-front: dark cap on top, stone front face below."""
    sc.box(x0, y0, x1, y1, top_h, mat, bevel=1, bump=0.05)
    sc.LM[sc.rect(x0, y0, x1, y0 + top_h)] -= 0.25
    sc.add_detail(sc.rect(x0, y0 + top_h, x1, y1) & (((sc.yy - y0 - top_h) % 6) < 1), -0.9)
    if kind == 'Elite':
        rune_band(sc, sc.rect(x0 + 3, y0 + top_h + 2, x1 - 3, y1 - 2), kind, 0.6)
    if kind == 'Boss':
        skull(sc, (x0 + x1) / 2, y0 + top_h / 2 + 1, 0.75)


def wall_top(sc, m, col_w=None, seed=9):
    """the dark slate wall top exactly as the room background draws it (rooms.side_walls / south_overlay)."""
    from rooms import tiles_grid, shade_tiles
    if col_w:
        T, B = tiles_grid(sc, 0, -6, col_w, sc.h, col_w, 32, 0)
    else:
        T, B = tiles_grid(sc, -8, 0, sc.w + 16, sc.h, 32, 20, 16)
    T[~m] = -1
    shade_tiles(sc, T, B, 'slate', rng=np.random.default_rng(seed), var=0.12, bump=0.03, bevel=1, tilt=0.02)
    sc.LM[m] -= 0.5 if col_w else 0.42


def jamb(sc, x0, y0, x1, y1, kind, axis):
    """door-frame block: a raised stone cap sitting in the wall top, lighter than the wall, bevelled."""
    m = sc.rect(x0, y0, x1, y1)
    sc.LM[m] = 0
    sc.E[m] = 0
    mat = 'iron' if kind == 'Boss' else 'slate'
    sc.flat(m, mat, detail=0.2)
    sc.add_detail(sc.rect(x0, y0, x1, y0 + 1) | sc.rect(x0, y0, x0 + 1, y1), 0.9)     # lit top/left lip
    sc.add_detail(sc.rect(x0, y1 - 1, x1, y1) | sc.rect(x1 - 1, y0, x1, y1), -1.0)    # shaded bottom/right
    # rivets
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    if kind == 'Normal':
        for dx, dy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
            px = cx + dx * ((x1 - x0) / 2 - 2.5)
            py = cy + dy * ((y1 - y0) / 2 - 2.5)
            sc.paint(sc.rect(px - 0.5, py - 0.5, px + 1, py + 1), 'iron', (0, 0.5, 0.8), 0.8)
    elif kind == 'Elite':
        sc.dome(cx, cy, 2.6, 2.2, 'gold', tilt=0.3)
        sc.emit(sc.ell(cx, cy, 1.3, 1.1), 'violet', 0.9)
    else:
        skull(sc, cx, cy - 2, 0.55)
    sc.LM[m] -= 0.22
    if kind == 'Boss':
        sc.add_detail(sc.rect(x0 + 1, y0 + 1, x1 - 1, y1 - 1) & ~sc.rect(x0 + 2, y0 + 2, x1 - 2, y1 - 2), 0)
        sc.paint(sc.rect(x0, y0, x1, y0 + 1) | sc.rect(x0, y1 - 1, x1, y1), 'rust', (0, 0.6, 0.8), 0.2)
    return m


def passage(sc, hole, kind, o, axis):
    """floor of the corridor behind the door: same floor stone as the rooms, fading into darkness outward."""
    sc.flat(hole, 'fstone', detail=0.0)
    if axis == 'x':      # east passage: seams every 16 px, darker to the right
        sc.add_detail(hole & ((sc.xx % 16) < 1), -2.0)
        sc.add_detail(hole & (np.abs(sc.yy - (sc.yy[hole].min() + sc.yy[hole].max()) / 2) < 0.6), -2.0)
        f = np.clip(sc.xx / sc.w, 0, 1)
    else:                # south passage: darker downward
        sc.add_detail(hole & ((sc.yy % 16) < 1), -2.0)
        sc.add_detail(hole & (np.abs(sc.xx - sc.w / 2) < 0.6), -2.0)
        f = np.clip(sc.yy / sc.h, 0, 1)
    sc.LM[hole] -= (0.1 + 0.6 * f)[hole]
    if kind != 'Normal':
        g = GLOW[kind]
        # light from the room beyond: stepped bands (no dithering), brighter the further out, stronger when open
        k = 0.6 + 0.4 * o
        st = np.where(f > 0.72, 0.26, np.where(f > 0.5, 0.15, 0.0)) * k
        m = hole & (st > 0)
        sc.emit(m, g, st.astype(np.float32))


def leaf(sc, x0, y0, x1, y1, kind, axis, clip):
    """the door leaf seen from above: a thick plank/iron bar across the opening with a short front face."""
    mat = {'Normal': 'wood', 'Elite': 'darkwood', 'Boss': 'rust'}[kind]
    top = sc.rect(x0, y0, x1, y1) & clip
    if not top.any():
        return
    sc.LM[sc.rect(x0 - 5, y0 - 5, x1 + 1, y1 + 4) & clip] = 0
    sc.E[sc.rect(x0 - 5, y0 - 5, x1 + 1, y1 + 4) & clip] = 0
    if axis == 'x':     # bar runs north-south: grain along y, lit west edge, shaded east edge
        sc.flat(top, mat, detail=(((np.floor(sc.xx - x0) % 3) == 0) * -0.5))
        sc.add_detail(top & (sc.xx < x0 + 1), 0.8)
        sc.add_detail(top & (sc.xx >= x1 - 1), -1.0)
        bands = [y0 + 5, (y0 + y1) / 2 - 1, y1 - 7]
        for yb in bands:
            band = sc.rect(x0, yb, x1, yb + 2) & top
            sc.paint(band, 'iron', (0, 0.6, 0.8), 0.4)
        # front (south) end face of the bar
        sc.paint(sc.rect(x0, y1, x1, y1 + 2) & clip, mat, (0, -0.4, 0.9), -1.2)
    else:               # bar runs east-west: grain along x, lit top edge, a 3 px south face
        sc.flat(top, mat, detail=(((np.floor(sc.yy - y0) % 3) == 0) * -0.5))
        sc.add_detail(top & (sc.yy < y0 + 1), 0.8)
        for xb in (x0 + 6, (x0 + x1) / 2 - 1, x1 - 8):
            band = sc.rect(xb, y0, xb + 2, y1 + 3) & clip
            sc.paint(band, 'iron', (0, 0.6, 0.8), 0.4)
        sc.paint(sc.rect(x0, y1, x1, y1 + 3) & clip & ~sc.rect(0, 0, 0, 0), mat, (0, -0.5, 0.9), -1.1)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    if kind == 'Elite' and clip[int(np.clip(cy - 3, 0, sc.h - 1)), int(np.clip(cx - 3, 0, sc.w - 1))]:
        sc.dome(cx, cy, 2.4, 2.0, 'gold', tilt=0.3)
        sc.emit(sc.ell(cx, cy, 1.1, 0.9) & clip, 'violet', 0.9)
    if kind == 'Boss':      # spikes pointing into the room
        if axis == 'x':
            for yb in np.arange(y0 + 3, y1 - 2, 7):
                sc.paint(sc.poly([(x0, yb), (x0 - 4, yb + 1.5), (x0, yb + 3)]) & clip, 'bone', (-0.4, 0.3, 1), 0.4)
        else:
            for xb in np.arange(x0 + 3, x1 - 2, 7):
                sc.paint(sc.poly([(xb, y0), (xb + 1.5, y0 - 4), (xb + 3, y0)]) & clip, 'bone', (0, 0.6, 0.8), 0.4)


def side_door(o, kind='Normal'):
    """east door in the side wall top (22 px = the 0.7-unit rim). Lit inner lip on the left like the room."""
    W, H = 22, 80
    sc = Scene(W, H, 111)
    sc.smooth = True
    hole = sc.rect(0, 18, W, 62)
    wall_top(sc, ~hole, col_w=W)
    passage(sc, hole, kind, o, 'x')
    # inner lip (room side) only on the wall parts
    sc.paint(sc.rect(0, 0, 2, 18) | sc.rect(0, 62, 2, H), 'stone', (0, 0.6, 0.8), 0.6, oid=sc.new_id())
    # threshold: a worn sill where the room floor meets the passage
    sc.paint(sc.rect(0, 18, 3, 62), 'fstone', (0, 0.72, 0.68), 0.5, oid=sc.new_id())
    # leaf slides north into the jamb
    lift = o * 46
    leaf(sc, 8, 18 - lift, 15, 62 - lift, kind, 'x', hole)
    jamb(sc, 0, 10, W, 20, kind, 'x')
    jamb(sc, 0, 60, W, 70, kind, 'x')
    sc.dither_amt = 0.0
    return sc.render(outline=False, inner=True, ambient=0.16, key=0.85, glow_halo=True, halo_alpha=0.45)


# ------------------------------------------------------------------ south door
def south_door(o, kind='Normal'):
    """door in the south wall top. Same size/placement as the Wall_South overlay strip it sits on (42 px):
    lit lip along the top (room side), dark slate top, the opening shows the passage floor."""
    W, H = 80, 42
    sc = Scene(W, H, 121)
    sc.smooth = True
    hole = sc.rect(18, 0, 62, H)
    wall_top(sc, ~hole)
    sc.paint(sc.rect(0, 0, 18, 3) | sc.rect(62, 0, W, 3), 'stone', (0, 0.9, 0.4), 0.8, oid=sc.new_id())
    sc.paint(sc.rect(0, 3, 18, 4) | sc.rect(62, 3, W, 4), 'slate', (0, -0.6, 0.8), -1.2, oid=sc.new_id())
    passage(sc, hole, kind, o, 'y')
    sc.paint(sc.rect(18, 0, 62, 3), 'fstone', (0, 0.72, 0.68), 0.5, oid=sc.new_id())
    shift = o * 46
    leaf(sc, 18 - shift, 12, 62 - shift, 19, kind, 'y', hole)
    jamb(sc, 9, 2, 20, H - 2, kind, 'y')
    jamb(sc, 60, 2, 71, H - 2, kind, 'y')
    sc.dither_amt = 0.0
    return sc.render(outline=False, inner=True, ambient=0.16, key=0.85, glow_halo=True, halo_alpha=0.45)


def build():
    import os
    os.makedirs(OUT, exist_ok=True)
    res = {}
    for kind in KINDS:
        res['gate' + kind] = [gate(o, kind) for o in (0, 0.25, 0.5, 0.75, 1.0)]
        res['side' + kind] = [side_door(o, kind) for o in (0, 0.33, 0.66, 1.0)]
        res['south' + kind] = [south_door(o, kind) for o in (0, 0.33, 0.66, 1.0)]
        for k, im in enumerate(res['gate' + kind]):
            save(im, f'{OUT}/Gate_{kind}_{k:02d}.png')
        for k, im in enumerate(res['side' + kind]):
            save(im, f'{OUT}/DoorSide_{kind}_{k:02d}.png')
        for k, im in enumerate(res['south' + kind]):
            save(im, f'{OUT}/DoorSouth_{kind}_{k:02d}.png')
    return res


if __name__ == '__main__':
    from PIL import Image
    from eng import sheet
    r = build()
    rows = []
    for kind in KINDS:
        rows.append(sheet(r['gate' + kind] + r['side' + kind] + r['south' + kind], 3))
    W = max(x.shape[1] for x in rows)
    Hs = sum(x.shape[0] for x in rows)
    can = Image.new('RGB', (W, Hs), (24, 20, 30))
    y = 0
    for row in rows:
        can.paste(Image.fromarray(row).convert('RGB'), (0, y))
        y += row.shape[0]
    can.save('prev_doors.png')
