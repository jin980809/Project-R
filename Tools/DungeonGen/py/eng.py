"""Shaded pixel-art renderer for dungeon assets.
Objects are painted as per-pixel (normal, material, detail) buffers, then lit with one key light,
quantized onto hand-picked colour ramps with ordered dithering, and outlined.
Screen axes: x right, y DOWN (image rows).  Normal: (nx right, ny up-screen, nz toward viewer)."""
import math
import numpy as np
from PIL import Image, ImageFilter

BAYER = (np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) + 0.5) / 16.0


def hx(s):
    s = s.lstrip('#')
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4))


def R(*cols):
    return np.array([hx(c) for c in cols], np.float32)


# colour ramps, dark -> light (hue shifted: purple shadows, warm lights)
# Every ramp is built only from the Endesga 32 palette, the same palette the game's screen shader snaps to,
# so the map, props, facilities and (after the shader) characters and effects all share one set of colours.
E = {
    'k': '#181425', 'n1': '#262b44', 'n2': '#3a4466', 'n3': '#5a6988', 'n4': '#8b9bb4', 'n5': '#c0cbdc', 'w': '#ffffff',
    'p0': '#3e2731', 'b1': '#733e39', 'b2': '#b86f50', 'b3': '#c28569', 's1': '#e4a672', 's2': '#e8b796', 's3': '#ead4aa',
    'r1': '#a22633', 'r2': '#e43b44', 'r3': '#be4a2f', 'o1': '#d77643', 'o2': '#f77622', 'y1': '#feae34', 'y2': '#fee761',
    'g0': '#193c3e', 'g1': '#265c42', 'g2': '#3e8948', 'g3': '#63c74d', 'u1': '#124e89', 'u2': '#0099db', 'u3': '#2ce8f5',
    'm1': '#68386c', 'm2': '#b55088', 'm3': '#f6757a', 'hot': '#ff0044',
}


def P(*keys):
    return R(*[E[k] for k in keys])


RAMPS = {
    'stone': P('k', 'n1', 'n2', 'n3', 'n4', 'n5'),
    'slate': P('k', 'n1', 'n2', 'n3', 'n4'),
    'brick': P('k', 'p0', 'b1', 'b2', 'b3'),
    'moss': P('k', 'g0', 'g1', 'g2', 'g3'),
    'wood': P('k', 'p0', 'b1', 'b2', 'b3', 's1'),
    'darkwood': P('k', 'p0', 'b1', 'b2'),
    'iron': P('k', 'n1', 'n2', 'n3', 'n4', 'n5', 'w'),
    'rust': P('k', 'p0', 'b1', 'r3', 'o1'),
    'gold': P('p0', 'b1', 'o2', 'y1', 'y2', 'w'),
    'bone': P('p0', 'b1', 'b3', 's2', 's3', 'w'),
    'cloth_r': P('k', 'p0', 'r1', 'r2', 'm3'),
    'cloth_p': P('k', 'n1', 'm1', 'm2', 'm3'),
    'cloth_b': P('k', 'n1', 'u1', 'u2', 'u3'),
    'cloth_g': P('k', 'g0', 'g1', 'g2', 'g3'),
    'leather': P('k', 'p0', 'b1', 'b2', 'b3'),
    'hay': P('p0', 'b1', 'b2', 's1', 'y1', 'y2'),
    'dirt': P('k', 'p0', 'b1', 'b2', 'b3'),
    'water': P('k', 'n1', 'g0', 'u1', 'u2', 'u3'),
    'coal': P('k', 'n1', 'n2'),
    'flesh_g': P('k', 'g0', 'g1', 'g2', 'g3'),
    'blood': P('k', 'p0', 'r1', 'r2'),
    'web': P('n3', 'n4', 'n5', 'w'),
    'candle': P('b1', 'b3', 's2', 's3', 'w'),
    # floors keep a narrow, dark value range so characters, monsters and effects read clearly on top
    'fstone': P('k', 'n1', 'n2', 'n3', 'n4'),
    'fslate': P('k', 'n1', 'n2', 'n3', 'n4'),
    'fbrick': P('k', 'p0', 'b1', 'b2', 'b3'),
    'fdirt': P('k', 'p0', 'b1', 'b2', 'b3'),
}
# emissive ramps (index by glow strength 0..1)
GLOW = {
    'fire': P('p0', 'r1', 'r3', 'o2', 'y1', 'y2', 'w'),
    'ember': P('p0', 'r1', 'r2', 'o2', 'y1'),
    'cyan': P('g0', 'u1', 'u2', 'u3', 'w'),
    'violet': P('n1', 'm1', 'm2', 'm3', 's3'),
    'blood': P('p0', 'r1', 'r2', 'hot', 'm3'),
    'teal': P('g0', 'g1', 'u1', 'u2', 'u3'),
    'gold': P('b1', 'o2', 'y1', 'y2', 'w'),
    'lava': P('p0', 'r1', 'r3', 'o2', 'y1', 'y2'),
    'moon': P('n1', 'n2', 'n3', 'n4', 'n5', 'w'),
    'green': P('g0', 'g1', 'g2', 'g3', 'w'),
}
LIGHT = np.array([-0.5, 0.62, 0.6])
LIGHT = LIGHT / np.linalg.norm(LIGHT)


