import math
import cv2
import numpy as np
from lib import *
from rec import *
from core import part, skill
from job_bow_staff import hgrid, FIRE

BLOOD = [(0, C('#120002', 0)), (0.25, C('#4a0008', 0.85)), (0.55, C('#a8001a')), (0.85, C('#ff3a4a')), (1, C('#ffd0d0'))]
DARKFLAME = [(0, C('#100000', 0)), (0.25, C('#3a0006', 0.75)), (0.5, C('#9a0a10')), (0.78, C('#ff3020')), (1, C('#ffc0a0'))]
STEELG = [(0, C('#20242c', 0)), (0.3, C('#4a525e', 0.6)), (0.6, C('#9aa6b4', 0.9)), (0.9, C('#eef4fa')), (1, C('#ffffff'))]
LAVA = [(0, C('#200400', 0)), (0.3, C('#8a1a00', 0.9)), (0.6, C('#ff6a00')), (0.9, C('#ffe060')), (1, C('#ffffff'))]
EARTH = (C('#4a3420'), C('#c8a478'))
QUAKE = [(0, C('#2a1a0a', 0)), (0.4, C('#7a5a34', 0.55)), (0.8, C('#c8a070', 0.85)), (1, C('#f0dcb8'))]
SUN = [(0, C('#3a2400', 0)), (0.2, C('#a06a00', 0.8)), (0.5, C('#ffc21a')), (0.8, C('#fff0a0')), (1, C('#ffffff'))]
VINE = [(0, C('#0a2a0a', 0)), (0.3, C('#2a7a1a', 0.7)), (0.6, C('#8ad84a')), (0.9, C('#e8ffb0')), (1, C('#ffffff'))]


# ============================================================ AXE
def rage_cast(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.6)
    grow = ease_out(min(1, t * 2.5))
    shape = np.exp(-(X / (0.32 + 0.2 * np.clip(Y + 0.3, 0, 1))) ** 2) * smooth(-0.95, -0.2, Y) * (1 - smooth(0.45, 0.65, Y)) * grow
    # dark smoke behind
    n = N(301, 6, 4).sample(X * 1.0, Y * 0.8 + t * 1.2)
    sm = np.clip(shape * (n * 1.4 - 0.4) * 1.2, 0, 1)
    cv.over(C('#140608')[:3], sm * 0.7 * a)
    flames(cv, X * 1.1, Y, shape, t, 302, DARKFLAME, rise=2.6, scale=3.0, gain=1.7, alpha=a)
    # pulsing vein ring
    pulse_r = 0.45 + 0.1 * math.sin(t * TAU * 3)
    n2 = N(303, 10, 3).sample(A / TAU * 4, R * 2)
    veins = band(R, pulse_r + (n2 - 0.5) * 0.25, 0.02) * (n2 > 0.45)
    cv.glow(veins, C('#ff1020', 0.9), 3, a)
    cv.ramp(np.clip(veins, 0, 1), BLOOD, cover=a)
    ps = Particles(np.random.default_rng(304), 18, (cv.w / 2, cv.h * 0.7), (20, 60), (0.4, 0.7), angle=(-math.pi * 0.8, -math.pi * 0.2), size=(1.5, 3), spawn=(0, 0.6), jitter=s * 0.3)
    dots(cv, ps, t, [(0, C('#300004', 0)), (0.5, C('#a00010')), (1, C('#ff5060'))], mode='over', shrink=0.2)


def whirl_axe(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.6)
    spin = t * TAU * 2.2
    # heavy axe blades smeared around (three sweeping steel arcs) with blood glints
    for k in range(3):
        head = spin + k * TAU / 3
        slash_arc(cv, X, Y, 0.66, head, 1.25, 0.22, STEELG, sign=1, streak_seed=313 + k, glow=C('#c8d4e8', 0.4), alpha=a)
        slash_arc(cv, X, Y, 0.72, head, 0.9, 0.06, BLOOD, sign=1, streak_seed=316 + k, alpha=a)
    # dust tornado skirt
    n = N(311, 6, 4).sample(A / TAU * 2 - t * 2, R * 1.5)
    dust = band(R, 0.85, 0.12) * (n * 1.3 - 0.2)
    cv.ramp(np.clip(dust, 0, 1), QUAKE, cover=a * 0.9)
    ps = Particles(np.random.default_rng(312), 22, (cv.w / 2, cv.h / 2), (90, 170), (0.3, 0.55), size=(1.5, 3), gravity=120, spawn=(0.1, 0.6), jitter=s * 0.5)
    dots(cv, ps, t, [(0, C('#300004', 0)), (0.5, C('#9a0012')), (1, C('#ff4050'))], mode='over', shrink=0.3)
    if t > 0.6:
        cv.c *= a


