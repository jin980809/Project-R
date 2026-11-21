import math
import cv2
import numpy as np
from lib import *
from rec import *
from core import part, skill
from job_bow_staff import hgrid, FIRE, ICE, TOXIC, shard as _s, snowflake
from job_axe_blunt import EARTH, SUN

WHITE = [(0, C('#2a3040', 0)), (0.3, C('#6a7a94', 0.6)), (0.65, C('#d0e0f4')), (1, C('#ffffff'))]
CYANW = [(0, C('#0a2a4a', 0)), (0.3, C('#2a7ac8', 0.7)), (0.65, C('#9ae4ff')), (1, C('#ffffff'))]
LIME = [(0, C('#2a3a00', 0)), (0.3, C('#8aa000', 0.8)), (0.6, C('#e8f04a')), (0.9, C('#fffcc0')), (1, C('#ffffff'))]
REDB = [(0, C('#2a0000', 0)), (0.3, C('#a00a0a', 0.85)), (0.6, C('#ff3a2a')), (0.9, C('#ffd0b0')), (1, C('#ffffff'))]
TRACER = [(0, C('#3a2000', 0)), (0.3, C('#c87000', 0.8)), (0.6, C('#ffd030')), (0.9, C('#fff8d0')), (1, C('#ffffff'))]
SMK = (C('#3a3a3e'), C('#9a9aa4'))
GHOST = [(0, C('#1a0a2a', 0)), (0.3, C('#5a2a9a', 0.6)), (0.6, C('#b07aff', 0.9)), (0.9, C('#f0e0ff')), (1, C('#ffffff'))]
HEXP = [(0, C('#0a0010', 0)), (0.3, C('#3a0a5a', 0.85)), (0.6, C('#9a2ad8')), (0.9, C('#f0a0ff')), (1, C('#ffffff'))]
MIASMA = [(0, C('#0a1a06', 0)), (0.3, C('#3a5a1a', 0.6)), (0.55, C('#6a3a8a', 0.8)), (0.8, C('#9ad84a', 0.9)), (1, C('#e8ffc0'))]
SOUL = [(0, C('#2a0a2a', 0)), (0.3, C('#9a3a9a', 0.6)), (0.6, C('#ff8ae0', 0.9)), (0.9, C('#ffe0f8')), (1, C('#ffffff'))]
HOLY = [(0, C('#3a2a00', 0)), (0.25, C('#c09000', 0.6)), (0.55, C('#ffe060', 0.9)), (0.85, C('#fffbe0')), (1, C('#ffffff'))]
HEALG = [(0, C('#0a3a1a', 0)), (0.3, C('#2ab060', 0.6)), (0.6, C('#9affc0', 0.9)), (0.9, C('#f0fff4')), (1, C('#ffffff'))]


def bolt(cv, cx, cy, L, ang, vane='#d8dce4'):
    sh = tr(rot([(-L * 0.45, -1.8), (L * 0.32, -1.8), (L * 0.32, 1.8), (-L * 0.45, 1.8)], ang), cx, cy)
    shaded_poly(cv, sh, C('#3a3a44'), C('#8a8a98'), C('#14141a'), '#08080c', 1.0, gloss=False)
    hd = tr(rot([(L * 0.28, -5), (L * 0.5, 0), (L * 0.28, 5)], ang), cx, cy)
    shaded_poly(cv, hd, C('#c0c8d4'), C('#ffffff'), C('#4a5260'), '#101418', 1.0)
    for sg in (-1, 1):
        f = tr(rot([(-L * 0.45, 0), (-L * 0.32, 0), (-L * 0.4, sg * 5.5), (-L * 0.48, sg * 5.5)], ang), cx, cy)
        shaded_poly(cv, f, C(vane), C('#ffffff'), C('#5a5a68'), '#14141a', 0.8, gloss=False)


# ============================================================ CROSSBOW
def pierce_bolt_proj(cv, t, rng):
    X, Y = hgrid(cv)
    asp = cv.w / cv.h
    for k in range(3):
        ph = (t + k / 3) % 1.0
        x0 = 0.45 - ph * 1.4
        e = np.hypot((X - x0) * asp / 0.35, Y / (0.35 + ph * 0.6))
        cv.ramp(np.clip(band(e, 1.0, 0.12) * (1 - ph) * (X < x0 + 0.05), 0, 1), WHITE)
    bolt(cv, cv.w * 0.58, cv.h / 2, cv.w * 0.75, 0)


def puncture_impact(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.4)
    needle = np.exp(-(Y / (0.03 + 0.04 * (1 - t))) ** 2) * np.clip(1 - np.abs(X + 0.2 - t * 0.4) / 0.9, 0, 1)
    cv.glow(needle, C('#ffffff', 1), 5, a)
    cv.ramp(np.clip(needle, 0, 1), WHITE, cover=a)
    ring(cv, np.hypot(X / 0.5, Y), 0.3 + 0.6 * ease_out(t), 0.06 * (1 - t), WHITE, alpha=a)
    ps = Particles(np.random.default_rng(601), 18, (cv.w / 2, cv.h / 2), (60, 150), (0.3, 0.55), angle=(-0.7, 0.7), size=(1.4, 2.8), gravity=180, spawn=(0, 0.1))
    dots(cv, ps, t, [(0, C('#300004', 0)), (0.5, C('#a00014')), (1, C('#ff5a6a'))], mode='over', shrink=0.3)


def sonic_muzzle(cv, t, rng):
    X, Y, R, A, s = P(cv)
    f = 1 - t
    for k in range(2):
        e = np.hypot((X - 0.1 - k * 0.3 * t) / 0.35, Y / (0.5 + 0.4 * t))
        cv.ramp(np.clip(band(e, 1.0, 0.1) * f * (X > -0.5), 0, 1), WHITE)
    cv.glow(np.exp(-(R / 0.2) ** 2) * f, C('#ffffff', 1), 6, 1)


