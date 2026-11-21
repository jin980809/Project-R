"""Room facilities, chest, stairs, doors and torches with animation frames.
Every frame of one object has the same canvas so the pivot (3 px above the bottom, centre for Decal/Door/Torch/Fx) stays put."""
import math
import numpy as np
from eng import Scene, save, shadow_under, over, BAYER, GLOW, RAMPS

OUT = 'out'


def thr(sc):
    return BAYER[(np.arange(sc.h)[:, None] % 4), (np.arange(sc.w)[None, :] % 4)]


def frames(fn, n, name, **kw):
    out = []
    for i in range(n):
        img = fn(i / n, i)
        save(img, f'{OUT}/{name}_{i:02d}.png')
        out.append(img)
    return out


def motes(sc, rng, t, cx, cy, spread, rise, glow, n=8, size=1):
    for k in range(n):
        ph = (rng.random() + t) % 1.0
        x = cx + (rng.random() - 0.5) * spread + math.sin((ph + k) * 6.28) * 2
        y = cy - ph * rise
        s = sc.rect(x, y, x + size, y + size)
        sc.emit(s, glow, 0.95 * (1 - ph) + 0.2)


# ================================================================== ALTAR (start room, job change)
def altar(t, i, burst=None):
    sc = Scene(72, 92, 11)
    by = 89
    rng = np.random.default_rng(5)
    # stepped plinth
    sc.box(4, by - 16, 68, by, 6, 'stone', bevel=1.5, bump=0.06)
    sc.box(12, by - 28, 60, by - 14, 5, 'slate', bevel=1.5, bump=0.06)
    # rune band on the front of the plinth
    band = sc.rect(8, by - 8, 64, by - 6)
    pulse = 0.45 + 0.25 * math.sin(t * 6.283)
    runes = band & (((np.floor(sc.xx / 3)) % 2) == 0)
    sc.emit(runes, 'violet', pulse)
    # pillars holding a ring
    for x in (16, 56):
        sc.cyl(x - 3.5, x + 3.5, by - 54, by - 26, 'slate', bump=0.06)
        sc.box(x - 5, by - 58, x + 5, by - 52, 2, 'stone', bevel=1)
        sc.emit(sc.ell(x, by - 62 - math.sin(t * 6.283 + x) * 1, 2, 2.4), 'violet', 0.6 + 0.3 * math.sin(t * 6.283 * 2 + x))
    # floating crystal
    cy = by - 50 + math.sin(t * 6.283) * 2.5
    pts = [(36, cy - 20), (45, cy - 4), (36, cy + 10), (27, cy - 4)]
    m = sc.poly(pts)
    left = m & (sc.xx < 36)
    top = m & (sc.yy < cy - 4)
    strength = np.full((sc.h, sc.w), 0.55, np.float32)
    strength[left] += 0.15
    strength[top] += 0.18
    if burst is not None:
        strength += burst * 0.4
    sc.emit(m, 'violet', np.clip(strength, 0, 1))
    sc.emit(sc.line_mask([(36, cy - 18), (36, cy + 8)], 1) & m, 'violet', 0.98)
    # orbiting shards
    for k in range(3):
        a = t * 6.283 + k * 2.094
        x = 36 + math.cos(a) * 16
        y = cy - 4 + math.sin(a) * 5
        sc.emit(sc.poly([(x, y - 3), (x + 2, y), (x, y + 3), (x - 2, y)]), 'violet', 0.7 if math.sin(a) > 0 else 0.45)
    motes(sc, rng, t, 36, by - 30, 40, 40, 'violet', 7)
    if burst is not None:
        r = 6 + burst * 34
        ring = (np.abs(np.sqrt((sc.xx - 36) ** 2 + ((sc.yy - (cy - 4)) / 0.8) ** 2) - r) < 1.8 * (1 - burst) + 0.6)
        sc.emit(ring & (sc.M < 0) | ring & (sc.M == -2), 'violet', 0.95 - burst * 0.4)
        for k in range(10):
            a = k * 0.628 + 0.3
            d = 4 + burst * 30
            x, y = 36 + math.cos(a) * d, cy - 4 + math.sin(a) * d * 0.8
            sc.emit(sc.rect(x, y, x + 2, y + 2), 'violet', 1 - burst * 0.6)
    img = sc.render(outline_col=(10, 6, 16), halo_alpha=0.6)
    return shadow_under(img, 36, by, 32, 4)


