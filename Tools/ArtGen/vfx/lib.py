"""Procedural painted VFX toolkit (numpy/cv2). Canvas is premultiplied RGBA float32.
Coordinates: pixel space, origin top-left, y down. Helper `grid` gives centered coords where
radius 1 == half the canvas size.
"""
import math
import numpy as np
import cv2

TAU = math.tau


def C(hexstr, a=1.0):
    h = hexstr.lstrip('#')
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)] + [a], np.float32)


class Canvas:
    def __init__(self, w, h=None):
        h = h or w
        self.w, self.h = w, h
        self.c = np.zeros((h, w, 4), np.float32)

    # normal "over" with straight colour rgb (h,w,3)|(3,) and coverage a (h,w)
    def over(self, rgb, a):
        a = np.clip(a, 0, 1)[..., None]
        rgb = np.broadcast_to(np.asarray(rgb, np.float32)[..., :3], self.c[..., :3].shape)
        self.c[..., :3] = rgb * a + self.c[..., :3] * (1 - a)
        self.c[..., 3:] = a + self.c[..., 3:] * (1 - a)

    # additive light (bright overlaps). alpha grows too so it shows on transparent bg
    def add(self, rgb, a, alpha_gain=1.0):
        a = np.clip(a, 0, 4)[..., None]
        rgb = np.broadcast_to(np.asarray(rgb, np.float32)[..., :3], self.c[..., :3].shape)
        self.c[..., :3] = self.c[..., :3] + rgb * a
        self.c[..., 3:] = np.minimum(1, self.c[..., 3:] + a * alpha_gain)
        self.c[..., :3] = np.minimum(self.c[..., :3], self.c[..., 3:])

    # colour from a ramp applied to intensity field v, coverage from ramp alpha * cover
    def ramp(self, v, stops, cover=1.0, mode='over'):
        rgb, a = ramp(v, stops)
        if mode == 'over':
            self.over(rgb, a * cover)
        else:
            self.add(rgb, a * cover)

    def glow(self, mask, color, sigma, strength=1.0):
        g = blur(mask, sigma) * strength
        self.add(np.asarray(color)[:3], g * (np.asarray(color)[3] if len(color) > 3 else 1))

    def outline(self, mask, color, px=2.0, under=True):
        """dark rim around mask (drawn under current content)."""
        m = np.clip(mask, 0, 1)
        d = cv2.dilate(m, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (int(px * 2 + 1),) * 2))
        d = blur(d, 0.6)
        rim = np.clip(d - m, 0, 1) if not under else d
        tmp = Canvas(self.w, self.h)
        tmp.over(np.asarray(color)[:3], rim * (np.asarray(color)[3] if len(color) > 3 else 1))
        if under:
            tmp.c = composite(tmp.c, self.c)
            self.c = tmp.c
        else:
            self.c = composite(self.c, tmp.c)

    def image(self):
        a = self.c[..., 3:4]
        rgb = np.where(a > 1e-4, self.c[..., :3] / np.maximum(a, 1e-4), 0)
        out = np.concatenate([np.clip(rgb, 0, 1), np.clip(a, 0, 1)], -1)
        return (out * 255 + 0.5).astype(np.uint8)

    def paste(self, other, x=0, y=0, alpha=1.0):
        """composite another canvas (top-left at x,y)."""
        oc = other.c * alpha
        h, w = oc.shape[:2]
        x0, y0 = max(0, x), max(0, y)
        x1, y1 = min(self.w, x + w), min(self.h, y + h)
        if x1 <= x0 or y1 <= y0:
            return
        src = oc[y0 - y:y1 - y, x0 - x:x1 - x]
        dst = self.c[y0:y1, x0:x1]
        self.c[y0:y1, x0:x1] = src + dst * (1 - src[..., 3:])


def composite(top, bottom):
    return top + bottom * (1 - top[..., 3:])


def ramp(v, stops):
    """stops: list of (pos, rgba array). returns rgb (h,w,3), a (h,w)"""
    v = np.clip(v, 0, 1)
    pos = np.array([s[0] for s in stops], np.float32)
    cols = np.stack([np.asarray(s[1], np.float32) if len(s[1]) == 4 else np.append(np.asarray(s[1], np.float32), 1) for s in stops])
    out = np.empty(v.shape + (4,), np.float32)
    for ch in range(4):
        out[..., ch] = np.interp(v, pos, cols[:, ch])
    return out[..., :3], out[..., 3]