def scope_cast(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.6)
    k = ease_out(min(1, t * 2), 3)
    r = 0.75 - 0.15 * k
    rot_a = (1 - k) * 3 + t
    notch = (np.cos((A - rot_a) * 24) > 0.3) * band(R, r + 0.08, 0.04)
    v = np.maximum(band(R, r, 0.035), notch)
    v = np.maximum(v, band(R, r * 0.5, 0.012))
    for q in range(4):
        ang = q * math.pi / 2
        ca, sa = math.cos(ang), math.sin(ang)
        u = X * ca + Y * sa; d = -X * sa + Y * ca
        v = np.maximum(v, np.exp(-(d / 0.012) ** 2) * (u > 0.08) * (u < r))
    glass = (1 - smooth(r * 0.9, r, R)) * (0.15 + 0.25 * smooth(0.3, -0.5, X + Y))
    cv.ramp(np.clip(glass, 0, 1), LIME, cover=0.6 * a)
    cv.glow(v, C('#e8f04a', 0.8), 4, a)
    cv.ramp(np.clip(v, 0, 1), LIME, cover=a)
    # converging laser lines
    for q in range(3):
        ang = q * TAU / 3 + 0.5
        L = 1.0 - 0.9 * k
        ca, sa = math.cos(ang), math.sin(ang)
        u = X * ca + Y * sa; d = -X * sa + Y * ca
        las = np.exp(-(d / 0.01) ** 2) * (u > L * 0.2) * (u < 1.0)
        cv.add(C('#ff3a2a')[:3], las * a * 0.8)
    dot = pulse(t, 0.45, 1.0)
    cv.glow(np.exp(-(R / 0.05) ** 2), C('#ff2a1a', 1), 4, dot * 1.5)


def burst_bolt_proj(cv, t, rng):
    X, Y = hgrid(cv)
    tr_ = np.exp(-(Y / 0.12) ** 2) * smooth(-1, 0.1, X) * (X < 0.2)
    cv.glow(tr_, C('#3aa0ff', 0.7), 3, 0.8)
    cv.ramp(np.clip(tr_, 0, 1), CYANW)
    bolt(cv, cv.w * 0.6, cv.h / 2, cv.w * 0.7, 0, vane='#7ac8ff')


def metal_sparks(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.4)
    cv.glow(np.exp(-(R / 0.2) ** 2) * (1 - t), C('#8ad0ff', 1), 5, 1.2)
    ps = Particles(np.random.default_rng(611), 20, (cv.w / 2, cv.h / 2), (90, 200), (0.25, 0.5), angle=(math.pi * 0.5, math.pi * 1.5), size=(0.8, 1.8), gravity=200, spawn=(0, 0.05))
    sparks(cv, ps, t, C('#eaf8ff')[:3], C('#3a9cff', 0.8), streak=0.06)


def triple_muzzle(cv, t, rng):
    X, Y, R, A, s = P(cv)
    for k in range(3):
        f = pulse(t, k * 0.2, k * 0.2 + 0.45)
        if f > 0:
            y = (k - 1) * 0.35
            st = poly((cv.h, cv.w), star_poly(cv.w * 0.5, cv.h * (0.5 + y * 0.5), s * 0.35 * f, s * 0.07, 4, 0))
            cv.glow(st, C('#7ac8ff', 1), 4, f)
            cv.add(C('#ffffff')[:3], st)


def bomb_bolt_proj(cv, t, rng):
    bolt(cv, cv.w * 0.45, cv.h / 2, cv.w * 0.7, 0, vane='#c84a3a')
    bx, by = cv.w * 0.7, cv.h / 2
    m = disc((cv.h, cv.w), bx, by, cv.h * 0.3)
    cv.outline(m, C('#080808'), 1.4)
    yy, xx = np.mgrid[0:cv.h, 0:cv.w].astype(np.float32)
    shade = np.clip(1 - np.hypot(xx - bx + 3, yy - by + 3) / (cv.h * 0.3), 0, 1)
    cv.over(C('#2a2a30')[:3] * (1 - shade[..., None]) + C('#7a7a88')[:3] * shade[..., None], m)
    blink = 1.0 if (t * 4) % 1.0 < 0.5 else 0.2
    led = soft_dot((cv.h, cv.w), bx + 2, by - cv.h * 0.12, 3)
    cv.glow(led, C('#ff2a1a', 1), 4, blink * 1.5)
    cv.add(C('#ffb0a0')[:3], led * blink)


def flashbang(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.55)
    f = pulse(t, 0, 0.25)
    cv.glow(np.exp(-(R / (0.12 + 0.3 * f)) ** 2) * f, C('#ffffff', 1), 7, 1.4)
    cv.add(C('#ffffff')[:3], np.exp(-(R / (0.08 + 0.2 * f)) ** 2) * f)
    ring(cv, R, 0.2 + 0.75 * ease_out(t), 0.07 * (1 - t) + 0.01, REDB, alpha=a)
    ring(cv, R, 0.1 + 0.55 * ease_out(t), 0.04 * (1 - t), REDB, alpha=a * 0.8)
    cs, rs = [], []
    r2 = np.random.default_rng(621)
    for k in range(8):
        ang = k * TAU / 8 + r2.uniform(-0.3, 0.3)
        d = s * (0.15 + 0.4 * ease_out(t))
        cs.append((cv.w / 2 + math.cos(ang) * d, cv.h / 2 + math.sin(ang) * d)); rs.append(s * 0.13 * (0.6 + t))
    if t > 0.15:
        smoke(cv, rng, cs, rs, t, 622, *SMK, alpha=0.8 * a, erode=t)
    ps = Particles(np.random.default_rng(623), 10, (cv.w / 2, cv.h / 2), (100, 190), (0.35, 0.6), size=(2, 3.5), gravity=200, spawn=(0, 0.05))
    pos, age, alive, _ = ps.state(t)
    for i in np.nonzero(alive)[0]:
        pts = tr(rot([(-ps.size[i], -ps.size[i] * 0.4), (ps.size[i], -ps.size[i] * 0.6), (ps.size[i] * 0.4, ps.size[i] * 0.6)], t * 9 + i), pos[i][0], pos[i][1])
        shaded_poly(cv, pts, C('#6a6a74'), C('#c8c8d4'), C('#24242a'), '#0a0a0e', 0.8)


def trap_zone(cv, t, rng):
    X, Y, R, A, s = P(cv)
    n = N(631, 6, 4)
    frost = (1 - smooth(0.6, 0.98, R)) * (0.25 + 0.25 * n.sample(X * 0.8, Y * 0.8))
    cv.ramp(np.clip(frost, 0, 1), ICE, cover=0.35)
    mist = spiral(R, A, 3, 2.5, t * TAU, 0.4) * (1 - smooth(0.4, 0.9, R)) * 0.5
    cv.ramp(np.clip(mist, 0, 1), CYANW, cover=0.4)
    teeth = []
    for k in range(14):
        ang = k * TAU / 14
        teeth.append((math.sin(ang), ang))
    teeth.sort()
    snap = 0.5 + 0.5 * math.sin(t * TAU)
    for _, ang in teeth:
        x = cv.w / 2 + math.cos(ang) * s * 0.78; y = cv.h / 2 + math.sin(ang) * s * 0.78
        L = s * (0.22 + 0.06 * snap)
        _s(cv, x - math.cos(ang) * L * 0.4, y - math.sin(ang) * L * 0.4, L, L * 0.45, ang + math.pi, C('#9ae0ff'), C('#ffffff'), C('#2a5aa0'))
    tw = abs(math.sin(t * TAU * 2))
    snowflake(cv, cv.w / 2, cv.h / 2, s * 0.22, t * TAU / 6, C('#ffffff')[:3], C('#5ac8ff', 1), 1.6, alpha=0.5 + 0.5 * tw)