# ================================================================== SHOP (hooded merchant + stall)
def shop(t, i, coin=None):
    sc = Scene(124, 96, 21)
    by = 93
    rng = np.random.default_rng(9)
    flick = 0.7 + 0.18 * math.sin(t * 6.283 * 2) + 0.08 * math.sin(t * 6.283 * 5)
    # back poles + canopy
    for x in (12, 112):
        sc.cyl(x - 2, x + 2, 18, by - 8, 'darkwood')
    can = sc.poly([(4, 14), (120, 14), (116, 30), (8, 30)])
    folds = np.sin(sc.xx * 0.45) * 0.5
    N = np.stack([folds, np.full_like(folds, 0.45), np.ones_like(folds)], -1)
    stripe = ((np.floor(sc.xx / 12)) % 2) == 0
    sc.paint(can & stripe, 'cloth_p', N, folds)
    sc.paint(can & ~stripe, 'bone', N, folds - 0.6)
    # torn hem
    for k in range(20):
        x = 6 + k * 5.8
        d = 30 + (k * 7 % 5)
        sc.paint(sc.poly([(x, 29), (x + 5.8, 29), (x + 3, d)]), 'cloth_p' if (int(x / 12) % 2 == 0) else 'bone', (0, 0, 1), -0.4)
    # merchant (behind the counter)
    bob = math.sin(t * 6.283) * 0.8
    mx, my = 62, 52 + bob
    body = sc.poly([(mx - 15, by - 22), (mx - 11, my - 4), (mx, my - 12), (mx + 11, my - 4), (mx + 15, by - 22)])
    dx = np.clip((sc.xx - mx) / 15, -1, 1)
    Nb = np.stack([dx, np.full_like(dx, 0.1), np.sqrt(1 - dx * dx) + 0.2], -1)
    sc.paint(body, 'cloth_g', Nb, np.sin(sc.xx * 0.6) * 0.3)
    hood = sc.ell(mx, my - 14, 10, 11)
    sc.paint(hood, 'cloth_g', np.stack([dx * 1.2, -(sc.yy - (my - 18)) / 12, np.full_like(dx, 0.8)], -1), 0)
    face = sc.ell(mx, my - 12, 6, 7)
    sc.paint(face, 'coal', (0, 0, 1), -0.5)
    blink = (i % 6) == 4
    if not blink:
        sc.emit(sc.rect(mx - 4, my - 13, mx - 2, my - 12) | sc.rect(mx + 2, my - 13, mx + 4, my - 12), 'gold', 0.85)
    # long nose / beak
    sc.paint(sc.poly([(mx - 1, my - 11), (mx + 1, my - 11), (mx, my - 6)]), 'bone', (0.2, 0.2, 1), -0.2)
    # hands on the counter
    for x in (mx - 12, mx + 12):
        sc.dome(x, by - 31, 3, 2.5, 'flesh_g', tilt=0.5)
    # counter
    sc.box(6, by - 32, 118, by, 8, 'wood', bevel=1.5, bump=0.04)
    planks = ((np.floor(sc.xx) % 14) == 0) & sc.rect(6, by - 24, 118, by)
    sc.add_detail(planks, -0.9)
    sc.box(4, by - 33, 120, by - 29, 1.5, 'darkwood', bevel=0.5)
    # goods: potions, skull, scroll, sack
    goods = [('cloth_r', 18, 7), ('cloth_b', 26, 9), ('cloth_g', 33, 6), ('gold', 90, 5), ('cloth_p', 98, 8), ('cloth_r', 106, 6)]
    for mat, x, hh in goods:
        sc.dome(x, by - 33 - hh / 2, 3, hh / 2 + 1, mat, tilt=0.1)
        sc.cyl(x - 1, x + 1, by - 35 - hh, by - 32 - hh, 'wood')
        sc.add_detail(sc.rect(x - 2, by - 33 - hh, x - 1, by - 30 - hh / 2), 1.6)
    sc.dome(46, by - 36, 4.5, 4, 'bone', tilt=0.4)
    sc.paint(sc.rect(44, by - 37, 45.5, by - 35) | sc.rect(46.5, by - 37, 48, by - 35), 'coal', (0, 0, 1))
    sc.hcyl(72, 86, by - 39, by - 34, 'bone')
    # sack in front
    sc.dome(112, by - 8, 9, 9, 'leather', tilt=0.2, bump=0.05)
    sc.paint(sc.rect(109, by - 18, 115, by - 15), 'hay', (0, 0.3, 1))
    sc.dome(12, by - 7, 7, 7, 'leather', tilt=0.3)
    # lantern hanging from the canopy
    lx, ly = 22, 40 + math.sin(t * 6.283) * 0.6
    sc.paint(sc.line_mask([(lx, 30), (lx, ly - 6)], 1), 'iron', (0, 0, 1))
    sc.box(lx - 4, ly - 6, lx + 4, ly - 4, 1, 'iron', bevel=0)
    sc.emit(sc.rect(lx - 3, ly - 4, lx + 3, ly + 4), 'fire', flick)
    sc.emit(sc.rect(lx - 1, ly - 2, lx + 1, ly + 2), 'fire', min(1, flick + 0.2))
    sc.box(lx - 4, ly + 4, lx + 4, ly + 6, 1, 'iron', bevel=0)
    # coin pop
    if coin is not None:
        for k in range(5):
            a = -math.pi / 2 + (k - 2) * 0.45
            d = coin * 20
            x = mx + math.cos(a) * d
            y = by - 40 + math.sin(a) * d + coin * coin * 18
            sc.dome(x, y, 2.4, 2.4 * abs(math.cos(coin * 12 + k)) + 0.6, 'gold', tilt=0.6)
        for k in range(6):
            a = k * 1.047 + coin * 2
            r = 4 + coin * 14
            x, y = mx + math.cos(a) * r, by - 46 + math.sin(a) * r * 0.7
            sc.emit(sc.rect(x, y, x + 1, y + 1), 'gold', 1 - coin * 0.7)
    img = sc.render(halo_alpha=0.6)
    return shadow_under(img, 62, by, 58, 4)


