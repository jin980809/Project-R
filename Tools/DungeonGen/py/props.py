"""Obstacles and small props (baseline 3 px above the bottom edge, like before)."""
import math
import numpy as np
from eng import Scene, save, shadow_under

OUT = 'out'


def finish(sc, path, sh=None, outline=True, **kw):
    img = sc.render(outline=outline, **kw)
    if sh:
        img = shadow_under(img, *sh)
    save(img, f'{OUT}/{path}.png')
    return img


def barrel(seed=3):
    sc = Scene(26, 34, seed)
    by = 31
    # body: bulging cylinder
    oid = sc.new_id()
    m = sc.rect(3, 7, 23, by)
    bulge = sc.rect(2, 10, 24, by - 3)
    body = m | bulge
    cx, r = 13, 11
    dx = np.clip((sc.xx - cx) / r, -1, 1)
    N = np.stack([dx, np.zeros_like(dx) - 0.05, np.sqrt(1 - dx * dx) + 0.1], -1)
    staves = ((np.floor(sc.xx) % 5) == 0).astype(np.float32) * -0.7
    grain = (sc.noise.field(34, 26, 2, 2) - 0.5) * 0.6
    sc.paint(body, 'wood', N, staves + grain, oid)
    for y in (11, by - 6):
        hm = sc.rect(2, y, 24, y + 2) & body
        sc.paint(hm, 'iron', N, 0, oid)
        sc.add_detail(sc.rect(2, y, 24, y + 1) & body, 0.4)
    # rivets
    for x in (6, 13, 20):
        for y in (11, by - 6):
            sc.add_detail(sc.rect(x, y, x + 1, y + 1), 1.2)
    # lid
    lid = sc.ell(13, 7.5, 10, 3.5)
    sc.flat(lid, 'wood', detail=((np.floor(sc.xx) % 4) == 0) * -0.6, oid=sc.new_id())
    rim = lid & ~sc.ell(13, 7.5, 8.2, 2.4)
    sc.flat(rim, 'darkwood', oid=sc.G[7, 13])
    return finish(sc, 'Prop_Barrel', (13, by, 11, 3))


def crate(seed=4):
    sc = Scene(32, 32, seed)
    by = 29
    oid, top, front = sc.box(3, 5, 29, by, 8, 'wood', bevel=1, bump=0.08, bscale=3)
    planks = ((np.floor(sc.yy - 13) % 5) == 0).astype(np.float32) * -0.8
    sc.add_detail(front, planks[front])
    sc.add_detail(top, (((np.floor(sc.xx) % 6) == 0) * -0.7)[top])
    # frame
    for m in (sc.rect(3, 13, 6, by), sc.rect(26, 13, 29, by), sc.rect(3, by - 3, 29, by), sc.rect(3, 13, 29, 15)):
        sc.wallface(m, 'darkwood', oid=sc.new_id())
    diag = sc.line_mask([(6, 15), (26, by - 3)], 2)
    sc.wallface(diag & front, 'darkwood', oid=sc.new_id())
    for x, y in ((4, 14), (27, 14), (4, by - 2), (27, by - 2)):
        sc.paint(sc.rect(x, y, x + 1, y + 1), 'iron', (0, 0.3, 1))
    return finish(sc, 'Prop_Crate', (16, by, 14, 3))


def pillar(seed=5, broken=False, name='Prop_Pillar'):
    H = 70
    sc = Scene(34, H, seed)
    by = H - 3
    # plinth
    sc.box(3, by - 9, 31, by, 4, 'stone', bevel=1, bump=0.12)
    # shaft
    top_y = 8 if not broken else 26
    _, shaft = sc.cyl(8, 26, top_y + 5, by - 9, 'stone', bump=0.18, bscale=3)
    flutes = ((np.floor(sc.xx - 8) % 4) == 0).astype(np.float32) * -0.5
    sc.add_detail(shaft, flutes[shaft])
    rings = ((np.floor(sc.yy) % 13) == 0) & shaft
    sc.add_detail(rings, -0.8)
    if not broken:
        sc.box(4, top_y, 30, top_y + 7, 3, 'stone', bevel=1, bump=0.12)
    else:
        jag = sc.poly([(8, top_y + 5), (11, top_y), (14, top_y + 3), (18, top_y - 2), (21, top_y + 2), (26, top_y + 1), (26, top_y + 8), (8, top_y + 8)])
        sc.flat(jag & ~shaft, 'stone', bump=0.3, bscale=2)
        # rubble at the foot
        for (x, y, r) in ((29, by - 2, 3.5), (5, by - 1, 2.5)):
            sc.dome(x, y, r, r * 0.75, 'stone', bump=0.2, bscale=2)
    # moss and cracks
    n = sc.noise.field(H, 34, 5, 2)
    moss = shaft & (n > 0.68)
    sc.paint(moss, 'moss', sc.N, (n - 0.7) * 2)
    crack = sc.line_mask([(17, top_y + 14), (15, top_y + 22), (18, top_y + 28), (16, top_y + 34)], 1) & shaft
    sc.add_detail(crack, -1.6)
    return finish(sc, name, (17, by, 15, 3.5))


