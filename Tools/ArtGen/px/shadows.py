"""Bake cast shadows of buildings, trees and big props onto Ground_Px.png (run after scene.py).
Sun from the upper-left: each object's silhouette is leaned to the right and squashed onto the ground."""
import json, numpy as np
from PIL import Image
from px import PPU, w2p
SHEAR=0.55     # horizontal shift per pixel of height
SQUASH=0.55    # vertical squash of the silhouette
DARK=np.array([0.60,0.64,0.74])   # multiply (slightly blue)
KINDS={'building':1.0,'tree':1.0}
PROPS=('Prop_Windmill','Prop_Gate','Prop_StallFruit','Prop_StallBread','Prop_Well','Prop_Lamp','Prop_Fountain','Prop_Cart','Prop_Board','Prop_Signpost','Prop_Haystack','Prop_Barrel','Prop_Crate','Bush','Bush_Flower','Bush_Yellow','Prop_Bench','Prop_Anvil','Prop_Sack')
g=Image.open('out/Ground_Px.png').convert('RGBA'); G=np.array(g).astype(float)
H,W=G.shape[:2]
shadow=np.zeros((H,W),bool)
items=json.load(open('out/scene.json'))
for it in items:
    if not (it['kind'] in KINDS or it['file'] in PROPS): continue
    im=np.array(Image.open(f"out/{it['file']}.png").convert('RGBA'))
    a=im[...,3]>200
    # ignore the soft contact-shadow pixels baked into the sprite (they are translucent)
    h,w=a.shape; base=h-3
    px,py=w2p(it['x'],it['y']); ox=int(round(px-w/2)); oy=int(round(py-base))
    ys,xs=np.nonzero(a)
    hgt=(base-ys).clip(0,None)
    sx=(ox+xs+hgt*SHEAR).astype(int); sy=(py-hgt*SQUASH).astype(int)
    ok=(sx>=0)&(sx<W)&(sy>=0)&(sy<H)
    shadow[sy[ok],sx[ok]]=True
    # fill 1-px holes from the projection
    shadow[sy[ok],np.clip(sx[ok]+1,0,W-1)]=True
# close small gaps
s2=shadow.copy(); s2[1:-1]|=shadow[:-2]&shadow[2:]; s2[:,1:-1]|=shadow[:,:-2]&shadow[:,2:]
shadow=s2
G[shadow,:3]*=DARK
Image.fromarray(G.clip(0,255).astype(np.uint8),'RGBA').save('out/Ground_Px.png')
print('shadow px',shadow.sum())