# ================================================================== FORGE
def forge(t, i, strike=None):
    sc = Scene(120, 100, 31)
    by = 97
    rng = np.random.default_rng(3)
    flick = 0.62 + 0.2 * math.sin(t * 6.283 * 2) + 0.1 * math.sin(t * 6.283 * 3 + 1)
    # chimney
    sc.cyl(70, 92, 2, 40, 'brick', bump=0.06)
    sc.add_detail(sc.rect(70, 2, 92, 40) & ((sc.yy % 7) < 1), -0.9)
    sc.box(68, 0, 94, 6, 2, 'slate', bevel=1)
    # furnace body
    oid, top, front = sc.box(54, 34, 110, by - 4, 9, 'brick', bevel=2, bump=0.05)
    bricks = (((sc.yy - 43) % 7) < 1) | ((((sc.xx + ((np.floor((sc.yy - 43) / 7) % 2) * 6)) % 12) < 1))
    sc.add_detail(front & bricks, -1.0)
    sc.add_detail(top, -0.3)
    # mouth with fire
    mouth = sc.rect(66, 54, 98, 80) | sc.ell(82, 54, 16, 9)
    sc.paint(mouth, 'coal', (0, 0, 1), -2)
    arch = (sc.rect(63, 54, 101, 80) | sc.ell(82, 54, 19, 12)) & ~mouth
    sc.wallface(arch & front, 'stone', detail=0.3, bump=0.05)
    fire = mouth & (sc.yy > 58)
    f = sc.noise.field(sc.h, sc.w, 3, 2)
    phase = np.sin(sc.xx * 0.5 + t * 6.283 * 2) * 0.12
    strength = np.clip(0.35 + (sc.yy - 58) / 26 + (f - 0.5) * 0.6 + phase, 0, 1) * flick + 0.15
    tongues = mouth & (sc.yy > 46 + (np.sin(sc.xx * 0.7 + t * 6.283 * 3) + 1) * 5)
    sc.emit(fire | tongues, 'fire', np.clip(strength, 0, 1))
    # coals bed
    for k in range(9):
        x = 69 + k * 3.6
        sc.emit(sc.ell(x, 78, 2.4, 1.6), 'ember', 0.6 + 0.3 * math.sin(t * 6.283 + k))
    # bellows on the right
    sc.box(100, by - 26, 118, by - 14, 4, 'leather', bevel=1)
    sc.cyl(98, 102, by - 22, by - 18, 'iron')
    # anvil
    ax, ay = 30, by - 14
    sc.box(ax - 8, ay - 2, ax + 8, by - 2, 3, 'darkwood', bevel=1)   # stump
    sc.box(ax - 14, ay - 12, ax + 12, ay - 4, 3, 'iron', bevel=1)
    sc.paint(sc.poly([(ax + 12, ay - 12), (ax + 20, ay - 10), (ax + 12, ay - 8)]), 'iron', (0.6, 0.4, 0.8))
    sc.box(ax - 6, ay - 4, ax + 6, ay, 1, 'iron', bevel=0.5)
    # glowing ingot on the anvil
    glow_bar = sc.rect(ax - 6, ay - 15, ax + 4, ay - 12)
    sc.emit(glow_bar, 'lava', 0.75 + 0.1 * math.sin(t * 6.283 * 2))
    # hammer leaning
    sc.paint(sc.line_mask([(8, by - 4), (16, by - 30)], 2), 'wood', (0.3, 0, 1))
    sc.box(11, by - 36, 23, by - 28, 2, 'iron', bevel=0.5)
    # quench bucket
    sc.cyl(40, 52, by - 12, by - 2, 'wood')
    sc.flat(sc.ell(46, by - 12, 6, 2), 'water', detail=-0.5)
    for y in (by - 9, by - 5):
        sc.paint(sc.rect(40, y, 52, y + 1), 'iron', (0, 0, 1))
    # embers rising from the chimney
    motes(sc, rng, t, 81, 30, 14, 30, 'ember', 6)
    # strike: sparks fly from the anvil
    if strike is not None:
        s = strike
        cx, cy = ax - 1, ay - 14
        if s < 0.25:
            fl = sc.ell(cx, cy, 6 * (1 - s * 2), 4 * (1 - s * 2))
            sc.emit(fl, 'gold', 1.0)
        for k in range(16):
            a = -math.pi * (0.1 + 0.8 * ((k * 0.618) % 1))
            sp = 10 + (k * 7 % 9) * 2.5
            d = s * sp
            x = cx + math.cos(a) * d
            y = cy + math.sin(a) * d + s * s * 20
            L = 3
            pts = [(x, y), (x - math.cos(a) * L, y - math.sin(a) * L)]
            sc.emit(sc.line_mask(pts, 1), 'gold', 1 - s * 0.8)
        # hammer swinging in
        ang = -1.2 + min(1, s * 4) * 1.2 if s < 0.3 else -min(1, (s - 0.3) * 2) * 0.9
        hx, hy = cx + 18, cy - 4
        L = 16
        ex, ey = hx + math.cos(math.pi + ang) * L, hy + math.sin(math.pi + ang) * L
        sc.paint(sc.line_mask([(hx, hy), (ex, ey)], 2), 'wood', (0, 0, 1))
        sc.paint(sc.ell(ex, ey, 4, 3), 'iron', (0, 0.4, 1))
    img = sc.render(halo_alpha=0.65)
    return shadow_under(img, 66, by, 56, 4)


# ================================================================== FISHING POOL (floor level)
POOL_W, POOL_H = 248, 156
POOL_CX, POOL_CY = 124, 70


