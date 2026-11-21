"""Room backgrounds (634x388, 32 px = 1 unit). Layout matches the game:
floor 18.4x10 (x -9.2..9.2, y -5..5), north wall face 1.4 above the floor, E/W/S wall tops 0.7.
World (x, y) -> pixel P(x, y)."""
import math
import numpy as np
from scipy.spatial import cKDTree
from PIL import Image, ImageFilter
from eng import Scene, save, BAYER, RAMPS, GLOW

U = 32
# Enter-the-Gungeon style: tall north wall face (82 px) with a dark wall top (20 px) above it,
# E/W/S walls seen from above as dark tops with a bright inner lip.
HX, HY, RIM, FACE, CAP = 9.2, 5.0, 0.7, 2.5625, 0.625
W, H = 634, 445
WT = int(CAP * U)          # 20: wall top (cap) -> face starts here
OUT = 'out'


def P(x, y):
    return ((x + HX + RIM) * U, (HY + FACE + CAP - y) * U)


FX0, FY0 = [int(v) for v in P(-HX, HY)]
FX1, FY1 = [int(round(v)) for v in P(HX, -HY)]
TORCH_X = (-6.5, -3.2, 3.2, 6.5)
TORCH_PY = 60              # torch flame height on the face (world y = 5 + 1.3)
PILLAR_X = (-8.45, -4.85, -1.6, 1.6, 4.85, 8.45)


# ------------------------------------------------------------------ tile helpers
def tiles_rows(sc, x0, y0, x1, y1, rng, row_h=(18, 24), w_rng=(24, 46), stagger=True):
    """irregular running-bond tiles -> id map + per-id bounds."""
    T = np.full((sc.h, sc.w), -1, np.int32)
    bounds = []
    y = y0
    r = 0
    while y < y1:
        h = int(rng.integers(row_h[0], row_h[1] + 1))
        h = min(h, y1 - y) if y1 - y - h < row_h[0] // 2 else h
        x = x0 - int(rng.integers(0, w_rng[0])) if stagger else x0
        while x < x1:
            w = int(rng.integers(w_rng[0], w_rng[1] + 1))
            xa, xb = max(x, x0), min(x + w, x1)
            if xb > xa:
                T[y:min(y + h, y1), xa:xb] = len(bounds)
                bounds.append((x, y, x + w, y + h))
            x += w
        y += h
        r += 1
    return T, np.array(bounds, np.float32)


def tiles_grid(sc, x0, y0, x1, y1, tw, th, offset=0):
    T = np.full((sc.h, sc.w), -1, np.int32)
    bounds = []
    for j, y in enumerate(range(y0, y1, th)):
        off = (offset * (j % 2))
        for x in range(x0 - off, x1, tw):
            xa, xb = max(x, x0), min(x + tw, x1)
            if xb > xa:
                T[y:min(y + th, y1), xa:xb] = len(bounds)
                bounds.append((x, y, x + tw, y + th))
    return T, np.array(bounds, np.float32)


def tiles_voronoi(sc, region, rng, spacing=22):
    ys, xs = np.nonzero(region)
    # jittered grid (even sizes, no thin slivers), rows offset like laid stones
    pts = []
    for j, y in enumerate(np.arange(ys.min() - spacing * 0.5, ys.max() + spacing, spacing * 0.8)):
        for x in np.arange(xs.min() - spacing * 0.5 + (j % 2) * spacing * 0.5, xs.max() + spacing, spacing):
            pts.append((x + rng.uniform(-0.28, 0.28) * spacing, y + rng.uniform(-0.25, 0.25) * spacing * 0.8))
    pts = np.array(pts, np.float32)
    tree = cKDTree(pts)
    q = np.stack([sc.xx[region], sc.yy[region]], 1)
    d, i = tree.query(q, k=2)
    T = np.full((sc.h, sc.w), -1, np.int32)
    T[region] = i[:, 0]
    E = np.zeros((sc.h, sc.w), np.float32)
    E[region] = d[:, 1] - d[:, 0]      # distance to the cell border (approx x2)
    C = np.zeros((sc.h, sc.w, 2), np.float32)
    C[region] = pts[i[:, 0]]
    D0 = np.zeros((sc.h, sc.w), np.float32)
    D0[region] = d[:, 0]
    return T, E, C, D0, len(pts)


def shade_tiles(sc, T, B, mat, kind='floor', gap=1.0, bevel=2.0, rng=None, var=0.35, bump=0.12, mortar_mat=None,
                tilt=0.12, chip=0.0):
    """bevelled tiles from an id map. kind 'floor' (horizontal) or 'wall' (vertical face)."""
    m = T >= 0
    if not m.any():
        return
    idx = np.where(m, T, 0)
    bx0, by0, bx1, by1 = (B[idx, k] for k in range(4))
    ex0 = sc.xx - bx0
    ex1 = bx1 - sc.xx
    ey0 = sc.yy - by0
    ey1 = by1 - sc.yy
    # chipped corners
    if chip:
        noise = sc.noise.field(sc.h, sc.w, 3, 2)
        gap_px = gap + (noise > 1 - chip) * 1.0
    else:
        gap_px = gap
    mortar = m & ((ex0 < gap_px) | (ey0 < gap_px))
    stone = m & ~mortar
    n = len(B)
    rv = (rng.random(n) - 0.5) * 2 * var
    tx = (rng.random(n) - 0.5) * 2 * tilt
    ty = (rng.random(n) - 0.5) * 2 * tilt
    N = np.zeros((sc.h, sc.w, 3), np.float32)
    if kind == 'floor':
        N[..., 1] = 0.72
        N[..., 2] = 0.68
    else:
        N[..., 1] = -0.18
        N[..., 2] = 1.0
    N[..., 0] += tx[idx]
    N[..., 1] += ty[idx]
    b = bevel
    N[..., 0] += np.where(ex0 < gap + b, -0.55, 0) + np.where(ex1 < b, 0.55, 0)
    N[..., 1] += np.where(ey0 < gap + b, 0.55, 0) + np.where(ey1 < b, -0.65, 0)
    if bump:
        N = sc.bumped(N, bump, 3)
    detail = rv[idx]
    if kind == 'floor':
        # relief: every slab is a raised block lit from the top-left. Bright top/left lip, darker right edge,
        # a visible front face (its thickness) along the bottom, and a soft top-to-bottom face gradient.
        # Some slabs sit higher (thicker, brighter) and some lower (their top lip falls into shadow).
        N = np.zeros((sc.h, sc.w, 3), np.float32)
        N[..., 1] = 0.72
        N[..., 2] = 0.68
        hgt = rng.choice([0, 0, 0, 1, -1], n)
        h = hgt[idx]
        thick = np.where(h > 0, 5, np.where(h < 0, 2, 4))
        tile_h = np.maximum(by1 - by0, 1)
        g = np.clip((ey0 - gap) / tile_h, 0, 1)
        detail = detail + 0.45 * (0.5 - g) + h * 0.3
        top = (ey0 >= gap) & (ey0 < gap + 2.0)
        detail = detail + np.where(top, np.where(h < 0, -0.6, 1.3), 0)
        detail = detail + np.where((ex0 >= gap) & (ex0 < gap + 1.5) & ~top, 0.6, 0)
        detail = detail + np.where(ex1 < 1.5, -0.8, 0)
        face = ey1 < thick
        detail = np.where(face, rv[idx] - 1.3 - np.where(ey1 < 1, 0.5, 0), detail)
    sc.paint(stone, mat, N, detail, oid=0)
    sc.G[stone] = idx[stone] + 1000
    mm = mortar_mat or mat
    sc.paint(mortar, mm, (0, 0.3, 1), -2.2, oid=0)
    sc.G[mortar] = 1


