import json,sys
sys.path.insert(0,'/home/claude/v3')
from PIL import Image
from px import *
from npcs import NPCS
import compose as CP
items=json.load(open('out/scene.json'))
G0=Image.open('out/Ground_Px.png').convert('RGBA'); M=200
G=Image.new('RGBA',(G0.width+2*M,G0.height+2*M),(0,0,0,255)); G.paste(G0,(M,M))
def w2p(x,y): return ((x+18)*PPU+M,(10-y)*PPU+M)
def pivot(im): return im.width/2, im.height-3
draw=[]
for it in items:
    draw.append((it['y'],it))
for n in NPCS: draw.append((n['pos'][1],dict(kind='npc',n=n)))
draw.sort(key=lambda t:-t[0])
cache={}
LOOKHAIR=[(242,242,242),(56,56,64),(115,77,51),(242,204,102),(209,82,56),(242,143,51),(242,143,184),(158,107,224),(92,143,242),(107,194,107),(153,153,163),(191,224,250)]
for _,it in draw:
    if it['kind']=='npc':
        n=it['n']; c=n['c']; import os
        ch=Image.open(f'/home/claude/v3/out/Chest_{c}.png'); hp=f'/home/claude/v3/out/Helmet_{c}.png'
        hm=Image.open(hp) if os.path.exists(hp) else None
        im=CP.compose(ch,hm,hair_col=LOOKHAIR[n['look'][3]%12])
        im=im.crop(im.getbbox()); s=1.3*PPU/100*n.get('scale',1)
        im=im.resize((max(1,int(im.width*s)),max(1,int(im.height*s))),Image.LANCZOS)
        if n['left']: im=im.transpose(Image.FLIP_LEFT_RIGHT)
        x,y=w2p(*n['pos']); G.alpha_composite(im,(int(x-im.width/2),int(y-im.height+2)))
        continue
    im=Image.open(f"out/{it['file']}.png").convert('RGBA')
    x,y=w2p(it['x'],it['y'])
    if it['kind']=='fence':
        w=int(it['width']*PPU); t=Image.new('RGBA',(w,im.height))
        for k in range(0,w,im.width): t.alpha_composite(im,(k,0)) if k+im.width<=w else t.alpha_composite(im.crop((0,0,w-k,im.height)),(k,0))
        im=t
    px_,py_=im.width/2,im.height-3
    ox,oy=int(round(x-px_)),int(round(y-py_))
    G.alpha_composite(im,(ox,oy))
    for (pre,n,(lx,ly),fps,off) in it['loops']:
        a=Image.open(f'out/{pre}_00.png').convert('RGBA')
        ax,ay=int(ox+lx-a.width/2),int(oy+ly-a.height/2)
        G.alpha_composite(a,(ax,ay))
G=G.crop((M,M,M+G0.width,M+G0.height)); G.save('preview.png')
G.resize((G.width*2,G.height*2),Image.NEAREST).crop((300,350,1500,1050)).save('preview_zoom.png')