def fishing(t, i):
    sc = Scene(POOL_W, POOL_H, 41)
    rng = np.random.default_rng(7)
    by = POOL_H - 3
    f = sc.noise.field(sc.h, sc.w, 7, 2)
    d = ((sc.xx - POOL_CX) / 114) ** 2 + ((sc.yy - POOL_CY) / 62) ** 2
    water = (d + (f - 0.5) * 0.35) < 1
    shore = ((d + (f - 0.5) * 0.35) < 1.32) & ~water
    # rocky rim
    sc.flat(shore, 'slate', bump=0.25, bscale=2, detail=(f - 0.5) * 1.2)
    for k in range(32):
        a = k / 32 * 6.283 + rng.random() * 0.2
        x = POOL_CX + math.cos(a) * 119
        y = POOL_CY + math.sin(a) * 66
        r = rng.uniform(4, 8)
        sc.dome(x, y, r, r * 0.7, 'stone', tilt=0.45, bump=0.1)
    # water: depth gradient + moving caustic highlights
    depth = np.clip(1 - d, 0, 1)
    wave = np.sin(sc.xx * 0.22 + sc.yy * 0.5 - t * 6.283) * 0.5 + np.sin(sc.xx * 0.13 - sc.yy * 0.31 + t * 6.283 * 2) * 0.5
    N = np.stack([wave * 0.25, np.full_like(wave, 0.85), np.full_like(wave, 0.55)], -1)
    sc.paint(water, 'water', N, -1.6 - depth * 1.8 + 0.4 * (wave > 0.75))
    glint = water & (wave > 0.86) & (thr(sc) > 0.55)
    sc.add_detail(glint, 2.2)
    # glowing lily pads / mushrooms
    for (x, y) in ((44, 50), (196, 84), (170, 34), (70, 100), (210, 56)):
        sc.flat(sc.ell(x, y, 6, 3), 'moss', detail=0.4)
        sc.emit(sc.ell(x + 1, y - 1, 1.6, 1.2), 'teal', 0.7 + 0.25 * math.sin(t * 6.283 + x))
    # glowing fish shadows circling
    for k in range(2):
        a = t * 6.283 + k * 3.14
        x = POOL_CX + math.cos(a) * 70
        y = POOL_CY + math.sin(a) * 32
        fm = sc.ell(x, y, 4, 2) & water
        sc.emit(fm, 'teal', 0.35)
    # wooden pier from the south shore into the water
    px0, px1 = POOL_CX - 10, POOL_CX + 10
    pier = sc.rect(px0, POOL_CY + 6, px1, by - 2)
    for y in range(POOL_CY + 6, by - 2, 5):
        sc.box(px0, y, px1, y + 5, 3, 'wood', bevel=0.5)
    for x in (px0 - 1, px1 - 2):
        for y in (POOL_CY + 8, POOL_CY + 40):
            sc.cyl(x, x + 3, y - 4, y + 4, 'darkwood')
    # stalagmites on the far shore
    for (x, h) in ((26, 26), (40, 16), (206, 30), (222, 18)):
        sc.paint(sc.poly([(x - 6, POOL_CY - 6), (x, POOL_CY - 6 - h), (x + 6, POOL_CY - 6)]), 'stone', (0.0, 0.1, 1), 0)
        sc.add_detail(sc.poly([(x, POOL_CY - 6 - h), (x + 6, POOL_CY - 6), (x, POOL_CY - 6)]), -0.7)
    img = sc.render(outline=False, halo_alpha=0.5)
    return img


def ripple(t, i, kind='float'):
    """bobber + rings, centred (Fx_ pivot = centre)."""
    sc = Scene(40, 28, 51)
    cx, cy = 20, 16
    if kind == 'float':
        dy = math.sin(t * 6.283) * 1.0
        for k in range(2):
            r = ((t + k * 0.5) % 1.0)
            ring = np.abs(np.sqrt((sc.xx - cx) ** 2 + ((sc.yy - cy) / 0.45) ** 2) - (4 + r * 12)) < 0.8
            sc.emit(ring & (thr(sc) < 1 - r), 'moon', 0.55 - r * 0.3)
        sc.dome(cx, cy - 2 + dy, 3, 3, 'cloth_r', tilt=0.6)
        sc.paint(sc.rect(cx - 3, cy - 1 + dy, cx + 3, cy + 1 + dy), 'bone', (0, 0.3, 1))
    elif kind == 'bite':
        dy = [2, 4, 1, 5][i % 4]
        for k in range(3):
            r = ((t * 2 + k / 3) % 1.0)
            ring = np.abs(np.sqrt((sc.xx - cx) ** 2 + ((sc.yy - cy) / 0.45) ** 2) - (3 + r * 15)) < 1
            sc.emit(ring, 'moon', 0.8 - r * 0.4)
        sc.dome(cx, cy - 2 + dy, 3, 3, 'cloth_r', tilt=0.6)
        for k in range(5):
            a = -math.pi * (0.15 + 0.7 * k / 4)
            x, y = cx + math.cos(a) * (5 + (i % 2) * 3), cy + math.sin(a) * (4 + (i % 2) * 3)
            sc.emit(sc.rect(x, y, x + 1.5, y + 1.5), 'moon', 0.9)
        sc.emit(sc.rect(cx - 2, 2, cx + 3, 9), 'gold', 0.95 if i % 2 == 0 else 0.0)   # "!" mark
        sc.emit(sc.rect(cx - 0.5, 2, cx + 1.5, 7) | sc.rect(cx - 0.5, 8, cx + 1.5, 10), 'gold', 0.98)
    else:   # splash
        s = t
        for k in range(14):
            a = -math.pi * (0.05 + 0.9 * ((k * 0.618) % 1))
            sp = 8 + (k * 5 % 7) * 1.5
            x = cx + math.cos(a) * sp * s
            y = cy + math.sin(a) * sp * s * 1.4 + s * s * 14
            sc.emit(sc.ell(x, y, 1.4, 1.4), 'moon', 0.95 - s * 0.6)
        r = 3 + s * 16
        ring = np.abs(np.sqrt((sc.xx - cx) ** 2 + ((sc.yy - cy) / 0.45) ** 2) - r) < 1.2
        sc.emit(ring, 'moon', 0.8 - s * 0.5)
        if s < 0.5:
            sc.emit(sc.ell(cx, cy - 4 - s * 10, 3, 5), 'moon', 0.9)
    return sc.render(outline=False, halo_alpha=0.4)


