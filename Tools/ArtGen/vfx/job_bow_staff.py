import math
import numpy as np
from lib import *
from rec import *
from core import part, skill

FIRE = [(0, C('#200400', 0)), (0.18, C('#6a0e00', 0.8)), (0.4, C('#d23a00')), (0.65, C('#ff9a1a')), (0.85, C('#ffe27a')), (1, C('#fffbe8'))]
WINDY = [(0, C('#1a3a10', 0)), (0.25, C('#5a9a2a', 0.6)), (0.6, C('#d8f07a')), (0.9, C('#fbffe0')), (1, C('#ffffff'))]
TOXIC = [(0, C('#0e2a06', 0)), (0.25, C('#2f7a10', 0.8)), (0.55, C('#7ad81a')), (0.85, C('#d8ff8a')), (1, C('#ffffff'))]
NATURE = [(0, C('#063a1a', 0)), (0.25, C('#169a4a', 0.6)), (0.6, C('#6ef09a')), (0.9, C('#e0ffe8')), (1, C('#ffffff'))]
HAWK = [(0, C('#3a1400', 0)), (0.25, C('#b04a00', 0.8)), (0.6, C('#ff9a2a')), (0.9, C('#ffe8b0')), (1, C('#ffffff'))]
ICE = [(0, C('#08203a', 0)), (0.25, C('#2a6ab0', 0.7)), (0.55, C('#7ad4ff')), (0.85, C('#e4fbff')), (1, C('#ffffff'))]
VIOLET = [(0, C('#1a0636', 0)), (0.25, C('#5a1ab0', 0.8)), (0.55, C('#b25aff')), (0.85, C('#f0d0ff')), (1, C('#ffffff'))]
ARCANE = [(0, C('#16063a', 0)), (0.25, C('#3a1aa0', 0.7)), (0.55, C('#7a6aff')), (0.85, C('#e0dcff')), (1, C('#ffffff'))]
SMOKE_D = (C('#2a2420'), C('#7a6a5a'))


def arrow(cv, cx, cy, L, ang, fletch=('#e8d24a', '#fff7a0', '#7a6a10'), head=('#b8c4d4', '#ffffff', '#4a5464'), shaft=('#8a5a2a', '#d8a060', '#3a2410')):
    sh = tr(rot([(-L * 0.45, -1.8), (L * 0.32, -1.8), (L * 0.32, 1.8), (-L * 0.45, 1.8)], ang), cx, cy)
    shaded_poly(cv, sh, C(shaft[0]), C(shaft[1]), C(shaft[2]), '#1a0e06', 1.1, gloss=False)
    hd = tr(rot([(L * 0.3, -6), (L * 0.5, 0), (L * 0.3, 6), (L * 0.34, 0)], ang), cx, cy)
    shaded_poly(cv, hd, C(head[0]), C(head[1]), C(head[2]), '#141822', 1.1)
    for sgn in (-1, 1):
        f = tr(rot([(-L * 0.46, 0), (-L * 0.3, 0), (-L * 0.38, sgn * 8), (-L * 0.5, sgn * 8)], ang), cx, cy)
        shaded_poly(cv, f, C(fletch[0]), C(fletch[1]), C(fletch[2]), '#2a2408', 1.0, gloss=False)


def hgrid(cv):
    yy, xx = np.mgrid[0:cv.h, 0:cv.w].astype(np.float32)
    return (xx + 0.5) / cv.w * 2 - 1, (yy + 0.5) / cv.h * 2 - 1


# ============================================================ BOW
def fan_proj(cv, t, rng):
    X, Y = hgrid(cv)
    rib = np.zeros_like(X)
    for k in range(2):
        y0 = 0.35 * np.sin(X * 6 - t * TAU + k * math.pi) * smooth(0.6, -1, X)
        rib = np.maximum(rib, np.exp(-((Y - y0) / 0.1) ** 2) * smooth(-1, -0.2, X) * (X < 0.2))
    cv.ramp(np.clip(rib, 0, 1), WINDY, cover=0.9)
    arrow(cv, cv.w * 0.58, cv.h / 2, cv.w * 0.8, 0)