def axe_shape(cv, cx, cy, sz, ang):
    handle = tr(rot([(-sz * 0.55, -2.6), (sz * 0.45, -2.6), (sz * 0.45, 2.6), (-sz * 0.55, 2.6)], ang), cx, cy)
    shaded_poly(cv, handle, C('#8a5228'), C('#d8965a'), C('#3a1e0a'), '#1a0c04', 1.3, gloss=False)
    unit = [(0.22, 0.06), (0.46, 0.06), (0.5, -0.1), (0.62, -0.22), (0.64, -0.38), (0.55, -0.52), (0.4, -0.54), (0.3, -0.42), (0.27, -0.22), (0.22, -0.08)]
    head = tr(rot([(x * sz, y * sz) for x, y in unit], ang), cx, cy)
    shaded_poly(cv, head, C('#a8b4c4'), C('#ffffff'), C('#44505e'), '#10141c', 1.4)


def axe_proj(cv, t, rng):
    X, Y, R, A, s = P(cv)
    ang = -t * TAU
    arcs = band(R, 0.78, 0.07) * np.clip(np.cos(A - ang - 0.9) * 1.2, 0, 1)
    cv.ramp(np.clip(arcs * 0.8, 0, 1), [(0, C('#3a2a10', 0)), (0.5, C('#ffb040', 0.6)), (1, C('#fff2c0'))])
    axe_shape(cv, cv.w / 2, cv.h / 2, s * 1.35, ang)


def chop_impact(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.5)
    reveal = ease_out(min(1, t * 4), 3)
    v = np.zeros_like(X)
    for ang in (math.radians(-55), math.radians(-125)):
        ca, sa = math.cos(ang), math.sin(ang)
        u = X * ca + Y * sa; d = -X * sa + Y * ca
        v = np.maximum(v, np.exp(-(d / (0.07 * np.clip(1 - (u / 0.8) ** 2, 0, 1) + 1e-3)) ** 2) * (u < -0.8 + 1.6 * reveal))
    cv.glow(v, C('#ff7a1a', 0.9), 6, a * 1.2)
    cv.ramp(np.clip(v, 0, 1), [(0, C('#200800', 0)), (0.3, C('#7a2a00', 0.9)), (0.7, C('#ffa040')), (1, C('#fff4d0'))], cover=a)
    ps = Particles(np.random.default_rng(321), 9, (cv.w / 2, cv.h / 2), (60, 140), (0.4, 0.7), size=(2.5, 4.5), gravity=280, spawn=(0, 0.1), angle=(-math.pi, 0))
    pos, age, alive, _ = ps.state(t)
    for i in np.nonzero(alive)[0]:
        pts = tr(rot([(-ps.size[i], -1.2), (ps.size[i], -1.2), (ps.size[i], 1.2), (-ps.size[i], 1.2)], t * 9 + i), pos[i][0], pos[i][1])
        shaded_poly(cv, pts, C('#b07a40'), C('#e8c08a'), C('#5a3410'), '#2a1606', 0.8, gloss=False)
    ps2 = Particles(np.random.default_rng(322), 16, (cv.w / 2, cv.h / 2), (100, 200), (0.2, 0.4), size=(1, 2), spawn=(0, 0.1))
    sparks(cv, ps2, t, C('#fff0b0')[:3], C('#ff8a20', 0.7), streak=0.05)