# ================================================================== ORE VEIN
def ore(t, i, hit=None, depleted=False):
    sc = Scene(100, 84, 61)
    by = 81
    rng = np.random.default_rng(13)
    # rock mass
    sc.dome(50, 52, 44, 28, 'slate', tilt=0.35, bump=0.3, bscale=3, ao_rim=0.25)
    sc.dome(34, 44, 24, 20, 'stone', tilt=0.45, bump=0.3, bscale=3)
    sc.dome(66, 50, 22, 18, 'slate', tilt=0.4, bump=0.3, bscale=3)
    sc.dome(52, 34, 16, 13, 'stone', tilt=0.5, bump=0.25, bscale=2)
    rockm = sc.M >= 0
    # veins (glowing cracks)
    veins = np.zeros((sc.h, sc.w), bool)
    for pts in ([(20, 60), (32, 48), (40, 50), (52, 38)], [(60, 66), (66, 54), (78, 48)], [(44, 70), (50, 60)]):
        veins |= sc.line_mask(pts, 2)
    veins &= rockm
    vstr = 0.25 if depleted else 0.5 + 0.2 * math.sin(t * 6.283)
    sc.emit(veins, 'cyan', vstr)
    # crystal clusters
    clusters = [(30, 36, 'cyan', 16), (58, 26, 'cyan', 20), (74, 40, 'gold', 13), (42, 58, 'gold', 11), (82, 58, 'cyan', 10)]
    for k, (x, y, g, h) in enumerate(clusters):
        if depleted:
            # broken stumps
            sc.paint(sc.poly([(x - 4, y + 2), (x - 2, y - 3), (x + 1, y - 1), (x + 4, y + 2)]), 'slate', (0, 0.4, 1), -0.4)
            continue
        for j, (dx, hh, w, lean) in enumerate(((-5, h * 0.75, 2.5, -0.35), (0, h * 1.1, 3.2, 0.05), (5, h * 0.6, 2.4, 0.4))):
            bx = x + dx
            base_y = y + 2
            tipx, tipy = bx + lean * hh, base_y - hh
            shoulder = 0.78
            sx_l, sx_r = bx - w + lean * hh * shoulder, bx + w + lean * hh * shoulder
            sy = base_y - hh * shoulder
            m = sc.poly([(bx - w, base_y), (sx_l, sy), (tipx, tipy), (sx_r, sy), (bx + w, base_y)])
            lface = sc.poly([(bx - w, base_y), (sx_l, sy), (tipx, tipy), (bx + lean * hh * 0.5, base_y)])
            sweep = ((t * 1.0 + k * 0.17 + j * 0.1) % 1.0)
            band = np.abs((base_y - sc.yy) / max(1, hh) - sweep) < 0.1
            st = np.full((sc.h, sc.w), 0.42, np.float32)
            st[lface] = 0.66
            st[band & m] = 0.97
            sc.emit(m, g, st)
            sc.emit(sc.line_mask([(bx + lean * hh * 0.5, base_y), (tipx, tipy)], 1) & m, g, 0.85)
    if not depleted:
        motes(sc, rng, t, 50, 40, 60, 26, 'cyan', 5)
    # pickaxe stuck in the rock (decor)
    sc.paint(sc.line_mask([(90, by - 4), (80, by - 32)], 2), 'wood', (0.4, 0, 1))
    sc.paint(sc.poly([(70, by - 34), (80, by - 36), (92, by - 30), (80, by - 32)]), 'iron', (0.2, 0.6, 0.8))
    # hit: chips and sparks
    if hit is not None:
        s = hit
        cx, cy = 50, 46
        if s < 0.3:
            sc.emit(sc.ell(cx, cy, 7 * (1 - s * 3) + 1, 5 * (1 - s * 3) + 1), 'gold', 1.0)
        for k in range(10):
            a = -math.pi * (0.05 + 0.9 * ((k * 0.618) % 1))
            sp = 12 + (k * 7 % 5) * 4
            x = cx + math.cos(a) * sp * s
            y = cy + math.sin(a) * sp * s + s * s * 30
            if k % 2:
                sc.dome(x, y, 2.2, 1.8, 'stone', tilt=0.5)
            else:
                sc.emit(sc.poly([(x, y - 2), (x + 1.5, y), (x, y + 2), (x - 1.5, y)]), 'cyan' if k % 4 else 'gold', 0.95 - s * 0.4)
    img = sc.render(halo_alpha=0.55)
    return shadow_under(img, 50, by, 44, 4)


# ================================================================== STABLE (pen; the offered mount is drawn by the game)
def stable(t, i):
    sc = Scene(136, 92, 71)
    by = 89
    rng = np.random.default_rng(17)
    flick = 0.7 + 0.15 * math.sin(t * 6.283 * 2) + 0.08 * math.sin(t * 6.283 * 5)
    # straw bed
    f = sc.noise.field(sc.h, sc.w, 3, 2)
    bed = sc.ell(68, 66, 52, 18)
    sc.flat(bed, 'hay', bump=0.2, bscale=2, detail=(f - 0.5) * 1.6)
    for k in range(120):
        x, y = 68 + rng.normal(0, 22), 66 + rng.normal(0, 8)
        a = rng.uniform(-0.8, 0.8)
        L = rng.uniform(3, 6)
        sc.paint(sc.line_mask([(x, y), (x + math.cos(a) * L, y + math.sin(a) * L * 0.5)], 1), 'hay', (math.sin(a), 0.7, 0.7), rng.uniform(0, 0.8))
    # back fence
    for x in range(8, 132, 20):
        sc.cyl(x - 3, x + 3, 22, 60, 'wood', bump=0.05)
        sc.flat(sc.ell(x, 22, 3, 1.4), 'wood', detail=0.5)
    for y in (28, 44):
        sc.box(4, y, 132, y + 5, 2, 'wood', bevel=0.5, bump=0.04)
    # side posts (front corners)
    for x in (6, 130):
        sc.cyl(x - 3, x + 3, 40, by - 2, 'wood')
        sc.flat(sc.ell(x, 40, 3, 1.4), 'wood', detail=0.5)
    sc.box(2, 56, 12, 60, 1.5, 'wood', bevel=0.5)
    sc.box(124, 56, 134, 60, 1.5, 'wood', bevel=0.5)
    # trough (front left) and hay bale (front right)
    sc.box(14, by - 18, 44, by - 4, 5, 'wood', bevel=1)
    sc.flat(sc.rect(17, by - 17, 41, by - 14), 'water', detail=-0.4)
    sc.box(96, by - 22, 124, by - 4, 8, 'hay', bevel=2, bump=0.15, bscale=2)
    for x in (103, 117):
        sc.paint(sc.rect(x, by - 22, x + 2, by - 4), 'darkwood', (0, 0, 1))
    # lantern on the left post
    lx, ly = 6, 30
    sc.paint(sc.line_mask([(lx, 36), (lx + 8, 36)], 1), 'iron', (0, 0, 1))
    sc.box(lx + 5, ly + 6, lx + 13, ly + 8, 1, 'iron', bevel=0)
    sc.emit(sc.rect(lx + 6, ly + 8, lx + 12, ly + 16), 'fire', flick)
    sc.box(lx + 5, ly + 16, lx + 13, ly + 18, 1, 'iron', bevel=0)
    # sign: horse shoe
    ring = sc.ell(68, 14, 8, 8) & ~sc.ell(68, 14, 5, 5) & (sc.yy < 19)
    sc.paint(ring, 'iron', (0, 0.4, 1))
    sc.box(52, 4, 84, 8, 1.5, 'darkwood', bevel=0.5)
    img = sc.render(halo_alpha=0.6)
    return shadow_under(img, 68, by, 64, 4)


