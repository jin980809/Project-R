import numpy as np, random, math
from px import *
from objects import BASE, OUTG, awning, door, window, roof, wall, chimney, sign
rng=random.Random(3)
def save(cv,name,oc=OUT,outline=True,sh=None):
    if outline: cv.outline(oc)
    if sh: shadow(cv,*sh)
    cv.save(f'out/{name}.png'); return cv
def blob_canopy(cv,cx,cy,r,pal,seed,lobes=7):
    L0,L1,L2,L3=pal; R=random.Random(seed)
    m=np.zeros(cv.a.shape[:2],bool)
    pts=[(cx,cy,r)]
    for i in range(lobes):
        a=i/lobes*6.283+R.random()*0.4; d=r*0.55
        pts.append((cx+math.cos(a)*d,cy+math.sin(a)*d*0.85,r*0.55+R.random()*r*0.12))
    for x,y,rr in pts: m|=cv.ell_mask(x,y,rr,rr)
    cv.mask_fill(m,L1)
    # shade lower-right
    for x,y,rr in pts:
        sm=cv.ell_mask(x+rr*0.35,y+rr*0.4,rr*0.85,rr*0.8)&m&~cv.ell_mask(x-rr*0.2,y-rr*0.25,rr*0.9,rr*0.85)
        cv.mask_fill(sm,L0)
    # highlights upper-left clusters
    for x,y,rr in pts:
        if y<cy+r*0.2:
            hm=cv.ell_mask(x-rr*0.3,y-rr*0.35,rr*0.45,rr*0.38)&m
            cv.mask_fill(hm,L2); cv.mask_fill(cv.ell_mask(x-rr*0.38,y-rr*0.45,rr*0.2,rr*0.16)&m,L3)
    # leaf texture
    yy,xx=np.where(m)
    for _ in range(len(yy)//22):
        i=R.randrange(len(yy)); y,x=yy[i],xx[i]
        if cv.a[y,x,1]==L1[1]: cv.put(x,y,L0); cv.put(x+1,y-1,L2)
    return m
LEAF=[PAL['L0'],PAL['L1'],PAL['L2'],PAL['L3']]
LEAF2=[(30,90,70),(44,124,86),(70,156,96),(110,190,120)]
AUT=[(130,70,40),(186,104,48),(222,150,64),(244,196,100)]
def tree(name,size,pal=LEAF,fruit=None,seed=1):
    W=int(size*1.15); H=int(size*1.12)
    cv=Canvas(W,H)
    cx=W/2; trunk_top=H-BASE-int(size*0.3)
    cv.rect(cx-4,trunk_top,8,H-BASE-trunk_top,PAL['WD1']); cv.rect(cx+1,trunk_top,3,H-BASE-trunk_top,PAL['WD0']); cv.rect(cx-3,trunk_top,1,H-BASE-trunk_top,PAL['WD2'])
    cv.rect(cx-6,H-BASE-2,12,2,PAL['WD1'])
    m=blob_canopy(cv,cx,size*0.48,size*0.42,pal,seed)
    if fruit:
        R=random.Random(seed+9); yy,xx=np.where(m)
        for _ in range(9):
            i=R.randrange(len(yy)); cv.rect(xx[i],yy[i],2,2,fruit); cv.put(xx[i],yy[i],(255,230,200))
    cv.outline(OUTG)
    shadow(cv,cx,H-BASE,size*0.4,4,80)
    cv.save(f'out/{name}.png')
def pine(name,size,seed=2):
    W=int(size*0.8); H=int(size*1.35); cv=Canvas(W,H); cx=W/2
    cv.rect(cx-3,H-BASE-12,6,12,PAL['WD1']); cv.rect(cx,H-BASE-12,3,12,PAL['WD0'])
    P=LEAF2
    tiers=4
    for i in range(tiers):
        t=i/(tiers-1); top=4+i*size*0.22; bot=top+size*0.38; hw=W*0.22+W*0.26*t
        cv.poly([(cx,top),(cx+hw,bot),(cx-hw,bot)],P[1])
        cv.poly([(cx+1,top+3),(cx+hw,bot),(cx+hw*0.25,bot)],P[0])
        cv.poly([(cx-1,top+4),(cx-hw*0.6,bot-3),(cx-hw*0.15,bot-3)],P[2])
        cv.hline(int(cx-hw),int(cx+hw),int(bot),P[0])
    cv.outline(OUTG); shadow(cv,cx,H-BASE,W*0.32,3,80); cv.save(f'out/{name}.png')
def bush(name,w,flowers=None,seed=4):
    cv=Canvas(w+4,int(w*0.85)); m=blob_canopy(cv,(w+4)/2,w*0.48,w*0.36,LEAF,seed,lobes=5)
    if flowers:
        R=random.Random(seed); yy,xx=np.where(m)
        for _ in range(7):
            i=R.randrange(len(yy)); x,y=xx[i],yy[i]; cv.put(x,y,flowers); cv.put(x+1,y,flowers); cv.put(x,y+1,flowers); cv.put(x+1,y+1,(255,240,200))
    cv.outline(OUTG); shadow(cv,(w+4)/2,cv.h-BASE,w*0.4,3,70); cv.save(f'out/{name}.png')
# ------------------------------------------------------------------ small props
def crate(name):
    cv=Canvas(18,20); cv.rect(1,3,16,14,PAL['WD2']); cv.rect(1,3,16,3,PAL['WD3']); cv.rect(1,3,2,14,PAL['WD1']); cv.rect(15,3,2,14,PAL['WD1']); cv.rect(1,15,16,2,PAL['WD1']); cv.line(3,6,14,15,PAL['WD1'])
    save(cv,name,sh=(9,17,9,2,60))
def barrel(name):
    cv=Canvas(16,22); cv.rect(2,2,12,16,PAL['WD2']); cv.rect(1,4,14,12,PAL['WD2']); cv.ellipse(8,3,6,2.4,PAL['WD3']); cv.ellipse(8,3,4,1.4,PAL['WD1'])
    for y in (6,13): cv.rect(1,y,14,2,PAL['S1'])
    cv.rect(10,4,3,14,PAL['WD1'])
    save(cv,name,sh=(8,19,8,2,60))
def sack(name):
    cv=Canvas(16,18); cv.ellipse(8,10,6,6,(214,190,140)); cv.rect(6,2,4,4,(214,190,140)); cv.rect(5,5,6,1,(160,130,90)); cv.ellipse(10,12,3,3,(190,164,116)); save(cv,name,sh=(8,15,7,2,60))
def pumpkin(name):
    cv=Canvas(16,14); cv.ellipse(8,8,7,5,(226,128,40)); cv.vline(5,4,12,(190,96,30)); cv.vline(11,4,12,(190,96,30)); cv.rect(7,1,2,3,(70,120,50)); cv.ellipse(6,6,2,1.5,(244,170,80)); save(cv,name,sh=(8,11,7,2,60))
def lamp(name):
    cv=Canvas(14,60); cv.rect(5,14,4,40,(52,52,64)); cv.rect(6,14,1,40,(96,96,110)); cv.rect(3,52,8,4,(52,52,64))
    cv.rect(2,4,10,11,(52,52,64)); cv.rect(4,6,6,7,PAL['WIN']); cv.rect(4,6,3,3,PAL['WIN2']); cv.rect(1,2,12,2,(52,52,64)); cv.rect(5,0,4,2,(52,52,64))
    save(cv,name,sh=(7,57,6,2,60))
def bench(name):
    cv=Canvas(42,20); cv.rect(2,4,38,4,PAL['WD2']); cv.rect(2,4,38,1,PAL['WD3']); cv.rect(2,10,38,4,PAL['WD2']); cv.rect(2,10,38,1,PAL['WD3']); cv.rect(2,13,38,1,PAL['WD1'])
    for x in (4,36): cv.rect(x,8,3,9,PAL['WD0'])
    save(cv,name,sh=(21,17,19,2,60))
def board(name):
    cv=Canvas(38,40); 
    for x in (4,32): cv.rect(x,6,3,32,PAL['WD0'])
    cv.rect(2,6,34,22,PAL['WD1']); cv.rect(4,8,30,18,PAL['WD2'])
    for (x,y,c) in [(6,10,(244,236,214)),(17,9,(240,220,170)),(26,11,(244,236,214)),(9,18,(230,210,200)),(21,18,(244,236,214))]:
        cv.rect(x,y,8,7,c); cv.put(x+3,y,(200,60,60)); cv.hline(x+1,x+6,y+3,(150,140,130)); cv.hline(x+1,x+5,y+5,(150,140,130))
    cv.rect(0,3,38,4,PAL['WD0']); cv.rect(1,2,36,2,ROOFS['red'][1])
    save(cv,name,sh=(19,37,16,2,60))
def signpost(name):
    cv=Canvas(30,40); cv.rect(13,6,4,32,PAL['WD1']); cv.poly([(2,8),(24,8),(28,12),(24,16),(2,16)],PAL['WD2']); cv.poly([(6,18),(28,18),(28,26),(6,26),(2,22)],PAL['WD2'])
    cv.hline(5,22,12,PAL['WD0']); cv.hline(9,25,22,PAL['WD0'])
    save(cv,name,sh=(15,37,8,2,60))
def well(name):
    cv=Canvas(48,54); 
    for x in (6,38): cv.rect(x,10,4,30,PAL['WD0'])
    cv.poly([(0,14),(24,0),(48,14),(44,16),(24,4),(4,16)],ROOFS['brown'][1]); cv.poly([(4,14),(24,3),(44,14)],ROOFS['brown'][2])
    cv.rect(8,20,32,2,PAL['WD1']); cv.rect(23,22,2,8,(90,90,90)); cv.rect(21,28,6,5,PAL['WD2'])
    cv.ellipse(24,40,20,8,PAL['S1']); cv.ellipse(24,38,16,5,PAL['S0']); cv.ellipse(24,39,13,3.5,PAL['W0'])
    cv.rect(4,40,40,8,PAL['S1']); cv.ellipse(24,48,20,4,PAL['S1'])
    for x in range(6,44,7): cv.vline(x,40,50,PAL['S0'])
    cv.hline(4,43,44,PAL['S0']); cv.rect(4,40,40,1,PAL['S2'])
    save(cv,name,sh=(24,50,22,3,70))
def cart(name):
    cv=Canvas(54,34); cv.rect(4,8,40,14,PAL['WD2']); cv.rect(4,8,40,3,PAL['WD3']); cv.rect(44,16,10,2,PAL['WD1'])
    for (x,y,c) in [(10,6,(220,60,50)),(16,5,(240,200,60)),(22,6,(220,60,50)),(28,5,(120,190,70)),(34,6,(240,140,50))]: cv.ellipse(x,y,4,3,c)
    cv.ellipse(16,24,7,7,PAL['WD0']); cv.ellipse(16,24,5,5,PAL['WD2']); cv.ellipse(16,24,1.5,1.5,PAL['WD0'])
    cv.line(16,19,16,29,PAL['WD0']); cv.line(11,24,21,24,PAL['WD0'])
    save(cv,name,sh=(27,31,24,2,60))
def haystack(name):
    cv=Canvas(34,26); cv.ellipse(17,15,15,9,(222,190,90)); cv.ellipse(15,11,10,6,(240,214,120)); 
    for x in range(5,30,4): cv.line(x,20,x+2,14,(190,150,60))
    save(cv,name,sh=(17,23,15,2,60))
def logseat(name):
    cv=Canvas(32,14); cv.rect(4,3,24,8,PAL['WD1']); cv.ellipse(4,7,3,4,PAL['WD3']); cv.ellipse(4,7,1.5,2,PAL['WD2']); cv.rect(6,4,20,2,PAL['WD2'])
    save(cv,name,sh=(16,11,14,2,60))
def campfire(name):
    cv=Canvas(30,18)
    for i in range(8):
        a=i/8*6.283; cv.ellipse(15+math.cos(a)*11,10+math.sin(a)*4,3,2.5,PAL['S1'] if i%2 else PAL['S2'])
    cv.line(8,12,22,7,PAL['WD1']); cv.line(8,7,22,12,PAL['WD0'])
    save(cv,name,sh=(15,15,13,2,60))
def anvil(name):
    cv=Canvas(24,18); cv.rect(4,3,16,4,(70,70,84)); cv.rect(1,3,4,2,(70,70,84)); cv.rect(9,7,6,4,(60,60,72)); cv.rect(6,11,12,4,(60,60,72)); cv.rect(4,3,16,1,(130,130,146))
    save(cv,name,sh=(12,15,10,2,60))
def fence_h(name):
    cv=Canvas(16,22)
    for x in (2,): cv.rect(x,4,4,15,PAL['WD1']); cv.rect(x,4,4,2,PAL['WD3'])
    cv.rect(0,8,16,3,PAL['WD2']); cv.rect(0,8,16,1,PAL['WD3']); cv.rect(0,14,16,3,PAL['WD2']); cv.rect(0,14,16,1,PAL['WD3'])
    cv.a[8:17,0:16][(cv.a[8:17,0:16,3]>0)&(np.arange(9)[:,None]%6>=2)&(np.arange(9)[:,None]%6<3)]=(*PAL['WD1'],255)
    # outline only top/bottom so tiles connect
    m=cv.alpha(); d=m.copy(); d[1:]|=m[:-1]; d[:-1]|=m[1:]; cv.mask_fill(d&~m,OUT)
    for y in range(22):
        if cv.a[y,2,3]==0 and cv.a[y,3,3]>0: pass
    cv.save(f'out/{name}.png')
def fence_post(name):
    cv=Canvas(8,22); cv.rect(2,4,4,15,PAL['WD1']); cv.rect(2,4,4,2,PAL['WD3']); save(cv,name)
def stall(name,c1,c2,goods):
    W=84; cv=Canvas(W,74)
    for x in (6,W-10): cv.rect(x,14,4,52,PAL['WD0'])
    cv.rect(4,44,W-8,26,PAL['WD2']); cv.rect(4,44,W-8,3,PAL['WD3'])
    for x in range(10,W-8,9): cv.vline(x,48,68,PAL['WD1'])
    cv.rect(4,66,W-8,3,PAL['WD1'])
    goods(cv)
    # awning with stripes (slanted)
    for i in range(0,W,8):
        c=c1 if (i//8)%2==0 else c2
        cv.poly([(i+6,4),(i+14,4),(i+8,20),(i,20)],c)
    for i in range(0,W,8): cv.ellipse(i+4,20,4,3,c1 if (i//8)%2==0 else c2)
    cv.rect(4,2,W-8,3,tuple(int(v*0.75) for v in c1))
    save(cv,name,sh=(W/2,70,W/2-2,3,70))
def fruit_goods(cv):
    for i,(c,h) in enumerate([((220,60,50),(250,130,110)),((240,170,50),(255,220,120)),((130,190,70),(190,230,120)),((150,70,150),(200,140,200)),((240,120,40),(255,180,110))]):
        x=10+i*14
        cv.rect(x-2,38,14,8,PAL['WD1'])
        for (dx,dy) in [(0,0),(5,0),(10,0),(2.5,-4),(7.5,-4)]: cv.ellipse(x+dx+1,38+dy,3,3,c); cv.put(x+dx,37+dy,h)
def bread_goods(cv):
    for i in range(5):
        x=10+i*14; cv.rect(x-2,40,14,6,PAL['WD1'])
        for (dx,dy) in [(0,0),(6,0),(3,-4)]:
            cv.ellipse(x+dx+3,38+dy,5,3,(208,150,80)); cv.hline(int(x+dx+1),int(x+dx+5),int(37+dy),(240,200,130)); cv.put(x+dx+3,38+dy,(160,100,50))
# ------------------------------------------------------------------ fountain + statue
def fountain():
    cv=Canvas(112,104); cx=56
    # lower basin
    cv.ellipse(cx,82,52,18,PAL['C1']); cv.rect(4,82,104,12,PAL['C1']); cv.ellipse(cx,94,52,8,PAL['C0'])
    for x in range(8,106,10): cv.vline(x,84,98,PAL['C0'])
    cv.ellipse(cx,80,52,16,PAL['C2']); cv.ellipse(cx,80,46,12,PAL['W0']); cv.ellipse(cx,81,44,10,PAL['W1'])
    for (x,y) in [(24,78),(80,84),(40,86),(70,76)]: cv.hline(x,x+5,y,PAL['W2'])
    # pillar & upper bowl
    cv.rect(cx-6,46,12,34,PAL['C2']); cv.rect(cx+2,46,4,34,PAL['C1'])
    cv.ellipse(cx,48,20,7,PAL['C1']); cv.ellipse(cx,46,20,6,PAL['C2']); cv.ellipse(cx,46,16,4,PAL['W1'])
    # statue: robed figure holding up an orb
    cv.rect(cx-7,40,14,6,PAL['S2'])
    cv.poly([(cx-8,40),(cx-5,18),(cx+5,18),(cx+8,40)],PAL['S2']); cv.poly([(cx+2,18),(cx+5,18),(cx+8,40),(cx+3,40)],PAL['S1'])
    cv.ellipse(cx,14,5,5,PAL['S2']); cv.ellipse(cx+2,15,3,4,PAL['S1'])
    cv.line(cx+4,22,cx+10,10,PAL['S2']); cv.ellipse(cx+11,7,4,4,(150,200,230)); cv.put(cx+10,6,(230,250,255))
    cv.outline()
    shadow(cv,cx,98,54,5,70)
    cv.save('out/Prop_Fountain.png')
    # water anim: spout from bowl falling into basin, 4 frames (overlay 112x60 centred at (56,60))
    for f in range(4):
        a=Canvas(112,64)
        for side in (-1,1):
            for k in range(14):
                t=k/13; x=56+side*(18+t*16); y=8+t*30+((k+f)%2)
                c=PAL['W3'] if (k+f)%3 else (255,255,255)
                a.rect(x,y,2,2,c)
        for i in range(6):
            ang=(i*60+f*15)*math.pi/180; a.put(56+math.cos(ang)*(30+f*3),52+math.sin(ang)*6,(220,240,250))
        a.rect(55,0,2,6,PAL['W3']); a.put(54+(f%2)*3,0,(255,255,255))
        a.save(f'out/Anim_Fountain_{f:02d}.png')
# ------------------------------------------------------------------ windmill
def windmill():
    W=96; H=150; cv=Canvas(W,H); cx=W/2; by=H-BASE
    body=[(cx-30,by),(cx-20,40),(cx+20,40),(cx+30,by)]
    cv.poly(body,PAL['P1'])
    m=cv.poly_mask(body)
    yy,xx=np.mgrid[0:H,0:W]
    cv.mask_fill(m&(xx>cx+8),PAL['P0'])
    for y in range(48,by,12): cv.mask_fill(m&(yy==y),PAL['WD1'])
    cv.mask_fill(m&(yy>by-10),PAL['S1']); cv.mask_fill(m&(yy==by-10),PAL['S0'])
    door(cv,int(cx),by,14,22); window(cv,int(cx),70,8,10)
    cv.poly([(cx-27,44),(cx,6),(cx+27,44)],ROOFS['red'][1]); cv.poly([(cx+2,8),(cx+27,44),(cx+6,44)],ROOFS['red'][0]); cv.poly([(cx-4,12),(cx-24,42),(cx-16,42)],ROOFS['red'][2])
    cv.rect(cx-27,42,54,4,ROOFS['red'][0])
    cv.ellipse(cx,30,5,5,PAL['WD0'])
    cv.outline(); shadow(cv,cx,by,36,5,80); cv.save('out/Prop_Windmill.png')
    # blades: 4 frames, 120x120, hub at centre
    for f in range(4):
        b=Canvas(120,120); ang0=f*22.5
        for k in range(4):
            a=math.radians(ang0+k*90); ux,uy=math.cos(a),math.sin(a); vx,vy=-uy,ux
            def P(s,t): return (60+ux*s+vx*t,60+uy*s+vy*t)
            b.line(*P(4,0),*P(56,0),PAL['WD0']); b.line(*P(4,1),*P(56,1),PAL['WD0'])
            b.poly([P(14,2),P(56,2),P(56,14),P(14,12)],(240,232,210))
            for s in (24,34,44): b.line(*P(s,2),*P(s,13),PAL['WD1'])
            b.line(*P(14,7),*P(56,8),PAL['WD1'])
        b.ellipse(60,60,5,5,PAL['WD0']); b.ellipse(60,60,2,2,PAL['WD2'])
        b.outline(); b.save(f'out/Anim_Windmill_{f:02d}.png')
# ------------------------------------------------------------------ tower gate
def gate():
    W=232; H=176; cv=Canvas(W,H); by=H-BASE; cx=W//2
    # main wall
    wall(cv,16,40,W-32,by-40,'stone')
    # towers either side
    for tx in (0,W-44):
        wall(cv,tx,20,44,by-20,'stone')
        for i in range(0,44,11): cv.rect(tx+i,12,7,9,PAL['S1']); cv.rect(tx+i,12,7,2,PAL['S2'])
        cv.rect(tx+18,50,8,14,(30,26,40)); cv.rect(tx+18,50,8,3,PAL['S0'])
        cv.rect(tx+18,90,8,14,(30,26,40))
    for i in range(44,W-44,12): cv.rect(i,32,8,9,PAL['S1']); cv.rect(i,32,8,2,PAL['S2'])
    # arch
    aw=64; ah=92; ax=cx-aw//2; ay=by-ah
    cv.rect(ax-8,ay+20,aw+16,ah-20,PAL['S2']); cv.ellipse(cx,ay+24,aw/2+8,30,PAL['S2'])
    for i in range(9):
        a=math.pi+i/8*math.pi; cv.line(cx+math.cos(a)*(aw/2+1),ay+24+math.sin(a)*23,cx+math.cos(a)*(aw/2+8),ay+24+math.sin(a)*30,PAL['S0'])
    cv.rect(ax,ay+24,aw,ah-24,(28,18,44)); cv.ellipse(cx,ay+24,aw/2,22,(28,18,44))
    cv.rect(cx-6,ay-8,12,12,PAL['S3']); cv.ellipse(cx,ay-2,4,4,(170,110,230))
    # banners
    for bx in (54,W-66):
        cv.rect(bx-2,52,16,3,PAL['WD0']); cv.rect(bx,55,12,34,(120,50,150)); cv.poly([(bx,89),(bx+6,84),(bx+12,89),(bx+12,94),(bx+6,90),(bx,94)],(120,50,150))
        cv.rect(bx+3,62,6,6,(232,196,90)); cv.rect(bx+10,55,2,36,(90,36,112))
    # torches holders
    for tx in (cx-50,cx+50): cv.rect(tx-2,by-56,5,10,PAL['WD0']); cv.rect(tx-4,by-58,9,3,(70,70,84))
    cv.rect(ax-14,by-2,aw+28,2,PAL['S3'])
    cv.outline(); shadow(cv,cx,by,W/2,5,80); cv.save('out/Prop_Gate.png')
    # portal anim (fills arch), 6 frames 64x72
    for f in range(6):
        p=Canvas(64,90); yy,xx=np.mgrid[0:90,0:64]
        inside=(((xx-32)/32)**2+((yy-24)/22)**2<=1)|((yy>=24)&(xx>=0)&(xx<64))
        ang=np.arctan2(yy-50,(xx-32)*1.1); r=np.hypot(xx-32,(yy-50)*0.8)
        v=np.sin(ang*3+r*0.28-f*1.05)
        cols=[(60,30,110),(110,60,180),(170,110,230),(220,180,255)]
        lvl=np.clip(((v+1)/2*3+(1-r/40)*1.2).astype(int),0,3)
        for i,c in enumerate(cols): p.mask_fill(inside&(lvl==i),c)
        p.mask_fill(inside&(r<5),(250,240,255))
        rr=random.Random(f)
        for _ in range(6):
            x=rr.randrange(6,58); y=rr.randrange(10,86)
            if inside[y,x]: p.put(x,y,(255,255,255))
        p.save(f'out/Anim_Portal_{f:02d}.png')
# ------------------------------------------------------------------ fx anims
def flames(prefix='Anim_Flame',n=4,s=1.0):
    for f in range(n):
        c=Canvas(14,20); R=random.Random(f*7)
        h=12+R.randint(-1,2); sway=[0,1,0,-1][f%4]
        c.poly([(7+sway,19-h-4),(12,16),(7,19),(2,16)],(240,110,40))
        c.poly([(7+sway,19-h+1),(10,16),(7,18),(4,16)],(255,190,70))
        c.rect(6,15,3,3,(255,240,170))
        if f%2: c.put(4+sway,4,(255,180,80))
        c.save(f'out/{prefix}_{f:02d}.png')
def campfire_flame():
    for f in range(4):
        c=Canvas(22,26); sway=[0,1,0,-1][f]
        c.poly([(11+sway,1+f%2),(18,18),(15,24),(7,24),(4,18)],(236,96,40))
        c.poly([(11-sway,7),(15,18),(13,23),(9,23),(7,18)],(255,184,64))
        c.poly([(11,13),(13,20),(11,23),(9,20)],(255,240,170))
        c.put(5+f,3,(255,170,60)); c.put(16-f,5,(255,210,90))
        c.save(f'out/Anim_Campfire_{f:02d}.png')
def smoke():
    for f in range(6):
        c=Canvas(18,34)
        for k in range(4):
            t=((k*6+f)%24)/24; y=30-t*28; x=9+math.sin(t*6+k)*3; r=2+t*4
            a=int(200*(1-t))
            m=c.ell_mask(x,y,r,r*0.8); c.a[m]=(220,220,226,a)
        c.save(f'out/Anim_Smoke_{f:02d}.png')
def make_all():
    tree('Tree_Big',58,seed=1); tree('Tree_Big2',54,seed=5); tree('Tree_Small',40,seed=3); tree('Tree_Apple',52,fruit=(220,50,50),seed=8)
    tree('Tree_Orange',50,pal=AUT,seed=11); pine('Tree_Pine',48)
    bush('Bush',26,seed=4); bush('Bush_Flower',26,flowers=(240,120,160),seed=6); bush('Bush_Yellow',22,flowers=(250,220,90),seed=9)
    crate('Prop_Crate'); barrel('Prop_Barrel'); sack('Prop_Sack'); pumpkin('Prop_Pumpkin'); lamp('Prop_Lamp'); bench('Prop_Bench')
    board('Prop_Board'); signpost('Prop_Signpost'); well('Prop_Well'); cart('Prop_Cart'); haystack('Prop_Haystack'); logseat('Prop_Log'); campfire('Prop_Campfire'); anvil('Prop_Anvil')
    fence_h('Fence_H'); fence_post('Prop_FencePost')
    stall('Prop_StallFruit',(206,58,58),(244,236,220),fruit_goods); stall('Prop_StallBread',(60,110,190),(244,236,220),bread_goods)
    fountain(); windmill(); gate(); flames(); campfire_flame(); smoke()
if __name__=='__main__': make_all()