def execute_trail(cv, t, rng):
    X, Y = hgrid(cv)
    a = fade_tail(t, 0.35)
    body = np.exp(-(Y / (0.12 + 0.3 * (X + 1) / 2)) ** 2) * smooth(-1, 0.6, X) * (1 - smooth(0.85, 1, X))
    n = N(331, 8, 3).sample(X * 2 - t * 3, Y)
    cv.ramp(np.clip(body * (0.4 + n * 0.8), 0, 1), DARKFLAME, cover=a)
    streaks(cv, X, Y, t, 332, BLOOD, density=8, speed=1.6, length=0.7, thick=0.04, yspan=0.5, alpha=a)


def execute_impact(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.55)
    # giant falling crescent (guillotine) from above
    drop = ease_in(min(1, t * 3.2), 2.5)
    head = math.radians(-160) + math.radians(150) * drop
    slash_arc(cv, X, Y - 0.2, 0.62, head, math.radians(40 + 110 * drop), 0.3, BLOOD, sign=1, streak_seed=341, glow=C('#ff0a1a', 0.8), alpha=a)
    slash_arc(cv, X, Y - 0.2, 0.62, head, math.radians(30 + 90 * drop), 0.09, DARKFLAME, sign=1, streak_seed=342, alpha=a)
    if t > 0.3:
        cr = crack(cv, (t - 0.3) / 0.4)
        cv.over(C('#140204')[:3], cr * a)
        cv.glow(cr, C('#ff1a2a', 0.8), 3, a)
        ps = Particles(np.random.default_rng(343), 26, (cv.w * 0.55, cv.h * 0.85), (70, 170), (0.35, 0.6), angle=(-math.pi * 0.95, -math.pi * 0.05), size=(1.5, 3.4), gravity=300, spawn=(0.3, 0.4))
        dots(cv, ps, t, [(0, C('#300004', 0)), (0.5, C('#a00014')), (1, C('#ff4a5a'))], mode='over', shrink=0.25)


def crack(cv, prog):
    shape = (cv.h, cv.w)
    r2 = np.random.default_rng(345)
    m = np.zeros(shape, np.float32)
    cx, cy = cv.w * 0.55, cv.h * 0.85
    for k in range(5):
        ang = math.pi + k * math.pi / 4 + r2.uniform(-0.2, 0.2)
        x, y = cx, cy
        pts = [(x, y)]
        for j in range(5):
            ang += r2.uniform(-0.4, 0.4)
            x += math.cos(ang) * cv.w * 0.07; y += math.sin(ang) * cv.w * 0.03
            pts.append((x, y))
        c = max(2, int(len(pts) * min(1, prog)))
        m = np.maximum(m, stroke(shape, pts[:c], [2.4 - j * 0.35 for j in range(c)]))
    return m


def split_cast(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.65)
    reach = ease_out(min(1, t * 2.2), 3)
    shape = (cv.h, cv.w)
    m = np.zeros(shape, np.float32)
    r2 = np.random.default_rng(351)
    for ang0 in (math.radians(-15), math.radians(165), math.radians(80), math.radians(-100)):
        x, y = cv.w / 2, cv.h / 2
        ang = ang0
        pts = [(x, y)]
        L = 0.95 if ang0 in (math.radians(-15), math.radians(165)) else 0.55
        for j in range(8):
            ang += r2.uniform(-0.35, 0.35)
            step = s * L / 8
            x += math.cos(ang) * step; y += math.sin(ang) * step * 0.8
            pts.append((x, y))
        c = max(2, int(len(pts) * reach))
        m = np.maximum(m, stroke(shape, pts[:c], [5 * (1 - j / 9) + 1 for j in range(c)]))
    cv.over(C('#1a0a04')[:3], blur(cv2.dilate(m, np.ones((3, 3), np.uint8)), 1) * a)
    cv.glow(m, C('#ff5a00', 1), 5, a * 1.2)
    cv.ramp(np.clip(m * (1.1 - t * 0.5), 0, 1), LAVA, cover=a)
    ring(cv, R, 0.2 + 0.75 * reach, 0.05 * (1 - t) + 0.01, QUAKE, alpha=a * 0.8)
    ps = Particles(np.random.default_rng(352), 16, (cv.w / 2, cv.h / 2), (50, 120), (0.4, 0.7), angle=(-math.pi, 0), size=(3, 6.5), gravity=260, spawn=(0, 0.3), jitter=s * 0.3)
    pos, age, alive, _ = ps.state(t)
    for i in np.nonzero(alive)[0]:
        rock(cv, np.random.default_rng(i), pos[i][0], pos[i][1], ps.size[i], t * 5 + i, C('#6a4a34'), C('#c8a07a'), C('#2a1a0e'))


