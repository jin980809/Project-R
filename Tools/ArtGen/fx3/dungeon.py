"""Dungeon room art in the village pixel style (32 px = 1 unit, point filter).
Room_<theme>.png covers floor 18.4x10, north wall face 1.4 and E/W/S wall tops 0.7 (634x388 px, centre = world (0, 0.3375))."""
import numpy as np, random, math, os
from PIL import Image
from px import Canvas, PAL, ROOFS, OUT, shadow
OUTD='dout'; os.makedirs(OUTD,exist_ok=True)
U=32
HX,HY=9.2,5.0; RIM=0.7; FACE=1.4
W=int(round((HX+RIM)*2*U)); H=int(round((HY+FACE+HY+RIM)*U))   # 634 x 387 -> 388
H=388
def P(x,y): return ((x+HX+RIM)*U,(HY+FACE-y)*U)
FX0,FY0=P(-HX,HY); FX1,FY1=P(HX,-HY)
FX0,FY0,FX1,FY1=int(FX0),int(FY0),int(FX1),int(FY1)
yy,xx=np.mgrid[0:H,0:W]
def vnoise(scale,seed):
    r=np.random.default_rng(seed); g=r.random((H//scale+3,W//scale+3))
    im=Image.fromarray((g*255).astype(np.uint8)).resize(((W//scale+3)*scale,(H//scale+3)*scale),Image.BICUBIC)
    return np.array(im)[:H,:W]/255.0
dith=((xx+yy)%2==0)
def shade(c,f): return tuple(int(max(0,min(255,v*f))) for v in c)
def blend(c,d,t): return tuple(int(c[i]+(d[i]-c[i])*t) for i in range(3))
# ------------------------------------------------------------------ floors
def floor_stone(cv,m,seed,dark=1.0,moss=True):
    base=[shade((146,136,132),0.95*dark),shade((138,128,126),dark),shade((158,148,140),dark)]
    mort=shade(PAL['C0'],0.85*dark); hi=shade(PAL['C3'],0.92*dark)
    rng=np.random.default_rng(seed)
    # flagstones: rows of 24px, staggered widths 24..40
    y=FY0
    while y<FY1:
        hgt=24 if (y//24)%2==0 else 20
        x=FX0-int(rng.integers(0,20))
        while x<FX1:
            w=int(rng.integers(26,44))
            col=base[int(rng.integers(0,3))]
            cv.rect(x,y,w,hgt,col)
            cv.rect(x,y,w,1,hi); cv.rect(x,y,1,hgt,hi)
            cv.rect(x,y+hgt-1,w,1,mort); cv.rect(x+w-1,y,1,hgt,mort)
            if rng.random()<0.25:   # crack
                cx=x+int(rng.integers(4,max(5,w-4))); cy=y+3
                for k in range(int(rng.integers(4,9))):
                    cv.put(cx,cy+k,mort); cx+=int(rng.integers(-1,2))
            x+=w
        y+=hgt
    if moss:
        n=vnoise(28,seed+3)
        mm=m&(n>0.8); cv.mask_fill(mm&dith,(92,118,74)); cv.mask_fill(m&(n>0.85),(84,112,66)); cv.mask_fill(m&(n>0.9)&~dith,(104,140,76))
    # pebbles
    for _ in range(140):
        x=int(rng.integers(FX0,FX1)); y=int(rng.integers(FY0,FY1))
        cv.put(x,y,mort); cv.put(x+1,y,hi)
def floor_wood(cv,m,seed):
    rng=np.random.default_rng(seed)
    cols=[PAL['WD2'],shade(PAL['WD2'],0.92),PAL['WD3']]
    for y in range(FY0,FY1,8):
        x=FX0-int(rng.integers(0,40))
        while x<FX1:
            w=int(rng.integers(40,90)); c=cols[int(rng.integers(0,3))]
            cv.rect(x,y,w,8,c); cv.rect(x,y,w,1,shade(c,1.12)); cv.rect(x,y+7,w,1,PAL['WD0']); cv.rect(x+w-1,y,1,8,PAL['WD0'])
            cv.put(x+3,y+3,PAL['WD0']); cv.put(x+w-4,y+3,PAL['WD0'])
            x+=w
    # rug in the middle
    rx0,ry0=P(-5.2,2.6); rx1,ry1=P(5.2,-2.6)
    cv.rect(rx0,ry0,rx1-rx0,ry1-ry0,(120,40,44)); cv.rect(rx0+4,ry0+4,rx1-rx0-8,ry1-ry0-8,(162,58,56))
    cv.rect(rx0+10,ry0+10,rx1-rx0-20,ry1-ry0-20,(120,40,44)); cv.rect(rx0+14,ry0+14,rx1-rx0-28,ry1-ry0-28,(176,74,64))
    for x in range(int(rx0)+20,int(rx1)-20,16):
        cv.rect(x,ry0+6,6,2,(232,196,90)); cv.rect(x,ry1-8,6,2,(232,196,90))
    cx,cy=(rx0+rx1)/2,(ry0+ry1)/2
    cv.poly([(cx,cy-30),(cx+46,cy),(cx,cy+30),(cx-46,cy)],(232,196,90)); cv.poly([(cx,cy-24),(cx+38,cy),(cx,cy+24),(cx-38,cy)],(162,58,56))
    cv.poly([(cx,cy-12),(cx+18,cy),(cx,cy+12),(cx-18,cy)],(232,196,90))
    for x in range(int(rx0),int(rx1),4): cv.rect(x,ry0-3,2,3,(232,214,170)); cv.rect(x,ry1,2,3,(232,214,170))
def floor_cave(cv,m,seed):
    rng=np.random.default_rng(seed)
    n=vnoise(20,seed)*0.6+vnoise(6,seed+1)*0.4
    cv.mask_fill(m,(112,98,90)); cv.mask_fill(m&(n>0.55),(126,112,100)); cv.mask_fill(m&(n>0.66)&dith,(142,126,112)); cv.mask_fill(m&(n<0.35),(96,84,80))
    cv.mask_fill(m&(n<0.28)&dith,(84,74,72))
    for _ in range(70):
        x=int(rng.integers(FX0+6,FX1-6)); y=int(rng.integers(FY0+6,FY1-6)); r=int(rng.integers(2,5))
        cv.ellipse(x,y,r+1,r*0.7+1,(70,62,62)); cv.ellipse(x,y-1,r,r*0.7,(150,138,126)); cv.put(x-1,y-2,(182,170,156))
    for _ in range(18):   # crystal glints
        x=int(rng.integers(FX0+8,FX1-8)); y=int(rng.integers(FY0+8,FY1-8))
        cv.poly([(x,y-6),(x+3,y),(x,y+2),(x-3,y)],(120,170,220)); cv.put(x,y-4,(220,240,255))
    nm=vnoise(30,seed+9); cv.mask_fill(m&(nm>0.82)&dith,(84,108,66))
def magic_circle(cv,cx,cy,r,col,col2):
    for k in range(2):
        rr=r-k*10
        mm=cv.ell_mask(cx,cy,rr,rr*0.55)&~cv.ell_mask(cx,cy,rr-2,rr*0.55-2)
        cv.mask_fill(mm,col)
    for i in range(6):
        a=i*math.pi/3; a2=a+2*math.pi/3
        cv.line(cx+math.cos(a)*(r-10),cy+math.sin(a)*(r-10)*0.55,cx+math.cos(a2)*(r-10),cy+math.sin(a2)*(r-10)*0.55,col2)
def floor_boss(cv,m,seed):
    floor_stone(cv,m,seed,dark=0.72,moss=False)
    x0,_=P(-1.6,0); x1,_=P(1.6,0)
    cv.rect(x0,FY0,x1-x0,(FY1-FY0)*0.62,(110,30,36)); cv.rect(x0+3,FY0,x1-x0-6,(FY1-FY0)*0.62,(150,40,44))
    cv.rect(x0+3,FY0,2,(FY1-FY0)*0.62,(232,196,90)); cv.rect(x1-5,FY0,2,(FY1-FY0)*0.62,(232,196,90))
    cx,cy=P(0,0); magic_circle(cv,cx,cy,96,(150,40,52),(110,30,40))
# ------------------------------------------------------------------ walls
def wall_face(cv,theme,seed):
    y0=0; y1=FY0
    x0=0; x1=W
    rng=np.random.default_rng(seed)
    if theme=='wood':
        cv.rect(x0,y0,x1,y1,PAL['P1']); cv.rect(x0,y1-6,x1,6,PAL['P0'])
        for x in range(0,W,48): cv.rect(x,y0,4,y1,PAL['WD1']); cv.rect(x+3,y0,1,y1,PAL['WD0'])
        cv.rect(x0,y0+14,x1,4,PAL['WD1']); cv.rect(x0,y0+17,x1,1,PAL['WD0'])
        # shelves with bottles
        for sx in (60,420):
            cv.rect(sx,22,90,3,PAL['WD0'])
            for k in range(8):
                c=[(200,60,60),(70,140,200),(110,180,90),(230,190,80)][k%4]
                cv.rect(sx+4+k*11,14,6,8,c); cv.rect(sx+5+k*11,12,4,2,shade(c,0.7))
    elif theme=='cave':
        n=vnoise(10,seed+4)
        mm=(yy<y1)
        cv.mask_fill(mm,(98,86,82)); cv.mask_fill(mm&(n>0.55),(116,102,94)); cv.mask_fill(mm&(n>0.7)&dith,(134,120,108)); cv.mask_fill(mm&(n<0.3),(80,70,68))
        for _ in range(16):
            x=int(rng.integers(10,W-10)); y=int(rng.integers(8,y1-8))
            cv.poly([(x,y-7),(x+4,y),(x,y+3),(x-4,y)],(110,180,230)); cv.put(x,y-4,(230,245,255))
    else:
        dark=0.75 if theme=='boss' else 1.0
        a=shade(PAL['S1'],dark); b=shade(PAL['S0'],dark); c=shade(PAL['S2'],dark)
        cv.rect(x0,y0,x1,y1,a)
        for i,y in enumerate(range(y0,y1,8)):
            cv.hline(0,W-1,y,b); cv.hline(0,W-1,y+1,c)
            for x in range((i%2)*10,W,20): cv.vline(x,y,y+7,b)
        if theme=='stone':
            n=vnoise(12,seed+6); cv.mask_fill((yy<y1)&(n>0.78)&dith,(80,104,64)); cv.mask_fill((yy<y1)&(n>0.85),(70,96,58))
    # top cap and bottom shadow
    cv.rect(0,0,W,5,shade(PAL['S2'],0.9)); cv.rect(0,4,W,1,PAL['S0'])
    cv.rect(0,y1-3,W,3,shade(PAL['S0'],0.7))
    # contact shadow on the floor under the wall
    m=(yy>=y1)&(yy<y1+7)&(xx>=FX0)&(xx<FX1)
    cv.a[m,:3]=(cv.a[m,:3]*0.7).astype(np.uint8)
def rims(cv,theme):
    top=shade(PAL['S2'],0.85 if theme!='boss' else 0.65); edge=PAL['S0']; hi=PAL['S3']
    if theme=='wood': top=PAL['WD1']; hi=PAL['WD2']
    if theme=='cave': top=(92,82,78); hi=(120,108,100)
    # west/east
    cv.rect(0,FY0,FX0,H-FY0,top); cv.rect(FX1,FY0,W-FX1,H-FY0,top)
    cv.rect(0,FY1,W,H-FY1,top)
    for x in range(0,W,16):
        cv.vline(x,FY1,H-1,shade(top,0.85))
    for y in range(FY0,H,16):
        cv.hline(0,FX0-1,y,shade(top,0.85)); cv.hline(FX1,W-1,y,shade(top,0.85))
    # inner edges (lip toward the floor)
    cv.rect(FX0-3,FY0,3,FY1-FY0+3,hi); cv.rect(FX0-1,FY0,1,FY1-FY0+1,edge)
    cv.rect(FX1,FY0,3,FY1-FY0+3,hi); cv.rect(FX1,FY0,1,FY1-FY0+1,edge)
    cv.rect(FX0-3,FY1,FX1-FX0+6,3,hi); cv.rect(FX0,FY1,FX1-FX0,1,edge)
    # floor shadow along the side walls
    for x0,x1 in ((FX0,FX0+5),(FX1-5,FX1)):
        m=(xx>=x0)&(xx<x1)&(yy>=FY0)&(yy<FY1); cv.a[m,:3]=(cv.a[m,:3]*0.8).astype(np.uint8)
def room(theme,seed):
    cv=Canvas(W,H)
    m=(xx>=FX0)&(xx<FX1)&(yy>=FY0)&(yy<FY1)
    {'stone':lambda:floor_stone(cv,m,seed),'wood':lambda:floor_wood(cv,m,seed),'cave':lambda:floor_cave(cv,m,seed),'boss':lambda:floor_boss(cv,m,seed)}[theme]()
    wall_face(cv,theme,seed); rims(cv,theme)
    if theme in ('stone','boss'):
        # banners on the north wall
        for bx in (-8.4,8.4) if theme=='stone' else (-5,-1.5,1.5,5):
            x,_=P(bx,0); col=(60,90,150) if theme=='stone' else (130,30,40)
            cv.rect(x-8,6,16,2,PAL['WD0']); cv.rect(x-6,8,12,26,col); cv.poly([(x-6,34),(x,30),(x+6,34),(x+6,38),(x,34),(x-6,38)],col)
            cv.rect(x-2,14,4,4,(232,196,90))
    cv.save(f'{OUTD}/Room_{theme.capitalize()}.png'); return cv
# ------------------------------------------------------------------ doors
def gate(closed):
    c=Canvas(58,52)
    c.rect(2,6,54,46,PAL['S2']); c.ellipse(29,16,27,14,PAL['S2'])
    c.rect(8,16,42,36,(26,20,30)); c.ellipse(29,18,21,12,(26,20,30))
    for i in range(7):
        a=math.pi+i/6*math.pi; c.line(29+math.cos(a)*21,18+math.sin(a)*12,29+math.cos(a)*27,18+math.sin(a)*15,PAL['S0'])
    c.rect(3,20,5,32,PAL['S1']); c.rect(50,20,5,32,PAL['S1'])
    if closed:
        for x in range(11,49,6): c.rect(x,10,2,42,(70,70,84)); c.put(x,10,(150,150,166))
        for y in (24,38): c.rect(9,y,40,2,(70,70,84))
        for x in range(11,49,6): c.poly([(x-1,50),(x+1,50),(x,52)],(150,150,166))
    else:
        # warm light spilling from beyond
        m=c.ell_mask(29,48,18,10)&(c.a[...,0]==26)
        c.mask_fill(m&((np.mgrid[0:52,0:58][0]+np.mgrid[0:52,0:58][1])%2==0),(70,52,50))
    c.outline(); c.save(f'{OUTD}/Gate{"Closed" if closed else "Open"}.png')
def door(closed):
    # 70x29, top of the picture faces into the room
    c=Canvas(70,29)
    c.rect(0,0,70,29,(0,0,0,0))
    c.rect(4,0,62,29,(40,32,36)); c.rect(4,0,62,4,(70,56,56))
    c.rect(0,0,6,29,PAL['S1']); c.rect(64,0,6,29,PAL['S1']); c.rect(0,0,6,2,PAL['S3']); c.rect(64,0,6,2,PAL['S3'])
    if closed:
        for x in range(8,62,9): c.rect(x,2,7,25,PAL['WD2']); c.rect(x,2,7,1,PAL['WD3']); c.rect(x+6,2,1,25,PAL['WD0'])
        c.rect(6,8,58,3,PAL['WD0']); c.rect(6,18,58,3,PAL['WD0'])
        for x in (12,34,56): c.put(x,9,(200,200,210)); c.put(x,19,(200,200,210))
    else:
        for y in range(6,29,4): c.rect(8,y,54,1,(52,42,46))
    c.outline(); c.save(f'{OUTD}/Door{"Closed" if closed else "Open"}.png')
def torch():
    for f in range(4):
        c=Canvas(14,26)
        c.rect(5,12,4,12,PAL['WD1']); c.rect(4,11,6,3,(70,70,84)); c.rect(3,22,8,3,(70,70,84))
        sway=[0,1,0,-1][f]
        c.poly([(7+sway,0+f%2),(11,8),(9,12),(5,12),(3,8)],(236,96,40))
        c.poly([(7,4),(9,9),(7,12),(5,9)],(255,190,70)); c.put(7,9,(255,240,170)); c.put(7,10,(255,240,170))
        c.outline(); c.save(f'{OUTD}/Torch_{f:02d}.png')
# ------------------------------------------------------------------ props (baseline 3 px from the bottom)
def pillar():
    c=Canvas(30,62); by=59
    c.rect(4,by-6,22,6,PAL['S1']); c.rect(4,by-6,22,1,PAL['S3'])
    c.rect(7,10,16,by-16,PAL['S2']); c.rect(19,10,4,by-16,PAL['S1']); c.rect(8,10,2,by-16,PAL['S3'])
    for y in range(18,by-8,10): c.hline(7,22,y,PAL['S0'])
    c.rect(4,4,22,7,PAL['S1']); c.rect(4,4,22,2,PAL['S3'])
    c.rect(9,30,4,8,PAL['G0']); c.put(10,29,PAL['G1']); c.rect(8,by-10,5,4,PAL['G0'])
    c.outline(); shadow(c,15,by,13,3,80); c.save(f'{OUTD}/Prop_Pillar.png')
def rock():
    c=Canvas(36,30); by=27
    c.ellipse(18,17,16,11,PAL['S1']); c.ellipse(15,14,12,8,PAL['S2']); c.ellipse(13,11,6,4,PAL['S3'])
    c.ellipse(24,21,6,4,PAL['S0']); c.ellipse(14,8,7,3,PAL['G0']); c.put(12,6,PAL['G1']); c.put(15,7,PAL['G1'])
    c.outline(); shadow(c,18,by,16,3,80); c.save(f'{OUTD}/Prop_Rock.png')
def crate():
    c=Canvas(30,30); by=27
    c.rect(3,6,24,20,PAL['WD2']); c.rect(3,6,24,5,PAL['WD3'])
    c.rect(3,6,3,20,PAL['WD1']); c.rect(24,6,3,20,PAL['WD1']); c.rect(3,23,24,3,PAL['WD1']); c.rect(3,11,24,2,PAL['WD1'])
    c.line(6,13,23,23,PAL['WD1']); c.line(6,14,23,24,PAL['WD1'])
    c.outline(); shadow(c,15,by,13,3,80); c.save(f'{OUTD}/Prop_Crate.png')
def barrel():
    c=Canvas(24,32); by=29
    c.rect(3,6,18,22,PAL['WD2']); c.rect(2,9,20,16,PAL['WD2']); c.ellipse(12,6,9,3.5,PAL['WD3']); c.ellipse(12,6,6,2,PAL['WD1'])
    for y in (10,21): c.rect(2,y,20,2,PAL['S1'])
    c.rect(15,7,4,20,PAL['WD1'])
    c.outline(); shadow(c,12,by,10,3,80); c.save(f'{OUTD}/Prop_Barrel.png')
def chest():
    c=Canvas(36,30); by=27
    c.rect(4,12,28,14,PAL['WD2']); c.rect(4,12,28,2,PAL['WD3']); c.rect(4,22,28,4,PAL['WD1'])
    c.rect(3,5,30,9,PAL['WD1']); c.ellipse(18,6,15,4,PAL['WD2']); c.rect(3,13,30,2,(200,160,60))
    for x in (6,28): c.rect(x,5,3,21,(200,160,60))
    c.rect(15,12,6,7,(232,196,90)); c.rect(17,15,2,3,(80,60,30))
    c.outline(); shadow(c,18,by,16,3,80); c.save(f'{OUTD}/Chest.png')
def stairs():
    c=Canvas(76,76)
    c.ellipse(38,38,36,36,PAL['S1']); c.ellipse(38,38,32,32,PAL['S2'])
    for k in range(5):
        y=18+k*9; w=40-k*5
        c.rect(38-w//2,y,w,7,shade(PAL['S2'],1.0-k*0.08)); c.rect(38-w//2,y+6,w,1,PAL['S0'])
    c.ellipse(38,64,12,5,(90,150,230)); c.ellipse(38,64,8,3,(200,230,255))
    for i in range(8):
        a=i*math.pi/4; c.rect(38+math.cos(a)*34-1,38+math.sin(a)*34-1,3,3,(120,180,240))
    c.outline(); c.save(f'{OUTD}/Decal_Stairs.png')
def altar():
    c=Canvas(48,52); by=49
    c.rect(6,by-14,36,14,PAL['S1']); c.rect(6,by-14,36,2,PAL['S3']); c.rect(10,by-24,28,10,PAL['S2']); c.rect(10,by-24,28,2,PAL['S3'])
    c.poly([(24,4),(32,18),(24,28),(16,18)],(150,110,230)); c.poly([(24,6),(28,16),(24,24)],(210,190,255)); c.put(22,12,(255,255,255))
    for x in (12,36): c.rect(x-2,by-22,4,6,(232,196,90))
    c.outline(); shadow(c,24,by,20,3,80); c.save(f'{OUTD}/Fac_Altar.png')
def shopstall():
    c=Canvas(64,58); by=55
    for x in (6,54): c.rect(x,14,4,by-14,PAL['WD0'])
    c.rect(4,by-20,56,20,PAL['WD2']); c.rect(4,by-20,56,3,PAL['WD3'])
    for i,col in enumerate([(220,60,50),(240,200,60),(120,190,70),(150,70,150)]):
        c.ellipse(12+i*12,by-24,4,4,col)
    for i in range(0,64,8): c.poly([(i+4,4),(i+12,4),(i+8,16),(i,16)],(90,60,150) if (i//8)%2==0 else (244,236,220))
    for i in range(0,64,8): c.ellipse(i+4,16,4,3,(90,60,150) if (i//8)%2==0 else (244,236,220))
    c.outline(); shadow(c,32,by,28,3,80); c.save(f'{OUTD}/Fac_Shop.png')
def forge():
    c=Canvas(60,54); by=51
    c.rect(26,10,30,by-10,PAL['S1']); c.rect(26,10,30,3,PAL['S3']); c.rect(32,24,18,14,(40,30,30)); c.rect(34,28,14,8,(250,120,40)); c.rect(37,30,8,4,(255,220,120))
    c.rect(36,0,10,12,PAL['S0'])
    c.rect(4,by-14,22,5,(70,70,84)); c.rect(1,by-14,5,3,(70,70,84)); c.rect(10,by-9,10,6,(60,60,72)); c.rect(4,by-14,22,1,(140,140,156))
    c.outline(); shadow(c,30,by,26,3,80); c.save(f'{OUTD}/Fac_Forge.png')
def fishing():
    c=Canvas(90,56); by=53
    c.ellipse(45,30,42,22,PAL['D2']); c.ellipse(45,30,38,19,PAL['W0']); c.ellipse(45,31,36,17,PAL['W1'])
    for x,y in ((26,26),(52,36),(60,24)): c.hline(x,x+6,y,PAL['W2'])
    c.rect(58,8,26,6,PAL['WD2']); c.rect(58,8,26,1,PAL['WD3']); 
    for x in (60,80): c.rect(x,14,3,10,PAL['WD0'])
    c.ellipse(24,40,5,3,PAL['L2']); c.put(24,39,(240,170,190))
    c.outline(); c.save(f'{OUTD}/Fac_Fishing.png')
def ore():
    c=Canvas(56,48); by=45
    c.ellipse(28,30,26,16,(96,86,84)); c.ellipse(24,26,18,12,(122,110,104)); c.ellipse(20,22,8,5,(150,138,130))
    for (x,y,col) in [(16,26,(110,180,240)),(30,20,(230,190,80)),(38,30,(110,180,240)),(24,34,(230,190,80))]:
        c.poly([(x,y-6),(x+3,y),(x,y+3),(x-3,y)],col); c.put(x,y-3,(255,255,255))
    c.rect(44,by-14,3,14,PAL['WD1']); c.poly([(38,by-16),(52,by-18),(52,by-14),(40,by-12)],(140,140,156))
    c.outline(); shadow(c,28,by,24,3,80); c.save(f'{OUTD}/Fac_Ore.png')
def stable():
    c=Canvas(66,46); by=43
    c.rect(4,by-16,26,14,(222,190,90)); c.rect(4,by-16,26,3,(240,214,120)); 
    for x in range(6,30,4): c.vline(x,by-13,by-3,(190,150,60))
    c.rect(36,by-24,4,24,PAL['WD1']); c.rect(56,by-24,4,24,PAL['WD1']); c.rect(34,by-20,30,3,PAL['WD2']); c.rect(34,by-12,30,3,PAL['WD2'])
    c.poly([(40,by-30),(54,by-30),(56,by-24),(38,by-24)],(140,70,40)); c.rect(45,by-26,4,4,(232,196,90))
    c.outline(); shadow(c,33,by,30,3,80); c.save(f'{OUTD}/Fac_Stable.png')
if __name__=='__main__':
    for i,t in enumerate(['stone','wood','cave','boss']): room(t,11+i)
    gate(False); gate(True); door(False); door(True); torch()
    pillar(); rock(); crate(); barrel(); chest(); stairs(); altar(); shopstall(); forge(); fishing(); ore(); stable()
    print('ok',W,H)