def rock(seed=6):
    sc = Scene(40, 32, seed)
    by = 29
    sc.dome(20, 19, 17, 11, 'stone', tilt=0.4, bump=0.35, bscale=3, ao_rim=0.3)
    sc.dome(13, 16, 9, 7, 'stone', tilt=0.5, bump=0.3, bscale=2)
    sc.dome(29, 22, 7, 5, 'slate', tilt=0.3, bump=0.3, bscale=2)
    n = sc.noise.field(32, 40, 4, 2)
    top = sc.M >= 0
    sc.paint(top & (n > 0.62) & (sc.yy < 17), 'moss', sc.N, 0)
    sc.add_detail(sc.line_mask([(18, 12), (21, 18), (19, 24)], 1) & top, -1.4)
    return finish(sc, 'Prop_Rock', (20, by, 17, 3.5))


def urn(seed=7):
    sc = Scene(22, 30, seed)
    by = 27
    cx = 11
    body = sc.ell(cx, 18, 8.5, 9)
    neck = sc.rect(7, 6, 15, 12)
    dx = np.clip((sc.xx - cx) / 8.5, -1, 1)
    N = np.stack([dx, -(sc.yy - 18) / 12, np.sqrt(np.clip(1 - dx * dx, 0, 1)) + 0.1], -1)
    oid = sc.paint(body | neck, 'brick', N)
    band = (sc.rect(2, 15, 20, 17) & body)
    sc.paint(band, 'gold', N, 0, oid)
    lip = sc.ell(cx, 6, 5.5, 2)
    sc.flat(lip, 'brick', oid=sc.new_id())
    sc.flat(sc.ell(cx, 6, 3.5, 1.2), 'coal', oid=sc.new_id())
    sc.add_detail(sc.line_mask([(13, 20), (15, 24), (14, 26)], 1) & body, -1.4)
    return finish(sc, 'Prop_Urn', (11, by, 9, 2.5))


def statue(seed=8):
    """Weathered gargoyle on a plinth."""
    sc = Scene(36, 60, seed)
    by = 57
    sc.box(4, by - 12, 32, by, 5, 'stone', bevel=1, bump=0.15)
    # body
    sc.dome(18, 30, 10, 13, 'slate', tilt=0.2, bump=0.25, bscale=2)
    # wings
    for s in (-1, 1):
        w = sc.poly([(18 + s * 6, 22), (18 + s * 16, 10), (18 + s * 15, 22), (18 + s * 17, 30), (18 + s * 9, 36)])
        sc.wallface(w, 'slate', bump=0.3, bscale=2)
        sc.add_detail(sc.line_mask([(18 + s * 8, 24), (18 + s * 14, 14)], 1) & w, -1)
    sc.dome(18, 30, 9, 12, 'slate', tilt=0.2, bump=0.25, bscale=2)
    # head with horns
    sc.dome(18, 16, 6, 5.5, 'slate', tilt=0.3, bump=0.2, bscale=2)
    for s in (-1, 1):
        sc.wallface(sc.poly([(18 + s * 3, 12), (18 + s * 8, 4), (18 + s * 5, 13)]), 'bone')
    sc.emit(sc.rect(15, 15, 17, 17) | sc.rect(19, 15, 21, 17), 'blood', 0.75)
    # claws on plinth
    for x in (13, 21):
        sc.dome(x, by - 13, 3, 2, 'slate', bump=0.2)
    n = sc.noise.field(60, 36, 5, 2)
    solid = sc.M >= 0
    sc.paint(solid & (n > 0.72), 'moss', sc.N, 0)
    return finish(sc, 'Prop_Statue', (18, by, 16, 3.5))


def bones(seed=9):
    """non-colliding floor clutter (decal)."""
    sc = Scene(30, 18, seed)
    sc.dome(11, 9, 5, 4.5, 'bone', tilt=0.5, bump=0.1)
    sc.paint(sc.rect(8, 9, 10, 11) | sc.rect(12, 9, 14, 11), 'coal', (0, 0, 1))
    sc.paint(sc.rect(10, 12, 13, 14), 'bone', (0, -0.2, 1))
    for p in ([(15, 13), (26, 9)], [(17, 6), (27, 14)], [(3, 15), (9, 14)]):
        sc.paint(sc.line_mask(p, 2), 'bone', (0, 0.4, 1))
        for q in p:
            sc.dome(q[0], q[1], 1.6, 1.4, 'bone')
    return finish(sc, 'Decal_Bones', (15, 13, 13, 3))


if __name__ == '__main__':
    import os
    os.makedirs(OUT, exist_ok=True)
    from eng import sheet
    from PIL import Image
    ims = [barrel(), crate(), pillar(), pillar(broken=True, name='Prop_PillarBroken'), rock(), urn(), statue(), bones()]
    Image.fromarray(sheet(ims, 4)).save('prev_props.png')