def roll_trail(cv, t, rng):
    a = fade_tail(t, 0.4)
    cs, rs = [], []
    for i in range(24):
        u = i / 23
        if u > ease_out(min(1, t * 2.5)):
            continue
        ang = u * TAU * 2.5
        x = cv.w * (0.05 + 0.9 * u) + math.cos(ang) * cv.h * 0.22
        y = cv.h * 0.55 + math.sin(ang) * cv.h * 0.22
        cs.append((x, y)); rs.append(cv.h * 0.07 * (1 + t))
    smoke(cv, rng, cs, rs, t, 641, *EARTH, alpha=0.8 * a, erode=t * 0.7)
    X, Y = hgrid(cv)
    streaks(cv, X, Y, t, 642, CYANW, density=6, speed=1.4, length=0.5, thick=0.04, yspan=0.4, alpha=a * 0.8)


skill('CrossPierce', dict(proj_size=1.0, impact_size=0.6, muzzle_size=0.8, muzzle_dir=True, impact_dir=True),
      projectile=part(pierce_bolt_proj, 6, 128, 40, fps=20, loop=True), impact=part(puncture_impact, 9, 128, fps=22), muzzle=part(sonic_muzzle, 6, 128, fps=24))
skill('CrossAim', dict(cast_scale=1.3), cast=part(scope_cast, 14, 224, fps=20))
skill('CrossBurst', dict(proj_size=0.8, impact_size=0.4, muzzle_size=0.7),
      projectile=part(burst_bolt_proj, 4, 128, 40, fps=16, loop=True), impact=part(metal_sparks, 8, 96, fps=24), muzzle=part(triple_muzzle, 8, 128, fps=24))
skill('CrossBomb', dict(proj_size=0.9, impact_scale=1.2),
      projectile=part(bomb_bolt_proj, 4, 128, 48, fps=8, loop=True), impact=part(flashbang, 12, 192, fps=20))
skill('CrossTrap', dict(cast_ground=True, cast_scale=1.05), cast=part(trap_zone, 12, 256, fps=12, loop=True))
skill('CrossRoll', dict(trail_width=1.3), trail=part(roll_trail, 8, 320, 128, fps=20))


# ============================================================ GUN
def tracer_proj(cv, t, rng):
    X, Y = hgrid(cv)
    body = np.exp(-(Y / 0.32) ** 2) * smooth(-1, 0.6, X) * (1 - smooth(0.75, 0.95, X))
    cv.glow(body, C('#ffb000', 0.9), 3, 1)
    cv.ramp(np.clip(body, 0, 1), TRACER)
    cv.add(C('#ffffff')[:3], np.exp(-(Y / 0.15) ** 2) * smooth(0.0, 0.7, X) * (1 - smooth(0.75, 0.9, X)))


def ricochet(cv, t, rng):
    X, Y, R, A, s = P(cv)
    cv.glow(np.exp(-(R / 0.25) ** 2) * (1 - t), C('#ffb000', 1), 4, 1.2)
    ps = Particles(np.random.default_rng(701), 12, (cv.w / 2, cv.h / 2), (60, 140), (0.25, 0.5), size=(0.8, 1.5), gravity=160, spawn=(0, 0.05))
    sparks(cv, ps, t, C('#fff4b0')[:3], C('#ff9a00', 0.8), streak=0.06)


def star_muzzle(cv, t, rng):
    X, Y, R, A, s = P(cv)
    f = 1 - ease_out(t, 1.5)
    if f > 0.05:
        pts = []
        for i in range(12):
            ang = i * TAU / 12
            rr = (0.9 if i == 0 else (0.5 if i % 2 == 0 else 0.18)) * s * (0.6 + 0.4 * f)
            if i in (2, 10):
                rr = 0.6 * s * f
            pts.append((cv.w * 0.3 + math.cos(ang) * rr, cv.h / 2 + math.sin(ang) * rr * 0.8))
        m = poly((cv.h, cv.w), pts)
        cv.glow(m, C('#ff8a00', 1), 6, f * 1.3)
        cv.ramp(np.clip(m * (0.6 + 0.4 * np.exp(-(R / 0.3) ** 2)), 0, 1) * f, TRACER)
    if t > 0.3:
        smoke(cv, rng, [(cv.w * (0.3 + 0.3 * t), cv.h * (0.5 - 0.1 * t))], [s * 0.2 * t], t, 702, *SMK, alpha=(1 - t) * 0.8)


def rapid_cast(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.6)
    speed = np.zeros_like(X)
    for k in range(10):
        ang = k * TAU / 10 + 0.2
        ca, sa = math.cos(ang), math.sin(ang)
        u = X * ca + Y * sa; d = -X * sa + Y * ca
        ph = (t * 2 + k * 0.37) % 1.0
        speed = np.maximum(speed, np.exp(-(d / 0.015) ** 2) * (u > 0.85 - ph * 0.7) * (u < 1.05 - ph * 0.7))
    cv.ramp(np.clip(speed, 0, 1), TRACER, cover=a)
    items = []
    for k in range(8):
        ang = k * TAU / 8 + t * TAU * 1.5
        items.append((math.sin(ang), ang))
    items.sort()
    for _, ang in items:
        x = cv.w / 2 + math.cos(ang) * s * 0.6; y = cv.h / 2 + math.sin(ang) * s * 0.25
        L, W = s * 0.16, s * 0.06
        body = tr(rot([(-L / 2, -W), (L / 2, -W), (L / 2, W), (-L / 2, W)], ang * 2), x, y)
        shaded_poly(cv, body, C('#d8a030'), C('#fff0a0'), C('#7a4a00'), '#2a1a00', 1.0)
    ring(cv, np.hypot(X, Y / 0.42), 0.6, 0.03, TRACER, alpha=a * 0.6)
    if t > 0.6:
        cv.c *= a


