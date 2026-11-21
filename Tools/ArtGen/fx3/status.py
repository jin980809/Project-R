"""Status / buff overlay sprites (coloured, package style: dark outline, flat fill, one highlight). 100 px = 1 unit."""
import math, random, cairo, os
from PIL import Image
OUT='out'; os.makedirs(OUT,exist_ok=True)
def rgb(h): h=h.lstrip('#'); return tuple(int(h[i:i+2],16)/255 for i in (0,2,4))
def render(name,w,h,draw):
    S=3; s=cairo.ImageSurface(cairo.FORMAT_ARGB32,w*S,h*S); c=cairo.Context(s); c.scale(S,S)
    c.set_line_join(cairo.LINE_JOIN_ROUND); c.set_line_cap(cairo.LINE_CAP_ROUND)
    draw(c); s.flush(); s.write_to_png('/tmp/st.png')
    Image.open('/tmp/st.png').resize((w,h),Image.LANCZOS).save(f'{OUT}/{name}.png')
def smooth(c,pts,tension=0.6):
    n=len(pts); c.move_to(*pts[0])
    for i in range(n):
        p0=pts[(i-1)%n]; p1=pts[i]; p2=pts[(i+1)%n]; p3=pts[(i+2)%n]
        c1=(p1[0]+(p2[0]-p0[0])*tension/3,p1[1]+(p2[1]-p0[1])*tension/3)
        c2=(p2[0]-(p3[0]-p1[0])*tension/3,p2[1]-(p3[1]-p1[1])*tension/3)
        c.curve_to(*c1,*c2,*p2)
    c.close_path()
def shape(c,build,fill,out,lw=2.2,a=1.0):
    c.new_path(); build(c); c.set_source_rgba(*out,a); c.set_line_width(lw*2); c.stroke_preserve(); c.set_source_rgba(*fill,a); c.fill(); c.new_path()
def flat(c,build,fill,a=1.0):
    c.new_path(); build(c); c.set_source_rgba(*fill,a); c.fill(); c.new_path()
def flame_pts(x,base,h,w,sway):
    return [(x-w,base),(x-w*0.8,base-h*0.4),(x+sway*0.5-w*0.3,base-h*0.75),(x+sway,base-h),(x+w*0.4+sway*0.3,base-h*0.6),(x+w*0.9,base-h*0.3),(x+w,base)]
# ---------------------------------------------------------------- Burn (6 frames, 64x96): flames licking up the body
FO=rgb('6e1e10'); F1=rgb('f2662a'); F2=rgb('ffb43c'); F3=rgb('fff1a8')
for f in range(6):
    def draw(c,f=f):
        for k,(x,h,w) in enumerate([(18,46,9),(46,52,10),(32,66,12)]):
            ph=f*1.05+k*2.0
            hh=h*(0.85+0.15*math.sin(ph)); sw=4*math.sin(ph*1.3)
            base=90-k*4
            shape(c,lambda c,x=x,hh=hh,w=w,sw=sw,base=base: smooth(c,flame_pts(x,base,hh,w,sw)),F1,FO)
            flat(c,lambda c,x=x,hh=hh,w=w,sw=sw,base=base: smooth(c,flame_pts(x+1,base-2,hh*0.62,w*0.6,sw*0.6)),F2)
            flat(c,lambda c,x=x,hh=hh,w=w,sw=sw,base=base: smooth(c,flame_pts(x+1,base-3,hh*0.3,w*0.3,sw*0.3)),F3)
    render(f'Burn_{f:02d}',64,96,draw)
# ---------------------------------------------------------------- Bleed (6 frames, 48x64): drops dripping from a cut
BO=rgb('4a0a10'); B1=rgb('d02a34'); B2=rgb('ff7a7a')
def drop(x,y,r):
    return lambda c: smooth(c,[(x,y-r*1.9),(x+r,y),(x,y+r),(x-r,y)],0.9)
for f in range(6):
    def draw(c,f=f):
        # cut mark
        c.new_path(); c.move_to(10,14); c.line_to(36,8); c.set_source_rgb(*BO); c.set_line_width(7); c.stroke()
        c.move_to(10,14); c.line_to(36,8); c.set_source_rgb(*B1); c.set_line_width(3.5); c.stroke()
        for k,(x,ph) in enumerate([(16,0.0),(28,0.45),(22,0.75)]):
            p=(f/6+ph)%1
            y=16+p*44; r=3.2+1.2*(1-p)
            shape(c,drop(x,y,r),B1,BO,1.6,1-p*0.6)
            flat(c,lambda c,x=x,y=y,r=r:(c.arc(x-r*0.35,y-r*0.2,r*0.35,0,6.3)),B2,1-p*0.6)
    render(f'Bleed_{f:02d}',48,64,draw)