# ================================================================== CHEST
def chest(t, i, mode='closed', o=0.0):
    sc = Scene(48, 48, 81)
    by = 45
    # lid behind the body when open (drawn first)
    if mode != 'closed':
        # o: 0 closed .. 1 fully open. lid front face rises and shortens (rotating backwards)
        lid_h = 12 * (1 - o) + 3 * o
        top = 12 - 10 * o
        if o > 0.05:
            sc.box(5, top, 43, 24, max(2, 5 * (1 - o) + 1), 'wood', bevel=1.2)
            sc.paint(sc.rect(5, top, 43, top + 2), 'gold', (0, 0.3, 1))
            for x in (8, 38):
                sc.paint(sc.rect(x, top, x + 3, 24), 'gold', (0, 0.4, 1))
            sc.LM[sc.rect(5, top + 2, 43, 24)] -= 0.2 * o
    # body
    sc.box(6, 22, 42, by - 1, 5, 'wood', bevel=1, bump=0.04)
    sc.add_detail(sc.rect(6, 27, 42, by) & ((sc.xx % 6) < 1), -0.6)
    for x in (8, 38):
        sc.paint(sc.rect(x, 22, x + 3, by - 1), 'gold', (0, -0.1, 1))
    sc.paint(sc.rect(6, by - 4, 42, by - 1), 'gold', (0, -0.1, 1))
    if mode == 'closed':
        sc.box(5, 12, 43, 24, 5, 'wood', bevel=1.5)
        for x in (8, 38):
            sc.paint(sc.rect(x, 12, x + 3, 24), 'gold', (0, 0.4, 1))
        sc.paint(sc.rect(5, 22, 43, 24), 'gold', (0, -0.1, 1))
        sc.box(21, 20, 27, 28, 2, 'gold', bevel=0.5)
        sc.paint(sc.rect(23, 24, 25, 27), 'coal', (0, 0, 1))
        sweep = (t * 1.3) % 1.3
        band = np.abs((sc.xx - 5) / 38 - sweep + (sc.yy - 12) / 40) < 0.05
        sc.add_detail(band & (sc.M == sc.mat('gold')), 2.5)
        sparkle = [(10, 14), (36, 26), (24, 20), (14, 30)][i % 4]
        sc.emit(sc.rect(sparkle[0], sparkle[1], sparkle[0] + 1, sparkle[1] + 1), 'gold', 1.0)
    else:
        hole = sc.rect(8, 22, 40, 27)
        sc.paint(hole, 'coal', (0, 0, 1), -1)
        pile = sc.ell(24, 25, 15, 4) & sc.rect(8, 20, 40, 27)
        st = 0.45 + 0.35 * o + 0.15 * (((sc.xx + sc.yy + i) % 4) == 0)
        sc.emit(pile, 'gold', np.full((sc.h, sc.w), 1, np.float32) * st)
        sc.box(21, 22, 27, 27, 2, 'gold', bevel=0.5)
        for k in range(6):
            ph = (t + k / 6) % 1
            x = 10 + k * 5.5
            y = 20 - ph * 16 * o
            if o > 0.3:
                sc.emit(sc.rect(x, y, x + 1, y + 1 + (k % 2)), 'gold', 1 - ph * 0.5)
    img = sc.render(halo_alpha=0.55)
    return shadow_under(img, 24, by, 20, 3)


# ================================================================== STAIRS (floor decal, centre pivot)
def stairs(t, i):
    sc = Scene(112, 112, 91)
    cx, cy = 56, 56
    rr = np.sqrt((sc.xx - cx) ** 2 + ((sc.yy - cy) / 0.62) ** 2)
    plat = rr < 52
    sc.flat(plat & (rr >= 46), 'stone', bump=0.08, detail=0.3)
    sc.flat(plat & (rr < 46), 'slate', bump=0.06)
    # spiral steps rising toward the centre (lighter = higher)
    ang = np.arctan2((sc.yy - cy) / 0.62, sc.xx - cx)
    steps = 12
    k = np.floor(((ang + math.pi) / (2 * math.pi)) * steps)
    ring = plat & (rr < 44) & (rr > 16)
    edge = ring & (((ang + math.pi) / (2 * math.pi) * steps % 1) < 0.12)
    sc.flat(ring, 'stone', detail=(k / steps - 0.5) * 1.6)
    sc.add_detail(edge, -1.4)
    # glowing portal well in the middle
    pr = np.sqrt((sc.xx - cx) ** 2 + ((sc.yy - cy) / 0.62) ** 2)
    well = pr < 16
    swirl = np.sin(np.arctan2((sc.yy - cy) / 0.62, sc.xx - cx) * 3 + pr * 0.4 - t * 6.283 * 2)
    sc.emit(well, 'moon', np.clip(0.45 + swirl * 0.2 + (1 - pr / 16) * 0.35, 0, 1))
    # rotating rune ring on the rim
    rim = plat & (rr >= 46) & (rr < 51)
    on = ((np.floor(((ang + math.pi) / (2 * math.pi) * 24 + t * 24)) % 3) == 0)
    sc.emit(rim & on & ((sc.xx + sc.yy) % 2 == 0), 'moon', 0.75)
    # beams of light rising from the well
    for k2 in range(6):
        ph = (t + k2 / 6) % 1
        a = k2 * 1.047 + t * 2
        x = cx + math.cos(a) * 8
        y = cy + math.sin(a) * 5 - ph * 30
        sc.emit(sc.rect(x, y, x + 1, y + 2), 'moon', 1 - ph * 0.6)
    img = sc.render(outline=True, halo_alpha=0.6)
    return img


