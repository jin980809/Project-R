"""Reusable painted effect recipes built on lib."""
import math
import numpy as np
import cv2
from lib import *


def P(cv):
    """pixel->centered helper: returns X,Y (radius1=half canvas), R, A and pixel scale s"""
    X, Y = grid(cv.w, cv.h)
    R, A = polar(X, Y)
    return X, Y, R, A, min(cv.w, cv.h) / 2.0


_noise_cache = {}


def N(seed, base=8, octaves=4, size=256):
    k = (seed, base, octaves, size)
    if k not in _noise_cache:
        _noise_cache[k] = Noise(seed, size, octaves, base)
    return _noise_cache[k]


def slash_arc(cv, X, Y, r, head, sweep, width, stops, sign=1, streak_seed=1, cut=0.0, glow=None, sharp=1.0, alpha=1.0):
    """Crescent slash on circle radius r (centered units). head angle (rad), sweep length (rad).
    sign=+1 travels counter-clockwise in screen (y down => angle increasing)."""
    R, A = polar(X, Y)
    tail = head - sign * sweep
    d = ((A - tail) * sign) % TAU
    p = d / max(sweep, 1e-4)
    inside = (p >= 0) & (p <= 1)
    p = np.clip(p, 0, 1)
    thick = width * np.power(p, 0.65 * sharp) * np.sqrt(np.clip(1 - p ** 8, 0, 1))
    off = R - r
    # sharp outer edge, soft inner trail
    v = np.where(off > 0, np.exp(-(off / np.maximum(thick * 0.35, 1e-4)) ** 2),
                 np.exp(-(off / np.maximum(thick, 1e-4)) ** 2))
    v = v * inside * np.power(p, 1.3)
    n = N(streak_seed, base=4, octaves=3)
    streak = n.sample(R * 6.0 + 0.0, (A / TAU) * 0.15)
    v = v * (0.6 + 0.55 * streak)
    if cut > 0:
        v = v * (p > cut)
    if glow is not None:
        cv.glow(v, glow, 6, 0.9 * alpha)
    cv.ramp(np.clip(v, 0, 1), stops, cover=alpha)
    return v


def ring(cv, R, r, w, stops, noise=None, amount=0.0, alpha=1.0, mode='over'):
    v = band(R, r, w)
    if noise is not None:
        v = v * (1 - amount + amount * noise)
    cv.ramp(np.clip(v, 0, 1), stops, cover=alpha, mode=mode)
    return v


def rays(cv, R, A, count, phase, width, r0, r1, stops, alpha=1.0, seed=3, mode='add'):
    n = N(seed, base=6, octaves=2)
    k = np.cos((A + phase) * count)
    beam = np.clip((k - (1 - width)) / width, 0, 1) ** 1.5
    jitter = n.sample(A / TAU * 4, R * 0.1)
    v = beam * smooth(r0, r0 + 0.1, R) * (1 - smooth(r1 * 0.6, r1, R)) * (0.6 + 0.6 * jitter)
    cv.ramp(np.clip(v, 0, 1), stops, cover=alpha, mode=mode)
    return v


def flames(cv, X, Y, shape_mask, t, seed, stops, rise=1.6, scale=2.4, warp=0.25, gain=1.4, alpha=1.0):
    n1 = N(seed, base=5, octaves=4)
    n2 = N(seed + 7, base=7, octaves=3)
    w = n2.sample(X * 1.2 + t * 0.3, Y * 1.2 + t * rise * 0.6) - 0.5
    f = n1.sample(X * scale * 0.5 + w * warp, Y * scale * 0.5 + t * rise + w * warp)
    v = np.clip(shape_mask * (f * gain + shape_mask * 0.55 - 0.45), 0, 1)
    cv.ramp(v, stops, cover=alpha)
    return v