def endure_cast(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.65)
    rune_circle(cv, X, (Y - 0.55) / 0.35, np.hypot(X, (Y - 0.55) / 0.35), np.arctan2((Y - 0.55) / 0.35, X), 0.6, t * 2, C('#ffd08a', a), C('#ff7a00', a), seed=9, spokes=8, alpha=a)
    close = ease_in(min(1, t * 1.6), 2)
    r2 = np.random.default_rng(361)
    plates = []
    for k in range(7):
        ang = k * TAU / 7 + t * 2.0
        rr = 0.85 - 0.5 * close
        plates.append((math.sin(ang), ang, rr, k))
    plates.sort()
    for _, ang, rr, k in plates:
        x = cv.w / 2 + math.cos(ang) * rr * s
        y = cv.h / 2 + math.sin(ang) * rr * s * 0.45 - s * 0.05
        sz = s * (0.17 + 0.03 * (k % 3))
        rock(cv, np.random.default_rng(k), x, y, sz, ang, C('#8a7a6a'), C('#e0d4c4'), C('#3a2e24'))
    sh = pulse(t, 0.55, 1.0)
    if sh > 0:
        cv.glow(np.exp(-((X / 0.4) ** 2 + (Y / 0.55) ** 2)) * sh, C('#ff8a20', 1), 6, 0.8)
    if t > 0.65:
        cv.c *= a


skill('AxeRage', dict(cast_scale=1.5), cast=part(rage_cast, 14, 224, fps=20))
skill('AxeWhirl', dict(cast_scale=1.05), cast=part(whirl_axe, 12, 256, fps=22))
skill('AxeThrow', dict(proj_size=0.85, proj_spin=0, impact_size=0.75), projectile=part(axe_proj, 8, 96, fps=24, loop=True), impact=part(chop_impact, 10, 160, fps=22))
skill('AxeExecute', dict(trail_width=1.3, impact_size=1.4), trail=part(execute_trail, 8, 320, 128, fps=22), impact=part(execute_impact, 12, 224, fps=22))
skill('AxeSplit', dict(cast_ground=True, cast_scale=1.15), cast=part(split_cast, 14, 256, fps=20))
skill('AxeEndure', dict(cast_scale=1.4), cast=part(endure_cast, 14, 224, fps=20))


# ============================================================ BLUNT
def slam_cast(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.6)
    Rg = np.hypot(X, Y / 0.75)
    crater = (1 - smooth(0.15, 0.35, Rg)) * min(1, t * 4)
    cv.over(C('#2a1a0e')[:3], crater * 0.75 * a)
    cv.over(C('#6a4a2a')[:3], band(Rg, 0.34, 0.04) * min(1, t * 4) * a)
    for k in range(2):
        lt = t * 1.4 - k * 0.2
        if 0 < lt < 1:
            n = N(401 + k, 8, 3).sample(A / TAU * 4, lt)
            ring(cv, Rg, 0.25 + 0.7 * ease_out(lt), 0.09 * (1 - lt) + 0.01, QUAKE, noise=n, amount=0.6, alpha=(1 - lt))
    f = pulse(t, 0, 0.3)
    if f > 0:
        cv.glow(np.exp(-(Rg / 0.3) ** 2) * f, C('#fff0c0', 1), 8, 1.5)
    ps = Particles(np.random.default_rng(402), 14, (cv.w / 2, cv.h / 2), (70, 150), (0.4, 0.7), angle=(-math.pi, 0), size=(3, 6), gravity=300, spawn=(0, 0.08))
    pos, age, alive, _ = ps.state(t)
    for i in np.nonzero(alive)[0]:
        rock(cv, np.random.default_rng(i + 40), pos[i][0], pos[i][1], ps.size[i], t * 6 + i, C('#7a5a3a'), C('#d8b88a'), C('#2e1e10'))
    for k in range(3):
        ang = t * TAU + k * TAU / 3
        x = cv.w / 2 + math.cos(ang) * s * 0.3
        y = cv.h / 2 - s * 0.35 + math.sin(ang) * s * 0.08
        if t > 0.35:
            st = poly((cv.h, cv.w), star_poly(x, y, s * 0.07, s * 0.03, 5))
            cv.outline(st, C('#3a2a00'), 1.2)
            cv.over(C('#ffe14a')[:3], st * a)