# ================================================================== DOORS
def gate(o):
    """north portcullis, 72x80 bottom-centre pivot (fills the 2.56-unit wall face). o: 0 closed .. 1 open"""
    sc = Scene(72, 80, 101)
    cx = 36
    spring = 30            # where the arch starts curving
    arch_out = sc.rect(2, spring, 70, 80) | sc.ell(cx, spring + 2, 34, 26)
    hole = sc.rect(11, spring, 61, 80) | sc.ell(cx, spring + 2, 25, 19)
    sc.wallface(arch_out & ~hole, 'stone', bump=0.06, detail=0.25)
    for k in range(11):
        a = math.pi + k / 10 * math.pi
        sc.add_detail(sc.line_mask([(cx + math.cos(a) * 25, spring + 2 + math.sin(a) * 19), (cx + math.cos(a) * 34, spring + 2 + math.sin(a) * 26)], 1) & arch_out & ~hole, -1.2)
    sc.wallface(sc.poly([(cx - 5, 1), (cx + 5, 1), (cx + 4, 12), (cx - 4, 12)]), 'stone', detail=0.7)
    sc.emit(sc.ell(cx, 6, 1.6, 2), 'blood', 0.7)
    # darkness beyond (gets deeper toward the top of the opening)
    sc.wallface(hole, 'coal', detail=-1)
    sc.LM[hole] -= 0.2 * np.clip(1 - (sc.yy[hole] - 12) / 60, 0, 1)
    # bars slide upward into the wall
    lift = o * 62
    for x in range(15, 60, 6):
        sc.cyl(x - 1.3, x + 1.3, 12 - lift, 78 - lift, 'iron')
        sc.paint(sc.poly([(x - 1.6, 78 - lift), (x + 1.6, 78 - lift), (x, 81 - lift)]), 'iron', (0, -0.3, 1), 0.8)
    for y in (34, 56):
        sc.hcyl(11, 61, y - lift, y - lift + 3, 'iron')
    iron = sc.M == sc.mat('iron')
    sc.M[iron & ~hole] = -1
    sc.M[(sc.yy < spring - 16) & hole & (sc.M == sc.mat('iron'))] = sc.mat('coal')
    # the arch is drawn again over the bars that slid up into the wall
    sc.wallface(arch_out & ~hole, 'stone', bump=0.06, detail=0.25)
    for k in range(11):
        a = math.pi + k / 10 * math.pi
        sc.add_detail(sc.line_mask([(cx + math.cos(a) * 25, spring + 2 + math.sin(a) * 19), (cx + math.cos(a) * 34, spring + 2 + math.sin(a) * 26)], 1) & arch_out & ~hole, -1.2)
    sc.wallface(sc.poly([(cx - 5, 1), (cx + 5, 1), (cx + 4, 12), (cx - 4, 12)]), 'stone', detail=0.7)
    sc.emit(sc.ell(cx, 6, 1.6, 2), 'blood', 0.7)
    # side columns with braziers of light at the base
    for x in (2, 63):
        sc.cyl(x, x + 7, spring + 4, 80, 'stone', bump=0.05)
        sc.box(x - 1, spring, x + 8, spring + 5, 2, 'stone', bevel=1)
    return sc.render(halo_alpha=0.4)


def door(o):
    """side door, 70x29 centre pivot, top of the picture faces into the room. o: 0 closed .. 1 open"""
    sc = Scene(70, 29, 111)
    sc.flat(sc.rect(4, 0, 66, 29), 'coal', detail=-0.6)
    for y in range(4, 29, 4):
        sc.add_detail(sc.rect(6, y, 64, y + 1), 0.8)
    for x in (0, 64):
        sc.box(x, 0, x + 6, 29, 4, 'stone', bevel=1)
    # two door leaves sliding apart
    gap = o * 30
    for side in (-1, 1):
        if side < 0:
            x0, x1 = 6 - gap, 35 - gap
        else:
            x0, x1 = 35 + gap, 64 + gap
        x0c, x1c = max(6, x0), min(64, x1)
        if x1c - x0c < 1:
            continue
        sc.box(x0c, 2, x1c, 27, 3, 'wood', bevel=1)
        sc.add_detail(sc.rect(x0c, 2, x1c, 27) & (((sc.xx - x0) % 7) < 1), -0.8)
        for y in (8, 19):
            sc.paint(sc.rect(x0c, y, x1c, y + 2), 'iron', (0, 0.4, 1))
        sc.add_detail(sc.rect(x0c, 8, x1c, 9) & (((sc.xx - x0) % 9) < 1), 1.5)
    return sc.render(halo_alpha=0.3)