# ---------------------------------------------------------------- Slow (6 frames, 96x48 ground ring of ice + snail-ish frost)
IO=rgb('1d4e7a'); I1=rgb('7fd2ff'); I2=rgb('d8f4ff')
for f in range(6):
    def draw(c,f=f):
        c.save(); c.translate(48,30); c.scale(1,0.42)
        c.new_path(); c.arc(0,0,38,0,6.3); c.set_source_rgba(*I1,0.35); c.fill()
        c.new_path(); c.arc(0,0,38,0,6.3); c.set_source_rgba(*IO,0.9); c.set_line_width(5); c.stroke_preserve(); c.set_source_rgba(*I2,1); c.set_line_width(2.2); c.stroke()
        c.restore()
        for k in range(7):
            a=k*2*math.pi/7+f*0.12
            x=48+math.cos(a)*36; y=30+math.sin(a)*15
            hgt=10+4*math.sin(k*2.1+f*0.9)
            shape(c,lambda c,x=x,y=y,h=hgt:(c.move_to(x-4,y),c.line_to(x,y-h),c.line_to(x+4,y),c.close_path()),I1,IO,1.4)
            flat(c,lambda c,x=x,y=y,h=hgt:(c.move_to(x-3,y-1),c.line_to(x,y-h+2),c.line_to(x,y-1),c.close_path()),I2)
    render(f'Slow_{f:02d}',96,48,draw)
# ---------------------------------------------------------------- Stun (8 frames, 96x40): 3 stars orbiting
SO=rgb('6a4a08'); S1=rgb('ffd84a'); S2=rgb('fff6c0')
def star(x,y,r,rot):
    pts=[]
    for i in range(10):
        a=rot+i*math.pi/5-math.pi/2; rr=r if i%2==0 else r*0.45
        pts.append((x+math.cos(a)*rr,y+math.sin(a)*rr))
    def b(c): c.move_to(*pts[0]); [c.line_to(*p) for p in pts[1:]]; c.close_path()
    return b
for f in range(8):
    def draw(c,f=f):
        c.save(); c.translate(48,22); c.scale(1,0.35); c.new_path(); c.arc(0,0,36,0,6.3); c.restore()
        c.set_source_rgba(*SO,0.5); c.set_line_width(2); c.stroke()
        items=[]
        for k in range(3):
            a=f*2*math.pi/8+k*2*math.pi/3
            items.append((math.sin(a),48+math.cos(a)*36,22+math.sin(a)*12,a))
        items.sort()
        for depth,x,y,a in items:
            r=7+2*depth
            shape(c,star(x,y,r,a),S1,SO,1.6,0.75+0.25*depth)
            flat(c,lambda c,x=x,y=y,r=r:c.arc(x-r*0.2,y-r*0.25,r*0.25,0,6.3),S2,0.9)
    render(f'Stun_{f:02d}',96,40,draw)
# ---------------------------------------------------------------- Weaken (6 frames, 64x80): purple arrows sinking
WO=rgb('2e1048'); W1=rgb('a45cf0'); W2=rgb('e0c4ff')
def arrow_down(x,y,s):
    return lambda c:(c.move_to(x-5*s,y-12*s),c.line_to(x+5*s,y-12*s),c.line_to(x+5*s,y),c.line_to(x+10*s,y),c.line_to(x,y+11*s),c.line_to(x-10*s,y),c.line_to(x-5*s,y),c.close_path())
for f in range(6):
    def draw(c,f=f):
        for k,(x,ph,s) in enumerate([(18,0.0,0.9),(44,0.5,0.8),(31,0.25,1.1)]):
            p=(f/6+ph)%1
            y=18+p*50; a=math.sin(p*math.pi)
            shape(c,arrow_down(x,y,s),W1,WO,1.6,a)
            flat(c,lambda c,x=x,y=y,s=s:(c.rectangle(x-3*s,y-11*s,2.5*s,10*s)),W2,a)
    render(f'Weaken_{f:02d}',64,80,draw)
# ---------------------------------------------------------------- AuraPower (6 frames, 96x128): red flame aura behind body
PO=rgb('5c1408'); P1=rgb('ff5a2c'); P2=rgb('ffa046')
for f in range(6):
    def draw(c,f=f):
        pts=[]
        n=14
        for i in range(n):
            t=i/(n-1); x=10+76*t
            h=70+30*math.sin(t*math.pi)*1.0+10*math.sin(f*1.05+i*1.7)
            pts.append((x,124-h if i%2==0 else 124-h*0.7))
        poly=[(8,124)]+pts+[(88,124)]
        shape(c,lambda c:smooth(c,poly,0.5),P1,PO,2,0.55)
        inner=[(20,124)]+[(x*0.75+12,124-(124-y)*0.6) for x,y in pts]+[(78,124)]
        flat(c,lambda c:smooth(c,inner,0.5),P2,0.5)
    render(f'AuraPower_{f:02d}',96,128,draw)
