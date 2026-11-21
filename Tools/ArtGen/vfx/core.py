import os
import zlib
import numpy as np
from PIL import Image
from lib import Canvas
from cel import cel

SK = {}


def part(fn, frames, w, h=None, fps=20, loop=False):
    return dict(fn=fn, frames=frames, w=w, h=h or w, fps=fps, loop=loop)


def skill(name, meta=None, **parts):
    SK[name] = dict(parts=parts, meta=meta or {})


def render_part(p, seed=0):
    out = []
    n = p['frames']
    for i in range(n):
        t = i / n if p['loop'] else (i / max(1, n - 1))
        cv = Canvas(p['w'], p['h'])
        rng = np.random.default_rng(seed * 1000 + 7)
        p['fn'](cv, t, rng)
        out.append(cel(cv.image()))
    return out


def write_skill(name, root, only=None):
    d = SK[name]
    folder = os.path.join(root, name)
    os.makedirs(folder, exist_ok=True)
    for f in os.listdir(folder):
        os.remove(os.path.join(folder, f))
    meta = dict(d['meta'])
    frames_by_part = {}
    for pname, p in d['parts'].items():
        imgs = render_part(p, seed=zlib.crc32((name + pname).encode()) % 997)
        frames_by_part[pname] = imgs
        for i, im in enumerate(imgs):
            Image.fromarray(im, 'RGBA').save(os.path.join(folder, f'{pname}_{i:02d}.png'), optimize=True)
        meta.setdefault(pname + '_fps', p['fps'])
    with open(os.path.join(folder, 'meta.txt'), 'w') as fh:
        for k, v in meta.items():
            if isinstance(v, bool):
                v = 1 if v else 0
            fh.write(f'{k}={v}\n')
    return frames_by_part


def sheet(frames_by_part, bg=(40, 44, 52)):
    rows = []
    for pname, imgs in frames_by_part.items():
        h = max(im.shape[0] for im in imgs)
        row = []
        for im in imgs:
            base = np.zeros((h, im.shape[1], 3), np.float32) + np.array(bg, np.float32)
            a = im[..., 3:4] / 255.0
            pad = np.zeros((h, im.shape[1], 3), np.float32) + np.array(bg)
            pad[:im.shape[0]] = im[..., :3] * a + base[:im.shape[0]] * (1 - a)
            row.append(pad)
        rows.append(np.concatenate(row, 1))
    W = max(r.shape[1] for r in rows)
    rows = [np.pad(r, ((0, 0), (0, W - r.shape[1]), (0, 0)), constant_values=bg[0]) for r in rows]
    return Image.fromarray(np.concatenate(rows, 0).astype(np.uint8))