def quake_zone(cv, t, rng):
    X, Y, R, A, s = P(cv)
    base = 1 - smooth(0.7, 1.0, R)
    waves = 0.5 + 0.5 * np.sin(R * 22 - t * TAU * 2)
    n = N(411, 6, 4).sample(X * 0.8, Y * 0.8)
    v = np.clip(base * (0.3 + 0.35 * waves * (0.6 + n * 0.6)), 0, 1) * 0.55
    cv.ramp(v, QUAKE)
    shape = (cv.h, cv.w)
    flick = 0.6 + 0.4 * math.sin(t * TAU * 4)
    m = np.zeros(shape, np.float32)
    r2 = np.random.default_rng(412)
    for k in range(6):
        ang = k * TAU / 6 + r2.uniform(-0.3, 0.3)
        x, y = cv.w / 2 + math.cos(ang) * s * 0.15, cv.h / 2 + math.sin(ang) * s * 0.15
        pts = [(x, y)]
        for j in range(5):
            ang += r2.uniform(-0.5, 0.5)
            x += math.cos(ang) * s * 0.13; y += math.sin(ang) * s * 0.13
            pts.append((x, y))
        m = np.maximum(m, stroke(shape, pts, [2.2 - j * 0.3 for j in range(6)]))
    cv.over(C('#24160a')[:3], m * 0.9)
    cv.add(C('#ffb060')[:3], m * 0.25 * flick, 0)
    for k in range(10):
        ph = (k * 0.137 + t * 2) % 1.0
        ang = k * 2.4
        rr = 0.2 + 0.06 * k
        x = cv.w / 2 + math.cos(ang) * rr * s; y = cv.h / 2 + math.sin(ang) * rr * s - math.sin(math.pi * ph) * s * 0.12
        rock(cv, np.random.default_rng(k), x, y, s * 0.035, ph * 4, C('#8a6a4a'), C('#e0c09a'), C('#3a2614'))


def comet_trail(cv, t, rng):
    X, Y = hgrid(cv)
    a = fade_tail(t, 0.4)
    reach = ease_out(min(1, t * 2.5))
    arcy = 0.5 - 1.05 * np.sin(math.pi * (X + 1) / 2)
    tail = np.exp(-((Y - arcy) / 0.09) ** 2) * (X < -1 + 2 * reach) * smooth(-1, -1 + 2 * reach, X)
    cv.glow(tail, C('#ffc21a', 0.9), 6, a)
    cv.ramp(np.clip(tail, 0, 1), SUN, cover=a)
    r2 = np.random.default_rng(421)
    for k in range(12):
        u = r2.uniform(0, reach)
        x = -1 + 2 * u
        y = 0.5 - 1.05 * math.sin(math.pi * u) + r2.normal(0, 0.12)
        tw = abs(math.sin(t * 8 + k))
        st = poly((cv.h, cv.w), star_poly((x + 1) / 2 * cv.w, (y + 1) / 2 * cv.h, cv.h * 0.07 * tw, cv.h * 0.012, 4, 0))
        cv.add(C('#fff7c8')[:3], st * a)


