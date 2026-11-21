import numpy as np, random, math, json, sys
from PIL import Image
from px import *
from layout import *
CAMPFIRE=(6.0,5.4)
rng=random.Random(21)
def img(f): return Image.open(f'out/{f}.png')
items=[]  # dict(kind,name,file,x,y,coll,fade,loops,light)
def add(kind,name,file,x,y,coll=None,fade=None,loops=(),**kw):
    d=dict(kind=kind,name=name,file=file,x=round(x,2),y=round(y,2),coll=coll,fade=fade,loops=list(loops)); d.update(kw); items.append(d); return d
def size(file):
    im=img(file); return im.width/PPU, im.height/PPU
# buildings
for name,(file,x,y) in BUILDINGS.items():
    w,h=size(file)
    wallw={'Bld_Shop':141,'Bld_Library':154,'Bld_Barber':104,'Bld_Tavern':147,'Bld_Smithy':116,'Bld_HouseA':100,'Bld_HouseB':92,'Bld_HouseC':96}[file]/PPU
    loops=[]
    add('building',name,file,x,y,coll=(wallw+0.3,0.7),fade=(-wallw/2-0.3,0.1,wallw/2+0.3,h-0.6),loops=loops)
# chimney smoke: chimney x offsets (wall-local px) from objects.py
chim={'Bld_Shop':100,'Bld_Library':20,'Bld_Barber':80,'Bld_Tavern':24,'Bld_Smithy':88,'Bld_HouseA':76,'Bld_HouseB':14,'Bld_HouseC':70}
for it in items:
    if it['kind']=='building':
        im=img(it['file']); mx,e=10,5
        cx=mx+e+chim[it['file']]+5; cy=14-10-14
        it['loops'].append(('Anim_Smoke',6,(cx,cy+2),7,rng.random()))
gate=add('prop','TowerGate','Prop_Gate',GATE[0],GATE[1],coll=None,loops=[('Anim_Portal',6,(116,128),10,0),('Anim_Flame',4,(66,106),10,0),('Anim_Flame',4,(166,106),10,0.37)],light=('portal',116,120))
wm=add('prop','Windmill','Prop_Windmill',*WINDMILL,coll=(1.7,0.6),fade=(-1.9,0.1,1.9,6.2),loops=[('Anim_Windmill',4,(48,30),5,0)])
add('prop','Fountain','Prop_Fountain',FOUNTAIN[0],FOUNTAIN[1]-1.35,coll=(3.1,1.3),loops=[('Anim_Fountain',4,(56,68),8,0)])
lamps=[(-3.4,2.4),(3.4,2.4),(-4.3,-3.4),(4.3,-3.4),(-6.0,0.45),(6.4,0.45),(-12.2,-1.75),(12.2,-1.75)]
for i,(x,y) in enumerate(lamps): add('prop',f'Lamp{i}','Prop_Lamp',x,y,coll=(0.3,0.2),light=('lamp',0,1.6))
add('prop','BenchL','Prop_Bench',-2.3,-3.6,coll=(1.2,0.25)); add('prop','BenchR','Prop_Bench',2.3,-3.6,coll=(1.2,0.25))
add('prop','StallFruit','Prop_StallFruit',-3.6,-4.7,coll=(2.5,0.5)); add('prop','StallBread','Prop_StallBread',3.6,-4.7,coll=(2.5,0.5))
add('prop','Board','Prop_Board',-2.5,1.7,coll=(1.0,0.2)); add('prop','Signpost','Prop_Signpost',1.5,-7.7,coll=(0.3,0.2))
add('prop','Well','Prop_Well',-10.2,-2.6,coll=(1.3,0.45)); add('prop','Cart','Prop_Cart',12.6,-2.4,coll=(1.6,0.4))
add('prop','Anvil','Prop_Anvil',-13.1,2.85,coll=(0.6,0.25))
add('prop','Campfire','Prop_Campfire',6.0,5.4,coll=(0.8,0.3),loops=[('Anim_Campfire',4,(15,-2),10,0)],light=('fire',15,2))
for i,(x,y) in enumerate([(4.9,5.45),(7.1,5.45),(6.0,4.7)]): add('prop',f'Log{i}','Prop_Log',x,y,coll=(0.9,0.2))
for i,(x,y) in enumerate([(10.3,6.75),(3.9,8.2)]): add('prop',f'Hay{i}','Prop_Haystack',x,y,coll=(0.9,0.3))
deco=[('Prop_Crate',-5.7,-4.3),('Prop_Barrel',-5.9,-3.8),('Prop_Crate',5.6,-4.3),('Prop_Sack',5.9,-3.9),('Prop_Barrel',12.0,0.5),('Prop_Barrel',12.3,0.2),('Prop_Crate',17.0,0.4),
      ('Prop_Barrel',-16.9,3.1),('Prop_Crate',-16.5,2.8),('Prop_Sack',-11.1,0.6),('Prop_Sack',-10.8,0.4),('Prop_Crate',-6.2,0.8),('Prop_Pumpkin',5.2,8.0),('Prop_Pumpkin',9.4,8.5),('Prop_Pumpkin',6.8,8.9),
      ('Prop_Barrel',10.6,-7.6),('Prop_Crate',-9.0,-7.6),('Prop_Sack',-3.9,6.7),('Prop_Crate',-9.8,5.8)]
