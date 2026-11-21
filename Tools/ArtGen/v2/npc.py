"""Village NPCs (original chibi designs, not the player package). Facing right in 3/4; flipped in game for left.
Each NPC: Idle_00..03 and Walk_00..03, 150x200 px, pivot 8 px above the bottom (feet).
Also the speech bubble sprite (9-slice)."""
import math
import sys
import cairo
from PIL import Image
from kit import *

OUTDIR = sys.argv[1] if len(sys.argv) > 1 else 'out'
SS = 2
CW, CH = 150, 200
SKIN = [rgb('f6d2b4'), rgb('e9b994'), rgb('c98f68'), rgb('f9dcc4')]


def P(*pts):
    def b(c):
        poly(c, pts)
    return b


def limb(c, x0, y0, x1, y1, w, col, lw=3.4):
    line(c, [(x0, y0), (x1, y1)], OUT, w + lw * 2)
    line(c, [(x0, y0), (x1, y1)], col, w)


def draw_npc(c, s, pose):
    sc = s.get('scale', 1.0)
    c.save()
    c.scale(sc, sc)
    bob = pose['bob']
    # shadow
    c.new_path(); ellipse(c, 2, 0, 30, 7); c.set_source_rgba(0.14, 0.12, 0.2, 0.28); c.fill()
    pants = s.get('pants', rgb('6c6488'))
    shoe = s.get('shoes', rgb('6b4a3e'))
    skirt = s.get('skirt')
    # legs (back leg first)
    if not skirt:
        for side, (dx, lift) in ((-1, pose['legB']), (1, pose['legF'])):
            hx = side * 7 + 2
            fx = hx + dx
            col = darker(pants, 0.12) if side < 0 else pants
            limb(c, hx, -34 + bob, fx, -8 - lift, 12, col)
            shape(c, lambda c, fx=fx, lift=lift: ellipse(c, fx + 3, -6 - lift, 9, 6), shoe, lw=3)
    else:
        for side, (dx, lift) in ((-1, pose['legB']), (1, pose['legF'])):
            fx = side * 8 + 2 + dx * 0.7
            shape(c, lambda c, fx=fx, lift=lift: ellipse(c, fx + 3, -6 - lift, 9, 6), shoe, lw=3)
        sw = 28 + (4 if s.get('wide') else 0)
        shape(c, lambda c: smooth(c, [(-18, -60 + bob), (20, -60 + bob), (sw, -12), (sw * 0.3, -6), (-sw * 0.6, -6), (-sw + 4, -12)], tension=0.35),
              skirt, top=0.0, bottom=0.18, band=0.4, lw=3.6)
    body = s['body']
    bw = 24 + (8 if s.get('wide') else 0)
    by = -36 + bob
    # back arm
    ab = pose['armB']
    sx, sy = -bw * 0.55, -76 + bob
    hx, hy = sx + math.sin(ab) * 26, sy + math.cos(ab) * 26
    limb(c, sx, sy, hx, hy, 11, darker(s.get('sleeve', body), 0.12))
    shape(c, lambda c: ellipse(c, hx, hy, 6.5, 6.5), darker(s['skin'], 0.08), lw=2.6)
    # torso
    shape(c, lambda c: smooth(c, [(-bw, by), (-bw + 4, -78 + bob), (0, -84 + bob), (bw - 2, -78 + bob), (bw + 2, by), (0, by + 4)], tension=0.35),
          body, top=0.12, bottom=0.15, band=0.35, lw=3.6)
    if s.get('apron'):
        shape(c, lambda c: smooth(c, [(-bw + 8, -62 + bob), (bw - 4, -62 + bob), (bw - 2, by + 2), (-bw + 6, by + 2)], tension=0.2), s['apron'], top=0.15, lw=3)
    if s.get('belt'):
        line(c, [(-bw + 2, -44 + bob), (bw, -44 + bob)], s['belt'], 5)
    if s.get('armor'):
        shape(c, lambda c: smooth(c, [(-bw + 2, -48 + bob), (-bw + 4, -78 + bob), (0, -82 + bob), (bw - 2, -78 + bob), (bw, -48 + bob)], tension=0.3), s['armor'], top=0.3, band=0.4, lw=3.2)
        shape(c, lambda c: ellipse(c, -bw + 2, -76 + bob, 11, 8), lighter(s['armor'], 0.1), lw=3)
    # held item behind front arm
    item = s.get('item')
    # head
    hcy = -116 + bob
    hr = 33
    skin = s['skin']
    hair = s.get('hair', rgb('6b4a3e'))
    style = s.get('hair_style', 'short')
    if style == 'long':
        shape(c, lambda c: smooth(c, [(-30, hcy - 10), (-34, hcy + 30), (-14, hcy + 44), (6, hcy + 30), (10, hcy - 20)], tension=0.4), darker(hair, 0.05), lw=3.4)
    if style == 'bun':
        shape(c, lambda c: ellipse(c, -18, hcy - 30, 15, 14), hair, top=0.2, lw=3.2)
    shape(c, lambda c: ellipse(c, -27, hcy + 4, 7, 9), darker(skin, 0.05), lw=3)    # ear (back side)
    shape(c, lambda c: ellipse(c, 0, hcy, hr, hr * 0.95), skin, top=0.0, bottom=0.12, band=0.3, lw=3.8)
    # face (3/4 right): eyes on the right half
    ex = 12
    if pose['blink']:
        line(c, [(ex - 5, hcy + 2), (ex + 1, hcy + 2)], OUT, 3)
        line(c, [(ex + 13, hcy + 2), (ex + 19, hcy + 2)], OUT, 3)
    else:
        for x in (ex - 2, ex + 16):
            shape(c, lambda c, x=x: ellipse(c, x, hcy + 1, 3.6, 5), OUT, outline=False)
            c.new_path(); ellipse(c, x + 1, hcy - 1.5, 1.3, 1.6); c.set_source_rgba(1, 1, 1, 1); c.fill()
    c.new_path(); ellipse(c, ex + 7, hcy + 9, 2.2, 1.6); c.set_source_rgba(*darker(skin, 0.3), 1); c.fill()  # nose
    c.new_path(); ellipse(c, ex - 9, hcy + 12, 5, 3); c.set_source_rgba(0.95, 0.55, 0.5, 0.45); c.fill()  # cheek
    c.new_path(); ellipse(c, ex + 24, hcy + 12, 3.5, 2.6); c.set_source_rgba(0.95, 0.55, 0.5, 0.45); c.fill()
    mouth = s.get('mouth', 'smile')
    if mouth == 'smile':
        c.new_path(); c.arc(ex + 7, hcy + 14, 4.5, 0.3, math.pi - 0.3); c.set_source_rgba(*OUT, 1); c.set_line_width(2.4); c.stroke()
    if s.get('beard'):
        shape(c, lambda c: smooth(c, [(-2, hcy + 16), (30, hcy + 14), (26, hcy + 30), (12, hcy + 38), (0, hcy + 30)], tension=0.4), s['beard'], top=0.15, lw=3.2)
    if s.get('mustache'):
        shape(c, lambda c: smooth(c, [(ex - 6, hcy + 15), (ex + 7, hcy + 11), (ex + 21, hcy + 15), (ex + 7, hcy + 18)], tension=0.4), s['mustache'], lw=2.4)
    if s.get('glasses'):
        for x in (ex - 2, ex + 16):
            c.new_path(); ellipse(c, x, hcy + 1, 7.5, 7); c.set_source_rgba(*OUT, 1); c.set_line_width(2.4); c.stroke()
        line(c, [(ex + 5, hcy), (ex + 9, hcy)], OUT, 2.4)
    # hair front
    if style in ('short', 'long', 'bun', 'messy'):
        pts = [(-34, hcy + 6), (-32, hcy - 22), (-10, hcy - 36), (16, hcy - 34), (32, hcy - 16), (30, hcy - 6), (14, hcy - 18), (0, hcy - 10), (-12, hcy - 16), (-22, hcy + 2)]
        if style == 'messy':
            pts = [(-34, hcy + 6), (-36, hcy - 18), (-24, hcy - 30), (-16, hcy - 44), (-4, hcy - 34), (8, hcy - 46), (18, hcy - 32), (34, hcy - 28), (30, hcy - 8), (16, hcy - 18), (0, hcy - 10), (-12, hcy - 16), (-22, hcy + 2)]
        shape(c, lambda c: smooth(c, pts, tension=0.45), hair, top=0.2, band=0.3, lw=3.6)
    elif style == 'bald':
        c.new_path(); c.arc(-6, hcy - 14, 12, math.pi * 1.1, math.pi * 1.5); c.set_source_rgba(1, 1, 1, 0.5); c.set_line_width(4); c.stroke()
        shape(c, lambda c: smooth(c, [(-34, hcy + 8), (-33, hcy - 6), (-22, hcy - 4), (-20, hcy + 8)], tension=0.4), hair, lw=3)
    hat = s.get('hat')
    if hat == 'beret':
        shape(c, lambda c: smooth(c, [(-34, hcy - 18), (-20, hcy - 40), (14, hcy - 42), (36, hcy - 26), (20, hcy - 16), (-14, hcy - 14)], tension=0.4), s['hat_col'], top=0.2, lw=3.4)
        shape(c, lambda c: ellipse(c, -2, hcy - 42, 4, 4), darker(s['hat_col'], 0.2), lw=2)
    elif hat == 'straw':
        shape(c, lambda c: ellipse(c, 0, hcy - 22, 50, 13), rgb('e9c97a'), top=0.2, lw=3.4)
        shape(c, lambda c: smooth(c, [(-24, hcy - 22), (-20, hcy - 46), (18, hcy - 48), (24, hcy - 22)], tension=0.3), rgb('e9c97a'), top=0.25, lw=3.4)
        line(c, [(-22, hcy - 27), (22, hcy - 27)], rgb('c0705c'), 5)
    elif hat == 'chef':
        shape(c, lambda c: rrect(c, -24, hcy - 40, 48, 20, 4), rgb('f6f3ea'), lw=3.2)
        for x, r in ((-16, 15), (0, 18), (16, 15)):
            shape(c, lambda c, x=x, r=r: ellipse(c, x, hcy - 50, r, r), rgb('f6f3ea'), top=0.0, bottom=0.1, band=0.4, lw=3.2)
    elif hat == 'helmet':
        shape(c, lambda c: smooth(c, [(-36, hcy - 2), (-32, hcy - 30), (0, hcy - 42), (32, hcy - 30), (36, hcy - 2), (30, hcy - 10), (-30, hcy - 10)], tension=0.4), s['armor'], top=0.3, band=0.35, lw=3.6)
        line(c, [(0, hcy - 42), (0, hcy - 12)], darker(s['armor'], 0.3), 3)
        shape(c, lambda c: smooth(c, [(-4, hcy - 42), (-2, hcy - 62), (10, hcy - 58), (6, hcy - 40)], tension=0.4), rgb('c46a6a'), lw=3)
    elif hat == 'scarf':
        shape(c, lambda c: smooth(c, [(-36, hcy + 4), (-32, hcy - 26), (0, hcy - 38), (30, hcy - 26), (32, hcy - 8), (18, hcy - 20), (-18, hcy - 18), (-22, hcy + 10)], tension=0.4), s['hat_col'], top=0.2, lw=3.4)
        for k in range(3):
            shape(c, lambda c, k=k: ellipse(c, -16 + k * 14, hcy - 26 + k * 2, 3, 3), lighter(s['hat_col'], 0.5), outline=False)
    elif hat == 'cap':
        shape(c, lambda c: smooth(c, [(-34, hcy - 6), (-30, hcy - 30), (0, hcy - 40), (30, hcy - 28), (30, hcy - 12)], tension=0.4), s['hat_col'], top=0.2, lw=3.4)
        shape(c, lambda c: smooth(c, [(20, hcy - 16), (46, hcy - 14), (44, hcy - 8), (20, hcy - 8)], tension=0.3), darker(s['hat_col'], 0.15), lw=3)
    # front arm + held item
    af = pose['armF']
    sx, sy = bw * 0.5, -76 + bob
    hx, hy = sx + math.sin(af) * 26, sy + math.cos(af) * 26
    if item == 'spear':
        line(c, [(hx, hy + 30), (hx + 2, hy - 110)], OUT, 8); line(c, [(hx, hy + 30), (hx + 2, hy - 110)], rgb('9c6b55'), 4)
        shape(c, P((hx - 6, hy - 106), (hx + 10, hy - 106), (hx + 2, hy - 130)), rgb('cfd6de'), top=0.3, lw=2.8)
    if item == 'cane':
        line(c, [(hx + 4, hy), (hx + 10, -2)], OUT, 8); line(c, [(hx + 4, hy), (hx + 10, -2)], rgb('9c6b55'), 4)
    limb(c, sx, sy, hx, hy, 11, s.get('sleeve', body))
    if item == 'hammer':
        ang = pose.get('tool', 0)
        tx, ty = hx + math.cos(ang) * 30, hy + math.sin(ang) * 30
        line(c, [(hx, hy), (tx, ty)], OUT, 8); line(c, [(hx, hy), (tx, ty)], rgb('9c6b55'), 4)
        c.save(); c.translate(tx, ty); c.rotate(ang)
        shape(c, lambda c: rrect(c, -6, -12, 14, 24, 3), rgb('6f7380'), top=0.3, lw=3)
        c.restore()
    if item == 'book':
        shape(c, lambda c: rrect(c, hx - 4, hy - 20, 26, 30, 3), rgb('7f8fc4'), top=0.2, lw=3)
        line(c, [(hx + 2, hy - 18), (hx + 2, hy + 8)], lighter(rgb('7f8fc4'), 0.4), 2.4)
    if item == 'scissors':
        ang = pose.get('tool', 0)
        for sgn in (-1, 1):
            e = (hx + 22 * math.cos(ang + sgn * 0.25), hy - 10 + 22 * math.sin(ang + sgn * 0.25))
            line(c, [(hx, hy - 4), e], OUT, 5.5); line(c, [(hx, hy - 4), e], rgb('dfe3e8'), 2.6)
    if item == 'basket':
        shape(c, lambda c: smooth(c, [(hx - 14, hy - 2), (hx + 16, hy - 2), (hx + 12, hy + 18), (hx - 10, hy + 18)], tension=0.2), rgb('c69a6b'), top=0.2, lw=3)
        c.new_path(); c.arc(hx + 1, hy - 2, 13, math.pi, 2 * math.pi); c.set_source_rgba(*OUT, 1); c.set_line_width(3); c.stroke()
        for k, col in enumerate((rgb('e0705e'), rgb('f2b84c'))):
            shape(c, lambda c, k=k, col=col: ellipse(c, hx - 5 + k * 10, hy - 4, 6, 6), col, lw=2.2)
    if item == 'bread':
        shape(c, lambda c: ellipse(c, hx + 6, hy - 6, 16, 9), rgb('d9a35e'), top=0.3, lw=2.8)
    if item == 'apple':
        shape(c, lambda c: ellipse(c, hx + 4, hy - 6, 8, 8), rgb('e0705e'), top=0.3, lw=2.6)
    shape(c, lambda c: ellipse(c, hx, hy, 6.5, 6.5), skin, lw=2.6)
    c.restore()


