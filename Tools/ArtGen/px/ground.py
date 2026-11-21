import numpy as np, random, math
from px import *
from layout import *
from PIL import Image, ImageDraw
rng=np.random.default_rng(7); random.seed(7)
W,H=MAPW,MAPH
def P(x,y): return w2p(x,y)
cv=Canvas(W,H,PAL['G1']+(255,))
yy,xx=np.mgrid[0:H,0:W]
# ---- grass texture: low-freq light/dark patches with dithered borders
def vnoise(scale,seed):
    r=np.random.default_rng(seed); g=r.random((H//scale+3,W//scale+3))
    im=Image.fromarray((g*255).astype(np.uint8)).resize(((W//scale+3)*scale,(H//scale+3)*scale),Image.BICUBIC)
    return np.array(im)[:H,:W]/255.0
n=vnoise(48,1)*0.65+vnoise(16,2)*0.35
dith=((xx+yy)%2==0)
cv.mask_fill((n>0.62)|((n>0.58)&dith),(106,170,66))
cv.mask_fill((n<0.34)|((n<0.38)&dith),(84,150,60))
# ---- masks for features
def blank(): return Image.new('L',(W,H),0)
def path_mask(segs,width,wobble=2):
    im=blank(); d=ImageDraw.Draw(im)
    for (x0,y0),(x1,y1) in segs:
        a=P(x0,y0); b=P(x1,y1); d.line([a,b],fill=1,width=int(width*PPU)); 
        for p in (a,b): r=width*PPU/2; d.ellipse([p[0]-r,p[1]-r,p[0]+r,p[1]+r],fill=1)
    m=np.array(im).astype(bool)
    # wobble edges
    e=vnoise(6,3)
    grow=np.zeros_like(m); grow[1:]|=m[:-1]; grow[:-1]|=m[1:]; grow[:,1:]|=m[:,:-1]; grow[:,:-1]|=m[:,1:]
    shrink=m.copy(); shrink[1:]&=m[:-1]; shrink[:-1]&=m[1:]; shrink[:,1:]&=m[:,:-1]; shrink[:,:-1]&=m[:,1:]
    return (m & ~(~shrink & (e<0.3))) | (grow & (e>0.75))
terrace=(np.abs((xx/PPU-18))<PLATEAU_X)&((10-yy/PPU)>CLIFF_BASE)
cliff=terrace&((10-yy/PPU)<=CLIFF_TOP)
stairs=cliff&(np.abs(xx/PPU-18)<STAIR_HALF)
roads=[((0,2.8),(0,-10.5)),((-18.5,ROAD_Y),(18.5,ROAD_Y)),((0,3.9),(0,6.2)),
       ((-8.6,0.9),(-8.6,ROAD_Y)),((9.6,1.4),(9.6,ROAD_Y)),((14.6,0.6),(14.6,ROAD_Y)),
       ((-13.4,-5.0),(-13.4,-6.0)),((-13.4,-6.0),(-11.0,-6.0)),((-11.0,-6.0),(-11.0,ROAD_Y)),
       ((-10.2,LANE_Y),(12.5,LANE_Y)),((-7.7,-7.4),(-7.7,LANE_Y)),((9.4,-7.2),(9.4,LANE_Y)),((-15.75,3.4),(-15.75,ROAD_Y))]
road=path_mask(roads,1.4)
road|=path_mask([((0,3.9),(0,5.6))],2.6)
# 3/4 view: plaza is an ellipse squashed like the fountain basin (y*0.62), plus a paved apron up to the stairs
PCY=P(0,PLAZA_CY)[1]
plaza=((xx-W/2)/(PLAZA_RX*PPU))**2+((yy-PCY)/(PLAZA_RY*PPU))**2<1
plaza|=(np.abs(xx-W/2)<(STAIR_HALF+0.1)*PPU)&(yy<PCY)&((10-yy/PPU)<=CLIFF_BASE+0.05)
road&=~cliff
plaza&=~terrace
# ---- dirt road
cv.mask_fill(road&~plaza,PAL['D1'])
rn=rng.random((H,W))
cv.mask_fill(road&~plaza&(rn<0.05),PAL['D2']); cv.mask_fill(road&~plaza&(rn>0.975),PAL['D0'])
cv.mask_fill(road&~plaza&(vnoise(10,9)>0.72)&dith,PAL['D2'])
def edge(m):
    g=np.zeros_like(m); g[1:]|=m[:-1]; g[:-1]|=m[1:]; g[:,1:]|=m[:,:-1]; g[:,:-1]|=m[:,1:]; return g&~m
def inner(m): return edge(~m)&m
cv.mask_fill(inner(road)&~plaza,PAL['D0'])
cv.mask_fill(edge(road|plaza)&~terrace,PAL['G0'])
# ---- plaza cobbles: concentric courses around the fountain, squashed like the basin (3/4 view)
cv.mask_fill(plaza,PAL['C1'])
fx,fy=P(FOUNTAIN[0],FOUNTAIN_BASIN_Y); SQ=0.62
ux=(xx-fx).astype(float); uy=(yy-fy)/SQ
rr=np.sqrt(ux**2+uy**2); th=np.arctan2(uy,ux)
CW=10.0; k=(rr//CW).astype(int)
nseg=np.maximum(6,np.round(2*np.pi*(k+0.5)*CW/14)).astype(int)
f=(th+np.pi)/(2*np.pi)*nseg+(k%2)*0.5
seg=np.floor(f).astype(int)%nseg
arc=(f-np.floor(f))*2*np.pi*(k+0.5)*CW/nseg   # px along the course inside the stone
lenp=2*np.pi*(k+0.5)*CW/nseg
rin=rr-k*CW
# mortar width in screen px: radial lines get thinner where the course is foreshortened (top/bottom)
mort=(rin<1.0*(np.abs(np.cos(th))*(1-SQ)+SQ)**-1*SQ)|(arc<1.0)
hl=~mort&(rin<2.6)&(uy>0)|~mort&(rin>CW-1.6)&(uy<0)   # lip catching the light on each course's far edge
dk=~mort&(rin>CW-1.4)&(uy>0)|~mort&(rin<2.2)&(uy<0)
cv.mask_fill(plaza&mort,PAL['C0']); cv.mask_fill(plaza&hl,PAL['C2'])
tone=np.random.default_rng(4).random((80,400))[np.clip(k,0,79),seg%400]
cv.mask_fill(plaza&~mort&(tone>0.82)&~hl&~dk,PAL['C2']); cv.mask_fill(plaza&~mort&((tone<0.15)|dk)&~hl,(138,132,130))
pr=inner(plaza); cv.mask_fill(pr,PAL['C0']); cv.mask_fill(edge(plaza)&~road,PAL['S1'])
# ring of light stone around fountain
_e=((xx-fx)/PPU)**2+((yy-fy)/(PPU*0.62))**2
ring=(_e<2.35**2)&(_e>2.1**2)&plaza
cv.mask_fill(ring,PAL['C2'])
# ---- terrace field & garden soil
def box(x0,y0,x1,y1):
    a=P(x0,y1); b=P(x1,y0); return (xx>=a[0])&(xx<b[0])&(yy>=a[1])&(yy<b[1])
fm=box(*FIELD); cv.mask_fill(fm,PAL['D0'])
rows=((yy-int(P(0,FIELD[3])[1]))%8)
cv.mask_fill(fm&(rows<3),PAL['D1']); cv.mask_fill(fm&(rows==3),(110,72,44))
# wheat sprouts
for y in range(int(P(0,FIELD[3])[1])+2,int(P(0,FIELD[1])[1])-2,8):
    for x in range(int(P(FIELD[0],0)[0])+3,int(P(FIELD[2],0)[0])-3,5):
        h=random.randint(3,5)
        for k in range(h): cv.put(x,y-k,PAL['G2'] if k<h-1 else (226,204,96))
        cv.put(x-1,y-h+2,(226,204,96)); cv.put(x+1,y-h+1,(208,184,82))
cv.mask_fill(edge(fm),PAL['G0'])
gm=box(*GARDEN); cv.mask_fill(gm,PAL['D0']); cv.mask_fill(gm&(((yy-int(P(0,GARDEN[3])[1]))%10)<4),PAL['D1'])
for y in range(int(P(0,GARDEN[3])[1])+6,int(P(0,GARDEN[1])[1])-4,10):
    for x in range(int(P(GARDEN[0],0)[0])+6,int(P(GARDEN[2],0)[0])-6,12):
        cv.ellipse(x,y-2,4,3,PAL['L1']); cv.put(x-1,y-4,PAL['L3']); cv.put(x+1,y-3,PAL['L3']); cv.put(x,y-5,PAL['L2'])
# ---- pond and stream
pond=Image.new('L',(W,H),0); d=ImageDraw.Draw(pond)
px_,py_=P(*POND)
pts=[]
for i in range(28):
    a=i/28*6.283; r=1+0.12*math.sin(a*3+1)+0.08*math.cos(a*5)
    pts.append((px_+math.cos(a)*3.0*PPU*r, py_+math.sin(a)*1.3*PPU*r))
d.polygon(pts,fill=1)
sx=[(-18.6,-6.6),(-17.4,-6.9),(-16.4,-7.6)]
d.line([P(*p) for p in sx],fill=1,width=int(0.9*PPU))
water=np.array(pond).astype(bool)
sand=edge(water); sand|=edge(sand|water)&~water
cv.mask_fill(edge(sand|water),PAL['G0'])
cv.mask_fill(sand,PAL['D2'])
cv.mask_fill(water,PAL['W1'])
cv.mask_fill(inner(water),PAL['W0']); 
sh=water.copy(); sh[2:]&=water[:-2]; cv.mask_fill(water&~sh,PAL['W0'])
for _ in range(40):
    x=rng.integers(int(px_-3*PPU),int(px_+3*PPU)); y=rng.integers(int(py_-1.2*PPU),int(py_+1.2*PPU))
    if water[y,x] and water[y,min(W-1,x+5)]: cv.hline(x,x+random.randint(2,4),y,PAL['W2'])
for (lx,ly) in [(-15.6,-8.3),(-12.6,-7.6),(-13.2,-8.8)]:
    a,b=P(lx,ly); cv.ellipse(a,b,4,2.5,PAL['L2']); cv.put(a+1,b-1,(240,170,190)); cv.put(a,b-1,(250,220,230))
# ---- cliff face with stairs
top=int(P(0,CLIFF_TOP)[1]); base=int(P(0,CLIFF_BASE)[1])
cm=cliff&~stairs
cv.mask_fill(cm,PAL['S1'])
row=(yy-top)//7; col=(xx+(row%2)*6)//12
cv.mask_fill(cm&(((yy-top)%7)==0),PAL['S0']); cv.mask_fill(cm&(((xx+(row%2)*6)%12)==0),PAL['S0'])
cv.mask_fill(cm&(((yy-top)%7)==1)&(((xx+(row%2)*6)%12)!=0),PAL['S2'])
cv.mask_fill(cm&(yy<top+3),PAL['G0']); cv.mask_fill(cm&(yy==top),PAL['G2'])
for x in range(0,W,3):
    if cm[top+3,x] and rng.random()<0.5: cv.put(x,top+3,PAL['G0']); 
    if cm[top+4,x] and rng.random()<0.25: cv.put(x,top+4,PAL['G0'])
cv.mask_fill(cm&(yy>=base-2),PAL['S0'])
cv.mask_fill((yy>=base)&(yy<base+3)&(np.abs(xx/PPU-18)<PLATEAU_X)&~(np.abs(xx/PPU-18)<STAIR_HALF)&~road,(64,110,50))
# stairs: steps
st=stairs; step=(yy-top)//5
cv.mask_fill(st,PAL['C2']); cv.mask_fill(st&(((yy-top)%5)>=3),PAL['C1']); cv.mask_fill(st&(((yy-top)%5)==4),PAL['C0'])
sx0=int(W/2-STAIR_HALF*PPU); sx1=int(W/2+STAIR_HALF*PPU)
cv.rect(sx0,top,3,base-top,PAL['S0']); cv.rect(sx1-3,top,3,base-top,PAL['S0'])
cv.rect(sx0+1,top,1,base-top,PAL['S2']); cv.rect(sx1-2,top,1,base-top,PAL['S2'])
# terrace side walls (seen as thin stone strip at x=+-11)
for sgn in (-1,1):
    x=int(W/2+sgn*PLATEAU_X*PPU)
    x0=x if sgn>0 else x-6
    cv.rect(x0,0,6,base,PAL['S1']); cv.rect(x0+(0 if sgn>0 else 5),0,1,base,PAL['S0'])
    for y in range(0,base,7): cv.rect(x0,y,6,1,PAL['S0'])
    cv.rect(x0+(5 if sgn>0 else 0),0,1,base,PAL['S2'] if sgn<0 else PAL['S0'])
    cv.rect(x0+(6 if sgn>0 else -3),0,3,base+3,(64,110,50))
# ---- grass details: tufts and flowers (not on roads/plaza/water/field/cliff)
free=~(road|plaza|water|sand|cliff|fm|gm)
for _ in range(1400):
    x=int(rng.integers(2,W-3)); y=int(rng.integers(3,H-2))
    if not free[y,x] or not free[y-3,x]: continue
    r=rng.random()
    if r<0.62:
        c=PAL['G0'] if cv.a[y,x,1]>150 else PAL['G3']
        cv.put(x,y,c); cv.put(x-1,y-1,c); cv.put(x+1,y-1,c); cv.put(x,y-2,c)
    elif r<0.9:
        fc=[(250,250,240),(250,220,90),(240,140,170),(160,190,250)][int(rng.integers(0,4))]
        cv.put(x,y,fc); cv.put(x-1,y,fc); cv.put(x+1,y,fc); cv.put(x,y-1,fc); cv.put(x,y+1,fc); cv.put(x,y,(250,210,80) if fc!=(250,220,90) else (230,130,50))
    else:
        cv.rect(x,y,3,2,PAL['S2']); cv.put(x,y+1,PAL['S1']); cv.put(x+2,y+1,PAL['S1'])
cv.save('out/Ground_Px.png')
np.save('out/masks.npy',np.stack([road,plaza,water,cliff,terrace,fm,gm]))
print('ground',W,H)