def tiles_floor(sc, x0, y0, x1, y1, rng, cell=32, merge=0.12, split=0.12):
    """Gungeon-style floor grid: square cells aligned to the room grid (no running bond, so it never reads
    as a wall), some merged into 2x2 slabs and some split into four small tiles."""
    T = np.full((sc.h, sc.w), -1, np.int32)
    bounds = []
    nx, ny = (x1 - x0 + cell - 1) // cell, (y1 - y0 + cell - 1) // cell
    used = np.zeros((ny, nx), bool)
    def put(ax, ay, bx, by):
        xa, xb, ya, yb = max(ax, x0), min(bx, x1), max(ay, y0), min(by, y1)
        if xb > xa and yb > ya:
            T[ya:yb, xa:xb] = len(bounds)
            bounds.append((ax, ay, bx, by))
    for j in range(ny):
        for i in range(nx):
            if used[j, i]:
                continue
            ax, ay = x0 + i * cell, y0 + j * cell
            r = rng.random()
            if r < merge and i + 1 < nx and j + 1 < ny and not used[j:j + 2, i:i + 2].any():
                used[j:j + 2, i:i + 2] = True
                put(ax, ay, ax + 2 * cell, ay + 2 * cell)
            elif r < merge + split:
                used[j, i] = True
                h = cell // 2
                for (dx, dy) in ((0, 0), (h, 0), (0, h), (h, h)):
                    put(ax + dx, ay + dy, ax + dx + h, ay + dy + h)
            else:
                used[j, i] = True
                put(ax, ay, ax + cell, ay + cell)
    return T, np.array(bounds, np.float32)


def shade_floor(sc, T, B, mat, rng, var=0.2, sunk=0.18, chip=0.1, mortar_mat=None):
    """flat floor tiles seen from above: 1 px grout, 1 px light lip on the top/left edge, 1 px dark lip on the
    bottom/right edge, low per-tile tone variation, a few sunken/worn tiles and chipped corners.
    No thick front faces: those belong to walls."""
    m = T >= 0
    if not m.any():
        return
    idx = np.where(m, T, 0)
    bx0, by0, bx1, by1 = (B[idx, k] for k in range(4))
    ex0, ex1, ey0, ey1 = sc.xx - bx0, bx1 - sc.xx, sc.yy - by0, by1 - sc.yy
    n = len(B)
    rv = (rng.random(n) - 0.5) * 2 * var
    low = rng.random(n) < sunk
    noise = sc.noise.field(sc.h, sc.w, 3, 2)
    cor = np.minimum(np.minimum(ex0, ex1), 99) + np.minimum(np.minimum(ey0, ey1), 99)
    chipped = (cor < 4.5) & (noise > 1 - chip * 3)
    mortar = m & ((ex0 < 1) | (ey0 < 1) | chipped)
    stone = m & ~mortar
    N = np.zeros((sc.h, sc.w, 3), np.float32)
    N[..., 1] = 0.72
    N[..., 2] = 0.68
    d = rv[idx] - np.where(low[idx], 0.35, 0)
    top = (ey0 < 2) | (ex0 < 2)
    bot = (ey1 < 1) | (ex1 < 1)
    # sunken tiles: the lip flips (top edge in shadow, bottom edge catches light)
    lw = low[idx]
    d = d + np.where(top & ~bot, np.where(lw, -0.7, 0.55), 0) + np.where(bot, np.where(lw, 0.4, -0.75), 0)
    sc.paint(stone, mat, N, d, oid=0)
    sc.G[stone] = idx[stone] + 1000
    sc.paint(mortar, mortar_mat or mat, (0, 0.3, 1), -2.0, oid=0)
    sc.G[mortar] = 1


def shade_voronoi(sc, region, mat, rng, spacing=22, var=0.35, gap=1.0, bump=0.1, mortar_mat=None):
    T, E, C, D0, n = tiles_voronoi(sc, region, rng, spacing)
    rv = (rng.random(n) - 0.5) * 2 * var
    idx = np.where(T >= 0, T, 0)
    crack = region & (E < gap)
    stone = region & ~crack
    # dome-ish rocks: normal leans away from the cell centre near the border
    dx = sc.xx - C[..., 0]
    dy = sc.yy - C[..., 1]
    rr = np.maximum(D0 + E * 0.5, 1)
    edge = np.clip(1 - E / 5, 0, 1)
    N = np.zeros((sc.h, sc.w, 3), np.float32)
    N[..., 0] = dx / rr * edge * 0.9
    N[..., 1] = 0.72 - dy / rr * edge * 0.9
    N[..., 2] = 0.68
    if bump:
        N = sc.bumped(N, bump, 3)
    # raised rocks: lit rim on the upper-left of each cell, a dark front face on the lower-right,
    # and the face itself a little lighter toward its top
    N = np.zeros((sc.h, sc.w, 3), np.float32)
    N[..., 1] = 0.72
    N[..., 2] = 0.68
    side = -(dx * 0.6 + dy * 1.0) / rr          # >0 on the upper-left half of a rock
    detail = rv[idx] + np.clip(-dy / rr, -1, 1) * 0.35
    detail = rv[idx] * 0.8
    detail = np.where((E < 2.4) & (side > 0.2), detail + 0.55, detail)
    detail = np.where((E < 2.0) & (side < -0.2), detail - 0.75, detail)
    sc.paint(stone, mat, N, detail, oid=0)
    sc.G[stone] = idx[stone] + 5000
    sc.paint(crack, mortar_mat or mat, (0, 0.3, 1), -2.4, oid=0)
    sc.G[crack] = 2
    return T


# ------------------------------------------------------------------ wall pieces
def wall_cap(sc, m, seed=5):
    """dark wall top seen from above (Gungeon style): faint large tiles, nearly black."""
    T, B = tiles_grid(sc, -8, 0, W + 16, H, 32, 20, 16)
    T[~m] = -1
    shade_tiles(sc, T, B, 'slate', rng=np.random.default_rng(seed), var=0.15, bump=0.03, bevel=1, tilt=0.02)
    sc.LM[m] -= 0.42


def north_wall(sc, rng, mat='stone', blocks=(12, (26, 44)), cap_mat=None):
    face = sc.rect(0, WT, W, FY0)
    cap = sc.rect(0, 0, W, WT)
    wall_cap(sc, cap)
    # bright lip where the top meets the face
    sc.paint(sc.rect(0, WT - 2, W, WT), 'stone', (0, 0.9, 0.4), 0.6, oid=sc.new_id())
    # cornice under the lip
    sc.box(0, WT, W, WT + 6, 2, mat, bevel=0)
    # stone blocks
    T, B = tiles_rows(sc, 0, WT + 6, W, FY0 - 8, rng, row_h=(blocks[0] - 1, blocks[0] + 1), w_rng=blocks[1])
    shade_tiles(sc, T, B, mat, kind='wall', rng=rng, var=0.3, bump=0.06, bevel=1.5, chip=0.15)
    # plinth at the base (protrudes: lit top + front)
    sc.box(0, FY0 - 8, W, FY0, 3, mat, bevel=0, bump=0.04)
    # pilasters (half columns) give the wall depth
    for x in PILLAR_X:
        px, _ = P(x, 0)
        sc.box(px - 11, WT, px + 11, WT + 7, 3, mat, bevel=1)                      # capital
        sc.cyl(px - 9, px + 9, WT + 7, FY0 - 9, mat, bump=0.06)
        sc.add_detail(sc.rect(px - 9, WT + 7, px + 9, FY0 - 9) & ((((sc.yy - WT) % 14) < 1)), -0.9)
        sc.box(px - 11, FY0 - 12, px + 11, FY0, 3, mat, bevel=1)                  # base
        # the pilaster casts a soft shadow on the wall to its right (light from the top-left)
        sh = sc.rect(px + 9, WT + 7, px + 16, FY0 - 9)
        sc.LM[sh] -= 0.12 * np.clip(1 - (sc.xx[sh] - px - 9) / 7, 0, 1)
    # vertical light falloff: the face gets darker toward the top (it recedes into the dark)
    sc.LM[face] -= 0.3 * np.clip(1 - (sc.yy[face] - WT) / (FY0 - WT), 0, 1) ** 1.5
    # floor contact shadow below the wall (deep, Gungeon-like)
    fl = sc.rect(FX0, FY0, FX1, FY0 + 18)
    sc.LM[fl] -= 0.42 * np.clip(1 - (sc.yy[fl] - FY0) / 18, 0, 1) ** 1.3
    return face


