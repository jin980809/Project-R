import math
import numpy as np
from lib import *
from rec import *
from core import part, skill

# ---------------------------------------------------------------- palettes
CRIMSON = [(0, C('#2a0006', 0)), (0.18, C('#6e0a16', 0.85)), (0.45, C('#d61e34')), (0.75, C('#ff7a7a')), (1, C('#fff4ef'))]
STEEL = [(0, C('#1b2a44', 0)), (0.2, C('#3a5c9a', 0.8)), (0.5, C('#8fd0ff')), (0.8, C('#e6f6ff')), (1, C('#ffffff'))]
GOLD = [(0, C('#3d2200', 0)), (0.2, C('#8a5200', 0.85)), (0.5, C('#f2b21c')), (0.8, C('#ffe68a')), (1, C('#fffbe8'))]
AZURE = [(0, C('#061a40', 0)), (0.2, C('#0f4fa8', 0.8)), (0.55, C('#2fa8ff')), (0.85, C('#bfeaff')), (1, C('#ffffff'))]
RAGE = [(0, C('#3b0500', 0)), (0.25, C('#a31e00', 0.8)), (0.55, C('#ff6a12')), (0.85, C('#ffd25a')), (1, C('#fffbe0'))]
WHEAT = [(0, C('#3b2a06', 0)), (0.2, C('#8a6a1c', 0.8)), (0.5, C('#e8c55a')), (0.85, C('#fff0b8')), (1, C('#ffffff'))]
TEAL = [(0, C('#033a33', 0)), (0.2, C('#0b7d6a', 0.7)), (0.55, C('#3fe0b8')), (0.85, C('#c8fff0')), (1, C('#ffffff'))]
ELEC = [(0, C('#06143a', 0)), (0.25, C('#1a3fb0', 0.7)), (0.55, C('#38c8ff')), (0.85, C('#d2f6ff')), (1, C('#ffffff'))]
EMBER = [(0, C('#2a0e00', 0)), (0.3, C('#a33a00', 0.9)), (0.6, C('#ff8a1a')), (0.9, C('#ffe9a0')), (1, C('#ffffff'))]
DUST = (C('#6b5a48'), C('#d8c7ad'))


# ============================================================ SWORD
def sword_spin(cv, t, rng):
    X, Y, R, A, s = P(cv)
    head = -0.6 + t * TAU * 1.35
    sweep = 0.6 + 2.4 * ease_out(min(1, t * 2.2))
    a = fade_tail(t, 0.55)
    # faint swirling floor of red wind
    sw = spiral(R, A, 2, 3.0, t * 14, 0.35) * band(R, 0.62, 0.22) * 0.55 * a
    cv.ramp(sw, CRIMSON, cover=0.7)
    for k in range(2):
        slash_arc(cv, X, Y, 0.74, head + k * math.pi, sweep, 0.2, CRIMSON, sign=1, streak_seed=11 + k,
                  glow=C('#ff2a3a', 0.6), alpha=a)
    slash_arc(cv, X, Y, 0.5, head + 0.9, sweep * 0.7, 0.12, CRIMSON, sign=1, streak_seed=19, alpha=a * 0.75)
    # blood droplets flung tangentially
    ps = Particles(np.random.default_rng(4), 26, (cv.w / 2, cv.h / 2), (60, 150), (0.35, 0.6), size=(1.6, 3.2),
                   gravity=120, spawn=(0.1, 0.5), jitter=s * 0.45)
    dots(cv, ps, t, [(0, C('#40000a', 0)), (0.4, C('#8a0012')), (0.8, C('#e8203a')), (1, C('#ff9a9a'))], mode='over', shrink=0.3)