def poses(kind):
    """4 idle + 4 walk poses."""
    idle = []
    for f in range(4):
        b = [0, -1.5, -2, -1][f]
        p = {'bob': b, 'legB': (0, 0), 'legF': (0, 0), 'armB': 0.1, 'armF': -0.1 + 0.05 * math.sin(f * 1.57), 'blink': f == 3}
        if kind == 'hammer':
            p['armF'] = [2.7, 2.0, 1.0, 1.8][f]
            p['tool'] = [-1.3, -0.4, 0.7, -0.2][f]
            p['bob'] = [0, -1, 1.5, 0][f]
            p['blink'] = f == 2
        if kind == 'snip':
            p['armF'] = 1.2
            p['tool'] = [-0.6, -0.2, -0.6, -0.2][f]
        if kind == 'read':
            p['armF'] = 1.35
        if kind == 'spear':
            p['armF'] = 0.3
        if kind == 'wave':
            p['armF'] = [1.9, 2.2, 1.9, 1.6][f]
        idle.append(p)
    walk = []
    for f in range(4):
        sw = [1, 0, -1, 0][f]
        p = {'bob': [0, -3, 0, -3][f], 'legB': (-9 * sw, 3 if sw < 0 else 0), 'legF': (9 * sw, 3 if sw > 0 else 0),
             'armB': 0.5 * sw, 'armF': -0.5 * sw, 'blink': False}
        if kind == 'spear':
            p['armF'] = 0.3
        if kind in ('read',):
            p['armF'] = 1.35
        if kind == 'hammer':
            p['armF'] = 0.2; p['tool'] = 1.2
        if kind == 'snip':
            p['armF'] = 0.4; p['tool'] = -0.4
        walk.append(p)
    return idle, walk