def fan_impact(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.4)
    sw = spiral(R, A, 3, 4, t * 10, 0.3) * band(R, 0.35 + 0.4 * t, 0.2)
    cv.ramp(np.clip(sw, 0, 1), WINDY, cover=a)
    for k in range(3):
        ang = k * TAU / 3 + 0.4
        d = s * (0.15 + 0.6 * ease_out(t))
        feather(cv, cv.w / 2 + math.cos(ang) * d, cv.h / 2 + math.sin(ang) * d + t * t * s * 0.4, s * 0.32, ang + t * 4, C('#e8d24a'), C('#fff7b0'), C('#8a7a10'))
    if t > 0.4:
        cv.c *= a


def fan_muzzle(cv, t, rng):
    X, Y, R, A, s = P(cv)
    f = 1 - t
    cone = (np.abs(A) < math.radians(28)) * band(R, 0.25 + 0.6 * ease_out(t), 0.12)
    cv.ramp(np.clip(cone * f * 1.2, 0, 1), WINDY)
    streaks(cv, X, Y * 1.8, t, 9, WINDY, density=8, speed=-1.2, length=0.5, thick=0.05, yspan=0.4, alpha=f)


def blast_proj(cv, t, rng):
    X, Y = hgrid(cv)
    arrow(cv, cv.w * 0.5, cv.h / 2, cv.w * 0.78, 0, fletch=('#c82a1a', '#ff8a6a', '#5a0a04'))
    tipx = cv.w * 0.9
    bx, by = (X - (tipx / cv.w * 2 - 1)), Y
    shape = np.exp(-((bx + 0.05) / 0.16) ** 2 - (by / 0.5) ** 2)
    flames(cv, X * 2, Y, shape, t, 101, FIRE, rise=-1.5, gain=1.6)
    ps = Particles(np.random.default_rng(int(t * 30) + 2), 10, (tipx, cv.h / 2), (40, 120), (0.3, 0.6), angle=(math.pi * 0.6, math.pi * 1.4), size=(0.8, 1.6))
    sparks(cv, ps, 0.4, C('#fff0a0')[:3], C('#ff6a00', 0.7), streak=0.04)


def firework_impact(cv, t, rng):
    X, Y, R, A, s = P(cv)
    cx, cy = cv.w / 2, cv.h / 2
    a = fade_tail(t, 0.55)
    f = pulse(t, 0, 0.3)
    core = np.exp(-(R / (0.1 + 0.35 * f)) ** 2) * f
    cv.glow(core, C('#ff5a1a', 1), 12, 1.2)
    cv.add(C('#fff3c0')[:3], core)
    # dark smoke billows
    r2 = np.random.default_rng(5)
    cs, rs = [], []
    for k in range(9):
        ang = k * TAU / 9 + r2.uniform(-0.2, 0.2)
        d = s * 0.35 * ease_out(t)
        cs.append((cx + math.cos(ang) * d, cy + math.sin(ang) * d - t * s * 0.15)); rs.append(s * (0.12 + 0.12 * ease_out(t)))
    if t > 0.1:
        smoke(cv, rng, cs, rs, t, 111, *SMOKE_D, alpha=a * 0.85, erode=t * 0.8)
    # fiery inner ball
    shape = np.clip(1 - R / (0.2 + 0.4 * ease_out(min(1, t * 2))), 0, 1)
    flames(cv, X, Y, shape * (1 - t), t, 112, FIRE, rise=1.0, gain=1.8)
    # shrapnel sparks in two colours
    for seed, col, gl in ((113, '#ffe27a', '#ff6a00'), (114, '#ff6a5a', '#ff2a10')):
        ps = Particles(np.random.default_rng(seed), 18, (cx, cy), (100, 200), (0.35, 0.65), size=(1, 2.2), gravity=60, drag=1.5, spawn=(0, 0.1))
        sparks(cv, ps, t, C(col)[:3], C(gl, 0.7), streak=0.09)


def leaf_trail(cv, t, rng):
    X, Y = hgrid(cv)
    a = fade_tail(t, 0.4)
    streaks(cv, X, Y, t, 21, NATURE, density=12, speed=1.4, length=0.7, thick=0.06, yspan=0.65, alpha=a)
    r2 = np.random.default_rng(22)
    for k in range(7):
        u = (r2.uniform() - t * 1.3) % 1.0
        x = cv.w * (0.05 + 0.9 * u); y = cv.h * (0.5 + 0.32 * math.sin(k * 2.1 + t * 6))
        leaf(cv, x, y, cv.h * 0.22, k + t * 9, C('#5ac85a'), C('#d0ffb0'), C('#1a6a2a'))
    if t > 0.4:
        cv.c *= a