def sword_cross(cv, t, rng):
    X, Y, R, A, s = P(cv)

    def cut(angle, t0, seed):
        lt = (t - t0) / 0.42
        if lt <= 0 or lt > 1.6:
            return
        ca, sa = math.cos(angle), math.sin(angle)
        u = X * ca + Y * sa
        d = -X * sa + Y * ca
        reveal = ease_out(min(1, lt * 2.4), 3)
        head = -0.95 + 1.9 * reveal
        thin = max(0.0, 1 - max(0, lt - 0.5) / 1.1)
        thick = 0.085 * thin * np.clip(1 - (u / 0.95) ** 2, 0, 1) ** 0.8
        v = np.exp(-(d / np.maximum(thick, 1e-4)) ** 2) * (u < head) * smooth(-0.95, -0.3, u) ** 0.5
        v *= (0.55 + 0.45 * smooth(head - 0.9, head, u))
        cv.glow(v, C('#7ac8ff', 0.8), 7, 1.0 * thin)
        cv.ramp(np.clip(v * 1.3, 0, 1), STEEL)
        # afterglow line
        if lt > 0.25:
            cv.add(C('#ffffff')[:3], np.exp(-(d / 0.012) ** 2) * (np.abs(u) < 0.9 * reveal) * 0.6 * thin, 0.6)
        ps = Particles(np.random.default_rng(seed), 14, (cv.w / 2 + ca * head * s, cv.h / 2 + sa * head * s),
                       (60, 180), (0.15, 0.3), size=(1, 2), spawn=(t0, t0 + 0.25))
        sparks(cv, ps, t, C('#e8f7ff')[:3], C('#5ab4ff', 0.7), streak=0.05)

    cut(math.radians(-38), 0.0, 1)
    cut(math.radians(38), 0.24, 2)
    # crossing flash
    f = pulse(t, 0.3, 0.62)
    if f > 0:
        st = poly((cv.h, cv.w), star_poly(cv.w / 2, cv.h / 2, s * 0.42 * f, s * 0.05, 4, 0))
        cv.glow(st, C('#9fdcff', 1), 10, 1.4 * f)
        cv.add(C('#ffffff')[:3], st)


def bash_trail(cv, t, rng):
    X, Y, R, A, s = P(cv)
    X = (np.mgrid[0:cv.h, 0:cv.w][1] + 0.5) / cv.w * 2 - 1
    Y = (np.mgrid[0:cv.h, 0:cv.w][0] + 0.5) / cv.h * 2 - 1
    v = np.zeros_like(X)
    for i in range(3):
        x0 = (t * 0.9 + i / 3) % 1.0 * 2.2 - 1.0
        curve = X - (x0 - 0.35 * Y ** 2)
        v = np.maximum(v, np.exp(-(curve / 0.05) ** 2) * (1 - Y ** 2) * smooth(-1.0, -0.4, x0) * (1 - smooth(0.7, 1.1, x0)))
    body = np.exp(-(Y / 0.5) ** 2) * smooth(-1, 0.2, X) * (1 - smooth(0.8, 1.0, X)) * 0.35
    n = N(31, 6, 3).sample(X * 2 - t * 2, Y)
    cv.ramp(np.clip(body * (0.5 + n), 0, 1), GOLD, cover=0.8)
    cv.glow(v, C('#ffcf3a', 0.9), 4, 1.1)
    cv.ramp(np.clip(v, 0, 1), GOLD)
    # dust kicked at the bottom
    ps = Particles(np.random.default_rng(3), 10, (cv.w * 0.2, cv.h * 0.8), (20, 60), (0.4, 0.8), angle=(math.pi, math.pi * 1.4), size=(5, 10), spawn=(0, 0.4))
    pos, age, alive, _ = ps.state(t)
    smoke(cv, rng, [pos[i] for i in np.nonzero(alive)[0]], [ps.size[i] * (0.6 + age[i]) for i in np.nonzero(alive)[0]], t, 5, *DUST, alpha=0.7)


def shield_pts(cx, cy, sz):
    pts = []
    for i in range(16):
        u = i / 15
        pts.append((cx + sz * (-0.8 + 1.6 * u), cy - sz * 0.9 + sz * 0.12 * math.sin(math.pi * u)))
    for i in range(16):
        u = i / 15
        a = math.pi * u
        pts.append((cx + sz * 0.8 * math.cos(a), cy - sz * 0.9 + sz * (0.2 + 1.6 * math.sin(a) ** 0.9) * (0.5 + 0.5 * math.sin(a))))
    return pts