def blur(m, sigma):
    if sigma <= 0:
        return m
    k = int(sigma * 3) * 2 + 1
    return cv2.GaussianBlur(m.astype(np.float32), (k, k), sigma, borderType=cv2.BORDER_CONSTANT)


def grid(w, h=None, cx=0.5, cy=0.5):
    h = h or w
    s = min(w, h) / 2.0
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    X = (xs + 0.5 - w * cx) / s
    Y = (ys + 0.5 - h * cy) / s
    return X, Y


def polar(X, Y):
    return np.hypot(X, Y), np.arctan2(Y, X)


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0 + 1e-9), 0, 1)
    return t * t * (3 - 2 * t)


def band(R, r, w):
    return np.exp(-((R - r) / max(w, 1e-4)) ** 2)


# ------------------------------------------------------------------ noise
class Noise:
    """Tileable fbm noise sampled with cv2.remap so it can scroll/warp smoothly."""

    def __init__(self, seed, size=256, octaves=4, base=8):
        rng = np.random.default_rng(seed)
        acc = np.zeros((size, size), np.float32)
        amp, tot = 1.0, 0.0
        cells = base
        for _ in range(octaves):
            g = rng.random((cells, cells)).astype(np.float32)
            g = np.tile(g, (3, 3))
            up = cv2.resize(g, (size * 3, size * 3), interpolation=cv2.INTER_CUBIC)[size:2 * size, size:2 * size]
            acc += up * amp
            tot += amp
            amp *= 0.5
            cells *= 2
        acc /= tot
        acc = (acc - acc.min()) / (acc.max() - acc.min() + 1e-6)
        self.t = acc
        self.size = size

    def sample(self, U, V):
        """U,V in noise-tile units (1 == whole tile)."""
        U, V = np.broadcast_arrays(np.asarray(U, np.float32), np.asarray(V, np.float32))
        mx = np.ascontiguousarray(U * self.size % self.size, dtype=np.float32)
        my = np.ascontiguousarray(V * self.size % self.size, dtype=np.float32)
        return cv2.remap(self.t, mx, my, cv2.INTER_LINEAR, borderMode=cv2.BORDER_WRAP)


# ------------------------------------------------------------------ strokes
def stroke(shape, pts, widths, values=None, ss=1):
    """Soft tapered stroke. pts list[(x,y)] pixel coords, widths per point (radius px).
    values per point intensity. returns float mask (max-composited)."""
    h, w = shape
    m = np.zeros((h, w), np.float32)
    pts = np.asarray(pts, np.float32)
    widths = np.broadcast_to(np.asarray(widths, np.float32), (len(pts),))
    values = np.ones(len(pts), np.float32) if values is None else np.broadcast_to(np.asarray(values, np.float32), (len(pts),))
    # resample densely
    seg = np.hypot(*np.diff(pts, axis=0).T) if len(pts) > 1 else np.array([0.0])
    L = np.concatenate([[0], np.cumsum(seg)])
    n = max(2, int(L[-1] / 0.6) + 1)
    t = np.linspace(0, L[-1], n)
    xs = np.interp(t, L, pts[:, 0]); ys = np.interp(t, L, pts[:, 1])
    ws = np.interp(t, L, widths); vs = np.interp(t, L, values)
    for x, y, r, v in zip(xs, ys, ws, vs):
        if r <= 0.05 or v <= 0:
            continue
        x0, x1 = int(max(0, x - r - 2)), int(min(w, x + r + 3))
        y0, y1 = int(max(0, y - r - 2)), int(min(h, y + r + 3))
        if x1 <= x0 or y1 <= y0:
            continue
        yy, xx = np.mgrid[y0:y1, x0:x1]
        d = np.hypot(xx + 0.5 - x, yy + 0.5 - y)
        disc = np.clip(r + 0.5 - d, 0, 1) * v
        np.maximum(m[y0:y1, x0:x1], disc, out=m[y0:y1, x0:x1])
    return m