def pellet_proj(cv, t, rng):
    X, Y = hgrid(cv)
    sm = np.exp(-(Y / 0.4) ** 2) * smooth(-1, 0.2, X) * (X < 0.3) * (0.4 + 0.6 * N(711, 6, 3).sample(X * 2 + t, Y))
    cv.ramp(np.clip(sm * 0.6, 0, 1), [(0, C('#3a3a3a', 0)), (1, C('#a0a0a0', 0.6))])
    core = np.exp(-(((X - 0.55) / 0.22) ** 2 + (Y / 0.5) ** 2))
    cv.glow(core, C('#ff6a00', 1), 3, 1)
    cv.ramp(np.clip(core, 0, 1), TRACER)


def dust_puff(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = 1 - t
    cs = [(cv.w / 2 + math.cos(k * 2.1) * s * 0.35 * ease_out(t), cv.h / 2 + math.sin(k * 2.1) * s * 0.3 * ease_out(t)) for k in range(5)]
    smoke(cv, rng, cs, [s * 0.25 * (0.5 + t)] * 5, t, 712, *EARTH, alpha=a * 0.9, erode=t)


def cone_muzzle(cv, t, rng):
    X, Y, R, A, s = P(cv)
    f = 1 - ease_out(t, 1.5)
    Xs = X + 0.7
    Rs = np.hypot(Xs, Y); As = np.arctan2(Y, Xs)
    cone = (np.abs(As) < math.radians(30)) * np.clip(1 - Rs / (0.6 + 0.9 * ease_out(t)), 0, 1)
    n = N(713, 6, 3).sample(Rs * 2 - t, As)
    cv.glow(cone * f, C('#ff6a00', 1), 5, 1)
    cv.ramp(np.clip(cone * (0.5 + n) * f, 0, 1), FIRE)
    if t > 0.25:
        cs = [(cv.w * (0.35 + 0.15 * k * t), cv.h * (0.5 + (k - 1) * 0.15)) for k in range(3)]
        smoke(cv, rng, cs, [s * 0.18 * t] * 3, t, 714, *SMK, alpha=(1 - t) * 0.8)


def grenade_proj(cv, t, rng):
    X, Y, R, A, s = P(cv)
    ang = t * TAU
    cx, cy = cv.w / 2, cv.h / 2
    m = disc((cv.h, cv.w), cx, cy, s * 0.55)
    yy, xx = np.mgrid[0:cv.h, 0:cv.w].astype(np.float32)
    lx, ly = cx - s * 0.2, cy - s * 0.2
    sh = np.clip(1 - np.hypot(xx - lx, yy - ly) / (s * 0.8), 0, 1)
    grid_ = (np.abs(np.sin((xx * math.cos(ang) + yy * math.sin(ang)) / s * 6)) < 0.18) | (np.abs(np.sin((-xx * math.sin(ang) + yy * math.cos(ang)) / s * 6)) < 0.18)
    rgb = C('#2a4a1a')[:3] * (1 - sh[..., None]) + C('#8ab04a')[:3] * sh[..., None]
    cv.outline(m, C('#0a1406'), 1.6)
    cv.over(rgb, m)
    cv.over(C('#16260c')[:3], m * grid_ * 0.6)
    tp = (cx + math.cos(ang - math.pi / 2) * s * 0.55, cy + math.sin(ang - math.pi / 2) * s * 0.55)
    cap = tr(rot([(-s * 0.18, -s * 0.12), (s * 0.18, -s * 0.12), (s * 0.18, s * 0.12), (-s * 0.18, s * 0.12)], ang), tp[0], tp[1])
    shaded_poly(cv, cap, C('#9aa0a8'), C('#ffffff'), C('#3a3e44'), '#0a0c10', 1.0)
    ring_ = band(np.hypot((xx - tp[0] - math.cos(ang) * s * 0.3) / s, (yy - tp[1] - math.sin(ang) * s * 0.3) / s), 0.14, 0.03)
    cv.over(C('#c8ccd4')[:3], ring_)


def big_boom(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.6)
    f = pulse(t, 0, 0.3)
    cv.glow(np.exp(-(R / (0.12 + 0.3 * f)) ** 2) * f, C('#ffd060', 1), 7, 1.4)
    ring(cv, np.hypot(X, Y / 0.7), 0.25 + 0.72 * ease_out(t), 0.05 * (1 - t) + 0.01, WHITE, alpha=a)
    cs, rs = [], []
    r2 = np.random.default_rng(721)
    for k in range(14):
        ang = r2.uniform(0, TAU); d = s * r2.uniform(0.05, 0.45) * ease_out(min(1, t * 1.5))
        cs.append((cv.w / 2 + math.cos(ang) * d, cv.h / 2 + math.sin(ang) * d * 0.8 - t * s * 0.3)); rs.append(s * r2.uniform(0.12, 0.22) * (0.6 + t))
    smoke(cv, rng, cs, rs, t, 722, C('#1e1a18'), C('#6a5a50'), alpha=0.9 * a, erode=t * 0.9)
    shape = np.clip(1 - R / (0.15 + 0.45 * ease_out(min(1, t * 2.5))), 0, 1) * (1 - ease_in(t, 1.2))
    flames(cv, X, Y, shape, t, 723, FIRE, rise=1.4, gain=2.0)
    ps = Particles(np.random.default_rng(724), 14, (cv.w / 2, cv.h / 2), (110, 210), (0.4, 0.7), angle=(-math.pi, 0), size=(2, 4), gravity=260, spawn=(0, 0.05))
    pos, age, alive, _ = ps.state(t)
    for i in np.nonzero(alive)[0]:
        rock(cv, np.random.default_rng(i + 70), pos[i][0], pos[i][1], ps.size[i], t * 8 + i, C('#4a4440'), C('#a09890'), C('#141210'))


def powder_zone(cv, t, rng):
    X, Y, R, A, s = P(cv)
    n = N(731, 12, 4).sample(X, Y)
    grains = (1 - smooth(0.6, 0.95, R)) * (n > 0.5)
    cv.over(C('#1a1414')[:3], blur(grains.astype(np.float32), 0.6) * 0.75)
    heat = (1 - smooth(0.3, 0.95, R)) * (0.25 + 0.15 * math.sin(t * TAU * 2))
    cv.ramp(np.clip(heat, 0, 1), REDB, cover=0.6)
    r2 = np.random.default_rng(int(t * 12) * 13 + 5)
    for k in range(5):
        ang = r2.uniform(0, TAU); rr = math.sqrt(r2.uniform()) * 0.75
        x = cv.w / 2 + math.cos(ang) * rr * s; y = cv.h / 2 + math.sin(ang) * rr * s
        size = s * r2.uniform(0.06, 0.14)
        st = poly((cv.h, cv.w), star_poly(x, y, size, size * 0.25, 6, r2.uniform(0, 1)))
        cv.glow(st, C('#ff4a00', 1), 4, 1)
        cv.ramp(st, TRACER)
    ps = Particles(np.random.default_rng(732), 30, (cv.w / 2, cv.h / 2), (30, 90), (0.15, 0.3), size=(0.8, 1.4), spawn=(0, 1), jitter=s * 0.4, gravity=100)
    sparks(cv, ps, t, C('#fff0a0')[:3], C('#ff5a00', 0.7), streak=0.05)


def gunroll_trail(cv, t, rng):
    a = fade_tail(t, 0.4)
    X, Y = hgrid(cv)
    r2 = np.random.default_rng(741)
    cs, rs = [], []
    for k in range(9):
        u = (r2.uniform() - t * 0.8) % 1.0
        cs.append((u * cv.w, cv.h * (0.68 + r2.uniform(-0.05, 0.12)))); rs.append(cv.h * (0.1 + 0.12 * (1 - u)))
    smoke(cv, rng, cs, rs, t, 742, *EARTH, alpha=0.7 * a, erode=0.3)
    for k in range(5):
        u = (k / 5 + t * 0.6) % 1.0
        x = cv.w * (0.15 + 0.75 * u); y = cv.h * (0.5 - 0.35 * math.sin(math.pi * u))
        body = tr(rot([(-5, -2.4), (5, -2.4), (5, 2.4), (-5, 2.4)], t * 12 + k), x, y)
        shaded_poly(cv, body, C('#d8a030'), C('#fff0a0'), C('#7a4a00'), '#2a1a00', 0.9)
    if t > 0.4:
        cv.c *= a


skill('GunSpray', dict(proj_size=0.6, impact_size=0.35, muzzle_size=0.75, muzzle_dir=True),
      projectile=part(tracer_proj, 2, 96, 24, fps=12, loop=True), impact=part(ricochet, 7, 96, fps=24), muzzle=part(star_muzzle, 6, 128, fps=26))
skill('GunRapid', dict(cast_scale=1.4), cast=part(rapid_cast, 14, 224, fps=22))
skill('GunShotgun', dict(proj_size=0.5, impact_size=0.4, muzzle_size=1.1, muzzle_dir=True),
      projectile=part(pellet_proj, 4, 96, 32, fps=16, loop=True), impact=part(dust_puff, 7, 96, fps=20), muzzle=part(cone_muzzle, 7, 160, fps=24))
skill('GunGrenade', dict(proj_size=0.5, impact_scale=1.15),
      projectile=part(grenade_proj, 8, 64, fps=20, loop=True), impact=part(big_boom, 14, 224, fps=20))
skill('GunPowder', dict(cast_ground=True, cast_scale=1.05), cast=part(powder_zone, 12, 256, fps=14, loop=True))
skill('GunRoll', dict(trail_width=1.4), trail=part(gunroll_trail, 8, 320, 128, fps=20))


# ============================================================ WAND
def curse_proj(cv, t, rng):
    X, Y = hgrid(cv)
    asp = cv.w / cv.h
    n = N(801, 5, 4).sample(X * 1.5 + t, Y * 0.8)
    tail = np.exp(-(Y / (0.25 + 0.35 * (0.3 - X) / 1.3)) ** 2) * smooth(-1, 0.1, X) * (X < 0.4)
    cv.ramp(np.clip(tail * (0.3 + n * 0.8), 0, 1), GHOST, cover=0.9)
    head = np.exp(-(((X - 0.45) * asp / 0.55) ** 2 + (Y / 0.55) ** 2))
    cv.glow(head, C('#a04aff', 0.9), 5, 1)
    skull(cv, cv.w * 0.72, cv.h * 0.5, cv.h * 0.55, C('#efe0ff'), C('#2a0a4a'))


def wisp_burst(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.4)
    sw = spiral(R, A, 4, 3, t * 8, 0.3) * band(R, 0.25 + 0.5 * t, 0.2)
    cv.ramp(np.clip(sw, 0, 1), GHOST, cover=a)
    f = pulse(t, 0, 0.5)
    if f > 0:
        skull(cv, cv.w / 2, cv.h / 2 - t * s * 0.3, s * 0.45 * (0.8 + 0.4 * t), C('#f0e4ff'), C('#2a0a4a'), alpha=f)


def sigil_muzzle(cv, t, rng):
    X, Y, R, A, s = P(cv)
    f = 1 - t
    rune_circle(cv, X, Y, R, A, 0.55, t * 4, C('#e0b0ff', f), C('#7a1ad0', f), seed=21, spokes=5, alpha=f)


def hex_cast(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.6)
    k = ease_out(min(1, t * 2))
    cv.ramp(np.clip((1 - smooth(0.3, 0.9, R)) * 0.35 * k, 0, 1), HEXP, cover=a)
    rune_circle(cv, X, Y, R, A, 0.85 * k + 0.05, t * 1.5, C('#e8a0ff', a), C('#8a1ad0', a), seed=22, spokes=5, glyphs=10, alpha=a)
    n = N(811, 5, 4)
    tend = np.zeros_like(X)
    for q in range(7):
        ang = q * TAU / 7 + 0.3
        bx = math.cos(ang) * 0.6 * k; by = math.sin(ang) * 0.6 * k
        h = 0.6 * pulse(t, 0.1 + q * 0.03, 0.9)
        dx = X - bx - 0.08 * np.sin((Y - by) * 9 + t * 8 + q)
        inside = (Y < by) & (Y > by - h)
        w = 0.06 * np.clip((Y - (by - h)) / max(h, 1e-3), 0, 1)
        tend = np.maximum(tend, np.exp(-(dx / np.maximum(w, 1e-3)) ** 2) * inside)
    cv.outline(tend, C('#0a0010'), 1.0)
    cv.ramp(np.clip(tend, 0, 1), HEXP)
    f = pulse(t, 0.25, 0.85)
    if f > 0:
        cv.glow(np.exp(-(R / 0.3) ** 2), C('#b040ff', 1), 8, f)
        skull(cv, cv.w / 2, cv.h / 2 - s * 0.1, s * 0.32, C('#f4e8ff'), C('#2a0040'), alpha=f)


def miasma_zone(cv, t, rng):
    X, Y, R, A, s = P(cv)
    n1 = N(821, 4, 5).sample(X * 0.7 + t * 0.25, Y * 0.7 - t * 0.15)
    n2 = N(822, 6, 4).sample(X * 1.1 - t * 0.2, Y * 1.1 + t * 0.25)
    shape = 1 - smooth(0.5, 1.0, R)
    v = np.clip(shape * (n1 * 0.9 + n2 * 0.6) - 0.25, 0, 1)
    cv.ramp(v, MIASMA)
    r2 = np.random.default_rng(823)
    for k in range(9):
        ph = (r2.uniform() + t) % 1.0
        ang = r2.uniform(0, TAU); rr = math.sqrt(r2.uniform()) * 0.7
        x = cv.w / 2 + math.cos(ang) * rr * s; y = cv.h / 2 + math.sin(ang) * rr * s - ph * s * 0.2
        b = band(np.hypot((np.mgrid[0:cv.h, 0:cv.w][1] - x) / s, (np.mgrid[0:cv.h, 0:cv.w][0] - y) / s), 0.025 + 0.04 * ph, 0.012)
        cv.ramp(np.clip(b * (1 - ph) * 1.3, 0, 1), TOXIC)
    f = pulse(t, 0.2, 0.8)
    skull(cv, cv.w / 2 + s * 0.2, cv.h / 2 - s * 0.15 - t * s * 0.15, s * 0.22, C('#c8f08a'), C('#1a2a0a'), alpha=f * 0.45)


def flask_proj(cv, t, rng):
    X, Y, R, A, s = P(cv)
    ang = t * TAU
    cx, cy = cv.w / 2, cv.h / 2 + s * 0.1
    body = disc((cv.h, cv.w), cx, cy, s * 0.48)
    neckpts = tr(rot([(-s * 0.14, -s * 0.4), (s * 0.14, -s * 0.4), (s * 0.14, -s * 0.85), (-s * 0.14, -s * 0.85)], ang, (0, 0)), cx, cy)
    neck = poly((cv.h, cv.w), neckpts)
    m = np.maximum(body, neck)
    cv.outline(m, C('#08140a'), 1.4)
    yy, xx = np.mgrid[0:cv.h, 0:cv.w].astype(np.float32)
    liq = body * ((yy - cy) * math.cos(ang * 0.0) > -s * 0.1 + s * 0.08 * np.sin((xx - cx) / s * 6 + t * 12))
    cv.over(C('#c8e0d8')[:3], m * 0.35)
    cv.over(C('#5ad81a')[:3], liq)
    cv.add(C('#d8ff8a')[:3], liq * soft_dot((cv.h, cv.w), cx - s * 0.1, cy + s * 0.15, s * 0.25) * 0.6, 0)
    cv.add(C('#ffffff')[:3], soft_dot((cv.h, cv.w), cx - s * 0.2, cy - s * 0.15, s * 0.1) * 0.9, 0)
    cork = tr(rot([(-s * 0.12, -s * 0.85), (s * 0.12, -s * 0.85), (s * 0.1, -s * 1.0), (-s * 0.1, -s * 1.0)], ang), cx, cy)
    shaded_poly(cv, cork, C('#9a6a3a'), C('#d8a870'), C('#4a2a10'), '#1a0a04', 1.0, gloss=False)


def acid_splash(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.55)
    n = N(831, 6, 4).sample(A / TAU * 4, R)
    pud = np.clip(1 - np.hypot(X, Y / 0.7) / ((0.3 + 0.55 * ease_out(min(1, t * 2))) * (0.8 + 0.4 * n)), 0, 1) ** 0.6
    cv.ramp(np.clip(pud * 0.8, 0, 1), TOXIC, cover=a)
    r2 = np.random.default_rng(832)
    for k in range(7):
        ang = k * TAU / 7 + r2.uniform(-0.3, 0.3)
        d = s * (0.1 + 0.6 * ease_out(t))
        pts = tr(rot([(-s * 0.08, -s * 0.03), (s * 0.08, -s * 0.05), (s * 0.03, s * 0.07)], ang + t * 6), cv.w / 2 + math.cos(ang) * d, cv.h / 2 + math.sin(ang) * d + t * t * s * 0.3)
        shaded_poly(cv, pts, C('#c8e8e0'), C('#ffffff'), C('#6a8a80'), '#1a2a24', 0.8)
    ps = Particles(np.random.default_rng(833), 16, (cv.w / 2, cv.h / 2), (5, 20), (0.4, 0.7), angle=(-math.pi, 0), size=(2, 4), spawn=(0.2, 0.6), jitter=s * 0.3)
    pos, age, alive, _ = ps.state(t)
    yy, xx = np.mgrid[0:cv.h, 0:cv.w].astype(np.float32)
    for i in np.nonzero(alive)[0]:
        b = band(np.hypot(xx - pos[i][0], yy - pos[i][1]), ps.size[i] * (1 + age[i]), 0.9)
        cv.ramp(np.clip(b * (1 - age[i]) * 1.3, 0, 1), TOXIC)


def spirit_trail(cv, t, rng):
    X, Y = hgrid(cv)
    a = fade_tail(t, 0.4)
    asp = cv.w / cv.h
    for k in range(4):
        cx = -0.7 + k * 0.45
        fade = (0.3 + 0.7 * k / 3) * a
        blob = np.exp(-(((X - cx) * asp / 0.55) ** 2 + ((Y + 0.05) / 0.6) ** 2))
        n = N(841 + k, 5, 3).sample(X * 2 - t * 2, Y)
        cv.ramp(np.clip(blob * (0.4 + 0.6 * n) * 0.8, 0, 1), GHOST, cover=fade * 0.8)
    streaks(cv, X, Y, t, 845, GHOST, density=8, speed=1.2, length=0.6, thick=0.05, yspan=0.5, alpha=a)


def ghost_burst(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.5)
    for k in range(6):
        ang = k * TAU / 6 + 0.3
        d = 0.15 + 0.55 * ease_out(t)
        cx, cy = math.cos(ang) * d, math.sin(ang) * d - t * 0.2
        w = np.exp(-(((X - cx) / 0.16) ** 2 + ((Y - cy) / 0.24) ** 2))
        tail = np.exp(-(((X - cx * 0.6) / 0.08) ** 2 + ((Y - cy * 0.6) / 0.15) ** 2)) * 0.6
        cv.ramp(np.clip((w + tail) * 0.9, 0, 1), GHOST, cover=a)
        yy, xx = cv.h / 2 + cy * s, cv.w / 2 + cx * s
        eyes = np.maximum(disc((cv.h, cv.w), xx - s * 0.05, yy - s * 0.03, s * 0.025), disc((cv.h, cv.w), xx + s * 0.05, yy - s * 0.03, s * 0.025))
        cv.over(C('#2a0a4a')[:3], eyes * a)


def drain_cast(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.65)
    r2 = np.random.default_rng(851)
    for k in range(8):
        a0 = k * TAU / 8 + r2.uniform(-0.2, 0.2)
        lt = np.clip((t - k * 0.04) * 1.6, 0, 1)
        if lt <= 0 or lt >= 1:
            continue
        rr = 0.95 * (1 - ease_in(lt, 1.6))
        ang = a0 + lt * 2.5
        cx, cy = math.cos(ang) * rr, math.sin(ang) * rr
        orb = np.exp(-(((X - cx) / 0.1) ** 2 + ((Y - cy) / 0.1) ** 2))
        tail_pts = []
        for j in range(8):
            l2 = max(0, lt - j * 0.03)
            r3 = 0.95 * (1 - ease_in(l2, 1.6)); a3 = a0 + l2 * 2.5
            tail_pts.append((cv.w / 2 + math.cos(a3) * r3 * s, cv.h / 2 + math.sin(a3) * r3 * s))
        tl = stroke((cv.h, cv.w), tail_pts, [s * 0.06 * (1 - j / 8) for j in range(8)], [1 - j / 8 for j in range(8)])
        cv.ramp(np.clip(np.maximum(orb, tl * 0.8), 0, 1), SOUL, cover=a)
    core = pulse(t, 0.3, 1.0)
    cv.glow(np.exp(-(R / 0.25) ** 2), C('#ff6ad8', 1), 8, core * 1.2)
    cv.add(C('#ffe8fa')[:3], np.exp(-(R / (0.1 + 0.08 * core)) ** 2) * core)


skill('WandCurse', dict(proj_size=0.75, impact_size=0.6, muzzle_size=0.6),
      projectile=part(curse_proj, 6, 112, 64, fps=18, loop=True), impact=part(wisp_burst, 9, 128, fps=20), muzzle=part(sigil_muzzle, 6, 128, fps=22))
skill('WandHex', dict(cast_ground=True, cast_scale=1.1), cast=part(hex_cast, 14, 256, fps=20))
skill('WandMiasma', dict(cast_ground=True, cast_scale=1.1), cast=part(miasma_zone, 12, 256, fps=12, loop=True))
skill('WandVenom', dict(proj_size=0.55, impact_scale=1.1), projectile=part(flask_proj, 8, 64, fps=20, loop=True), impact=part(acid_splash, 12, 192, fps=20))
skill('WandSpirit', dict(trail_width=1.5, impact_size=1.0), trail=part(spirit_trail, 8, 320, 128, fps=20), impact=part(ghost_burst, 11, 160, fps=20))
skill('WandDrain', dict(cast_scale=1.5), cast=part(drain_cast, 14, 224, fps=20))


# ============================================================ PRIEST
def heal_cast(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.6)
    ring(cv, np.hypot(X, Y / 0.6), 0.3 + 0.6 * ease_out(min(1, t * 1.5)), 0.04, HEALG, alpha=a)
    rays(cv, R, A, 8, t * 0.5, 0.18, 0.05, 0.7, HEALG, alpha=a * 0.6)
    r2 = np.random.default_rng(901)
    for k in range(7):
        ph = np.clip((t - k * 0.06) * 1.6, 0, 1)
        if ph <= 0 or ph >= 1:
            continue
        x = cv.w / 2 + r2.uniform(-0.6, 0.6) * s
        y = cv.h / 2 + s * 0.4 - ph * s * 0.9
        cross_glyph(cv, x, y, s * 0.1 * (1 - ph * 0.3), 0, C('#f0fff4', 1 - ph * 0.7), C('#3aff8a', 1 - ph), 0.3)
    f = pulse(t, 0, 0.6)
    cv.glow(np.exp(-(R / 0.4) ** 2), C('#5affa0', 1), 8, f * 0.7)


def sanctum_zone(cv, t, rng):
    X, Y, R, A, s = P(cv)
    cv.ramp(np.clip((1 - smooth(0.4, 0.95, R)) * 0.3, 0, 1), HOLY, cover=0.3)
    rune_circle(cv, X, Y, R, A, 0.9, t * TAU, C('#fff4c0'), C('#ffc020', 0.8), seed=31, spokes=8, glyphs=16, star=True)
    rune_circle(cv, X, Y, R, A, 0.5, -t * TAU, C('#fff4c0', 0.8), C('#ffc020', 0.6), seed=32, spokes=4, glyphs=8, star=True)
    ps = Particles(np.random.default_rng(911), 24, (cv.w / 2, cv.h / 2), (8, 22), (0.5, 1.0), angle=(-math.pi * 0.6, -math.pi * 0.4), size=(1.2, 2.6), spawn=(0, 1), jitter=s * 0.4)
    dots(cv, ps, t, [(0, C('#5a4000', 0)), (0.5, C('#ffe070', 0.9)), (1, C('#ffffff'))], mode='add', shrink=0.1)


def holy_orb_proj(cv, t, rng):
    X, Y, R, A, s = P(cv)
    rays(cv, R, A, 8, t * 1.2, 0.2, 0.2, 0.95, HOLY, alpha=0.9)
    core = np.exp(-(R / 0.38) ** 2)
    cv.glow(core, C('#ffd040', 1), 6, 1)
    cv.ramp(np.clip(core * 1.2, 0, 1), HOLY)
    st = poly((cv.h, cv.w), star_poly(cv.w / 2, cv.h / 2, s * 0.45, s * 0.12, 4, t * 2))
    cv.add(C('#ffffff')[:3], st)


def holy_burst(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.55)
    f = pulse(t, 0, 0.5)
    cross_glyph(cv, cv.w / 2, cv.h / 2, s * (0.3 + 0.55 * ease_out(min(1, t * 2))), 0, C('#fffbe8', a * (0.4 + 0.6 * f)), C('#ffc020', a), 0.13)
    ring(cv, R, 0.2 + 0.75 * ease_out(t), 0.05 * (1 - t), HOLY, alpha=a)
    ps = Particles(np.random.default_rng(921), 20, (cv.w / 2, cv.h / 2), (40, 110), (0.4, 0.7), size=(1, 2), spawn=(0, 0.2))
    sparks(cv, ps, t, C('#fff7c8')[:3], C('#ffc020', 0.7), streak=0.06)


def judge_cast(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.6)
    drop = ease_in(min(1, t * 3.5), 2)
    bottom = -1.0 + 1.25 * drop
    w = 0.12 + 0.12 * pulse(t, 0.25, 0.7)
    beam = np.exp(-(X / w) ** 2) * (Y < bottom) * smooth(-1.1, -0.6, Y)
    cv.glow(beam, C('#ffd040', 1), 8, a * 1.2)
    cv.ramp(np.clip(beam * 1.1, 0, 1), HOLY, cover=a)
    if t > 0.25:
        lt = (t - 0.25) / 0.75
        Rg = np.hypot(X, (Y - 0.25) / 0.45)
        ring(cv, Rg, 0.2 + 0.75 * ease_out(lt), 0.06 * (1 - lt) + 0.01, HOLY, alpha=a)
        rays(cv, Rg, np.arctan2((Y - 0.25) / 0.45, X), 12, 0.0, 0.15, 0.05, 0.9 * ease_out(lt), HOLY, alpha=a * (1 - lt))
        cv.glow(np.exp(-(Rg / 0.25) ** 2) * (1 - lt), C('#fff0a0', 1), 8, 1.2)
    # sword of light silhouette inside the beam
    if drop > 0.05:
        tip = (bottom + 1) / 2 * cv.h
        bl = [(cv.w / 2, tip), (cv.w / 2 - s * 0.06, tip - s * 0.4), (cv.w / 2 - s * 0.06, tip - s * 0.85), (cv.w / 2 + s * 0.06, tip - s * 0.85), (cv.w / 2 + s * 0.06, tip - s * 0.4)]
        m = poly((cv.h, cv.w), bl)
        guard = poly((cv.h, cv.w), [(cv.w / 2 - s * 0.2, tip - s * 0.85), (cv.w / 2 + s * 0.2, tip - s * 0.85), (cv.w / 2 + s * 0.2, tip - s * 0.92), (cv.w / 2 - s * 0.2, tip - s * 0.92)])
        cv.add(C('#ffffff')[:3], np.maximum(m, guard) * a)


def bless_wings(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.65)
    halo = band(np.hypot(X, (Y + 0.55) / 0.3), 0.32, 0.04) * ease_out(min(1, t * 2.5))
    cv.glow(halo, C('#ffd040', 1), 4, a)
    cv.ramp(np.clip(halo, 0, 1), HOLY, cover=a)
    op = ease_out(min(1, t * 2))
    for side in (-1, 1):
        for j in range(5):
            ang = math.radians(-160 + j * 22) if side < 0 else math.radians(-20 - j * 22)
            ang = -math.pi / 2 + (ang + math.pi / 2) * op
            L = s * (0.55 - j * 0.06)
            x0 = cv.w / 2 + side * s * 0.1; y0 = cv.h / 2 - s * 0.05
            feather(cv, x0 + math.cos(ang) * L * 0.55, y0 + math.sin(ang) * L * 0.55, L, ang, C('#fff8e8'), C('#ffffff'), C('#c8b080'), '#6a5a30')
    r2 = np.random.default_rng(941)
    for k in range(5):
        ph = (r2.uniform() + t * 0.8) % 1.0
        x = cv.w / 2 + r2.uniform(-0.8, 0.8) * s; y = -s * 0.1 + ph * cv.h
        feather(cv, x, y, s * 0.14, ph * 6 + k, C('#fffaf0'), C('#ffffff'), C('#d0c090'), '#7a6a40')
    if t > 0.65:
        cv.c *= a


def revive_cast(cv, t, rng):
    X, Y, R, A, s = P(cv)
    a = fade_tail(t, 0.65)
    v = np.zeros_like(X)
    for k in range(2):
        ph = t * TAU * 1.5 + k * math.pi
        x0 = 0.45 * np.sin(Y * 5 + ph) * (0.4 + 0.6 * (Y + 1) / 2)
        v = np.maximum(v, np.exp(-((X - x0) / 0.07) ** 2) * (Y > 0.8 - 1.8 * ease_out(min(1, t * 1.5))) * (Y < 0.85))
    cv.glow(v, C('#ffe080', 1), 5, a)
    cv.ramp(np.clip(v, 0, 1), HOLY, cover=a)
    ring(cv, np.hypot(X, (Y - 0.7) / 0.35), 0.5 + 0.3 * t, 0.05, HOLY, alpha=a)
    f = pulse(t, 0.3, 1.0)
    if f > 0:
        cx, cy = cv.w / 2, cv.h / 2 - s * 0.15
        loop = band(np.hypot((X) / 0.6, (Y + 0.42) / 0.75) * 0.25, 0.25 * 0.25, 0.018)
        stem = poly((cv.h, cv.w), [(cx - s * 0.05, cy - s * 0.12), (cx + s * 0.05, cy - s * 0.12), (cx + s * 0.07, cy + s * 0.5), (cx - s * 0.07, cy + s * 0.5)])
        arm = poly((cv.h, cv.w), [(cx - s * 0.3, cy - s * 0.12), (cx + s * 0.3, cy - s * 0.12), (cx + s * 0.3, cy - s * 0.02), (cx - s * 0.3, cy - s * 0.02)])
        m = np.maximum(np.maximum(stem, arm), np.clip(loop * 1.5, 0, 1))
        cv.glow(m, C('#ffd040', 1), 8, f * 1.3)
        cv.add(C('#fffbe8')[:3], m * f)
    ps = Particles(np.random.default_rng(951), 26, (cv.w / 2, cv.h * 0.85), (30, 80), (0.5, 0.8), angle=(-math.pi * 0.75, -math.pi * 0.25), size=(1.2, 2.6), spawn=(0, 0.6), jitter=s * 0.2)
    sparks(cv, ps, t, C('#fff8d0')[:3], C('#ffc020', 0.7), streak=0.1)


skill('PriestHeal', dict(cast_scale=1.0), cast=part(heal_cast, 14, 256, fps=18))
skill('PriestSanctum', dict(cast_ground=True, cast_scale=1.05), cast=part(sanctum_zone, 12, 256, fps=12, loop=True))
skill('PriestBolt', dict(proj_size=0.65, impact_scale=1.1), projectile=part(holy_orb_proj, 6, 96, fps=18, loop=True), impact=part(holy_burst, 11, 192, fps=20))
skill('PriestJudge', dict(cast_scale=1.15), cast=part(judge_cast, 14, 256, fps=20))
skill('PriestBless', dict(cast_scale=0.6), cast=part(bless_wings, 14, 224, fps=18))
skill('PriestRevive', dict(cast_scale=0.9), cast=part(revive_cast, 16, 256, fps=18))