def bash_impact(cv, t, rng):
    X, Y, R, A, s = P(cv)
    cx, cy = cv.w / 2, cv.h / 2
    a = fade_tail(t, 0.55)
    ring(cv, R, 0.3 + 0.62 * ease_out(t), 0.06 * (1 - t) + 0.01, GOLD, alpha=a)
    rays(cv, R, A, 8, t * 0.6, 0.25, 0.1, 0.95 * ease_out(min(1, t * 2)), GOLD, alpha=a * 0.9)
    sc = 0.3 + 0.12 * ease_out(min(1, t * 3)) - 0.04 * t
    m = shaded_poly(cv, shield_pts(cx, cy + s * 0.05, s * sc), C('#e8a91a'), C('#fff0a6'), C('#8a4e00'), '#3a2000', 2.0)
    cross_glyph(cv, cx, cy + s * 0.05, s * sc * 0.45, 0, C('#fff7d6', a), C('#ffd84a', 0.6), 0.2)
    cv.c *= 1.0
    # stun stars orbiting
    for k in range(3):
        ang = t * TAU * 1.2 + k * TAU / 3
        x = cx + math.cos(ang) * s * 0.55
        y = cy - s * 0.45 + math.sin(ang) * s * 0.15
        st = poly((cv.h, cv.w), star_poly(x, y, s * 0.09, s * 0.04, 5))
        cv.outline(st, C('#3a2a00'), 1.2)
        cv.over(C('#ffe14a')[:3], st * a)
    if t > 0.6:
        cv.c *= a


def guard_cast(cv, t, rng):
    X, Y, R, A, s = P(cv)
    cx, cy = cv.w / 2, cv.h / 2
    a = fade_tail(t, 0.6)
    rr = 0.2 + 0.72 * ease_out(min(1, t * 1.6))
    # hex lattice clipped to a ring
    q = (X * 7.0, Y * 7.0)
    hx = q[0] / 1.5
    hy = q[1] / math.sqrt(3) - (np.floor(hx) % 2) * 0.5
    fx = np.abs((hx % 1) - 0.5); fy = np.abs((hy % 1) - 0.5)
    lattice = np.clip(1 - np.minimum(np.abs(fx - 0.5) * 6, np.abs(fy - 0.5) * 9), 0, 1) ** 2
    shell = smooth(rr - 0.35, rr, R) * (1 - smooth(rr, rr + 0.03, R))
    v = lattice * shell * 0.8 + band(R, rr, 0.025)
    cv.glow(v, C('#ffd34a', 0.6), 5, a)
    cv.ramp(np.clip(v, 0, 1), GOLD, cover=a)
    # rising sigil
    up = ease_out(min(1, t * 2.2))
    sc = 0.22 * up
    if sc > 0.02:
        shaded_poly(cv, shield_pts(cx, cy - s * 0.1 * up, s * sc), C('#f0b830'), C('#fff3b8'), C('#9a5a00'), '#3a2200', 1.6)
    ps = Particles(np.random.default_rng(8), 22, (cx, cy + s * 0.2), (10, 40), (0.4, 0.7), angle=(-math.pi * 0.8, -math.pi * 0.2), size=(1.5, 3), spawn=(0.1, 0.6), jitter=s * 0.35)
    sparks(cv, ps, t, C('#fff3b0')[:3], C('#ffc23a', 0.6), streak=0.12)
    if t > 0.6:
        cv.c *= a


def charge_trail(cv, t, rng):
    X = (np.mgrid[0:cv.h, 0:cv.w][1] + 0.5) / cv.w * 2 - 1
    Y = (np.mgrid[0:cv.h, 0:cv.w][0] + 0.5) / cv.h * 2 - 1
    a = fade_tail(t, 0.4)
    core = np.exp(-(Y / (0.08 + 0.1 * (X + 1) / 2)) ** 2) * smooth(-1, 0.9, X) * (1 - smooth(0.92, 1.0, X))
    cv.glow(core, C('#3aa0ff', 0.8), 8, 0.8 * a)
    streaks(cv, X, Y, t, 4, AZURE, density=14, speed=1.6, length=0.6, thick=0.05, yspan=0.7, alpha=a)
    cv.ramp(np.clip(core * 0.9, 0, 1), AZURE, cover=a)
    # afterimage blade line
    blade = np.exp(-((Y + 0.05) / 0.025) ** 2) * smooth(0.2, 0.95, X) * (X < 0.97)
    cv.add(C('#ffffff')[:3], blade * a)