def meteor_impact(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.6)
    Rg = np.hypot(X, Y / 0.8)
    f = pulse(t, 0, 0.35)
    rays(cv, Rg, A, 10, 0.3, 0.2, 0.05, 0.95 * ease_out(min(1, t * 2.5)), SUN, alpha=a)
    cv.glow(np.exp(-(Rg / (0.15 + 0.3 * f)) ** 2) * f, C('#ffd040', 1), 10, 1.6)
    cv.add(C('#ffffff')[:3], np.exp(-(Rg / (0.1 + 0.2 * f)) ** 2) * f)
    ring(cv, Rg, 0.2 + 0.78 * ease_out(t), 0.06 * (1 - t) + 0.01, SUN, alpha=a)
    cs, rs = [], []
    for k in range(12):
        ang = k * TAU / 12
        d = s * (0.3 + 0.55 * ease_out(t))
        cs.append((cv.w / 2 + math.cos(ang) * d, cv.h / 2 + math.sin(ang) * d * 0.8)); rs.append(s * 0.12 * (0.5 + t))
    if t > 0.1:
        smoke(cv, rng, cs, rs, t, 431, *EARTH, alpha=0.75 * a, erode=t)
    ps = Particles(np.random.default_rng(432), 10, (cv.w / 2, cv.h / 2), (90, 170), (0.4, 0.7), angle=(-math.pi, 0), size=(3, 6), gravity=300, spawn=(0, 0.1))
    pos, age, alive, _ = ps.state(t)
    for i in np.nonzero(alive)[0]:
        rock(cv, np.random.default_rng(i + 9), pos[i][0], pos[i][1], ps.size[i], t * 6 + i, C('#7a5a3a'), C('#ffd890'), C('#2e1e10'))


def ram_trail(cv, t, rng):
    X, Y = hgrid(cv)
    a = fade_tail(t, 0.4)
    chevrons(cv, X, Y, t * 1.5, 3, None, SUN, w=0.07)
    r2 = np.random.default_rng(441)
    cs, rs = [], []
    for k in range(10):
        u = (r2.uniform() - t * 0.8) % 1.0
        cs.append((u * cv.w, cv.h * (0.62 + r2.uniform(-0.1, 0.2)))); rs.append(cv.h * (0.12 + 0.15 * (1 - u)))
    smoke(cv, rng, cs, rs, t, 442, *EARTH, alpha=0.7 * a, erode=0.3)
    if t > 0.4:
        cv.c *= a


def shatter_impact(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.5)
    f = pulse(t, 0, 0.3)
    st = poly((cv.h, cv.w), star_poly(cv.w / 2, cv.h / 2, s * 0.6 * (0.4 + f), s * 0.12, 8))
    cv.glow(st, C('#ffd040', 1), 8, f * 1.5)
    cv.add(C('#fffbe0')[:3], st * f)
    r2 = np.random.default_rng(451)
    for k in range(11):
        ang = k * TAU / 11 + r2.uniform(-0.2, 0.2)
        d = s * (0.1 + 0.75 * ease_out(t)) * r2.uniform(0.6, 1)
        x = cv.w / 2 + math.cos(ang) * d; y = cv.h / 2 + math.sin(ang) * d + t * t * s * 0.3
        sz = s * r2.uniform(0.06, 0.12)
        pts = tr(rot([(-sz, -sz * 0.5), (sz, -sz * 0.2), (sz * 0.3, sz * 0.7)], ang + t * 7 + k), x, y)
        shaded_poly(cv, pts, C('#ffd23a'), C('#fff6c0'), C('#a06a00'), '#3a2400', 1.0)
    if t > 0.5:
        cv.c *= a


def tower_shield(cx, cy, w, h):
    return [(cx - w / 2, cy - h / 2 + w * 0.1), (cx, cy - h / 2 - w * 0.05), (cx + w / 2, cy - h / 2 + w * 0.1), (cx + w / 2, cy + h / 2 - w * 0.25), (cx, cy + h / 2), (cx - w / 2, cy + h / 2 - w * 0.25)]


