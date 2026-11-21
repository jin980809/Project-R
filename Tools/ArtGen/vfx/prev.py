import sys, time, importlib
from core import SK, write_skill, sheet
mods = sys.argv[1].split(',')
for m in mods: importlib.import_module(m)
names = sys.argv[2].split(',') if len(sys.argv) > 2 and sys.argv[2] else list(SK)
for n in names:
    t0=time.time()
    fb = write_skill(n, 'out')
    im = sheet(fb)
    if im.width > 2400: im = im.resize((2400, int(im.height*2400/im.width)))
    im.save(f'prev/{n}.png')
    print(n, round(time.time()-t0,1))