def charge_impact(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.5)
    reveal = ease_out(min(1, t * 3), 3)
    thick = 0.12 * (1 - t) * np.clip(1 - X ** 2, 0, 1)
    v = np.exp(-((Y + X * 0.15) / np.maximum(thick, 1e-3)) ** 2) * (X < -1 + 2 * reveal)
    cv.glow(v, C('#2f8fff', 1), 9, 1.2 * a)
    cv.ramp(np.clip(v, 0, 1), AZURE, cover=a)
    ring(cv, R, 0.2 + 0.5 * ease_out(t), 0.05 * (1 - t), AZURE, alpha=a * 0.8)
    ps = Particles(np.random.default_rng(6), 24, (cv.w / 2, cv.h / 2), (80, 220), (0.2, 0.45), size=(1, 2.2), spawn=(0, 0.2))
    sparks(cv, ps, t, C('#e8f6ff')[:3], C('#4ab0ff', 0.7))


def roar_cast(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.55)
    n = N(41, 8, 3)
    for k in range(3):
        lt = t * 1.3 - k * 0.18
        if 0 < lt < 1:
            r = 0.15 + 0.8 * ease_out(lt)
            wob = n.sample(A / TAU * 3 + k * 0.3, R * 0.2 + lt) - 0.5
            v = band(R + wob * 0.08, r, 0.045 * (1 - lt) + 0.012)
            cv.glow(v, C('#ff4a1a', 0.8), 5, (1 - lt))
            cv.ramp(np.clip(v * (1.2 - lt), 0, 1), RAGE)
    # flame skirt at center
    shape = np.exp(-(X / (0.3 + 0.25 * np.clip(Y + 0.2, 0, 1))) ** 2) * smooth(-0.75, -0.2, Y) * (1 - smooth(0.35, 0.55, Y))
    flames(cv, X, Y, shape * 0.9 * a, t, 51, EMBER, rise=2.2, gain=1.5)
    # roar lines at start
    f = pulse(t, 0.0, 0.4)
    if f > 0:
        rays(cv, R, A, 12, 0.2, 0.12, 0.25, 0.95, RAGE, alpha=f)
    ps = Particles(np.random.default_rng(9), 30, (cv.w / 2, cv.h * 0.6), (20, 70), (0.4, 0.8), angle=(-math.pi * 0.85, -math.pi * 0.15), size=(1, 2.4), gravity=-30, spawn=(0, 0.6), jitter=s * 0.3)
    sparks(cv, ps, t, C('#ffd27a')[:3], C('#ff6a00', 0.7), streak=0.08)


skill('SwordSpin', dict(cast_ground=False), cast=part(sword_spin, 12, 256, fps=24))
skill('SwordCross', dict(cast_dir=True, cast_scale=1.15), cast=part(sword_cross, 12, 256, fps=24))
skill('SwordBash', dict(trail_width=1.4, impact_size=1.1), trail=part(bash_trail, 8, 320, 128, fps=20), impact=part(bash_impact, 12, 192, fps=20))
skill('SwordGuard', dict(), cast=part(guard_cast, 14, 256, fps=20))
skill('SwordCharge', dict(trail_width=1.5, impact_size=1.2, impact_dir=True), trail=part(charge_trail, 8, 320, 128, fps=22), impact=part(charge_impact, 10, 192, fps=24))
skill('SwordRoar', dict(cast_scale=1.6), cast=part(roar_cast, 14, 224, fps=20))