def rims(sc, mat='slate'):
    bottom = sc.rect(0, FY1, W, H)
    wall_cap(sc, bottom, 9)
    lip_b = sc.rect(FX0 - 2, FY1, FX1 + 2, FY1 + 2)
    sc.paint(lip_b, 'stone', (0, 0.9, 0.4), 0.7, oid=sc.new_id())
    side_walls(sc)


def side_walls(sc):
    """E/W walls seen from above, running the full height of the image so they cover both ends of the north
    wall (Gungeon: the side walls sit in front of the back wall). Mirror-symmetric. Called again in finish()
    so room decorations painted across the face never end up on top of them."""
    left = sc.rect(0, 0, FX0, FY1)
    right = sc.rect(FX1, 0, W, FY1)
    m = left | right
    sc.LM[m] = 0
    sc.E[m] = 0
    sc.EM[m] = -1
    sc.A[m] = 0
    # one column of long slabs per side (reads as the top of a wall running north-south)
    for x0, x1, off in ((0, FX0, 0), (FX1, W, 16)):
        T, B = tiles_grid(sc, x0, -off, x1, H, x1 - x0, 32, 0)
        T[~m] = -1
        shade_tiles(sc, T, B, 'slate', rng=np.random.default_rng(9), var=0.12, bump=0.03, bevel=1, tilt=0.02)
    sc.LM[m] -= 0.5
    # same lit inner lip on both sides, from the very top down to the south wall
    for lip in (sc.rect(FX0 - 2, 0, FX0, FY1 + 2), sc.rect(FX1, 0, FX1 + 2, FY1 + 2)):
        sc.paint(lip, 'stone', (0, 0.6, 0.8), 0.6, oid=sc.new_id())
    # a darker outer lip line so the wall top reads as a slab

    # the side walls cast a short shadow onto the north face and the floor next to them (symmetric)
    for x0, x1, sgn in ((FX0, FX0 + 10, 1), (FX1 - 10, FX1, -1)):
        sh = sc.rect(x0, WT, x1, FY1)
        d = (sc.xx[sh] - FX0) if sgn > 0 else (FX1 - sc.xx[sh])
        sc.LM[sh] -= 0.32 * np.clip(1 - d / 10, 0, 1)


def south_overlay(name='Wall_South'):
    """the south wall top drawn in front of characters (1.3 units, bottom part = the rim)."""
    hh = int(round(1.3 * U))
    sc = Scene(W, hh, 77)
    sc.smooth = True
    m = sc.rect(0, 0, W, hh)
    T, B = tiles_grid(sc, -8, 0, W + 16, hh, 32, 20, 16)
    shade_tiles(sc, T, B, 'slate', rng=np.random.default_rng(9), var=0.15, bump=0.03, bevel=1, tilt=0.02)
    sc.LM[m] -= 0.42
    sc.paint(sc.rect(0, 0, W, 3), 'stone', (0, 0.9, 0.4), 0.8, oid=sc.new_id())
    sc.paint(sc.rect(0, 3, W, 4), 'slate', (0, -0.6, 0.8), -1.2, oid=sc.new_id())
    sc.dither_amt = 0.0
    img = sc.render(outline=False, inner=True, ambient=0.16, key=0.85, glow_halo=False)
    img[..., 3] = 255
    save(img, f'{OUT}/{name}.png')
    return img


def floor_ao(sc, strength=0.45):
    """dark band along the walls + corners."""
    fl = sc.rect(FX0, FY0, FX1, FY1)
    d = np.minimum.reduce([sc.xx - FX0, FX1 - sc.xx, (sc.yy - FY0) * 0.7, FY1 - sc.yy])
    a = np.clip(1 - d / 22, 0, 1) ** 1.6
    sc.LM[fl] -= (a * strength)[fl]
    # north wall casts a deeper band
    top = np.clip(1 - (sc.yy - FY0) / 14, 0, 1)
    sc.LM[fl] -= (top * 0.25)[fl]
    # hard-edged cast shadow under the tall north wall face, stepped (no dithering)
    dy = sc.yy - FY0
    sc.LM[fl & (dy < 12)] -= 0.3
    sc.LM[fl & (dy >= 12) & (dy < 18)] -= 0.14



def vignette(sc, centre=(0, 0.3), strength=0.32, radius=(11, 7)):
    cx, cy = P(*centre)
    d = np.sqrt(((sc.xx - cx) / (radius[0] * U)) ** 2 + ((sc.yy - cy) / (radius[1] * U)) ** 2)
    sc.LM -= np.clip(d - 0.45, 0, 1) * strength


def light_pool(sc, x, y, r, amount, col=None, squash=0.6):
    cx, cy = P(x, y)
    d = np.sqrt(((sc.xx - cx) / (r * U)) ** 2 + ((sc.yy - cy) / (r * U * squash)) ** 2)
    f = np.clip(1 - d, 0, 1) ** 1.7
    sc.LM += f * amount
    if col is not None:
        if sc.TINT is None:
            sc.TINT = np.ones((sc.h, sc.w, 3), np.float32)
        c = np.asarray(col, np.float32)
        sc.TINT *= 1 + f[..., None] * (c - 1)


def torch_pools(sc, amount=0.22):
    for x in TORCH_X:
        light_pool(sc, x, 4.6, 2.6, amount, (1.12, 0.98, 0.82))


# ------------------------------------------------------------------ wall decorations
def wall_sconce_backing(sc):
    """iron plates behind the torches (torch sprite itself is animated in game)."""
    for x in TORCH_X:
        px, _ = P(x, 0)
        plate = sc.rect(px - 5, TORCH_PY + 2, px + 5, TORCH_PY + 20)
        oid = sc.new_id()
        sc.wallface(plate, 'iron', detail=-0.3, oid=oid)
        sc.paint(sc.rect(px - 5, TORCH_PY + 2, px + 5, TORCH_PY + 3), 'iron', (0, 0.6, 0.8), 0.5, oid=oid)
        for yy in (TORCH_PY + 4, TORCH_PY + 17):
            sc.add_detail(sc.rect(px - 4, yy, px - 3, yy + 1) | sc.rect(px + 3, yy, px + 4, yy + 1), 1.5)
        # soot stain above the flame and a warm glow on the stones around it
        soot = sc.ell(px, TORCH_PY - 22, 8, 14) & (sc.M >= 0) & (sc.yy < TORCH_PY - 8) & (sc.yy > WT)
        sc.LM[soot] -= 0.16
        glow = sc.ell(px, TORCH_PY - 4, 34, 30) & (sc.yy >= WT) & (sc.yy < FY0)
        d = np.sqrt(((sc.xx - px) / 34) ** 2 + ((sc.yy - TORCH_PY + 4) / 30) ** 2)
        sc.LM[glow] += (np.clip(1 - d, 0, 1) ** 1.5 * 0.3)[glow]
        if sc.TINT is None:
            sc.TINT = np.ones((sc.h, sc.w, 3), np.float32)
        sc.TINT[glow] *= (1 + (np.clip(1 - d, 0, 1)[..., None] * (np.array([1.15, 0.98, 0.8]) - 1)))[glow]


