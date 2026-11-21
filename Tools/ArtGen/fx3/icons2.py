"""Skill icons (128x128 white glyph with dark outline, tinted by the UI)."""
import math, cairo, os
from PIL import Image
O=(0.12,0.12,0.14); W=(0.97,0.97,0.97); G=(0.72,0.72,0.74)
def icon(name, draw):
    s=cairo.ImageSurface(cairo.FORMAT_ARGB32,256,256); c=cairo.Context(s); c.scale(2,2)
    c.set_line_join(cairo.LINE_JOIN_ROUND); c.set_line_cap(cairo.LINE_CAP_ROUND)
    draw(c); s.flush(); s.write_to_png('/tmp/ic.png')
    Image.open('/tmp/ic.png').resize((128,128),Image.LANCZOS).save(f'icons2/Icon_{name}.png')
def shape(c, build, col=W, lw=5):
    c.new_path(); build(c); c.set_source_rgb(*O); c.set_line_width(lw*2); c.stroke_preserve(); c.set_source_rgb(*col); c.fill(); c.new_path()
def poly(pts):
    def b(c):
        c.move_to(*pts[0]); [c.line_to(*p) for p in pts[1:]]; c.close_path()
    return b
def blade(x0,y0,x1,y1,w):
    dx,dy=x1-x0,y1-y0; L=math.hypot(dx,dy); nx,ny=-dy/L,dx/L
    return poly([(x0,y0),(x0+dx*0.6+nx*w,y0+dy*0.6+ny*w),(x1,y1),(x0+dx*0.6-nx*w*0.4,y0+dy*0.6-ny*w*0.4)])
def cres(cx,cy,r,a0,sw,th):
    def b(c):
        n=30; pts=[]
        for i in range(n+1):
            a=a0+sw*i/n; pts.append((cx+math.cos(a)*r,cy+math.sin(a)*r))
        for i in range(n,-1,-1):
            t=i/n; a=a0+sw*t; w=th*math.sin(t*math.pi)
            pts.append((cx+math.cos(a)*(r-w),cy+math.sin(a)*(r-w)))
        c.move_to(*pts[0]); [c.line_to(*p) for p in pts[1:]]; c.close_path()
    return b
def crossslash(c):
    shape(c,blade(22,22,106,106,12)); shape(c,blade(106,22,22,106,12))
def whirl(c):
    shape(c,cres(64,64,50,-2.6,2.4,18)); shape(c,cres(64,64,50,0.54,2.4,18),G)
    shape(c,lambda c:c.arc(64,64,10,0,6.3))
def sweep(c):
    shape(c,cres(40,64,58,-1.4,2.8,24)); shape(c,poly([(20,60),(70,60),(70,68),(20,68)]),G,4)
def pierce(c):
    shape(c,poly([(14,58),(84,52),(116,64),(84,76),(14,70)]))
    for y in (40,88): shape(c,poly([(16,y-2),(56,y-2),(56,y+2),(16,y+2)]),G,3)
def cleave(c):
    shape(c,cres(52,60,48,-1.7,2.6,26)); shape(c,poly([(70,104),(84,94),(98,108),(84,116)]),G,4)
def quake(c):
    shape(c,poly([(20,104),(36,58),(50,104)]),G); shape(c,poly([(46,104),(64,40),(82,104)])); shape(c,poly([(78,104),(94,64),(110,104)]),G)
    shape(c,poly([(12,104),(116,104),(116,112),(12,112)]),W,4)
def icenova(c):
    for k in range(8):
        a=k*math.pi/4; x,y=64+math.cos(a)*40,64+math.sin(a)*40
        shape(c,poly([(64+math.cos(a)*18-math.sin(a)*7,64+math.sin(a)*18+math.cos(a)*7),(x+math.cos(a)*14,y+math.sin(a)*14),(64+math.cos(a)*18+math.sin(a)*7,64+math.sin(a)*18-math.cos(a)*7)]),W if k%2 else G,4)
    shape(c,lambda c:c.arc(64,64,14,0,6.3))
def arrowrain(c):
    for x,y in ((34,30),(64,46),(94,30),(48,76),(84,74)):
        c.new_path(); c.move_to(x+6,y-24); c.line_to(x,y); c.set_source_rgb(*O); c.set_line_width(9); c.stroke()
        c.move_to(x+6,y-24); c.line_to(x,y); c.set_source_rgb(*W); c.set_line_width(4); c.stroke()
        shape(c,poly([(x-7,y-4),(x,y+9),(x+7,y-4)]),W,3)
    shape(c,lambda c:(c.save(),c.translate(64,104),c.scale(1,0.3),c.arc(0,0,46,0,6.3),c.restore()),G,4)
def hexs(c):
    c.new_path(); c.arc(64,64,50,0,6.3); c.set_source_rgb(*O); c.set_line_width(12); c.stroke_preserve(); c.set_source_rgb(*G); c.set_line_width(5); c.stroke()
    shape(c,lambda c:(c.save(),c.translate(64,58),c.scale(1,0.92),c.arc(0,0,26,0,6.3),c.restore()))
    shape(c,poly([(50,74),(78,74),(78,88),(50,88)]))
    c.set_source_rgb(*O); c.arc(54,58,7,0,6.3); c.fill(); c.arc(74,58,7,0,6.3); c.fill()
def buffpower(c):
    shape(c,poly([(64,14),(100,52),(78,52),(78,100),(50,100),(50,52),(28,52)]))
    for x in (24,104): shape(c,poly([(x-8,112),(x,84),(x+8,112)]),G,3)
def buffguard(c):
    shape(c,poly([(28,22),(100,22),(98,70),(64,110),(30,70)])); shape(c,poly([(64,30),(92,30),(90,68),(64,98)]),G,0.01)
def buffspeed(c):
    for k,y in enumerate((36,64,92)):
        shape(c,poly([(20+k*8,y-6),(96,y-6),(110,y),(96,y+6),(20+k*8,y+6)]),W if k!=1 else G,4)
def bufffocus(c):
    c.new_path(); c.arc(64,64,40,0,6.3); c.set_source_rgb(*O); c.set_line_width(14); c.stroke_preserve(); c.set_source_rgb(*W); c.set_line_width(7); c.stroke()
    for a in range(4):
        ang=a*math.pi/2; x0,y0=64+math.cos(ang)*26,64+math.sin(ang)*26; x1,y1=64+math.cos(ang)*58,64+math.sin(ang)*58
        c.new_path(); c.move_to(x0,y0); c.line_to(x1,y1); c.set_source_rgb(*O); c.set_line_width(14); c.stroke_preserve(); c.set_source_rgb(*W); c.set_line_width(7); c.stroke()
    shape(c,poly([(64,54),(74,64),(64,74),(54,64)]),G,3)
os.makedirs('icons2',exist_ok=True)
for n,fn in dict(CrossSlash=crossslash,Whirl=whirl,Sweep=sweep,Pierce=pierce,Cleave=cleave,Quake=quake,IceNova=icenova,ArrowRain=arrowrain,Hex=hexs,BuffPower=buffpower,BuffGuard=buffguard,BuffSpeed=buffspeed,BuffFocus=bufffocus).items():
    icon(n,fn)
from PIL import Image
import glob
fs=sorted(glob.glob('icons2/*.png')); s=Image.new('RGBA',(70*len(fs),70),(60,60,80,255))
for i,f in enumerate(fs): s.alpha_composite(Image.open(f).resize((64,64)),(i*70+3,3))
s.save('icons2prev.png')