# ============================================================ SPEAR
def pierce_trail(cv, t, rng):
    yy, xx = np.mgrid[0:cv.h, 0:cv.w].astype(np.float32)
    X = (xx + 0.5) / cv.w * 2 - 1
    Y = (yy + 0.5) / cv.h * 2 - 1
    a = fade_tail(t, 0.45)
    reach = ease_out(min(1, t * 3), 3)
    tip = -1 + 2 * reach
    lance = np.exp(-(Y / (0.12 * np.clip((tip - X) / 0.5, 0, 1) + 0.02)) ** 2) * (X < tip) * smooth(-1.1, -0.2, X)
    cv.glow(lance, C('#ffd23a', 0.9), 7, a)
    cv.ramp(np.clip(lance, 0, 1), GOLD, cover=a)
    for k in range(2):
        ph = X * 9 - t * 30 + k * math.pi
        y0 = 0.38 * np.sin(ph) * smooth(-1, 0, X)
        strand = np.exp(-((Y - y0) / 0.04) ** 2) * (X < tip) * (np.cos(ph) > -0.2 if k == 0 else np.cos(ph) < 0.2)
        cv.ramp(np.clip(strand * 0.9, 0, 1), WHEAT, cover=a)
    if reach > 0.2:
        st = poly((cv.h, cv.w), star_poly((tip + 1) / 2 * cv.w, cv.h / 2, cv.h * 0.45, cv.h * 0.06, 4, 0))
        cv.add(C('#fffbe0')[:3], st * a)


def pierce_impact(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.45)
    lens = np.exp(-(Y / (0.06 * (1 - t) + 0.01)) ** 2) * np.clip(1 - np.abs(X) / (0.4 + 0.6 * ease_out(t)), 0, 1)
    lens2 = np.exp(-(X / (0.04 * (1 - t) + 0.008)) ** 2) * np.clip(1 - np.abs(Y) / (0.2 + 0.3 * ease_out(t)), 0, 1)
    v = np.maximum(lens, lens2 * 0.8)
    cv.glow(v, C('#ffc02a', 1), 8, 1.3 * a)
    cv.ramp(np.clip(v * 1.2, 0, 1), GOLD, cover=a)
    ps = Particles(np.random.default_rng(14), 20, (cv.w / 2, cv.h / 2), (90, 200), (0.2, 0.4), angle=(-0.5, 0.5), size=(1, 2.4), spawn=(0, 0.15))
    sparks(cv, ps, t, C('#fff6c8')[:3], C('#ffb020', 0.7))
    ps2 = Particles(np.random.default_rng(15), 12, (cv.w / 2, cv.h / 2), (40, 90), (0.3, 0.5), size=(1.4, 2.6), gravity=160, spawn=(0, 0.2))
    dots(cv, ps2, t, [(0, C('#40000a', 0)), (0.5, C('#9a0016')), (1, C('#ff4a5a'))], mode='over')


def spear_body(cv, cx, cy, L, ang, shaft=('#7a4a24', '#c88a50', '#3a200c'), tip=('#c8d6e8', '#ffffff', '#5a6a80')):
    sh = tr(rot([(-L * 0.5, -3), (L * 0.22, -3), (L * 0.22, 3), (-L * 0.5, 3)], ang), cx, cy)
    shaded_poly(cv, sh, C(shaft[0]), C(shaft[1]), C(shaft[2]), '#1a0e06', 1.2, gloss=False)
    tp = tr(rot([(L * 0.18, -9), (L * 0.5, 0), (L * 0.18, 9), (L * 0.24, 0)], ang), cx, cy)
    shaded_poly(cv, tp, C(tip[0]), C(tip[1]), C(tip[2]), '#101828', 1.2)


def bolt_spear_proj(cv, t, rng):
    cx, cy = cv.w / 2, cv.h / 2
    L = cv.w * 0.8
    r2 = np.random.default_rng(int(t * 97) + 3)
    X, Y, R, A, s = P(cv)
    halo = np.exp(-((Y * cv.h / cv.w) / 0.18) ** 2) * np.clip(1 - np.abs(X) / 1.0, 0, 1)
    cv.ramp(np.clip(halo * 0.6, 0, 1), ELEC, cover=0.8)
    spear_body(cv, cx, cy, L, 0, tip=('#9fe8ff', '#ffffff', '#3a7ab0'))
    for k in range(2):
        x0 = cx - L * 0.45 + r2.uniform(0, L * 0.3)
        x1 = cx + L * 0.1 + r2.uniform(0, L * 0.35)
        draw_bolt(cv, r2, (x0, cy + r2.uniform(-8, 8)), (x1, cy + r2.uniform(-8, 8)), C('#ffffff')[:3], C('#36c0ff', 0.9), 1.1, 5, 1, 0.5)