# ================================================================== TORCH (wall sconce flame, centre pivot)
def torch(t, i):
    sc = Scene(18, 34, 121)
    sc.box(5, 18, 13, 22, 1.5, 'iron', bevel=0.5)
    sc.cyl(7, 11, 20, 32, 'wood')
    sc.paint(sc.rect(6, 30, 12, 33), 'iron', (0, 0.3, 1))
    sway = math.sin(t * 6.283) * 1.5
    f = sc.noise.field(sc.h, sc.w, 2, 1)
    flame = sc.poly([(9 + sway * 1.4, 1 + (i % 2)), (14, 12), (12.5, 18), (5.5, 18), (4, 12)])
    core = sc.poly([(9 + sway, 7), (11.5, 13), (10.5, 18), (7.5, 18), (6.5, 13)])
    st = np.clip(0.45 + (sc.yy / 18) * 0.35 + (f - 0.5) * 0.3, 0, 1)
    sc.emit(flame, 'fire', st)
    sc.emit(core, 'fire', np.clip(st + 0.35, 0, 1))
    return sc.render(halo_alpha=0.65)


def brazier(t, i):
    sc = Scene(30, 44, 131)
    by = 41
    sc.cyl(13, 17, by - 18, by - 2, 'iron')
    sc.box(7, by - 4, 23, by, 1.5, 'iron', bevel=0.5)
    bowl = sc.ell(15, by - 20, 12, 5) & (sc.yy > by - 22)
    sc.paint(bowl, 'iron', (0, -0.3, 1), 0.2)
    sc.flat(sc.ell(15, by - 22, 11, 3), 'coal', detail=-0.5)
    for k in range(5):
        sc.emit(sc.ell(7 + k * 4, by - 22, 2, 1.2), 'ember', 0.6 + 0.3 * math.sin(t * 6.283 + k))
    sway = math.sin(t * 6.283) * 2
    flame = sc.poly([(15 + sway, 2 + (i % 2) * 2), (23, by - 30), (24, by - 23), (6, by - 23), (7, by - 30)])
    core = sc.poly([(15 + sway * 0.6, 10), (19, by - 28), (19, by - 23), (11, by - 23), (11, by - 28)])
    f = sc.noise.field(sc.h, sc.w, 2, 1)
    st = np.clip(0.4 + (sc.yy / (by - 23)) * 0.35 + (f - 0.5) * 0.3, 0, 1)
    sc.emit(flame, 'fire', st)
    sc.emit(core, 'fire', np.clip(st + 0.35, 0, 1))
    img = sc.render(halo_alpha=0.6)
    return shadow_under(img, 15, by, 10, 2.5)


# ================================================================== build
def build():
    import os
    os.makedirs(OUT, exist_ok=True)
    res = {}
    res['altar'] = frames(lambda t, i: altar(t, i), 8, 'Fac_Altar_Idle')
    res['altar_b'] = frames(lambda t, i: altar(0.0, i, burst=i / 7), 8, 'Fac_Altar_Act')
    res['shop'] = frames(lambda t, i: shop(t, i), 6, 'Fac_Shop_Idle')
    res['shop_b'] = frames(lambda t, i: shop(0.0, 0, coin=i / 5), 6, 'Fac_Shop_Act')
    res['forge'] = frames(lambda t, i: forge(t, i), 6, 'Fac_Forge_Idle')
    res['forge_b'] = frames(lambda t, i: forge(i / 8, i, strike=i / 7), 8, 'Fac_Forge_Act')
    res['fish'] = frames(lambda t, i: fishing(t, i), 8, 'Fac_Fishing_Idle')
    res['bob'] = frames(lambda t, i: ripple(t, i, 'float'), 4, 'Fx_Bobber')
    res['bite'] = frames(lambda t, i: ripple(t, i, 'bite'), 4, 'Fx_Bite')
    res['splash'] = frames(lambda t, i: ripple(i / 5, i, 'splash'), 6, 'Fx_Splash')
    res['ore'] = frames(lambda t, i: ore(t, i), 6, 'Fac_Ore_Idle')
    res['ore_b'] = frames(lambda t, i: ore(0.0, 0, hit=i / 5), 6, 'Fac_Ore_Act')
    res['ore_d'] = frames(lambda t, i: ore(0.0, 0, depleted=True), 1, 'Fac_Ore_Empty')
    res['stable'] = frames(lambda t, i: stable(t, i), 6, 'Fac_Stable_Idle')
    res['chest'] = frames(lambda t, i: chest(t, i, 'closed'), 4, 'Chest_Idle')
    res['chest_o'] = frames(lambda t, i: chest(i / 5, i, 'open', o=min(1, (i + 1) / 5)), 6, 'Chest_Open')
    res['chest_l'] = frames(lambda t, i: chest(t, i, 'open', o=1.0), 4, 'Chest_Loop')
    res['stairs'] = frames(lambda t, i: stairs(t, i), 8, 'Decal_Stairs')
    res['gate'] = [gate(o) for o in (0, 0.25, 0.5, 0.75, 1.0)]
    for k, im in enumerate(res['gate']):
        save(im, f'{OUT}/Gate_{k:02d}.png')
    res['door'] = [door(o) for o in (0, 0.33, 0.66, 1.0)]
    for k, im in enumerate(res['door']):
        save(im, f'{OUT}/Door_{k:02d}.png')
    res['torch'] = frames(torch, 6, 'Torch')
    res['brazier'] = frames(brazier, 6, 'Prop_Brazier')
    return res


if __name__ == '__main__':
    from PIL import Image
    from eng import sheet
    r = build()
    rows = [sheet(r[k], 3) for k in ('altar', 'altar_b', 'shop', 'shop_b', 'forge', 'forge_b')]
    for n, row in enumerate(rows):
        Image.fromarray(row).save(f'prev_fac{n}.png')
    for k in ('fish', 'bob', 'bite', 'splash', 'ore', 'ore_b', 'ore_d', 'stable', 'chest', 'chest_o', 'chest_l', 'stairs', 'gate', 'door', 'torch', 'brazier'):
        Image.fromarray(sheet(r[k], 3)).save(f'prev_{k}.png')