def smoke(cv, rng, centers, radii, t, seed, dark, light, alpha=1.0, erode=0.0):
    """billowy puffs, light from top-left"""
    shape = (cv.h, cv.w)
    m = np.zeros(shape, np.float32)
    shade = np.zeros(shape, np.float32)
    for (x, y), r in zip(centers, radii):
        if r < 0.5:
            continue
        d = soft_dot(shape, x, y, r)
        hl = soft_dot(shape, x - r * 0.35, y - r * 0.4, r * 0.7)
        m = np.maximum(m, d)
        shade = np.maximum(shade, hl * d)
    X, Y = grid(cv.w, cv.h)
    n = N(seed, base=6, octaves=4).sample(X * 0.8 + t * 0.2, Y * 0.8 - t * 0.3)
    m = np.clip(m * 1.6 - 0.35 - erode * (1 - n) * 1.2, 0, 1)
    rgb = np.asarray(dark[:3])[None, None] * (1 - shade[..., None]) + np.asarray(light[:3])[None, None] * shade[..., None]
    cv.over(rgb, m * alpha)
    return m


def shard(cv, cx, cy, length, width, ang, base, light, dark, outline='#0d2238', ow=1.4):
    pts = [(length * 0.55, 0), (0, width * 0.5), (-length * 0.45, width * 0.18), (-length * 0.45, -width * 0.18), (0, -width * 0.5)]
    pts = tr(rot(pts, ang), cx, cy)
    m = shaded_poly(cv, pts, base, light, dark, outline, ow)
    # facet line
    a = tr(rot([(length * 0.55, 0), (-length * 0.45, 0)], ang), cx, cy)
    fl = stroke((cv.h, cv.w), a, [0.6, 0.4], [0.7, 0.2]) * m
    cv.add(np.array([1, 1, 1], np.float32), fl * 0.6, 0)
    return m


def rock(cv, rng, cx, cy, r, ang, base, light, dark, outline='#20140c'):
    k = rng.integers(5, 8)
    pts = []
    for i in range(k):
        a = ang + i * TAU / k + rng.uniform(-0.25, 0.25)
        rr = r * rng.uniform(0.7, 1.05)
        pts.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr))
    return shaded_poly(cv, pts, base, light, dark, outline, 1.3)


def rune_circle(cv, X, Y, R, A, r, t, color, glowc, seed=5, spokes=6, glyphs=12, alpha=1.0, star=True):
    shape = (cv.h, cv.w)
    s = min(cv.w, cv.h) / 2
    v = band(R, r, 0.018) + band(R, r * 0.84, 0.012) * 0.8
    rng = np.random.default_rng(seed)
    # glyph ticks between rings
    ang = A + t * 0.8
    k = np.cos(ang * glyphs)
    tick = (np.abs(((ang / TAU * glyphs * 4) % 1.0) - 0.5) < 0.07) * (R > r * 0.86) * (R < r * 0.98)
    v = np.maximum(v, tick * 0.9)
    if star:
        cx, cy = cv.w / 2, cv.h / 2
        pts = []
        for i in range(spokes + 1):
            a = -t * 0.6 + i * TAU / spokes * (2 if spokes in (5, 7) else 1)
            pts.append((cx + math.cos(a) * r * 0.84 * s, cy + math.sin(a) * r * 0.84 * s))
        line = stroke(shape, pts, 1.1)
        v = np.maximum(v, line * 0.85)
    v = np.clip(v, 0, 1)
    cv.glow(v, glowc, 4, 1.0 * alpha)
    cv.add(color[:3], v * alpha * (color[3] if len(color) > 3 else 1))
    return v


def spiral(R, A, arms, twist, phase, width):
    k = np.cos(arms * A + twist * np.log(R + 0.05) - phase)
    return np.clip((k - (1 - width)) / width, 0, 1)


def leaf(cv, cx, cy, size, ang, base, light, dark):
    pts = []
    for i in range(10):
        u = i / 9
        pts.append((size * (u - 0.5), size * 0.32 * math.sin(math.pi * u)))
    for i in range(9, -1, -1):
        u = i / 9
        pts.append((size * (u - 0.5), -size * 0.32 * math.sin(math.pi * u)))
    pts = tr(rot(pts, ang), cx, cy)
    m = shaded_poly(cv, pts, base, light, dark, '#123012', 1.0, gloss=False)
    vein = tr(rot([(-size * 0.5, 0), (size * 0.45, 0)], ang), cx, cy)
    cv.over(np.asarray(dark[:3]), stroke((cv.h, cv.w), vein, 0.5) * m * 0.7)
    return m


