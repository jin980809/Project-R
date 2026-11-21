"""Check NPC walking routes against colliders (props, buildings, terrain walls, standing NPCs)."""
import json, math
from npcs import NPCS
from layout import *
R=0.24
items=json.load(open('out/scene.json'))
rects=[]
for it in items:
    if it['coll']:
        cw,ch=it['coll']; cy=it['y']+ch*0.35
        rects.append((it['name'],it['x']-cw/2,cy-ch/2,it['x']+cw/2,cy+ch/2))
cm=(CLIFF_TOP+CLIFF_BASE)/2; chh=CLIFF_TOP-CLIFF_BASE+0.1; cwid=PLATEAU_X+0.2-STAIR_HALF; cc=(PLATEAU_X+0.2+STAIR_HALF)/2
def box(n,cx,cy,w,h): rects.append((n,cx-w/2,cy-h/2,cx+w/2,cy+h/2))
box('CliffL',-cc,cm,cwid,chh); box('CliffR',cc,cm,cwid,chh)
sh=10-CLIFF_BASE
box('SideL',-PLATEAU_X-0.1,CLIFF_BASE+sh/2,0.3,sh); box('SideR',PLATEAU_X+0.1,CLIFF_BASE+sh/2,0.3,sh)
box('Pond',-14.2,-8.1,5.6,2.3); box('Stream',-17.3,-7.0,2.4,0.8); box('Gate',0,6+2.25,7.2,4.4)
for n in NPCS:
    if not n.get('wp'): box('Npc'+n['name'],n['pos'][0],n['pos'][1]+0.15,0.5,0.35)
def seg_hits(a,b):
    hits=[]
    L=math.dist(a,b); k=max(2,int(L/0.05))
    for i in range(k+1):
        t=i/k; x=a[0]+(b[0]-a[0])*t; y=a[1]+(b[1]-a[1])*t
        for (n,x0,y0,x1,y1) in rects:
            if x0-R<x<x1+R and y0-0.05<y+0.15<y1+R+0.1 and n not in hits: hits.append(n)
    return hits
bad=0
for n in NPCS:
    wp=n.get('wp')
    if not wp: continue
    for i in range(len(wp)):
        a,b=wp[i],wp[(i+1)%len(wp)]
        h=seg_hits(a,b)
        if h: bad+=1; print(n['name'],a,'->',b,h)
print('bad',bad)