def arch_alcove(sc, px, w=26, top=WT + 22, deco='skull', rng=None):
    """recessed arch in the north wall with something inside."""
    bot = FY0 - 14
    hole = sc.rect(px - w / 2, top + w / 2 * 0.6, px + w / 2, bot) | sc.ell(px, top + w / 2 * 0.6, w / 2, w / 2 * 0.6)
    frame = (sc.rect(px - w / 2 - 3, top + w / 2 * 0.6, px + w / 2 + 3, bot + 2) | sc.ell(px, top + w / 2 * 0.6, w / 2 + 3, w / 2 * 0.6 + 3)) & ~hole
    sc.wallface(frame, 'stone', bump=0.12, detail=0.35)
    sc.wallface(hole, 'slate', detail=-1.8)
    sc.LM[hole] -= 0.25 * np.clip(1 - (sc.yy[hole] - top) / (bot - top), 0, 1)
    # keystone
    sc.wallface(sc.rect(px - 2, top - 3, px + 2, top + 2), 'stone', detail=0.8)
    # ledge
    sc.box(px - w / 2 - 2, bot - 2, px + w / 2 + 2, bot + 2, 1.5, 'stone', bevel=0.5)
    if deco == 'skull':
        sc.dome(px, bot - 6, 4.5, 4, 'bone', tilt=0.4)
        sc.paint(sc.rect(px - 3, bot - 7, px - 1, bot - 5) | sc.rect(px + 1, bot - 7, px + 3, bot - 5), 'coal', (0, 0, 1))
        sc.paint(sc.rect(px - 2, bot - 3, px + 2, bot - 2), 'bone', (0, -0.3, 1))
    elif deco == 'candles':
        for i, (dx, hh) in enumerate(((-5, 7), (0, 10), (5, 5))):
            sc.cyl(px + dx - 1.5, px + dx + 1.5, bot - 2 - hh, bot - 2, 'candle')
            sc.emit(sc.ell(px + dx, bot - 4 - hh, 1.2, 2.2), 'fire', 0.85)
        light_pool(sc, 0, 0, 0, 0)  # no-op keeps API symmetric
        cx = (px / U) - HX - RIM
        light_pool(sc, cx, 5.2, 1.1, 0.25, (1.15, 1.0, 0.8), squash=0.8)
    elif deco == 'urn':
        sc.dome(px, bot - 6, 4, 5, 'brick', tilt=0.2)
        sc.paint(sc.rect(px - 4, bot - 7, px + 4, bot - 6), 'gold', (0, 0.2, 1))
    elif deco == 'crystal':
        for dx, hh in ((-4, 8), (1, 12), (5, 6)):
            pts = [(px + dx - 2, bot - 2), (px + dx, bot - 2 - hh), (px + dx + 2, bot - 2)]
            sc.emit(sc.poly(pts), 'cyan', 0.55 + hh / 30)


def chain(sc, px, y0, y1):
    for i, y in enumerate(range(int(y0), int(y1), 3)):
        if i % 2 == 0:
            m = sc.ell(px, y + 1.5, 1.6, 2.2) & ~sc.ell(px, y + 1.5, 0.6, 1.2)
        else:
            m = sc.rect(px - 0.5, y, px + 0.5, y + 3)
        sc.paint(m, 'iron', (0.3 if i % 2 else -0.2, 0.2, 1))