def feather(cv, cx, cy, size, ang, base, light, dark, outline='#3a2a10'):
    pts = []
    for i in range(12):
        u = i / 11
        pts.append((size * (u - 0.5), -size * 0.22 * math.sin(math.pi * u ** 0.8)))
    for i in range(11, -1, -1):
        u = i / 11
        pts.append((size * (u - 0.5), size * 0.14 * math.sin(math.pi * u ** 0.8)))
    pts = tr(rot(pts, ang), cx, cy)
    m = shaded_poly(cv, pts, base, light, dark, outline, 1.0, gloss=False)
    q = tr(rot([(-size * 0.6, 0), (size * 0.5, 0)], ang), cx, cy)
    cv.over(np.asarray(dark[:3]), stroke((cv.h, cv.w), q, 0.5) * 0.8)
    return m


def cross_glyph(cv, cx, cy, size, ang, color, glowc, thick=0.22):
    a = size; b = size * thick
    pts = [(-b, -a), (b, -a), (b, -b), (a, -b), (a, b), (b, b), (b, a), (-b, a), (-b, b), (-a, b), (-a, -b), (-b, -b)]
    pts = tr(rot(pts, ang), cx, cy)
    m = poly((cv.h, cv.w), pts)
    cv.glow(m, glowc, size * 0.5, 1.2)
    cv.over(np.asarray(color[:3]), m * (color[3] if len(color) > 3 else 1))
    return m


def skull(cv, cx, cy, s, color, dark, alpha=1.0):
    shape = (cv.h, cv.w)
    head = disc(shape, cx, cy - s * 0.15, s * 0.55, 1.2)
    jaw = poly(shape, [(cx - s * 0.32, cy + s * 0.1), (cx + s * 0.32, cy + s * 0.1), (cx + s * 0.24, cy + s * 0.55), (cx - s * 0.24, cy + s * 0.55)])
    m = np.maximum(head, jaw)
    eyes = np.maximum(disc(shape, cx - s * 0.2, cy - s * 0.08, s * 0.15, 1), disc(shape, cx + s * 0.2, cy - s * 0.08, s * 0.15, 1))
    nose = poly(shape, [(cx, cy + s * 0.05), (cx - s * 0.07, cy + s * 0.2), (cx + s * 0.07, cy + s * 0.2)])
    teeth = np.zeros(shape, np.float32)
    for k in (-1, 0, 1):
        teeth = np.maximum(teeth, stroke(shape, [(cx + k * s * 0.12, cy + s * 0.32), (cx + k * s * 0.12, cy + s * 0.5)], 0.7))
    cv.over(np.asarray(color[:3]), m * alpha)
    cv.over(np.asarray(dark[:3]), np.maximum(np.maximum(eyes, nose), teeth) * m * alpha)
    return m


def chevrons(cv, X, Y, t, count, color, stops, spacing=0.45, w=0.06):
    # arrow-like chevrons moving along +x
    v = np.zeros_like(X)
    for i in range(count):
        x0 = ((t * 1.0 + i / count) % 1.0) * 2.2 - 1.1
        d = X - (x0 - np.abs(Y) * 0.8)
        v = np.maximum(v, np.exp(-(d / w) ** 2) * (np.abs(Y) < 0.7) * (1 - np.abs(Y) / 0.7))
    cv.ramp(np.clip(v, 0, 1), stops)
    return v


def streaks(cv, X, Y, t, seed, stops, density=10, speed=1.5, length=0.5, thick=0.03, yspan=0.8, alpha=1.0):
    """horizontal speed lines moving to -x (trail)"""
    rng = np.random.default_rng(seed)
    v = np.zeros_like(X)
    for i in range(density):
        y = rng.uniform(-yspan, yspan)
        L = length * rng.uniform(0.5, 1.2)
        x = ((rng.uniform(0, 1) - t * speed) % 1.0) * 2.6 - 1.3
        prof = np.exp(-((Y - y) / (thick * rng.uniform(0.6, 1.4))) ** 2)
        along = np.clip((X - (x - L)) / L, 0, 1) * (X < x)
        v = np.maximum(v, prof * along ** 1.5)
    cv.ramp(np.clip(v, 0, 1), stops, cover=alpha)
    return v