for i,(f,x,y) in enumerate(deco): add('prop',f'Deco{i}',f,x,y,coll=(0.45,0.2))
# garden fence (tiled) and posts
add('fence','GardenFenceTop','Fence_H',(GARDEN[0]+GARDEN[2])/2,GARDEN[3]+0.05,coll=None,width=GARDEN[2]-GARDEN[0])
add('fence','GardenFenceL','Fence_H',(GARDEN[0]+6.9)/2,GARDEN[1]-0.05,coll=(6.9-GARDEN[0],0.2),width=6.9-GARDEN[0])
add('fence','GardenFenceR','Fence_H',(7.9+GARDEN[2])/2,GARDEN[1]-0.05,coll=(GARDEN[2]-7.9,0.2),width=GARDEN[2]-7.9)
for s,x in (('L',GARDEN[0]),('R',GARDEN[2])):
    y=GARDEN[1]+0.45
    while y<GARDEN[3]: add('prop',f'GardenPost{s}{y:.1f}','Prop_FencePost',x,y,coll=(0.25,0.4)); y+=0.42
# terrace edge fence (cliff top, leave stairs)
for sgn in (-1,1):
    x0=STAIR_HALF+0.15; x1=PLATEAU_X-0.1
    add('fence',f'CliffFence{"LR"[sgn>0]}','Fence_H',sgn*(x0+x1)/2,CLIFF_TOP+0.12,coll=None,width=x1-x0)
# trees
masks=np.load('out/masks.npy'); road,plaza,water,cliff,terrace,fm,gm=masks
blocked=road|plaza|water|cliff|fm|gm
def pxy(x,y): return int((x+18)*PPU),int((10-y)*PPU)
rects=[]
for it in items:
    if it['kind']=='building':
        w,h=size(it['file']); rects.append((it['x']-w/2-0.2,it['y']-0.3,it['x']+w/2+0.2,it['y']+h))
rects+= [(-3.9,5.6,3.9,10.5),(WINDMILL[0]-1.4,WINDMILL[1]-0.3,WINDMILL[0]+1.4,WINDMILL[1]+3),(4.2,4.3,8.0,6.4),(-4.2,-5.6,4.2,-3.0),(-17.5,-9.6,-11.0,-6.5)]
for it in items:
    if it['kind']=='prop' and it['name'].startswith(('Lamp','Well','Cart','Board','Signpost','Deco','Anvil')): rects.append((it['x']-0.6,it['y']-0.4,it['x']+0.6,it['y']+0.6))
def free(x,y,r=0.5):
    for (a,b,c,d) in rects:
        if a-r<x<c+r and b-r<y<d+0.2: return False
    for dx in (-r,0,r):
        for dy in (-0.3,0,0.4):
            X,Y=pxy(x+dx,y+dy)
            if 0<=X<MAPW and 0<=Y<MAPH and blocked[Y,X]: return False
    for t in trees:
        if abs(t[1]-x)<0.95 and abs(t[2]-y)<0.75: return False
    return True
trees=[]
kinds=['Tree_Big','Tree_Big2','Tree_Small','Tree_Apple','Tree_Big','Tree_Orange','Tree_Pine','Tree_Big2']
cand=[]
for x in np.arange(-17.6,17.7,1.05):
    cand+=[(x,10.2),(x,9.3),(x,-9.6),(x,-10.3)]
for y in np.arange(-9.6,10.0,0.9):
    cand+=[(-17.5,y),(-16.6,y),(17.5,y),(16.6,y)]
cand+=[(-5.3,-2.2),(6.2,-2.8),(12.8,-5.0),(15.2,-4.4),(16.2,-6.6),(13.6,-6.8),(14.6,-8.4),(11.6,-5.6),(-4.6,6.3),(4.6,6.3),(-3.0,8.2),(3.4,8.8),
      (-9.6,-5.0),(-4.4,-8.0),(4.4,-7.9),(-11.6,4.4),(11.7,3.6),(-10.0,9.9),(-5.0,10.0),(9.2,4.3),(-12.6,7.6),(12.8,8.2),(-17,8),(-6.3,-6.3)]
for (x,y) in cand:
    x+=rng.uniform(-0.25,0.25); y+=rng.uniform(-0.2,0.2)
    if free(x,y): trees.append((rng.choice(kinds),x,y))
for i,(k,x,y) in enumerate(trees):
    w,h=size(k); add('tree',f'Tree{i}',k,x,y,coll=(0.4,0.25),fade=(-w/2,0.3,w/2,h-0.2))
# bushes
bushes=[]
for _ in range(400):
    x=rng.uniform(-17.5,17.5); y=rng.uniform(-9.8,9.8)
    if len(bushes)>=34: break
    if free(x,y,0.3) and all(abs(b[1]-x)>1.2 or abs(b[2]-y)>0.8 for b in bushes):
        bushes.append((rng.choice(['Bush','Bush_Flower','Bush_Yellow','Bush']),x,y))
for i,(k,x,y) in enumerate(bushes): add('prop',f'Bush{i}',k,x,y,coll=(0.6,0.2))
json.dump(items,open('out/scene.json','w'),indent=0)
print(len(items),'items',len(trees),'trees',len(bushes),'bushes')