def wall_cast(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.65)
    ring(cv, np.hypot(X, Y / 0.55), 0.62, 0.05, SUN, alpha=a * 0.8)
    items = []
    for k in range(6):
        ang = k * TAU / 6 + math.pi / 6
        items.append((math.sin(ang), ang, k))
    items.sort()
    for _, ang, k in items:
        lt = np.clip((t - k * 0.05) * 3.2, 0, 1)
        fall = (1 - ease_in(lt, 2)) * s * 0.8
        x = cv.w / 2 + math.cos(ang) * s * 0.62
        y = cv.h / 2 + math.sin(ang) * s * 0.62 * 0.55 - s * 0.12 - fall
        if lt <= 0:
            continue
        w, h = s * 0.26, s * 0.4
        shaded_poly(cv, tower_shield(x, y, w, h), C('#8a94a4'), C('#f0f4fa'), C('#3a4250'), '#141820', 1.6)
        rim = poly((cv.h, cv.w), tower_shield(x, y, w * 0.62, h * 0.7))
        cv.over(C('#d8a020')[:3], np.clip(poly((cv.h, cv.w), tower_shield(x, y, w * 0.78, h * 0.84)) - rim, 0, 1))
        if 0.95 < lt * 1.0 and t - k * 0.05 < 0.5:
            d = soft_dot((cv.h, cv.w), x, y + h * 0.5, w * 0.6)
            cv.add(C('#ffe08a')[:3], d * 0.6, 0.4)
    if t > 0.65:
        cv.c *= a


def vigor_cast(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.65)
    grow = ease_out(min(1, t * 1.8))
    moss = (1 - smooth(0.2, 0.95, R)) * grow
    n = N(461, 8, 4).sample(X, Y)
    cv.ramp(np.clip(moss * (0.2 + n * 0.5), 0, 1), VINE, cover=a)
    shape = (cv.h, cv.w)
    r2 = np.random.default_rng(462)
    m = np.zeros(shape, np.float32)
    for k in range(9):
        ang = k * TAU / 9 + r2.uniform(-0.2, 0.2)
        x0, y0 = cv.w / 2 + math.cos(ang) * s * 0.75, cv.h / 2 + math.sin(ang) * s * 0.75
        pts = []
        L = s * 0.5 * grow
        for j in range(20):
            u = j / 19
            curl = ang + math.pi + 2.2 * u * (1 if k % 2 else -1)
            pts.append((x0 + math.cos(ang + math.pi) * L * u + math.cos(curl) * L * 0.15 * u, y0 + math.sin(ang + math.pi) * L * u + math.sin(curl) * L * 0.15 * u))
        m = np.maximum(m, stroke(shape, pts, [3.2 * (1 - j / 20) + 0.6 for j in range(20)]))
        if grow > 0.5:
            for j in (8, 14):
                px, py = pts[j]
                leaf(cv, px, py, s * 0.1 * grow, ang + j, C('#5ac84a'), C('#d8ffa0'), C('#1a5a1a'))
    cv.outline(m, C('#0a2a08'), 1.2)
    cv.ramp(np.clip(m, 0, 1) * 0.9, VINE)
    ps = Particles(np.random.default_rng(463), 22, (cv.w / 2, cv.h / 2), (10, 35), (0.5, 0.8), angle=(-math.pi * 0.9, -math.pi * 0.1), size=(1.4, 3), spawn=(0.1, 0.6), jitter=s * 0.4)
    dots(cv, ps, t, [(0, C('#3a3a00', 0)), (0.5, C('#e8d84a', 0.9)), (1, C('#ffffff'))], mode='add', shrink=0.2)
    if t > 0.65:
        cv.c *= a


skill('BluntSlam', dict(cast_ground=True, cast_scale=1.1), cast=part(slam_cast, 12, 256, fps=20))
skill('BluntQuake', dict(cast_ground=True, cast_scale=1.05), cast=part(quake_zone, 12, 256, fps=14, loop=True))
skill('BluntLeap', dict(trail_width=1.6, impact_ground=True, impact_scale=1.15), trail=part(comet_trail, 8, 320, 128, fps=20), impact=part(meteor_impact, 13, 224, fps=20))
skill('BluntRam', dict(trail_width=1.6, impact_size=1.1), trail=part(ram_trail, 8, 320, 128, fps=20), impact=part(shatter_impact, 11, 192, fps=22))
skill('BluntWall', dict(cast_scale=1.5), cast=part(wall_cast, 14, 256, fps=20))
skill('BluntVigor', dict(cast_ground=True, cast_scale=1.4), cast=part(vigor_cast, 14, 256, fps=18))