def poly(shape, pts, ss=4):
    h, w = shape
    big = np.zeros((h * ss, w * ss), np.uint8)
    p = (np.asarray(pts, np.float64) * ss).round().astype(np.int32)
    cv2.fillPoly(big, [p], 255, lineType=cv2.LINE_8)
    return cv2.resize(big, (w, h), interpolation=cv2.INTER_AREA).astype(np.float32) / 255.0


def disc(shape, x, y, r, soft=1.0):
    h, w = shape
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.hypot(xx + 0.5 - x, yy + 0.5 - y)
    return np.clip((r - d) / max(soft, 1e-3) + 0.5, 0, 1)


def soft_dot(shape, x, y, r):
    h, w = shape
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    d2 = ((xx + 0.5 - x) ** 2 + (yy + 0.5 - y) ** 2) / max(r * r, 1e-3)
    return np.exp(-d2 * 2.2)


def lightning(rng, p0, p1, depth=6, jag=0.28, branches=2):
    """returns list of polylines (each list of (x,y)) and their width scale"""
    def subdivide(a, b, d, j):
        pts = [np.array(a, float), np.array(b, float)]
        for _ in range(d):
            new = [pts[0]]
            for i in range(len(pts) - 1):
                p, q = pts[i], pts[i + 1]
                m = (p + q) / 2
                dv = q - p
                nrm = np.array([-dv[1], dv[0]])
                m = m + nrm * rng.uniform(-j, j)
                new += [m, q]
            pts = new
            j *= 0.55
        return pts
    main = subdivide(p0, p1, depth, jag)
    out = [(main, 1.0)]
    for _ in range(branches):
        i = rng.integers(len(main) // 4, max(len(main) // 4 + 1, 3 * len(main) // 4))
        a = main[i]
        d = np.array(p1, float) - np.array(p0, float)
        ang = math.atan2(d[1], d[0]) + rng.choice([-1, 1]) * rng.uniform(0.4, 0.9)
        L = np.hypot(*d) * rng.uniform(0.2, 0.4)
        b = a + np.array([math.cos(ang), math.sin(ang)]) * L
        out.append((subdivide(a, b, depth - 2, jag * 0.8), 0.55))
    return out


def draw_bolt(cv, rng, p0, p1, core, glowc, width=2.2, depth=6, branches=2, jag=0.28):
    shape = (cv.h, cv.w)
    m = np.zeros(shape, np.float32)
    for pts, sc in lightning(rng, p0, p1, depth, jag, branches):
        n = len(pts)
        ws = [width * sc * (0.6 + 0.4 * math.sin(math.pi * i / max(1, n - 1))) for i in range(n)]
        m = np.maximum(m, stroke(shape, pts, ws))
    cv.glow(m, glowc, width * 3.5, 1.6)
    cv.glow(m, glowc, width * 1.2, 1.2)
    cv.add(core, m)
    return m


# ------------------------------------------------------------------ particles
class Particles:
    def __init__(self, rng, n, origin, speed, life, angle=(0, TAU), size=(1, 3), gravity=0.0, drag=0.0,
                 spawn=(0, 0), jitter=0.0):
        self.n = n
        a = rng.uniform(angle[0], angle[1], n)
        sp = rng.uniform(speed[0], speed[1], n)
        self.p0 = np.array(origin, np.float32)[None, :] + rng.normal(0, jitter, (n, 2)).astype(np.float32)
        self.v = np.stack([np.cos(a) * sp, np.sin(a) * sp], 1).astype(np.float32)
        self.life = rng.uniform(life[0], life[1], n)
        self.t0 = rng.uniform(spawn[0], spawn[1], n)
        self.size = rng.uniform(size[0], size[1], n)
        self.g = gravity
        self.drag = drag
        self.seed = rng.random(n)

    def state(self, t):
        """t in [0,1] global. returns pos(n,2), age01(n), alive(n), vel(n,2)"""
        lt = (t - self.t0)
        alive = (lt >= 0) & (lt <= self.life)
        tt = np.clip(lt, 0, None)
        if self.drag > 0:
            k = (1 - np.exp(-self.drag * tt)) / self.drag
        else:
            k = tt
        pos = self.p0 + self.v * k[:, None]
        pos[:, 1] += 0.5 * self.g * tt * tt
        vel = self.v * (np.exp(-self.drag * tt)[:, None] if self.drag > 0 else 1) + np.stack([np.zeros_like(tt), self.g * tt], 1)
        age = np.clip(lt / self.life, 0, 1)
        return pos, age, alive, vel


def sparks(cv, ps, t, color, glowc=None, streak=0.06, width=1.0, fade=True):
    pos, age, alive, vel = ps.state(t)
    shape = (cv.h, cv.w)
    m = np.zeros(shape, np.float32)
    for i in np.nonzero(alive)[0]:
        p = pos[i]; v = vel[i]
        tail = p - v * streak
        val = (1 - age[i]) if fade else 1.0
        r = ps.size[i] * width * (1 - 0.5 * age[i])
        m = np.maximum(m, stroke(shape, [tail, p], [r * 0.3, r], [val * 0.3, val]))
    if glowc is not None:
        cv.glow(m, glowc, 3.0, 1.2)
    cv.add(color, m)
    return m


def dots(cv, ps, t, ramp_stops, mode='add', grow=0.0, shrink=0.6):
    pos, age, alive, _ = ps.state(t)
    shape = (cv.h, cv.w)
    v = np.zeros(shape, np.float32)
    for i in np.nonzero(alive)[0]:
        r = ps.size[i] * (1 + grow * age[i]) * (1 - shrink * age[i])
        if r < 0.3:
            continue
        v = np.maximum(v, soft_dot(shape, pos[i][0], pos[i][1], r) * (1 - age[i] ** 2))
    cv.ramp(v, ramp_stops, mode=mode)
    return v


# ------------------------------------------------------------------ shaded objects (cartoon, outlined)
def shaded_poly(cv, pts, base, light, dark, outline_col='#1a1424', ow=1.6, light_dir=(-0.6, -0.8), gloss=True):
    shape = (cv.h, cv.w)
    m = poly(shape, pts)
    if m.max() <= 0:
        return m
    # shading: distance-based bevel along light direction
    pts = np.asarray(pts, np.float32)
    cx, cy = pts.mean(0)
    yy, xx = np.mgrid[0:cv.h, 0:cv.w].astype(np.float32)
    span = max(np.ptp(pts[:, 0]), np.ptp(pts[:, 1]), 1)
    s = ((xx - cx) * light_dir[0] + (yy - cy) * light_dir[1]) / (span * 0.5)
    s = np.clip(0.5 + 0.5 * s, 0, 1)
    rgb = (np.asarray(dark[:3])[None, None] * (1 - s[..., None]) + np.asarray(light[:3])[None, None] * s[..., None])
    rgb = rgb * 0.55 + np.asarray(base[:3]) * 0.45
    oc = C(outline_col) if isinstance(outline_col, str) else outline_col
    d = cv2.dilate(m, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (int(ow * 2 + 1),) * 2))
    cv.over(oc[:3], blur(d, 0.5))
    cv.over(rgb, m)
    if gloss:
        edge = np.clip(m - cv2.erode(m, np.ones((3, 3), np.uint8)), 0, 1)
        hl = edge * np.clip(s - 0.55, 0, 1) * 2.2
        cv.add(np.array([1, 1, 1], np.float32), hl * 0.8, 0)
    return m


def rot(pts, ang, c=(0, 0)):
    ca, sa = math.cos(ang), math.sin(ang)
    out = []
    for x, y in pts:
        x -= c[0]; y -= c[1]
        out.append((x * ca - y * sa + c[0], x * sa + y * ca + c[1]))
    return out


def tr(pts, dx, dy, s=1.0):
    return [(x * s + dx, y * s + dy) for x, y in pts]


def star_poly(cx, cy, r_out, r_in, n=5, rot0=-math.pi / 2):
    pts = []
    for i in range(n * 2):
        r = r_out if i % 2 == 0 else r_in
        a = rot0 + i * math.pi / n
        pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    return pts


def ease_out(t, p=2.5):
    return 1 - (1 - np.clip(t, 0, 1)) ** p


def ease_in(t, p=2.0):
    return np.clip(t, 0, 1) ** p


def pulse(t, a, b):
    """0 before a, rises to 1 at mid, 0 after b (sin hump)"""
    x = np.clip((t - a) / (b - a), 0, 1)
    return math.sin(math.pi * x)


def fade_tail(t, start=0.6):
    return 1.0 if t < start else max(0.0, 1 - (t - start) / (1 - start))