NPCS = {
    'Merchant': dict(skin=SKIN[1], body=rgb('8f72b8'), apron=rgb('efe4cf'), pants=rgb('5d5570'), hair=rgb('5a3e36'), hair_style='short',
                     mustache=rgb('5a3e36'), hat='beret', hat_col=rgb('a786d6'), wide=True, kind='wave'),
    'Librarian': dict(skin=SKIN[0], body=rgb('6f8fc4'), skirt=rgb('5f7bb0'), hair=rgb('c9c6cc'), hair_style='bun', glasses=True, item='book', kind='read'),
    'Barber': dict(skin=SKIN[3], body=rgb('f2f0ea'), pants=rgb('4f5568'), hair=rgb('3a3440'), hair_style='short', mustache=rgb('3a3440'),
                   belt=rgb('d96f6f'), item='scissors', kind='snip'),
    'Guard': dict(skin=SKIN[1], body=rgb('5d6f9a'), armor=rgb('a9b3c2'), pants=rgb('4c5470'), hair=rgb('6b4a3e'), hat='helmet', item='spear', kind='spear', mouth='flat'),
    'Smith': dict(skin=SKIN[2], body=rgb('9a6a55'), apron=rgb('6b5048'), pants=rgb('4d4448'), hair=rgb('6b4a3e'), hair_style='bald', beard=rgb('8a5a42'),
                  wide=True, item='hammer', kind='hammer', sleeve=SKIN[2]),
    'Elder': dict(skin=SKIN[0], body=rgb('8a9a6a'), pants=rgb('6b6058'), hair=rgb('e7e3e6'), hair_style='bald', beard=rgb('eeeaee'), item='cane', kind='idle'),
    'Grocer': dict(skin=SKIN[1], body=rgb('7aa36a'), skirt=rgb('6a915c'), apron=rgb('f3ead8'), hair=rgb('8a4f3a'), hat='scarf', hat_col=rgb('d9705e'),
                   item='apple', kind='wave'),
    'Baker': dict(skin=SKIN[3], body=rgb('f2ede2'), apron=rgb('e8d3b5'), pants=rgb('8d7f78'), hair=rgb('8a5a3a'), hat='chef', wide=True, item='bread', kind='idle'),
    'Villager': dict(skin=SKIN[0], body=rgb('e8c35f'), skirt=rgb('d9a84a'), hair=rgb('8a4f3a'), hair_style='long', item='basket', kind='idle'),
    'Farmer': dict(skin=SKIN[2], body=rgb('c96f5a'), pants=rgb('5d7ab0'), hair=rgb('5a3e36'), hat='straw', belt=rgb('6b4a3e'), kind='idle'),
    'Kid': dict(skin=SKIN[3], body=rgb('5fa3c9'), pants=rgb('7a6a5e'), hair=rgb('d9a84a'), hair_style='messy', hat='cap', hat_col=rgb('d96f6f'), scale=0.74, kind='idle'),
    'Youth': dict(skin=SKIN[1], body=rgb('6f9a6a'), pants=rgb('6b5a4e'), hair=rgb('3e2f2c'), hair_style='messy', belt=rgb('8d5d48'), kind='idle'),
}