def hawk_cast(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.6)
    lock = ease_out(min(1, t * 1.8), 3)
    r = 0.9 - 0.45 * lock
    spin = (1 - lock) * 2.2
    v = band(R, r, 0.025) * (np.cos((A + spin) * 4) < 0.75)
    for k in range(4):
        ang = k * math.pi / 2 + spin
        ca, sa = math.cos(ang), math.sin(ang)
        u = X * ca + Y * sa; d = -X * sa + Y * ca
        v = np.maximum(v, np.exp(-(d / 0.02) ** 2) * (u > r - 0.15) * (u < r + 0.12))
    v = np.maximum(v, band(R, r * 0.35, 0.02) * lock)
    cv.glow(v, C('#ff8a1a', 0.8), 4, a)
    cv.ramp(np.clip(v, 0, 1), HAWK, cover=a)
    # eye glyph flash once locked
    e = pulse(t, 0.4, 0.95)
    if e > 0:
        lens = (np.abs(Y) < 0.18 * np.clip(1 - (X / 0.32) ** 2, 0, 1)) * 1.0
        pupil = np.exp(-(R / 0.06) ** 2)
        m = blur(lens.astype(np.float32), 1)
        cv.glow(m, C('#ff9a2a', 1), 6, e)
        cv.over(C('#fff1d0')[:3], m * e)
        cv.over(C('#3a1400')[:3], pupil * e)
        rays(cv, R, A, 16, 0, 0.1, 0.3, 0.8, HAWK, alpha=e * 0.7)


def rain_zone(cv, t, rng):
    X, Y, R, A, s = P(cv)
    n = N(121, 5, 4)
    pud = (1 - smooth(0.6, 0.95, R * (0.9 + 0.2 * n.sample(X * 0.6, Y * 0.6))))
    ripple = 0.5 + 0.5 * np.sin(R * 30 - t * TAU * 2)
    cv.ramp(np.clip(pud * (0.3 + 0.15 * ripple), 0, 1), TOXIC, cover=0.3)
    r2 = np.random.default_rng(122)
    for k in range(14):
        ph = (r2.uniform() + t) % 1.0
        ang = r2.uniform(0, TAU); rr = math.sqrt(r2.uniform()) * 0.75
        x = cv.w / 2 + math.cos(ang) * rr * s; y = cv.h / 2 + math.sin(ang) * rr * s * 0.75
        if ph < 0.55:
            k2 = ph / 0.55
            ax = x - (1 - k2) * s * 0.35; ay = y - (1 - k2) * s * 0.9
            arrow(cv, ax, ay, s * 0.3, math.radians(68), fletch=('#3a7a1a', '#8ad84a', '#1a3a0a'), head=('#9ad84a', '#efffb0', '#3a5a10'))
        else:
            k2 = (ph - 0.55) / 0.45
            sp = band(np.hypot((X * s + cv.w / 2 - x) / s, (Y * s + cv.h / 2 - y) / s * 1.6), 0.04 + 0.12 * k2, 0.02)
            cv.ramp(np.clip(sp * (1 - k2) * 1.3, 0, 1), TOXIC)
    ps = Particles(np.random.default_rng(123), 10, (cv.w / 2, cv.h / 2), (5, 15), (0.5, 1.0), angle=(-math.pi, 0), size=(2, 4), spawn=(0, 1), jitter=s * 0.4)
    pos, age, alive, _ = ps.state(t)
    for i in np.nonzero(alive)[0]:
        b = band(np.hypot(*(np.mgrid[0:cv.h, 0:cv.w][::-1] - pos[i][:, None, None])) , ps.size[i] * (1 + age[i]), 0.9)
        cv.ramp(np.clip(b * (1 - age[i]), 0, 1), TOXIC)


def flower(cv, cx, cy, size, ang, petal='#ff8ad0', light='#ffe0f4', dark='#a02a7a'):
    for k in range(5):
        a = ang + k * TAU / 5
        pts = []
        for i in range(12):
            u = i / 11
            w = math.sin(math.pi * u) * size * 0.3
            pts.append((u * size * 0.6, w))
        for i in range(11, -1, -1):
            u = i / 11
            pts.append((u * size * 0.6, -math.sin(math.pi * u) * size * 0.3))
        shaded_poly(cv, tr(rot(pts, a), cx, cy), C(petal), C(light), C(dark), '#4a0a30', 0.9, gloss=False)
    m = disc((cv.h, cv.w), cx, cy, size * 0.15)
    cv.over(C('#ffe04a')[:3], m)