def lightning_strike(cv, t, rng):
    X, Y, R, A, s = P(cv)
    cx, cy = cv.w / 2, cv.h / 2
    a = fade_tail(t, 0.5)
    f = pulse(t, 0.0, 0.55)
    r2 = np.random.default_rng(int(t * 50) + 1)
    if t < 0.55:
        draw_bolt(cv, r2, (cx + r2.uniform(-10, 10), 0), (cx, cy), C('#ffffff')[:3], C('#3ac8ff', 1), 3.2 * (1 - t), 6, 3, 0.22)
    ring(cv, R, 0.15 + 0.75 * ease_out(t), 0.05 * (1 - t) + 0.01, ELEC, alpha=a)
    core = np.exp(-(R / (0.12 + 0.3 * f)) ** 2) * f
    cv.glow(core, C('#3ac8ff', 1), 10, 1.0)
    cv.add(C('#eaffff')[:3], core)
    for k in range(5):
        ang = r2.uniform(0, TAU)
        rr = s * (0.3 + 0.55 * ease_out(t))
        if t > 0.1:
            draw_bolt(cv, r2, (cx, cy), (cx + math.cos(ang) * rr, cy + math.sin(ang) * rr * 0.7), C('#e8fbff')[:3], C('#2a9cff', 0.8), 1.2 * a, 4, 0, 0.4)


def elec_muzzle(cv, t, rng):
    X, Y, R, A, s = P(cv)
    f = 1 - t
    r2 = np.random.default_rng(int(t * 40) + 5)
    cv.glow(np.exp(-(R / 0.25) ** 2) * f, C('#3ac8ff', 1), 6, 1)
    for k in range(4):
        ang = r2.uniform(-1.0, 1.0)
        draw_bolt(cv, r2, (cv.w / 2, cv.h / 2), (cv.w / 2 + math.cos(ang) * s * 0.8 * (0.5 + t), cv.h / 2 + math.sin(ang) * s * 0.8), C('#ffffff')[:3], C('#3ac8ff', 0.8), 1.0 * f, 4, 0, 0.45)


def spear_sweep(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.6)
    head = math.radians(-120) + math.radians(240) * ease_out(min(1, t * 1.6), 2.2)
    sweep = math.radians(40) + math.radians(170) * ease_out(min(1, t * 1.4))
    slash_arc(cv, X, Y, 0.72, head, sweep, 0.34, WHEAT, sign=1, streak_seed=61, glow=C('#ffd85a', 0.5), alpha=a)
    slash_arc(cv, X, Y, 0.48, head - 0.15, sweep * 0.8, 0.12, GOLD, sign=1, streak_seed=62, alpha=a * 0.8)
    # wind ribbons + leaves following the head
    for k in range(3):
        ang = head - k * 0.45 - 0.1
        rr = s * (0.55 + 0.3 * ((k * 37) % 7) / 7)
        if t < 0.95:
            leaf(cv, cv.w / 2 + math.cos(ang) * rr, cv.h / 2 + math.sin(ang) * rr, s * 0.1, ang + math.pi / 2 + t * 8, C('#a8c84a'), C('#e8ff9a'), C('#4a6a12'))
    ps = Particles(np.random.default_rng(66), 16, (cv.w / 2, cv.h / 2), (40, 90), (0.3, 0.6), size=(4, 8), spawn=(0.2, 0.6), jitter=s * 0.5)
    pos, age, alive, _ = ps.state(t)
    idx = np.nonzero(alive)[0]
    smoke(cv, rng, [pos[i] for i in idx], [ps.size[i] * (0.6 + age[i]) for i in idx], t, 7, *DUST, alpha=0.5 * a)