def banner(sc, px, cloth='cloth_r', length=30, torn=True, emblem='gold', rng=None):
    rng = rng or np.random.default_rng(int(px))
    top = WT + 6
    length = int(length * 1.7)
    rod = sc.rect(px - 9, top, px + 9, top + 2)
    sc.hcyl(px - 9, px + 9, top, top + 2.5, 'iron')
    pts = [(px - 7, top + 2), (px + 7, top + 2), (px + 7, top + length)]
    if torn:
        k = 7
        for i in range(k + 1):
            x = px + 7 - 14 * i / k
            pts.append((x, top + length - (rng.integers(0, 7) if i not in (0, k) else 0) - (5 if i == k // 2 else 0)))
    else:
        pts += [(px, top + length - 5), (px - 7, top + length)]
    pts.append((px - 7, top + 2))
    m = sc.poly(pts)
    folds = np.sin((sc.xx - px) * 0.9) * 0.45
    N = np.stack([folds, np.full_like(folds, -0.1), np.ones_like(folds)], -1)
    sc.paint(m, cloth, N, folds * 0.6)
    trim = m & (sc.rect(px - 7, top + 2, px - 5, top + 80) | sc.rect(px + 5, top + 2, px + 7, top + 80))
    sc.paint(trim, emblem, N, 0)
    if emblem:
        sk = sc.ell(px, top + 16, 3.6, 3.4)
        sc.paint(sk, emblem, (0, 0.3, 1), 0.3)
        sc.paint(sc.rect(px - 2, top + 15, px - 0.5, top + 17) | sc.rect(px + 0.5, top + 15, px + 2, top + 17), cloth, (0, 0, 1), -1)


def cobweb(sc, corner, size=26):
    """corner = 'tl' or 'tr' of the floor (drawn as light threads)."""
    cx = FX0 if corner == 'tl' else FX1
    cy = FY0
    sgn = 1 if corner == 'tl' else -1
    m = np.zeros((sc.h, sc.w), bool)
    for k in range(5):
        a = (k / 4) * math.pi / 2
        ex = cx + sgn * math.cos(a) * size
        ey = cy + math.sin(a) * size * 0.8
        m |= sc.line_mask([(cx, cy), (ex, ey)], 1)
    for r in (8, 15, 22):
        pts = []
        for k in range(5):
            a = (k / 4) * math.pi / 2
            pts.append((cx + sgn * math.cos(a) * r, cy + math.sin(a) * r * 0.8 + (2 if k in (1, 3) else 0)))
        m |= sc.line_mask(pts, 1)
    m &= (sc.yy >= cy) & ((sc.xx >= cx) if sgn > 0 else (sc.xx <= cx))
    thr = BAYER[(np.arange(sc.h)[:, None] % 4), (np.arange(sc.w)[None, :] % 4)]
    m &= thr < 0.8
    sc.paint(m, 'web', (0, 0.3, 1), 0.2)


def window_bars(sc, px, w=26):
    top, bot = WT + 14, FY0 - 22
    hole = sc.rect(px - w / 2, top, px + w / 2, bot)
    sc.emit(hole, 'moon', 0.25)
    sc.E[hole] += np.clip(1 - (sc.yy[hole] - top) / (bot - top), 0, 1) * 0.25
    for x in np.arange(px - w / 2 + 3, px + w / 2 - 1, 5):
        sc.cyl(x - 1, x + 1, top, bot, 'iron')
    sc.hcyl(px - w / 2, px + w / 2, top + 9, top + 11, 'iron')
    frame = sc.rect(px - w / 2 - 3, top - 3, px + w / 2 + 3, bot + 3) & ~hole
    sc.wallface(frame & (sc.M != sc.mat('iron')), 'stone', detail=0.5, bump=0.1)
    cx = px / U - HX - RIM
    light_pool(sc, cx + 0.8, 3.6, 2.4, 0.16, (0.86, 0.95, 1.15), squash=0.55)


def roots(sc, rng, n=4):
    for _ in range(n):
        x = rng.uniform(FX0 + 30, FX1 - 30)
        pts = [(x, 0)]
        y = 0
        while y < FY0 + 10:
            y += rng.uniform(4, 8)
            x += rng.uniform(-4, 4)
            pts.append((x, y))
        sc.paint(sc.line_mask(pts, 2), 'darkwood', (0.3, 0.1, 1), 0.3)


# ------------------------------------------------------------------ floor clutter
def pebbles(sc, rng, n, region, mat='stone'):
    ys, xs = np.nonzero(region)
    for _ in range(n):
        i = rng.integers(0, len(xs))
        x, y = xs[i], ys[i]
        r = rng.uniform(1.0, 2.4)
        sc.dome(x, y, r, r * 0.75, mat, tilt=0.5)


def crack_lines(sc, rng, n, region, depth=-1.6):
    ys, xs = np.nonzero(region)
    for _ in range(n):
        i = rng.integers(0, len(xs))
        x, y = float(xs[i]), float(ys[i])
        pts = [(x, y)]
        a = rng.uniform(0, math.pi * 2)
        for _ in range(rng.integers(3, 7)):
            a += rng.uniform(-0.8, 0.8)
            x += math.cos(a) * rng.uniform(3, 7)
            y += math.sin(a) * rng.uniform(2, 5)
            pts.append((x, y))
        m = sc.line_mask(pts, 1) & region
        sc.add_detail(m, depth)
        sc.G[m] = 3


def stains(sc, rng, n, region, kind='blood', size=(6, 14)):
    ys, xs = np.nonzero(region)
    f = sc.noise.field(sc.h, sc.w, 3, 2)
    for _ in range(n):
        i = rng.integers(0, len(xs))
        x, y = xs[i], ys[i]
        r = rng.uniform(*size)
        m = sc.ell(x, y, r, r * 0.6) & region & (f > 0.45)
        if kind == 'blood':
            sc.paint(m, 'blood', sc.N, sc.D - 0.2, oid=4)
        elif kind == 'soot':
            sc.LM[m] -= 0.2
        elif kind == 'moss':
            sc.paint(m, 'moss', sc.N, np.clip(sc.D, -1.0, 0.3), oid=5)


def puddle(sc, x, y, rx, ry, glow=None):
    cx, cy = P(x, y)
    f = sc.noise.field(sc.h, sc.w, 6, 2)
    d = ((sc.xx - cx) / (rx * U)) ** 2 + ((sc.yy - cy) / (ry * U)) ** 2
    m = (d + (f - 0.5) * 0.6) < 1
    sc.paint(m, 'water', (0, 0.9, 0.5), -1.5 + (1 - d) * 0.5, oid=6)
    # glints
    thr = BAYER[(np.arange(sc.h)[:, None] % 4), (np.arange(sc.w)[None, :] % 4)]
    g = m & (((sc.xx + sc.yy * 2) % 9) < 1) & (thr > 0.8)
    sc.add_detail(g, 2.5)
    if glow:
        sc.emit(m & (thr > 0.92), glow, 0.4)


def grate(sc, x, y, w=2.0, h=1.2):
    cx, cy = P(x, y)
    pw, ph = w * U / 2, h * U / 2
    sc.box(cx - pw - 2, cy - ph - 2, cx + pw + 2, cy + ph + 2, 3, 'iron', bevel=1)
    hole = sc.rect(cx - pw, cy - ph + 1, cx + pw, cy + ph)
    sc.paint(hole, 'coal', (0, 0.4, 1), -1.5)
    for xx in np.arange(cx - pw + 2, cx + pw, 5):
        sc.cyl(xx - 1, xx + 1, cy - ph + 1, cy + ph, 'iron', ny=0.4)


def rug(sc, x0, y0, x1, y1, cloth='cloth_r', trim='gold', worn=0.3):
    a, b = P(x0, y0)
    c, d = P(x1, y1)
    m = sc.rect(a, b, c, d)
    f = sc.noise.field(sc.h, sc.w, 4, 2)
    holes = f < worn * 0.45
    m &= ~(holes & ~sc.rect(a + 8, b + 8, c - 8, d - 8))
    pattern = (((np.floor((sc.xx - a) / 4) + np.floor((sc.yy - b) / 4)) % 2) == 0) * 0.25
    sc.flat(m, cloth, detail=pattern + (f - 0.5) * 0.6, bump=0.05)
    border = m & ~sc.rect(a + 5, b + 5, c - 5, d - 5)
    sc.flat(border & ~sc.rect(a + 2, b + 2, c - 2, d - 2) | (border & sc.rect(a + 2, b + 2, c - 2, d - 2) & ~sc.rect(a + 3, b + 3, c - 3, d - 3)), trim, detail=0)
    # diamond
    mx, my = (a + c) / 2, (b + d) / 2
    dm = (np.abs(sc.xx - mx) / ((c - a) * 0.3) + np.abs(sc.yy - my) / ((d - b) * 0.32)) < 1
    dm2 = (np.abs(sc.xx - mx) / ((c - a) * 0.27) + np.abs(sc.yy - my) / ((d - b) * 0.29)) < 1
    sc.flat(dm & ~dm2 & m, trim)
    # fringe
    for xx in range(int(a) + 2, int(c) - 2, 3):
        sc.flat(sc.rect(xx, b - 2, xx + 1, b) | sc.rect(xx, d, xx + 1, d + 2), 'bone', detail=-0.4)


def rune_circle(sc, x, y, r, glow='violet', strength=0.6, spokes=6, ring_w=1.6, squash=0.55):
    cx, cy = P(x, y)
    rr = np.sqrt((sc.xx - cx) ** 2 + ((sc.yy - cy) / squash) ** 2)
    m = np.zeros((sc.h, sc.w), bool)
    for k, rad in enumerate((r, r - 7, r * 0.45)):
        m |= np.abs(rr - rad) < ring_w * (1 if k else 1.3)
    ang = np.arctan2((sc.yy - cy) / squash, sc.xx - cx)
    for i in range(spokes):
        a = i * 2 * math.pi / spokes
        a2 = a + 2 * math.pi / spokes * 2
        m |= sc.line_mask([(cx + math.cos(a) * (r - 7), cy + math.sin(a) * (r - 7) * squash),
                           (cx + math.cos(a2) * (r - 7), cy + math.sin(a2) * (r - 7) * squash)], 1)
    # rune ticks between the rings
    band = (rr > r - 6) & (rr < r - 1)
    ticks = band & ((np.floor((ang + math.pi) / (2 * math.pi) * 48) % 3) == 0) & (((sc.xx + sc.yy) % 3) < 2)
    m |= ticks
    sc.emit(m, glow, strength)
    sc.LM += np.clip(1 - np.abs(rr - r) / 14, 0, 1) * 0.08


def coins(sc, rng, n, region):
    ys, xs = np.nonzero(region)
    for _ in range(n):
        i = rng.integers(0, len(xs))
        x, y = xs[i], ys[i]
        k = rng.integers(1, 4)
        for j in range(k):
            sc.dome(x + j * 1.5, y - j * 1.2, 2.2, 1.4, 'gold', tilt=0.7)


def rails(sc, y, x0=-9.2, x1=9.2):
    a, cy = P(x0, y)
    b, _ = P(x1, y)
    for xx in range(int(a), int(b), 9):
        sc.box(xx, cy - 9, xx + 5, cy + 9, 3, 'wood', bevel=0.5)
    for off in (-6, 5):
        sc.box(a, cy + off - 1, b, cy + off + 2, 1, 'iron', bevel=0)


# ------------------------------------------------------------------ rooms
FLOOR_RAMP = {'stone': 'fstone', 'slate': 'fslate', 'brick': 'fbrick', 'dirt': 'fdirt'}


def base_room(seed, floor='flag', wall='stone', floor_mat='stone', rim_mat='slate', torches=True):
    rng = np.random.default_rng(seed)
    floor_mat = FLOOR_RAMP.get(floor_mat, floor_mat)
    sc = Scene(W, H, seed)
    # no surface noise: the screen palette shader would turn it into speckles
    sc.smooth = True
    fl = sc.rect(FX0, FY0, FX1, FY1)
    if floor == 'flag':
        T, B = tiles_floor(sc, FX0, FY0, FX1, FY1, rng, 32)
        shade_floor(sc, T, B, floor_mat, rng)
    elif floor == 'brick':
        T, B = tiles_floor(sc, FX0, FY0, FX1, FY1, rng, 32, merge=0.05, split=0.45)
        shade_floor(sc, T, B, floor_mat, rng, var=0.2, sunk=0.1)
    elif floor == 'big':
        T, B = tiles_floor(sc, FX0, FY0, FX1, FY1, rng, 64, merge=0, split=0.25)
        shade_floor(sc, T, B, floor_mat, rng, var=0.12, sunk=0.12)
    elif floor == 'cave':
        shade_voronoi(sc, fl, floor_mat, rng, spacing=26, var=0.22, bump=0.1)
    elif floor == 'plank':
        T, B = tiles_rows(sc, FX0, FY0, FX1, FY1, rng, row_h=(9, 9), w_rng=(50, 110))
        shade_tiles(sc, T, B, floor_mat, rng=rng, var=0.3, bump=0.06, bevel=1, tilt=0.05)
        grain = ((sc.noise.field(H, W, 2, 1) - 0.5) * 1.2) * (np.sin(sc.xx * 0.7 + sc.noise.field(H, W, 8, 1) * 6) * 0.5 + 0.5)
        sc.add_detail(fl & (sc.M >= 0), grain[fl & (sc.M >= 0)] * 0.6)
    elif floor == 'checker':
        T, B = tiles_floor(sc, FX0, FY0, FX1, FY1, rng, 32, merge=0, split=0)
        shade_floor(sc, T, B, floor_mat, rng, var=0.08, sunk=0.06)
        chk = fl & ((np.floor((sc.xx - FX0) / 32) + np.floor((sc.yy - FY0) / 32)) % 2 == 0) & (sc.M >= 0)
        stone = chk & (sc.G >= 1000)
        sc.paint(stone, 'slate', sc.N, sc.D - 0.4, oid=0)
        sc.G[stone] = 7
    north_wall(sc, rng, mat=wall)
    rims(sc, rim_mat)
    if torches:
        wall_sconce_backing(sc)
    return sc, rng, fl


def finish(sc, name, torches=True, vig=0.36, dark=0.26):
    sc.dither_amt = 0.0
    side_walls(sc)
    fl = sc.rect(FX0, FY0, FX1, FY1)
    sc.LM[fl] -= dark
    blot = sc.noise.field(H, W, 90, 2)
    sc.LM[fl] += ((blot - 0.5) * 0.0)[fl]
    floor_ao(sc)
    if torches:
        torch_pools(sc)
    vignette(sc, strength=vig)
    img = sc.render(outline=False, inner=True, ambient=0.16, key=0.85, glow_halo=True, halo_alpha=0.45)
    img[..., 3] = 255
    save(img, f'{OUT}/Room_{name}.png')
    return img


def room_combat(variant):
    if variant == 0:
        sc, rng, fl = base_room(21)
        crack_lines(sc, rng, 26, fl)
        stains(sc, rng, 7, fl, 'moss', (8, 22))
        stains(sc, rng, 3, fl, 'blood', (5, 10))
        pebbles(sc, rng, 50, fl)
        grate(sc, 0, -0.2, 1.6, 1.0)
        for px, d in ((P(-8.0, 0)[0], 'skull'), (P(8.0, 0)[0], 'urn')):
            arch_alcove(sc, px, deco=d)
        for x in (-4.8, 4.8):
            banner(sc, P(x, 0)[0], 'cloth_r', rng=rng)
        cobweb(sc, 'tl')
        cobweb(sc, 'tr', 20)
        puddle(sc, -5.5, -2.8, 1.1, 0.5)
        puddle(sc, 6.2, 2.2, 0.8, 0.35)
        return finish(sc, 'StoneA')
    if variant == 1:
        sc, rng, fl = base_room(33, floor='brick', floor_mat='brick', wall='stone', rim_mat='slate')
        crack_lines(sc, rng, 18, fl)
        stains(sc, rng, 5, fl, 'moss', (6, 16))
        stains(sc, rng, 5, fl, 'blood', (6, 12))
        pebbles(sc, rng, 40, fl, 'brick')
        for x in (-6.5, 6.5):
            gx, gy = P(x, -2.5)
        grate(sc, -5.5, -2.6, 1.2, 0.8)
        grate(sc, 5.5, -2.6, 1.2, 0.8)
        window_bars(sc, P(0, 0)[0], 30)
        for x in (-8.3, 8.3):
            chain(sc, P(x, 0)[0], WT + 6, WT + 60)
        arch_alcove(sc, P(-4.8, 0)[0], deco='candles')
        arch_alcove(sc, P(4.8, 0)[0], deco='skull')
        cobweb(sc, 'tr')
        return finish(sc, 'StoneB')
    sc, rng, fl = base_room(47, floor='cave', floor_mat='stone', wall='stone', rim_mat='slate')
    crack_lines(sc, rng, 30, fl)
    stains(sc, rng, 12, fl, 'moss', (8, 24))
    pebbles(sc, rng, 120, fl)
    roots(sc, rng, 6)
    puddle(sc, 3.5, -2.6, 1.8, 0.7)
    puddle(sc, -6.4, 1.8, 0.9, 0.4)
    for x in (-4.8, 4.8):
        banner(sc, P(x, 0)[0], 'cloth_g', rng=rng, emblem='bone')
    arch_alcove(sc, P(0, 0)[0], deco='skull')
    cobweb(sc, 'tl', 30)
    cobweb(sc, 'tr', 24)
    return finish(sc, 'StoneC')


def room_elite():
    sc, rng, fl = base_room(55, floor='big', floor_mat='slate', wall='slate', rim_mat='slate')
    crack_lines(sc, rng, 30, fl, depth=-2)
    stains(sc, rng, 9, fl, 'blood', (6, 16))
    pebbles(sc, rng, 40, fl, 'slate')
    # glowing red cracks
    for _ in range(9):
        x, y = rng.uniform(FX0 + 40, FX1 - 40), rng.uniform(FY0 + 30, FY1 - 30)
        pts = [(x, y)]
        a = rng.uniform(0, 6.28)
        for _ in range(6):
            a += rng.uniform(-0.7, 0.7)
            x += math.cos(a) * 6
            y += math.sin(a) * 4
            pts.append((x, y))
        sc.emit(sc.line_mask(pts, 1) & fl, 'blood', 0.5)
    for x in (-8.3, -3.2 - 1.6, 3.2 + 1.6, 8.3):
        chain(sc, P(x, 0)[0], WT + 6, WT + 66)
    for x in (-6.4 + 1.6, 6.4 - 1.6):
        pass
    banner(sc, P(0, 0)[0], 'cloth_r', length=36, rng=rng, emblem='bone')
    arch_alcove(sc, P(-8.2, 0)[0], deco='skull')
    arch_alcove(sc, P(8.2, 0)[0], deco='skull')
    cobweb(sc, 'tl')
    cobweb(sc, 'tr')
    light_pool(sc, 0, 0, 6, -0.05)
    return finish(sc, 'Elite', vig=0.42)


def room_shrine():
    sc, rng, fl = base_room(61, floor='big', floor_mat='stone', wall='stone')
    crack_lines(sc, rng, 14, fl)
    stains(sc, rng, 4, fl, 'moss', (8, 16))
    pebbles(sc, rng, 26, fl)
    rune_circle(sc, 0, 1.2, 82, 'violet', 0.38, spokes=5)
    # carpet path from the south
    rug(sc, -1.1, -0.4, 1.1, -5.0, 'cloth_p', 'gold', worn=0.2)
    for x in (-3.4, 3.4):
        arch_alcove(sc, P(x * 1.5, 0)[0], deco='candles')
    for x in (-8.3, 8.3):
        banner(sc, P(x, 0)[0], 'cloth_p', rng=rng)
    cobweb(sc, 'tl')
    light_pool(sc, 0, 1.4, 4.0, 0.12, (1.05, 0.95, 1.2))
    return finish(sc, 'Shrine')


def room_vault():
    sc, rng, fl = base_room(71, floor='checker', floor_mat='stone', wall='stone')
    crack_lines(sc, rng, 10, fl)
    coins(sc, rng, 70, fl & ~sc.ell(*P(0, 0), 60, 34))
    # pedestal ring around the chest
    rune_circle(sc, 0, 0, 52, 'gold', 0.45, spokes=8, ring_w=1.2)
    for x in (-4.8, 4.8):
        banner(sc, P(x, 0)[0], 'cloth_b', rng=rng, torn=False)
    arch_alcove(sc, P(-8.2, 0)[0], deco='urn')
    arch_alcove(sc, P(8.2, 0)[0], deco='urn')
    arch_alcove(sc, P(0, 0)[0], deco='candles')
    light_pool(sc, 0, 0, 3.4, 0.14, (1.15, 1.05, 0.8))
    return finish(sc, 'Vault')


def room_market():
    sc, rng, fl = base_room(81, floor='flag', floor_mat='stone', wall='stone')
    # wooden platform where the merchant sets up
    a, b = P(-3.4, 3.9)
    c, d = P(3.4, 1.0)
    plat = sc.rect(a, b, c, d)
    T, B = tiles_rows(sc, int(a), int(b), int(c), int(d), rng, row_h=(8, 8), w_rng=(40, 90))
    shade_tiles(sc, T, B, 'wood', rng=rng, var=0.3, bump=0.05, bevel=1, tilt=0.05)
    sc.LM[plat] -= 0.18
    sc.box(a - 2, d - 1, c + 2, d + 4, 1, 'darkwood', bevel=0.5)
    rug(sc, -2.6, -1.2, 2.6, -4.2, 'cloth_r', 'gold', worn=0.35)
    crack_lines(sc, rng, 12, fl & ~plat)
    pebbles(sc, rng, 26, fl & ~plat)
    for x in (-8.3, 8.3):
        banner(sc, P(x, 0)[0], 'cloth_b', rng=rng)
    # shelves on the wall with jars
    for x in (-5.4, 5.4):
        px, _ = P(x, 0)
        sc.box(px - 34, WT + 44, px + 34, WT + 48, 1.5, 'wood', bevel=0.5)
        for k in range(9):
            jx = px - 30 + k * 7.5
            col = ['cloth_r', 'cloth_b', 'cloth_g', 'gold', 'cloth_p'][k % 5]
            hh = 6 + (k * 7) % 4
            sc.dome(jx, WT + 44 - hh / 2, 2.6, hh / 2, col, tilt=0.1)
            sc.paint(sc.rect(jx - 1, WT + 42 - hh, jx + 1, WT + 44 - hh), 'wood', (0, 0.4, 1))
    cobweb(sc, 'tl', 20)
    light_pool(sc, 0, 1.6, 4.2, 0.1, (1.1, 1.0, 0.85))
    return finish(sc, 'Market')


def room_forge():
    sc, rng, fl = base_room(91, floor='big', floor_mat='slate', wall='brick', rim_mat='slate')
    stains(sc, rng, 14, fl, 'soot', (10, 26))
    crack_lines(sc, rng, 16, fl)
    pebbles(sc, rng, 70, fl & sc.ell(*P(0, 1.6), 140, 70), 'coal')
    # glowing ember cracks around the forge
    for _ in range(14):
        x, y = P(rng.uniform(-3.5, 3.5), rng.uniform(-0.2, 3.4))
        pts = [(x, y)]
        a = rng.uniform(0, 6.28)
        for _ in range(4):
            a += rng.uniform(-0.8, 0.8)
            x += math.cos(a) * 5
            y += math.sin(a) * 3
            pts.append((x, y))
        sc.emit(sc.line_mask(pts, 1) & fl, 'ember', 0.55)
    # iron floor plate under the anvil area
    a, b = P(-3.6, 3.6)
    c, d = P(3.6, -0.4)
    T, B = tiles_grid(sc, int(a), int(b), int(c), int(d), 30, 26, 0)
    shade_floor(sc, T, B, 'iron', rng, var=0.22, sunk=0.1)
    sc.LM[sc.rect(a, b, c, d)] -= 0.28
    for x in range(int(a) + 4, int(c), 30):
        for y in range(int(b) + 4, int(d), 26):
            sc.add_detail(sc.rect(x, y, x + 1, y + 1), 2)
    # tool rack on the wall
    for x in (-5.4, 5.4):
        px, _ = P(x, 0)
        sc.box(px - 30, WT + 12, px + 30, WT + 15, 1, 'darkwood', bevel=0.5)
        for k, tool in enumerate(('ham', 'tong', 'ham', 'saw', 'tong', 'ham')):
            tx = px - 25 + k * 10
            sc.cyl(tx - 1, tx + 1, WT + 15, WT + 48, 'wood')
            if tool == 'ham':
                sc.box(tx - 4, WT + 44, tx + 4, WT + 51, 2, 'iron', bevel=0.5)
            elif tool == 'tong':
                sc.paint(sc.line_mask([(tx, WT + 15), (tx - 3, WT + 52)], 1) | sc.line_mask([(tx, WT + 15), (tx + 3, WT + 52)], 1), 'iron', (0, 0, 1))
            else:
                sc.wallface(sc.poly([(tx - 1, WT + 26), (tx + 6, WT + 26), (tx + 6, WT + 48), (tx - 1, WT + 38)]), 'iron', detail=0.4)
    for x in (-8.3, 8.3):
        chain(sc, P(x, 0)[0], WT + 6, WT + 54)
    light_pool(sc, 0, 1.6, 5.2, 0.18, (1.25, 0.95, 0.75))
    return finish(sc, 'Forge')


def room_boss():
    sc, rng, fl = base_room(101, floor='big', floor_mat='slate', wall='slate', rim_mat='slate')
    crack_lines(sc, rng, 40, fl, depth=-2.2)
    stains(sc, rng, 14, fl, 'blood', (8, 22))
    pebbles(sc, rng, 60, fl, 'slate')
    rune_circle(sc, 0, 0, 150, 'blood', 0.36, spokes=7, squash=0.52, ring_w=1.4)
    rune_circle(sc, 0, 0, 60, 'blood', 0.3, spokes=5, squash=0.52)
    for x in (-6.4, -2.4, 2.4, 6.4):
        banner(sc, P(x, 0)[0], 'cloth_r', length=36, rng=rng, emblem='bone')
    for x in (-8.4, 8.4):
        arch_alcove(sc, P(x, 0)[0], deco='skull')
    # bone piles in the corners
    for (x, y) in ((-8.2, 4.2), (8.2, 4.2), (-8.2, -4.2), (8.2, -4.2)):
        cx, cy = P(x, y)
        for k in range(9):
            bx = cx + rng.uniform(-14, 14)
            by = cy + rng.uniform(-8, 8)
            if k % 3 == 0:
                sc.dome(bx, by, 3.5, 3, 'bone', tilt=0.4)
            else:
                sc.paint(sc.line_mask([(bx - 4, by), (bx + 4, by + rng.uniform(-3, 3))], 2), 'bone', (0, 0.5, 1))
    cobweb(sc, 'tl', 34)
    cobweb(sc, 'tr', 34)
    light_pool(sc, 0, 0, 5.5, 0.1, (1.25, 0.85, 0.85))
    return finish(sc, 'Boss', vig=0.45)


def room_grotto():
    sc, rng, fl = base_room(111, floor='cave', floor_mat='slate', wall='stone', rim_mat='slate')
    stains(sc, rng, 18, fl, 'moss', (10, 26))
    pebbles(sc, rng, 90, fl, 'slate')
    roots(sc, rng, 7)
    for (x, y, r) in ((-6.8, -3.2, 1.0), (7.2, -2.8, 1.2), (-7.6, 2.6, 0.7), (6.8, 3.0, 0.8)):
        puddle(sc, x, y, r, r * 0.45, glow='teal')
    # glowing mushrooms
    for _ in range(26):
        x, y = rng.uniform(FX0 + 10, FX1 - 10), rng.uniform(FY0 + 8, FY1 - 8)
        if abs(x - P(0, 0)[0]) < 120 and abs(y - P(0, 1.6)[1]) < 70:
            continue
        for k in range(rng.integers(1, 4)):
            mx, my = x + k * 4, y + (k % 2) * 2
            sc.cyl(mx - 0.8, mx + 0.8, my - 3, my, 'bone')
            sc.emit(sc.ell(mx, my - 3.5, 2.6, 1.6), 'teal', 0.62)
    # dripping wall seams
    for x in (-7.5, -2.2, 2.8, 7.8):
        px, _ = P(x, 0)
        sc.paint(sc.rect(px, WT + 6, px + 2, FY0 - 2), 'water', (0, 0, 1), 0.6)
    arch_alcove(sc, P(-5.0, 0)[0], deco='crystal')
    arch_alcove(sc, P(5.0, 0)[0], deco='crystal')
    light_pool(sc, 0, 1.6, 5.0, 0.12, (0.8, 1.1, 1.15))
    return finish(sc, 'Grotto', vig=0.38)


def room_mine():
    sc, rng, fl = base_room(121, floor='cave', floor_mat='stone', wall='stone', rim_mat='slate')
    pebbles(sc, rng, 140, fl)
    crack_lines(sc, rng, 18, fl)
    stains(sc, rng, 8, fl, 'soot', (10, 22))
    rails(sc, -2.9)
    # ore chunks
    for _ in range(18):
        x, y = rng.uniform(FX0 + 10, FX1 - 10), rng.uniform(FY0 + 10, FY1 - 10)
        sc.dome(x, y, 3, 2.2, 'slate', bump=0.2)
        sc.emit(sc.rect(x - 1, y - 1, x + 1, y), rng.choice(['cyan', 'gold']), 0.6)
    # wooden support frames on the wall
    for x in (-6.0, -1.8, 1.8, 6.0):
        px, _ = P(x, 0)
        sc.cyl(px - 4, px + 4, 0, FY0, 'wood', bump=0.1)
        sc.add_detail(sc.rect(px - 4, 0, px + 4, FY0) & (((sc.yy % 9) < 1)), -0.6)
    sc.hcyl(0, W, WT, WT + 8, 'wood')
    # crystal clusters glued to the wall
    for x in (-8.2, -3.9, 3.9, 8.2):
        px, _ = P(x, 0)
        for dx, hh, wd in ((-5, 14, 3), (0, 20, 4), (5, 11, 3), (9, 7, 2)):
            pts = [(px + dx - wd, FY0 - 3), (px + dx, FY0 - 3 - hh), (px + dx + wd, FY0 - 3)]
            m = sc.poly(pts)
            sc.emit(m, 'cyan', 0.45 + 0.25 * (sc.xx < px + dx).astype(np.float32))
    light_pool(sc, 0, 1.6, 4.6, 0.12, (0.85, 1.05, 1.2))
    return finish(sc, 'Mine')


def room_den():
    sc, rng, fl = base_room(131, floor='cave', floor_mat='dirt', wall='stone', rim_mat='slate')
    # hay strewn about
    f = sc.noise.field(H, W, 10, 3)
    hay = fl & (f > 0.56)
    for _ in range(900):
        x, y = rng.uniform(FX0, FX1), rng.uniform(FY0, FY1)
        if not hay[int(y), int(x)]:
            continue
        a = rng.uniform(-0.6, 0.6)
        L = rng.uniform(3, 7)
        sc.paint(sc.line_mask([(x, y), (x + math.cos(a) * L, y + math.sin(a) * L * 0.5)], 1), 'hay', (math.sin(a), 0.6, 0.8), rng.uniform(-0.3, 0.6))
    pebbles(sc, rng, 40, fl, 'stone')
    # stalls: wooden fence posts along the north wall
    for x in np.arange(-8.6, 8.8, 1.2):
        px, _ = P(x, 0)
        sc.cyl(px - 2, px + 2, WT + 16, FY0 + 6, 'wood')
    for y in (WT + 24, WT + 44):
        sc.box(FX0, y, FX1, y + 4, 1.5, 'wood', bevel=0.5)
    # troughs
    for x in (-6.5, 6.5):
        cx, cy = P(x, 3.4)
        sc.box(cx - 22, cy - 6, cx + 22, cy + 8, 5, 'wood', bevel=1)
        sc.paint(sc.rect(cx - 19, cy - 4, cx + 19, cy - 1), 'water', (0, 0.9, 0.4), -0.2)
    light_pool(sc, 0, 1.6, 5, 0.16, (1.15, 1.02, 0.82))
    return finish(sc, 'Den', dark=0.42)


if __name__ == '__main__':
    import os, sys
    os.makedirs(OUT, exist_ok=True)
    which = sys.argv[1:] or ['all']
    fns = {
        'StoneA': lambda: room_combat(0), 'StoneB': lambda: room_combat(1), 'StoneC': lambda: room_combat(2),
        'Elite': room_elite, 'Shrine': room_shrine, 'Vault': room_vault, 'Market': room_market, 'Forge': room_forge,
        'Boss': room_boss, 'Grotto': room_grotto, 'Mine': room_mine, 'Den': room_den,
    }
    for k, f in fns.items():
        if 'all' in which or k in which:
            f()
            print(k)
    if 'all' in which or 'South' in which:
        south_overlay()