class Noise:
    def __init__(self, seed):
        self.rng = np.random.default_rng(seed)

    def field(self, h, w, scale, octaves=3):
        out = np.zeros((h, w), np.float32)
        amp, tot = 1.0, 0.0
        for o in range(octaves):
            s = max(1, scale / (2 ** o))
            gh, gw = int(h / s) + 3, int(w / s) + 3
            g = self.rng.random((gh, gw)).astype(np.float32)
            im = Image.fromarray((g * 255).astype(np.uint8)).resize((int(gw * s), int(gh * s)), Image.BICUBIC)
            a = np.asarray(im, np.float32)[:h, :w] / 255.0
            out += a * amp
            tot += amp
            amp *= 0.5
        return out / tot


class Scene:
    """Painter's-algorithm buffers."""

    def __init__(self, w, h, seed=1):
        self.w, self.h = w, h
        self.N = np.zeros((h, w, 3), np.float32)
        self.N[..., 2] = 1
        self.M = np.full((h, w), -1, np.int16)       # material index, -1 = empty
        self.D = np.zeros((h, w), np.float32)        # detail shading offset (-1..1)
        self.E = np.zeros((h, w), np.float32)        # emissive strength 0..1
        self.EM = np.full((h, w), -1, np.int16)      # emissive ramp index
        self.A = np.zeros((h, w), np.float32)        # occlusion 0..1 (darkens)
        self.G = np.zeros((h, w), np.int32)          # object id (for inner outlines)
        self.mats = []
        self.glows = []
        self.gid = 0
        self.noise = Noise(seed)
        self.yy, self.xx = np.mgrid[0:h, 0:w].astype(np.float32)
        self.yy += 0.5
        self.xx += 0.5
        self.extra = []   # (rgba array) overlays composited after shading
        self.dither_amt = 0.0
        self.LM = np.zeros((h, w), np.float32)   # baked light (+) / darkness (-), added before quantizing
        self.TINT = None                           # optional (h,w,3) multiplier applied after quantizing

    def mat(self, name):
        if name not in self.mats:
            self.mats.append(name)
        return self.mats.index(name)

    def glow(self, name):
        if name not in self.glows:
            self.glows.append(name)
        return self.glows.index(name)

    def new_id(self):
        self.gid += 1
        return self.gid

    # ---------------------------------------------------------------- masks
    def ell(self, cx, cy, rx, ry):
        return ((self.xx - cx) / rx) ** 2 + ((self.yy - cy) / ry) ** 2 <= 1.0

    def rect(self, x0, y0, x1, y1):
        return (self.xx >= x0) & (self.xx < x1) & (self.yy >= y0) & (self.yy < y1)

    def poly(self, pts):
        from PIL import ImageDraw
        im = Image.new('L', (self.w, self.h), 0)
        ImageDraw.Draw(im).polygon([(float(x), float(y)) for x, y in pts], fill=1)
        return np.asarray(im).astype(bool)

    def line_mask(self, pts, width):
        from PIL import ImageDraw
        im = Image.new('L', (self.w, self.h), 0)
        ImageDraw.Draw(im).line([(float(x), float(y)) for x, y in pts], fill=1, width=max(1, int(round(width))), joint='curve')
        return np.asarray(im).astype(bool)

    # ---------------------------------------------------------------- painting
    def paint(self, m, mat, normal, detail=None, oid=None, ao=None):
        if oid is None:
            oid = self.new_id()
        mi = self.mat(mat)
        self.M[m] = mi
        n = np.asarray(normal, np.float32)
        if n.ndim == 1:
            self.N[m] = n / np.linalg.norm(n)
        else:
            nn = n / np.maximum(1e-6, np.linalg.norm(n, axis=-1, keepdims=True))
            self.N[m] = nn[m]
        if detail is None:
            self.D[m] = 0
        elif np.ndim(detail) == 0:
            self.D[m] = detail
        else:
            self.D[m] = detail[m]
        self.A[m] = 0 if ao is None else (ao if np.ndim(ao) == 0 else ao[m])
        self.E[m] = 0
        self.EM[m] = -1
        self.G[m] = oid
        return oid

    def emit(self, m, glow, strength):
        gi = self.glow(glow)
        self.EM[m] = gi
        if np.ndim(strength) == 0:
            self.E[m] = strength
        else:
            self.E[m] = strength[m]
        self.M[m] = -2   # emissive only
        self.G[m] = self.new_id()

    def add_detail(self, m, v):
        self.D[m] += v

    # shaped primitives -----------------------------------------------------
    def dome(self, cx, cy, rx, ry, mat, tilt=0.35, bump=0.0, bscale=4, detail=None, oid=None, ao_rim=0.0):
        m = self.ell(cx, cy, rx, ry)
        dx = (self.xx - cx) / rx
        dy = (self.yy - cy) / ry
        r2 = np.clip(dx * dx + dy * dy, 0, 1)
        nz = np.sqrt(1 - r2)
        N = np.stack([dx, -dy + tilt, nz + 0.2], -1)
        if bump:
            N = self.bumped(N, bump, bscale)
        ao = ao_rim * r2 ** 2 if ao_rim else None
        return self.paint(m, mat, N, detail, oid, ao), m

    def cyl(self, x0, x1, y0, y1, mat, ny=0.0, bump=0.0, bscale=4, detail=None, oid=None):
        """vertical cylinder seen from the front (shading across x)."""
        m = self.rect(x0, y0, x1, y1)
        cx, r = (x0 + x1) / 2, (x1 - x0) / 2
        dx = np.clip((self.xx - cx) / r, -1, 1)
        N = np.stack([dx, np.full_like(dx, ny), np.sqrt(1 - dx * dx) + 0.15], -1)
        if bump:
            N = self.bumped(N, bump, bscale)
        return self.paint(m, mat, N, detail, oid), m

    def hcyl(self, x0, x1, y0, y1, mat, bump=0.0, detail=None, oid=None):
        """horizontal cylinder (log, roll) shading across y."""
        m = self.rect(x0, y0, x1, y1)
        cy, r = (y0 + y1) / 2, (y1 - y0) / 2
        dy = np.clip((self.yy - cy) / r, -1, 1)
        N = np.stack([np.zeros_like(dy), -dy, np.sqrt(1 - dy * dy) + 0.15], -1)
        if bump:
            N = self.bumped(N, bump, 3)
        return self.paint(m, mat, N, detail, oid), m

    def box(self, x0, y0, x1, y1, top_h, mat, bevel=1.0, bump=0.0, bscale=4, detail=None, oid=None, side_mat=None):
        """3/4-view block: top face (y0..y0+top_h) and front face below it."""
        oid = oid or self.new_id()
        top = self.rect(x0, y0, x1, y0 + top_h)
        front = self.rect(x0, y0 + top_h, x1, y1)
        Nt = np.zeros((self.h, self.w, 3), np.float32)
        Nt[..., 1] = 0.75
        Nt[..., 2] = 0.65
        # bevel: edges of the top lean outward
        if bevel > 0:
            ex = np.clip((self.xx - x0) / bevel, 0, 1) - np.clip((x1 - self.xx) / bevel, 0, 1)
            Nt[..., 0] += np.where(self.xx < x0 + bevel, -0.6, np.where(self.xx > x1 - bevel, 0.6, 0))
            Nt[..., 1] += np.where(self.yy < y0 + bevel, 0.5, 0)
        Nf = np.zeros((self.h, self.w, 3), np.float32)
        Nf[..., 1] = -0.25
        Nf[..., 2] = 1.0
        if bevel > 0:
            Nf[..., 0] += np.where(self.xx < x0 + bevel, -0.7, np.where(self.xx > x1 - bevel, 0.7, 0))
        if bump:
            Nt = self.bumped(Nt, bump, bscale)
            Nf = self.bumped(Nf, bump, bscale)
        self.paint(top, mat, Nt, detail, oid)
        self.paint(front, side_mat or mat, Nf, detail, oid)
        return oid, top, front

    def flat(self, m, mat, up=0.75, bump=0.0, bscale=4, detail=None, oid=None):
        """horizontal ground-like surface."""
        N = np.zeros((self.h, self.w, 3), np.float32)
        N[..., 1] = up
        N[..., 2] = 0.66
        if bump:
            N = self.bumped(N, bump, bscale)
        return self.paint(m, mat, N, detail, oid)

    def wallface(self, m, mat, bump=0.0, bscale=4, detail=None, oid=None):
        N = np.zeros((self.h, self.w, 3), np.float32)
        N[..., 1] = -0.2
        N[..., 2] = 1.0
        if bump:
            N = self.bumped(N, bump, bscale)
        return self.paint(m, mat, N, detail, oid)

    def bumped(self, N, amount, scale):
        if getattr(self, 'smooth', True):
            return N
        f = self.noise.field(self.h, self.w, scale, 3)
        gy, gx = np.gradient(f)
        N = N.copy()
        N[..., 0] += -gx * amount * scale * 0.8
        N[..., 1] += gy * amount * scale * 0.8
        return N

    # ---------------------------------------------------------------- shade
    def render(self, outline=True, outline_col=(8, 6, 12), inner=True, ambient=0.18, key=0.95, dither=True,
               light=None, glow_halo=True, halo_alpha=0.55):
        L = LIGHT if light is None else np.asarray(light) / np.linalg.norm(light)
        h, w = self.h, self.w
        out = np.zeros((h, w, 4), np.float32)
        lam = np.clip((self.N * L).sum(-1), -1, 1)
        val = ambient + key * np.clip(lam, 0, 1) * 0.85 + 0.12 * lam + self.D * 0.22 - self.A * 0.5 + self.LM
        thr = BAYER[(np.arange(h)[:, None] % 4), (np.arange(w)[None, :] % 4)]
        for mi, name in enumerate(self.mats):
            m = self.M == mi
            if not m.any():
                continue
            ramp = RAMPS[name]
            n = len(ramp)
            f = np.clip(val[m], 0, 0.999) * n
            if dither:
                idx = np.floor(f + (thr[m] - 0.5) * self.dither_amt).astype(int)
            else:
                idx = np.floor(f).astype(int)
            idx = np.clip(idx, 0, n - 1)
            out[m, :3] = ramp[idx]
            out[m, 3] = 255
        for gi, name in enumerate(self.glows):
            m = self.EM == gi
            if not m.any():
                continue
            ramp = GLOW[name]
            n = len(ramp)
            f = np.clip(self.E[m], 0, 0.999) * n
            idx = np.clip(np.floor(f).astype(int), 0, n - 1)
            out[m, :3] = ramp[idx]
            out[m, 3] = 255
        solid = out[..., 3] > 0
        if self.TINT is not None:
            lit = self.M >= 0
            out[lit, :3] = np.clip(out[lit, :3] * self.TINT[lit], 0, 255)
        res = out.astype(np.uint8)
        if inner:
            # inner lines: darken pixels whose object id differs from the pixel above/left (and not emissive)
            G = self.G
            edge = np.zeros_like(solid)
            edge[1:, :] |= (G[1:, :] != G[:-1, :]) & solid[:-1, :]
            edge[:, 1:] |= (G[:, 1:] != G[:, :-1]) & solid[:, :-1]
            edge &= solid & (self.M >= 0)
            res[edge, :3] = (res[edge, :3].astype(np.float32) * 0.62).astype(np.uint8)
        if outline:
            d = solid.copy()
            d[1:] |= solid[:-1]
            d[:-1] |= solid[1:]
            d[:, 1:] |= solid[:, :-1]
            d[:, :-1] |= solid[:, 1:]
            o = d & ~solid
            res[o, :3] = outline_col
            res[o, 3] = 255
        img = res
        if glow_halo and (self.EM >= 0).any():
            img = add_halo(img, self, halo_alpha)
        for ov in self.extra:
            img = over(ov, img)
        return img