def gale_cast(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.6)
    # tornado funnel: elliptical bands stacked upward
    v = np.zeros_like(X)
    for k in range(6):
        h = 0.55 - k * 0.2
        w = 0.25 + k * 0.08
        ph = t * 18 + k * 1.1
        e = ((X / w) ** 2 + ((Y - h) / (w * 0.32)) ** 2)
        ang = np.arctan2((Y - h) / 0.32, X)
        stripe = np.clip(np.cos(ang * 2 - ph) * 0.5 + 0.5, 0, 1) ** 2
        v = np.maximum(v, band(np.sqrt(e), 1.0, 0.18) * stripe * (0.6 + 0.4 * (k / 5)))
    v *= ease_out(min(1, t * 2.5))
    cv.glow(v, C('#3affc8', 0.6), 5, a)
    cv.ramp(np.clip(v, 0, 1), TEAL, cover=a)
    for k in range(7):
        ang = t * TAU * 2 + k * TAU / 7
        hh = 0.5 - ((k * 0.31 + t * 0.8) % 1.0) * 1.3
        w = 0.3 + (0.5 - hh) * 0.3
        x = cv.w / 2 + math.cos(ang) * w * s
        y = cv.h / 2 + hh * s + math.sin(ang) * w * s * 0.3
        if math.sin(ang) > -0.3:
            leaf(cv, x, y, s * 0.11, ang * 2, C('#6ad06a'), C('#d8ffb0'), C('#1a6a2a'))
    if t > 0.6:
        cv.c *= a


def leap_trail(cv, t, rng):
    yy, xx = np.mgrid[0:cv.h, 0:cv.w].astype(np.float32)
    a = fade_tail(t, 0.4)
    centers, radii = [], []
    for i in range(14):
        u = i / 13
        if u > ease_out(min(1, t * 2.5)):
            continue
        x = cv.w * (0.05 + 0.9 * u)
        y = cv.h * (0.75 - 0.5 * math.sin(math.pi * u))
        centers.append((x, y)); radii.append(cv.h * (0.08 + 0.06 * math.sin(math.pi * u)) * (1 + t))
    smoke(cv, rng, centers, radii, t, 71, C('#8a5a2a'), C('#ffd8a0'), alpha=0.75 * a, erode=t * 0.6)
    X = xx / cv.w * 2 - 1; Y = yy / cv.h * 2 - 1
    arc = np.exp(-((Y - (0.5 - 1.0 * np.sin(math.pi * (X + 1) / 2))) / 0.05) ** 2) * (X < -1 + 2 * ease_out(min(1, t * 2.5)))
    cv.ramp(np.clip(arc, 0, 1), EMBER, cover=0.8 * a)


def crack_field(cv, rng, cx, cy, n, length, prog, glow_stops, seed):
    r2 = np.random.default_rng(seed)
    shape = (cv.h, cv.w)
    m = np.zeros(shape, np.float32)
    for k in range(n):
        ang = k * TAU / n + r2.uniform(-0.3, 0.3)
        pts = [(cx, cy)]
        x, y = cx, cy
        segs = 6
        for j in range(segs):
            ang += r2.uniform(-0.5, 0.5)
            step = length * r2.uniform(0.7, 1.2) / segs
            x += math.cos(ang) * step; y += math.sin(ang) * step * 0.75
            pts.append((x, y))
        cut = max(2, int(len(pts) * prog))
        p = pts[:cut]
        ws = [2.6 * (1 - j / len(p)) + 0.6 for j in range(len(p))]
        m = np.maximum(m, stroke(shape, p, ws))
    return m