def bless_cast(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.6)
    sw = spiral(R, A, 4, -3.5, t * 9, 0.25) * band(R, 0.55, 0.32) * ease_out(min(1, t * 2))
    cv.glow(sw, C('#5aff9a', 0.6), 4, a)
    cv.ramp(np.clip(sw, 0, 1), NATURE, cover=a)
    for k in range(6):
        ang = k * TAU / 6 + t * 1.5
        bloom = ease_out(np.clip((t - k * 0.06) * 2.5, 0, 1))
        if bloom > 0.05:
            flower(cv, cv.w / 2 + math.cos(ang) * s * 0.62, cv.h / 2 + math.sin(ang) * s * 0.5, s * 0.26 * bloom, ang * 2)
    ps = Particles(np.random.default_rng(131), 26, (cv.w / 2, cv.h * 0.65), (15, 45), (0.5, 0.8), angle=(-math.pi * 0.9, -math.pi * 0.1), size=(1.5, 3.5), spawn=(0, 0.5), jitter=s * 0.35)
    dots(cv, ps, t, [(0, C('#1a6a3a', 0)), (0.4, C('#7af0a0', 0.8)), (1, C('#ffffff'))], mode='add', shrink=0.2)
    if t > 0.6:
        cv.c *= a


skill('BowFan', dict(proj_size=0.95, impact_size=0.55, muzzle_size=0.9, muzzle_dir=True),
      projectile=part(fan_proj, 6, 128, 48, fps=18, loop=True), impact=part(fan_impact, 9, 128, fps=22), muzzle=part(fan_muzzle, 6, 128, fps=24))
skill('BowBlast', dict(proj_size=1.0, impact_scale=1.25),
      projectile=part(blast_proj, 6, 128, 48, fps=18, loop=True), impact=part(firework_impact, 13, 192, fps=20))
skill('BowLeap', dict(trail_width=1.4), trail=part(leaf_trail, 8, 320, 128, fps=20))
skill('BowFocus', dict(cast_scale=1.3), cast=part(hawk_cast, 14, 224, fps=20))
skill('BowRain', dict(cast_ground=True, cast_scale=1.05), cast=part(rain_zone, 12, 256, fps=14, loop=True))
skill('BowBless', dict(cast_ground=True, cast_scale=1.3), cast=part(bless_cast, 14, 256, fps=18))


# ============================================================ STAFF
def fireball_proj(cv, t, rng):
    X, Y = hgrid(cv)
    asp = cv.w / cv.h
    Xa = X * asp
    head = np.exp(-(((Xa - 0.75) / 0.5) ** 2 + (Y / 0.55) ** 2))
    tail = np.exp(-(Y / (0.18 + 0.32 * np.clip((Xa + 1.5) / 2.3, 0, 1))) ** 2) * smooth(-1.6, 0.5, Xa) * (Xa < 0.9)
    shape = np.clip(head * 1.3 + tail * 0.8, 0, 1)
    n = N(141, 5, 4)
    f = n.sample(Xa * 0.5 + t * 1.0, Y * 0.5 + t * 0.2)
    v = np.clip(shape * (0.55 + f * 0.9) - 0.15, 0, 1)
    cv.glow(head, C('#ff5a00', 0.8), 6, 0.9)
    cv.ramp(v, FIRE)
    cv.add(C('#fffbe0')[:3], np.exp(-(((Xa - 0.8) / 0.25) ** 2 + (Y / 0.28) ** 2)) * 0.8)