def add_halo(img, sc, alpha=0.55):
    """soft-but-quantized coloured halo around emissive pixels (only where the image is empty)."""
    h, w = img.shape[:2]
    acc = np.zeros((h, w, 3), np.float32)
    wsum = np.zeros((h, w), np.float32)
    for gi, name in enumerate(sc.glows):
        m = (sc.EM == gi).astype(np.float32) * sc.E
        if m.max() <= 0:
            continue
        col = GLOW[name][len(GLOW[name]) * 2 // 3]
        b = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(3)), np.float32) / 255
        acc += b[..., None] * col
        wsum += b
    a = np.clip(wsum * 2.2, 0, 1) * alpha
    thr = BAYER[(np.arange(h)[:, None] % 4), (np.arange(w)[None, :] % 4)]
    q = np.where(a > 0.3, 0.55, np.where(a > 0.12, 0.3, 0.0))
    empty = img[..., 3] == 0
    m = empty & (q > 0.05)
    col = acc / np.maximum(wsum[..., None], 1e-6)
    out = img.copy()
    out[m, :3] = np.clip(col[m], 0, 255).astype(np.uint8)
    out[m, 3] = (np.clip(q[m], 0, 1) * 255 * 0.8).astype(np.uint8)
    return out


def over(top, bottom):
    t = top.astype(np.float32) / 255
    b = bottom.astype(np.float32) / 255
    a = t[..., 3:4] + b[..., 3:4] * (1 - t[..., 3:4])
    c = (t[..., :3] * t[..., 3:4] + b[..., :3] * b[..., 3:4] * (1 - t[..., 3:4])) / np.maximum(a, 1e-6)
    return (np.concatenate([c, a], -1) * 255).round().astype(np.uint8)


def shadow_under(img, cx, cy, rx, ry, alpha=110):
    """drop an ellipse contact shadow into empty pixels (dithered)."""
    h, w = img.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w] + 0.5
    d = ((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2
    m = (d <= 1) & (img[..., 3] == 0)
    out = img.copy()
    out[m] = (24, 20, 37, alpha)
    return out


def save(img, path):
    Image.fromarray(img, 'RGBA').save(path, optimize=True)


def upscale(img, k):
    return np.repeat(np.repeat(img, k, 0), k, 1)


def sheet(imgs, k=3, bg=(24, 20, 30), pad=6):
    hs = max(i.shape[0] for i in imgs) * k
    ws = sum(i.shape[1] * k + pad for i in imgs)
    can = np.zeros((hs, ws, 4), np.uint8)
    can[..., :3] = bg
    can[..., 3] = 255
    x = 0
    for i in imgs:
        u = upscale(i, k)
        sub = can[hs - u.shape[0]:hs, x:x + u.shape[1]]
        can[hs - u.shape[0]:hs, x:x + u.shape[1]] = over(u, sub)
        x += u.shape[1] + pad
    return can