def leap_impact(cv, t, rng):
    X, Y, R, A, s = P(cv)
    cx, cy = cv.w / 2, cv.h / 2
    a = fade_tail(t, 0.6)
    cr = crack_field(cv, rng, cx, cy, 7, s * 0.9, ease_out(min(1, t * 3)), EMBER, 72)
    cv.over(C('#1e0e04')[:3], blur(cr, 0.6) * a)
    cv.glow(cr, C('#ff7a1a', 0.9), 3, 0.9 * a * (1 - t * 0.6))
    cv.add(C('#ffd080')[:3], cr * 0.5 * a * (1 - t))
    ring(cv, R / np.array(1.0), 0.2 + 0.7 * ease_out(t), 0.07 * (1 - t) + 0.01, [(0, C('#5a3a1a', 0)), (0.5, C('#c08a50', 0.8)), (1, C('#ffe8c0'))], alpha=a)
    f = pulse(t, 0, 0.35)
    if f > 0:
        st = poly((cv.h, cv.w), star_poly(cx, cy, s * 0.5 * f, s * 0.08, 6))
        cv.glow(st, C('#ffb03a', 1), 8, 1.4)
        cv.add(C('#fff2c8')[:3], st)
    ps = Particles(np.random.default_rng(73), 12, (cx, cy), (60, 140), (0.4, 0.7), size=(3, 6), gravity=260, spawn=(0, 0.1), angle=(-math.pi, 0))
    pos, age, alive, _ = ps.state(t)
    for i in np.nonzero(alive)[0]:
        rock(cv, np.random.default_rng(i), pos[i][0], pos[i][1], ps.size[i], t * 6 + i, C('#8a6a4a'), C('#d8b890'), C('#3a2614'))


def storm_zone(cv, t, rng):
    X, Y, R, A, s = P(cv)
    n = N(81, 6, 4)
    swirl = n.sample(R * 1.2 - t, (A / TAU) + t * 0.5 + R * 0.3)
    edge = smooth(0.55, 0.95, R) * (1 - smooth(0.95, 1.0, R))
    cloud = np.clip(edge * (0.4 + swirl * 0.9), 0, 1)
    cv.ramp(cloud, [(0, C('#0a1030', 0)), (0.3, C('#1a2458', 0.75)), (0.7, C('#3a4a9a', 0.9)), (1, C('#8ab0ff'))])
    field = (1 - smooth(0.0, 0.95, R)) * (0.25 + 0.2 * np.sin(t * TAU * 2 + R * 8))
    cv.ramp(np.clip(field, 0, 1), ELEC, cover=0.25, mode='add')
    rune_circle(cv, X, Y, R, A, 0.9, t * TAU, C('#9ae4ff', 0.7), C('#2aa0ff', 0.5), seed=4, spokes=6, alpha=0.6, star=False)
    r2 = np.random.default_rng(int(t * 12 * 7) % 977 + 11)
    for k in range(2):
        ang = r2.uniform(0, TAU); rr = r2.uniform(0, 0.7)
        x = cv.w / 2 + math.cos(ang) * rr * s; y = cv.h / 2 + math.sin(ang) * rr * s * 0.7
        draw_bolt(cv, r2, (x + r2.uniform(-10, 10), y - s * 0.9), (x, y), C('#ffffff')[:3], C('#40c0ff', 0.9), 2.0, 5, 1, 0.3)
        hit = soft_dot((cv.h, cv.w), x, y, s * 0.12)
        cv.add(C('#bfefff')[:3], hit)


skill('SpearPierce', dict(trail_width=1.4, impact_size=0.9, impact_dir=True), trail=part(pierce_trail, 8, 320, 128, fps=22), impact=part(pierce_impact, 10, 192, fps=24))
skill('SpearThrow', dict(proj_size=1.3, proj_spin=0, impact_scale=1.4, muzzle_size=0.7),
      projectile=part(bolt_spear_proj, 6, 160, 64, fps=20, loop=True), impact=part(lightning_strike, 12, 192, fps=22), muzzle=part(elec_muzzle, 6, 128, fps=24))
skill('SpearSweep', dict(cast_dir=True, cast_scale=1.1), cast=part(spear_sweep, 12, 256, fps=22))
skill('SpearGale', dict(cast_scale=1.5), cast=part(gale_cast, 14, 224, fps=20))
skill('SpearLeap', dict(trail_width=1.6, impact_ground=True, impact_scale=1.3), trail=part(leap_trail, 8, 320, 128, fps=20), impact=part(leap_impact, 12, 192, fps=20))
skill('SpearStorm', dict(cast_ground=True, cast_scale=1.1), cast=part(storm_zone, 12, 256, fps=14, loop=True))
