"""Adds relief to the village ground (Ground_Px.png) so it stops reading as a flat painting:
- plaza bricks get a lit top lip, a dark bottom/front face and dark right edges; the plaza itself becomes a
  raised platform with a stone front face and a cast shadow on the grass/road below it
- the dirt roads become sunken: a grass bank face along their top edge, a lit grass lip along the bottom edge,
  shaded left edges, and pebbles with highlight + shadow
- grass gets raised clumps (lit top-left, shaded bottom-right) and tufts with a shadow pixel under them
Works on the deployed image (it already contains baked building/tree shadows) using the masks from ground.py."""
import sys
import numpy as np
from PIL import Image

src = sys.argv[1] if len(sys.argv) > 1 else 'out/Ground_Px.png'
dst = sys.argv[2] if len(sys.argv) > 2 else 'out/Ground_Px_3d.png'
img = np.asarray(Image.open(src).convert('RGBA')).astype(np.float32)
road, plaza, water, cliff, terrace, fm, gm = np.load('out/masks.npy')
H, W = road.shape
rng = np.random.default_rng(21)
yy, xx = np.mgrid[0:H, 0:W]
rgb = img[..., :3]


def shift(m, dy, dx):
    out = np.zeros_like(m)
    ys = slice(max(dy, 0), H + min(dy, 0))
    yd = slice(max(-dy, 0), H + min(-dy, 0))
    xs = slice(max(dx, 0), W + min(dx, 0))
    xd = slice(max(-dx, 0), W + min(-dx, 0))
    out[ys, xs] = m[yd, xd]
    return out


def mul(m, k):
    rgb[m] = np.clip(rgb[m] * k, 0, 255)


def setc(m, c):
    rgb[m] = c


grass = ~(road | plaza | water | cliff | fm | gm) & ~terrace
grass_t = ~(road | plaza | water | cliff | fm | gm)

# ---------------------------------------------------------------- plaza: bevelled bricks on a raised platform
by = yy // 6
off = (by % 2) * 5
row = yy % 6
col = (xx + off) % 10
# (paving relief is drawn by ground.py: concentric courses, see there)
# platform rim: the bottom edge of the plaza shows a 3 px stone front face, then a cast shadow
below_out = plaza & ~shift(plaza, -1, 0)
face = np.zeros_like(plaza)
for k in range(3):
    face |= shift(below_out, k, 0) & ~plaza
face &= ~water
setc(face, (88, 86, 96))
setc(face & shift(below_out, 0, 0), (120, 116, 122))
mul(face & (((xx // 7) % 2) == 0) & shift(below_out, 2, 0), 0.85)
shadow = np.zeros_like(plaza)
for k in range(3, 7):
    shadow |= shift(below_out, k, 0)
for k in range(1, 4):
    shadow |= shift(plaza, k, k) & ~plaza
shadow &= ~plaza & ~face & ~water
mul(shadow, 0.72)
# lit lip on the top/left edge of the plaza
top_in = plaza & ~shift(plaza, 1, 0)
mul(top_in, 1.15)

# ---------------------------------------------------------------- roads: sunken dirt paths
road_only = road & ~plaza
grass_above = road_only & shift(grass_t, 1, 0)           # road pixel whose upper neighbour is grass
bank = np.zeros_like(road_only)
for k in range(3):
    bank |= shift(grass_above, k, 0) & road_only
setc(bank, (58, 104, 46))
setc(bank & shift(grass_above, 0, 0), (84, 140, 58))                                 # grass bank face (dark green)
bank_shadow = np.zeros_like(road_only)
for k in range(3, 7):
    bank_shadow |= shift(grass_above, k, 0) & road_only
mul(bank_shadow & ~bank, 0.72)
lip = grass_t & shift(road_only, -1, 0)                    # grass pixel just below a road: lit lip
mul(lip, 1.25)
mul(shift(lip, 1, 0) & grass_t, 1.1)
left_edge = road_only & shift(grass_t, 0, 1)
mul(left_edge & ~bank, 0.72)
right_edge = road_only & shift(grass_t, 0, -1)
mul(right_edge & ~bank, 1.08)
# pebbles with a highlight and a shadow
for _ in range(700):
    x = int(rng.integers(1, W - 3))
    y = int(rng.integers(1, H - 3))
    if not (road_only[y, x] and road_only[y + 1, x + 1] and road_only[y, x + 1]):
        continue
    rgb[y, x] = (226, 196, 146)
    rgb[y, x + 1] = (200, 160, 110)
    rgb[y + 1, x] = (130, 88, 56)
    rgb[y + 1, x + 1] = (110, 74, 48)
# wheel ruts on long straight stretches: darker 1-px lines inside wide road pixels
inner_road = road_only & shift(road_only, 5, 0) & shift(road_only, -5, 0)
rut = inner_road & ((yy % 13) == 0) & (rng.random((H, W)) < 0.6)
mul(rut, 0.88)

# ---------------------------------------------------------------- grass: raised clumps and shaded tufts
for _ in range(260):
    cx = rng.uniform(4, W - 4)
    cy = rng.uniform(4, H - 4)
    rx = rng.uniform(5, 11)
    ry = rx * 0.55
    y0, y1 = int(cy - ry - 2), int(cy + ry + 3)
    x0, x1 = int(cx - rx - 2), int(cx + rx + 3)
    if y0 < 0 or x0 < 0 or y1 >= H or x1 >= W:
        continue
    sub_g = grass[y0:y1, x0:x1]
    if sub_g.mean() < 0.98:
        continue
    sy, sx = np.mgrid[y0:y1, x0:x1]
    d = ((sx + 0.5 - cx) / rx) ** 2 + ((sy + 0.5 - cy) / ry) ** 2
    body = d <= 1
    lit = body & (((sx - cx) / rx + (sy - cy) / ry) < -0.35)
    dark = body & (((sx - cx) / rx + (sy - cy) / ry) > 0.45)
    shade = (((sx + 0.5 - cx - 1.5) / rx) ** 2 + ((sy + 0.5 - cy - 1.5) / ry) ** 2 <= 1) & ~body
    block = rgb[y0:y1, x0:x1]
    block[lit] = np.clip(block[lit] * 1.22, 0, 255)
    block[dark] = np.clip(block[dark] * 0.8, 0, 255)
    block[shade] = np.clip(block[shade] * 0.7, 0, 255)
# tufts: a lit blade pair with a dark shadow pixel under it
for _ in range(2200):
    x = int(rng.integers(2, W - 3))
    y = int(rng.integers(3, H - 2))
    if not (grass[y, x] and grass[y - 2, x] and grass[y + 1, x]):
        continue
    base = rgb[y, x].copy()
    rgb[y - 1, x - 1] = np.clip(base * 1.2, 0, 255)
    rgb[y - 2, x] = np.clip(base * 1.25, 0, 255)
    rgb[y - 1, x + 1] = np.clip(base * 1.12, 0, 255)
    rgb[y, x] = np.clip(base * 0.9, 0, 255)
    rgb[y + 1, x] = np.clip(base * 0.72, 0, 255)
    rgb[y + 1, x + 1] = np.clip(base * 0.8, 0, 255)

out = np.concatenate([rgb, img[..., 3:4]], -1).round().astype(np.uint8)
Image.fromarray(out, 'RGBA').save(dst, optimize=True)
print('saved', dst)