# ---------------------------------------------------------------- AuraGuard (6 frames, 128x128): golden bubble with shimmer
GO=rgb('7a5a10'); G1=rgb('ffd970'); G2=rgb('fff7d6')
for f in range(6):
    def draw(c,f=f):
        cx,cy,r=64,64,58
        g=cairo.RadialGradient(cx,cy,r*0.2,cx,cy,r)
        g.add_color_stop_rgba(0,*G1,0.05); g.add_color_stop_rgba(0.85,*G1,0.22); g.add_color_stop_rgba(1,*G2,0.55)
        c.set_source(g); c.arc(cx,cy,r,0,6.3); c.fill()
        c.new_path(); c.arc(cx,cy,r,0,6.3); c.set_source_rgba(*GO,0.7); c.set_line_width(4); c.stroke_preserve(); c.set_source_rgba(*G2,0.9); c.set_line_width(2); c.stroke()
        a=-2.6+f*0.5
        c.new_path(); c.arc(cx,cy,r-7,a,a+0.7); c.set_source_rgba(*G2,0.9); c.set_line_width(4); c.stroke()
        for k in range(6):
            aa=k*1.05+f*0.3; x=cx+math.cos(aa)*r*0.7; y=cy+math.sin(aa)*r*0.7
            pts=[(x+math.cos(j*math.pi/3)*7,y+math.sin(j*math.pi/3)*7) for j in range(6)]
            c.new_path(); c.move_to(*pts[0]); [c.line_to(*p) for p in pts[1:]]; c.close_path(); c.set_source_rgba(*G2,0.45); c.set_line_width(1.5); c.stroke()
    render(f'AuraGuard_{f:02d}',128,128,draw)
# ---------------------------------------------------------------- AuraSpeed (6 frames, 96x48 ground): wind swirl at feet
AO=rgb('1a3d78'); A1=rgb('6fb2ff'); A2=rgb('e2f2ff')
for f in range(6):
    def draw(c,f=f):
        for k in range(3):
            rr=20+k*10; a0=f*1.05+k*2.1
            c.save(); c.translate(48,28); c.scale(1,0.45)
            c.new_path(); c.arc(0,0,rr,a0,a0+2.2)
            c.set_source_rgba(*AO,0.7); c.set_line_width(6); c.stroke_preserve(); c.set_source_rgba(*(A2 if k==1 else A1),0.95); c.set_line_width(2.8); c.stroke()
            c.restore()
        for k in range(3):
            x=20+k*28+((f*6)%10); y=10+k*6
            c.new_path(); c.move_to(x,y); c.line_to(x+14,y)
            c.set_source_rgba(*AO,0.6); c.set_line_width(5); c.stroke_preserve(); c.set_source_rgba(*A2,0.9); c.set_line_width(2); c.stroke()
    render(f'AuraSpeed_{f:02d}',96,48,draw)
# ---------------------------------------------------------------- AuraFocus (6 frames, 48x48): crosshair over the head
CO=rgb('6a3a08'); C1=rgb('ffb04a'); C2=rgb('fff0c8')
for f in range(6):
    def draw(c,f=f):
        rot=f*math.pi/12; r=14+1.5*math.sin(f)
        c.new_path(); c.arc(24,24,r,0,6.3); c.set_source_rgb(*CO); c.set_line_width(6); c.stroke_preserve(); c.set_source_rgb(*C1); c.set_line_width(3); c.stroke()
        for k in range(4):
            a=rot+k*math.pi/2
            x0,y0=24+math.cos(a)*(r-7),24+math.sin(a)*(r-7); x1,y1=24+math.cos(a)*(r+7),24+math.sin(a)*(r+7)
            c.new_path(); c.move_to(x0,y0); c.line_to(x1,y1); c.set_source_rgb(*CO); c.set_line_width(6); c.stroke_preserve(); c.set_source_rgb(*C2); c.set_line_width(2.6); c.stroke()
        c.new_path(); c.arc(24,24,2.5,0,6.3); c.set_source_rgb(*C2); c.fill()
    render(f'AuraFocus_{f:02d}',48,48,draw)
# ---------------------------------------------------------------- particle sprites (white, tinted by the particle colour)
def mote(c):
    g=cairo.RadialGradient(8,8,0,8,8,8); g.add_color_stop_rgba(0,1,1,1,1); g.add_color_stop_rgba(0.45,1,1,1,0.8); g.add_color_stop_rgba(1,1,1,1,0)
    c.set_source(g); c.arc(8,8,8,0,6.3); c.fill()
render('Particle_Mote',16,16,mote)
def dropw(c):
    shape(c,drop(8,10,4.2),(1,1,1),(0.55,0.55,0.55),1.0)
render('Particle_Drop',16,16,dropw)
def snow(c):
    for k in range(3):
        a=k*math.pi/3
        c.new_path(); c.move_to(8+math.cos(a)*7,8+math.sin(a)*7); c.line_to(8-math.cos(a)*7,8-math.sin(a)*7)
        c.set_source_rgb(1,1,1); c.set_line_width(2); c.stroke()
render('Particle_Snow',16,16,snow)
def spark(c):
    c.move_to(8,0);c.line_to(10,6);c.line_to(16,8);c.line_to(10,10);c.line_to(8,16);c.line_to(6,10);c.line_to(0,8);c.line_to(6,6);c.close_path()
    c.set_source_rgb(1,1,1); c.fill()
render('Particle_Spark',16,16,spark)