def fire_explosion(cv, t, rng):
    X, Y, R, A, s = P(cv)
    cx, cy = cv.w / 2, cv.h / 2
    a = fade_tail(t, 0.6)
    grow = 0.25 + 0.7 * ease_out(min(1, t * 1.8))
    n = N(151, 5, 4)
    edge = n.sample(A / TAU * 3, R * 0.5 - t * 0.6)
    shape = np.clip(1 - R / (grow * (0.8 + 0.35 * edge)), 0, 1) ** 0.7
    heat = shape * (1.1 - t * 0.9)
    f = n.sample(X * 0.6, Y * 0.6 + t * 0.8)
    v = np.clip(heat * (0.5 + f) - t * 0.25, 0, 1)
    cv.glow(shape * (1 - t), C('#ff4a00', 0.9), 10, 1.0)
    cv.ramp(v, FIRE)
    ring(cv, R, grow * 1.08, 0.04 * (1 - t), FIRE, alpha=a * 0.8)
    ps = Particles(np.random.default_rng(152), 26, (cx, cy), (60, 170), (0.4, 0.8), size=(1, 2.4), gravity=-40, drag=1.2, spawn=(0, 0.15))
    sparks(cv, ps, t, C('#ffd27a')[:3], C('#ff6a00', 0.7), streak=0.07)


def fire_muzzle(cv, t, rng):
    X, Y, R, A, s = P(cv)
    shape = np.clip(1 - R / (0.3 + 0.5 * ease_out(t)), 0, 1) * (1 - t)
    flames(cv, X, Y, shape, t, 161, FIRE, rise=1.2, gain=1.8)


def flame_zone(cv, t, rng):
    X, Y, R, A, s = P(cv)
    base = (1 - smooth(0.55, 0.95, R))
    cv.ramp(np.clip(base * 0.5, 0, 1), [(0, C('#200400', 0)), (0.5, C('#5a1000', 0.45)), (1, C('#c83a00', 0.6))])
    # flame tongues: noise scrolling upward, stronger where tall
    n1 = N(171, 6, 4)
    n2 = N(172, 10, 3)
    tongues = n1.sample(X * 0.9, Y * 0.5 + t) * 0.7 + n2.sample(X * 1.6, Y * 1.0 + t * 2) * 0.5
    v = np.clip(base * (tongues * 2.0 - 0.85) * (0.8 + 0.5 * smooth(0.6, -0.4, Y)), 0, 1)
    cv.ramp(v, [(0, C('#300800', 0)), (0.25, C('#b02800', 0.35)), (0.5, C('#ff6a10', 0.85)), (0.8, C('#ffd25a')), (1, C('#fffbe0'))])
    ps = Particles(np.random.default_rng(173), 24, (cv.w / 2, cv.h / 2), (10, 30), (0.4, 0.9), angle=(-math.pi * 0.7, -math.pi * 0.3), size=(1, 2), spawn=(0, 1), jitter=s * 0.45)
    pos, age, alive, vel = ps.state(t)
    sparks(cv, ps, t, C('#ffe08a')[:3], C('#ff5a00', 0.6), streak=0.1)


def ice_proj(cv, t, rng):
    X, Y = hgrid(cv)
    mist = np.exp(-(Y / 0.35) ** 2) * smooth(-1, 0.2, X) * (X < 0.3)
    n = N(181, 6, 3).sample(X * 2 + t, Y)
    cv.ramp(np.clip(mist * (0.3 + n * 0.6), 0, 1), ICE, cover=0.8)
    shard(cv, cv.w * 0.62, cv.h / 2, cv.w * 0.62, cv.h * 0.42, 0, C('#8ad8ff'), C('#ffffff'), C('#2a6ab0'))
    tw = pulse((t * 2) % 1, 0, 1)
    st = poly((cv.h, cv.w), star_poly(cv.w * 0.9, cv.h * 0.45, cv.h * 0.3 * tw, cv.h * 0.05, 4, 0))
    cv.add(C('#ffffff')[:3], st)


def ice_shatter(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.5)
    puff = np.exp(-(R / (0.25 + 0.4 * ease_out(t))) ** 2) * (1 - t)
    cv.ramp(np.clip(puff, 0, 1), ICE, cover=0.8)
    r2 = np.random.default_rng(191)
    for k in range(8):
        ang = k * TAU / 8 + r2.uniform(-0.3, 0.3)
        d = s * (0.1 + 0.7 * ease_out(t)) * r2.uniform(0.7, 1.0)
        shard(cv, cv.w / 2 + math.cos(ang) * d, cv.h / 2 + math.sin(ang) * d + t * t * s * 0.3, s * 0.3 * (1 - t * 0.5), s * 0.14, ang + t * 5, C('#9ae0ff'), C('#ffffff'), C('#2a6ab0'))
    if t > 0.5:
        cv.c *= a