def render_npc(name, spec):
    idle, walk = poses(spec.get('kind', 'idle'))
    for anim, ps in (('Idle', idle), ('Walk', walk)):
        for f, pose in enumerate(ps):
            s, c = canvas(CW * SS, CH * SS)
            c.scale(SS, SS)
            c.translate(CW / 2 - 4, CH - 8)
            draw_npc(c, spec, pose)
            s.flush()
            tmp = f'/tmp/npc_{name}_{anim}_{f}.png'
            s.write_to_png(tmp)
            Image.open(tmp).resize((CW, CH), Image.LANCZOS).save(f'{OUTDIR}/{name}_{anim}_{f:02d}.png')


def bubble():
    """Speech bubble 96x72 with a tail at the bottom-left; 9-slice borders 26 px (tail kept in the bottom border)."""
    W, H = 96, 72
    s, c = canvas(W * SS, H * SS)
    c.scale(SS, SS)

    def b(c):
        rrect(c, 4, 4, W - 8, H - 26, 16)
    c.new_path(); b(c)
    c.move_to(14, H - 24); c.line_to(12, H - 6); c.line_to(30, H - 24)
    c.set_source_rgba(*OUT, 1); c.set_line_width(7); c.stroke()
    c.new_path(); b(c); c.set_source_rgba(*rgb('fbf6ea'), 1); c.fill()
    c.new_path(); c.move_to(12, H - 26); c.line_to(12, H - 6); c.line_to(32, H - 26); c.close_path(); c.fill()
    s.flush()
    s.write_to_png('/tmp/bubble.png')
    Image.open('/tmp/bubble.png').resize((W, H), Image.LANCZOS).save(f'{OUTDIR}/Bubble.png')


if __name__ == '__main__':
    import os
    os.makedirs(OUTDIR, exist_ok=True)
    only = sys.argv[2:] or list(NPCS)
    for n in only:
        render_npc(n, NPCS[n])
    bubble()