def snowflake(cv, cx, cy, r, ang, color, glowc, w=1.6, alpha=1.0):
    shape = (cv.h, cv.w)
    m = np.zeros(shape, np.float32)
    for k in range(6):
        a = ang + k * TAU / 6
        p1 = (cx + math.cos(a) * r, cy + math.sin(a) * r)
        m = np.maximum(m, stroke(shape, [(cx, cy), p1], [w, w * 0.6]))
        for f in (0.45, 0.7):
            bx, by = cx + math.cos(a) * r * f, cy + math.sin(a) * r * f
            for sg in (-1, 1):
                b = a + sg * 0.8
                m = np.maximum(m, stroke(shape, [(bx, by), (bx + math.cos(b) * r * 0.25, by + math.sin(b) * r * 0.25)], [w * 0.7, w * 0.4]))
    cv.glow(m, glowc, 3, 1.2 * alpha)
    cv.add(color[:3], m * alpha)
    return m


def ice_muzzle(cv, t, rng):
    X, Y, R, A, s = P(cv)
    f = 1 - t
    cv.ramp(np.clip(np.exp(-(R / (0.3 + 0.3 * t)) ** 2) * f, 0, 1), ICE, cover=0.8)
    snowflake(cv, cv.w / 2, cv.h / 2, s * (0.4 + 0.3 * t), t * 1.5, C('#ffffff')[:3], C('#5ac8ff', 1), alpha=f)


def nova_cast(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.6)
    frost = (1 - smooth(0.2, 1.0, R / max(0.05, ease_out(min(1, t * 1.8))))) * 0.55
    n = N(201, 8, 4).sample(X, Y)
    cv.ramp(np.clip(frost * (0.6 + n * 0.6), 0, 1), ICE, cover=a)
    reach = ease_out(min(1, t * 1.8))
    r2 = np.random.default_rng(202)
    spikes = []
    for ring_i, rr in enumerate((0.35, 0.6, 0.85)):
        cnt = 6 + ring_i * 4
        for k in range(cnt):
            ang = k * TAU / cnt + ring_i * 0.3 + r2.uniform(-0.1, 0.1)
            spikes.append((rr, ang, r2.uniform(0.8, 1.2)))
    spikes.sort(key=lambda q: math.sin(q[1]))
    for rr, ang, sc in spikes:
        if rr > reach + 0.05:
            continue
        grow = np.clip((reach - rr + 0.25) / 0.25, 0, 1)
        h = s * 0.28 * sc * grow * (1 - 0.4 * max(0, t - 0.7) / 0.3)
        if h < 2:
            continue
        x = cv.w / 2 + math.cos(ang) * rr * s; y = cv.h / 2 + math.sin(ang) * rr * s * 0.8
        shard(cv, x, y - h * 0.4, h, h * 0.4, -math.pi / 2 + math.cos(ang) * 0.4, C('#9ae0ff'), C('#ffffff'), C('#2a5aa0'))
    f = pulse(t, 0, 0.4)
    if f > 0:
        snowflake(cv, cv.w / 2, cv.h / 2, s * 0.4 * (0.5 + f), t, C('#ffffff')[:3], C('#5ac8ff', 1), 2.4)
    if t > 0.6:
        cv.c *= a


def plasma_proj(cv, t, rng):
    X, Y, R, A, s = P(cv)
    n = N(211, 4, 4).sample(R * 1.5 - t, A / TAU + t * 0.5)
    core = np.clip(1 - R / (0.5 + 0.1 * n), 0, 1)
    cv.glow(core, C('#a03aff', 0.9), 8, 1)
    cv.ramp(np.clip(core * (0.6 + n * 0.6), 0, 1), VIOLET)
    r2 = np.random.default_rng(int(t * 60) + 4)
    for k in range(3):
        a0 = r2.uniform(0, TAU)
        draw_bolt(cv, r2, (cv.w / 2 + math.cos(a0) * s * 0.2, cv.h / 2 + math.sin(a0) * s * 0.2), (cv.w / 2 + math.cos(a0) * s * 0.85, cv.h / 2 + math.sin(a0) * s * 0.85), C('#ffffff')[:3], C('#c070ff', 0.8), 1.0, 4, 0, 0.5)


def plasma_impact(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.4)
    ring(cv, R, 0.2 + 0.6 * ease_out(t), 0.06 * (1 - t), VIOLET, alpha=a)
    r2 = np.random.default_rng(int(t * 30) + 9)
    for k in range(6):
        ang = r2.uniform(0, TAU)
        rr = s * (0.4 + 0.5 * t)
        draw_bolt(cv, r2, (cv.w / 2, cv.h / 2), (cv.w / 2 + math.cos(ang) * rr, cv.h / 2 + math.sin(ang) * rr), C('#ffffff')[:3], C('#b04aff', 0.9), 1.4 * a, 4, 1, 0.4)
    cv.glow(np.exp(-(R / 0.2) ** 2) * (1 - t), C('#d090ff', 1), 6, 1.2)


def plasma_muzzle(cv, t, rng):
    X, Y, R, A, s = P(cv)
    f = 1 - t
    rune_circle(cv, X, Y, R, A, 0.35 + 0.45 * t, t * 3, C('#e8c8ff', f), C('#a040ff', f), seed=7, spokes=5, alpha=f)


def blink_trail(cv, t, rng):
    X, Y = hgrid(cv)
    a = fade_tail(t, 0.35)
    body = np.exp(-(Y / (0.15 + 0.25 * (1 - (X + 1) / 2))) ** 2) * smooth(-1, -0.6, X) * (1 - smooth(0.7, 1, X))
    n = N(221, 8, 4).sample(X * 2 - t * 2, Y)
    cv.ramp(np.clip(body * (0.3 + n * 0.7) * 0.9, 0, 1), ARCANE, cover=a)
    r2 = np.random.default_rng(222)
    for k in range(16):
        x = r2.uniform(0.05, 0.95) * cv.w; y = cv.h * (0.5 + r2.normal(0, 0.18))
        tw = abs(math.sin(t * 9 + k))
        st = poly((cv.h, cv.w), star_poly(x, y, cv.h * 0.09 * tw, cv.h * 0.015, 4, 0))
        cv.add(C('#f0eaff')[:3], st * a)


def blink_portal(cv, t, rng):
    X, Y, R, A, s = P(cv)
    o = pulse(t, 0, 1)
    r = 0.15 + 0.6 * o
    v = spiral(R, A, 3, 5, t * 12, 0.3) * (1 - smooth(r * 0.6, r, R))
    cv.ramp(np.clip(v * 0.9, 0, 1), ARCANE, cover=o)
    rune_circle(cv, X, Y / 0.6, np.hypot(X, Y / 0.6), np.arctan2(Y / 0.6, X), r, t * 4, C('#e8e4ff', o), C('#6a5aff', o), seed=8, spokes=7, alpha=o)
    ps = Particles(np.random.default_rng(231), 20, (cv.w / 2, cv.h / 2), (30, 90), (0.3, 0.6), size=(1, 2), spawn=(0, 0.4))
    sparks(cv, ps, t, C('#ece8ff')[:3], C('#7a6aff', 0.7), streak=0.05)


skill('StaffFireball', dict(proj_size=1.0, impact_scale=1.15, muzzle_size=0.7),
      projectile=part(fireball_proj, 6, 128, 72, fps=20, loop=True), impact=part(fire_explosion, 12, 192, fps=20), muzzle=part(fire_muzzle, 6, 128, fps=22))
skill('StaffFlame', dict(cast_ground=True, cast_scale=1.05), cast=part(flame_zone, 12, 256, fps=14, loop=True))
skill('StaffIce', dict(proj_size=0.9, impact_size=0.6, muzzle_size=0.6),
      projectile=part(ice_proj, 6, 128, 48, fps=16, loop=True), impact=part(ice_shatter, 10, 160, fps=22), muzzle=part(ice_muzzle, 6, 128, fps=22))
skill('StaffNova', dict(cast_ground=True, cast_scale=1.1), cast=part(nova_cast, 14, 256, fps=20))
skill('StaffStorm', dict(proj_size=0.55, proj_spin=0, impact_size=0.55, muzzle_size=0.8),
      projectile=part(plasma_proj, 6, 96, fps=20, loop=True), impact=part(plasma_impact, 8, 128, fps=22), muzzle=part(plasma_muzzle, 6, 128, fps=22))
skill('StaffBlink', dict(trail_width=1.3, impact_size=0.9, cast_size=0.9), trail=part(blink_trail, 8, 320, 128, fps=20), impact=part(blink_portal, 12, 160, fps=22), cast=part(blink_portal, 12, 160, fps=22))
