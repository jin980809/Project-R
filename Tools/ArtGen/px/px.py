"""Tiny pixel-art helpers (no anti-aliasing). Canvas = numpy RGBA uint8 [h,w,4]."""
import numpy as np, random, math
from PIL import Image, ImageDraw
PPU=32
HALF=(18,10)
MAPW,MAPH=36*PPU,20*PPU
def w2p(x,y): return ((x+HALF[0])*PPU,(HALF[1]-y)*PPU)
OUT=(38,27,36)
def C(*v): return tuple(v)
PAL=dict(
 G0=(70,128,54),G1=(94,160,62),G2=(118,182,70),G3=(150,204,90),
 D0=(138,94,58),D1=(178,132,84),D2=(204,162,106),D3=(222,190,136),
 C0=(104,98,100),C1=(150,144,140),C2=(182,176,166),C3=(208,202,190),
 W0=(40,80,140),W1=(58,118,182),W2=(92,160,214),W3=(170,214,236),
 WD0=(84,52,36),WD1=(124,78,46),WD2=(162,108,62),WD3=(194,140,84),
 S0=(74,74,90),S1=(112,112,128),S2=(150,150,162),S3=(188,188,196),
 P0=(190,162,124),P1=(222,200,160),P2=(242,226,192),
 L0=(36,84,52),L1=(52,116,58),L2=(78,150,66),L3=(116,182,82),
 WIN=(255,214,120),WIN2=(255,240,190),GL=(70,110,160),
)
ROOFS=dict(
 orange=[(140,60,40),(198,94,50),(228,132,70),(244,168,104)],
 red=[(116,38,44),(170,56,56),(206,84,74),(230,122,102)],
 blue=[(40,56,108),(56,86,156),(82,122,194),(120,162,218)],
 teal=[(32,86,90),(48,124,120),(74,160,146),(116,194,168)],
 slate=[(50,50,66),(72,74,94),(98,102,124),(130,136,158)],
 purple=[(70,44,96),(104,66,140),(136,96,176),(172,136,206)],
 green=[(40,80,46),(60,112,58),(86,146,72),(124,180,96)],
 brown=[(80,50,34),(118,76,46),(154,104,62),(186,136,86)],
)
class Canvas:
    def __init__(s,w,h,fill=(0,0,0,0)):
        s.w,s.h=w,h; s.a=np.zeros((h,w,4),np.uint8); s.a[:]=fill
    def put(s,x,y,c):
        x,y=int(x),int(y)
        if 0<=x<s.w and 0<=y<s.h: s.a[y,x,:3]=c[:3]; s.a[y,x,3]=c[3] if len(c)>3 else 255
    def rect(s,x,y,w,h,c):
        x0,y0=max(0,int(x)),max(0,int(y)); x1,y1=min(s.w,int(x+w)),min(s.h,int(y+h))
        if x1>x0 and y1>y0: s.a[y0:y1,x0:x1,:3]=c[:3]; s.a[y0:y1,x0:x1,3]=c[3] if len(c)>3 else 255
    def hline(s,x0,x1,y,c): s.rect(min(x0,x1),y,abs(x1-x0)+1,1,c)
    def vline(s,x,y0,y1,c): s.rect(x,min(y0,y1),1,abs(y1-y0)+1,c)
    def mask_fill(s,m,c):
        s.a[m,:3]=c[:3]; s.a[m,3]=c[3] if len(c)>3 else 255
    def poly_mask(s,pts):
        im=Image.new('L',(s.w,s.h),0); ImageDraw.Draw(im).polygon([(round(x),round(y)) for x,y in pts],fill=1); return np.array(im).astype(bool)
    def ell_mask(s,cx,cy,rx,ry):
        yy,xx=np.mgrid[0:s.h,0:s.w]; return ((xx+0.5-cx)/rx)**2+((yy+0.5-cy)/ry)**2<=1.0
    def ellipse(s,cx,cy,rx,ry,c): s.mask_fill(s.ell_mask(cx,cy,rx,ry),c)
    def poly(s,pts,c): s.mask_fill(s.poly_mask(pts),c)
    def line(s,x0,y0,x1,y1,c):
        n=int(max(abs(x1-x0),abs(y1-y0)))+1
        for i in range(n+1):
            t=i/max(1,n); s.put(round(x0+(x1-x0)*t),round(y0+(y1-y0)*t),c)
    def alpha(s): return s.a[...,3]>0
    def outline(s,c=OUT,diag=False):
        m=s.alpha(); d=m.copy()
        d[1:]|=m[:-1]; d[:-1]|=m[1:]; d[:,1:]|=m[:,:-1]; d[:,:-1]|=m[:,1:]
        if diag:
            d[1:,1:]|=m[:-1,:-1]; d[:-1,:-1]|=m[1:,1:]; d[1:,:-1]|=m[:-1,1:]; d[:-1,1:]|=m[1:,:-1]
        s.mask_fill(d&~m,c)
    def paste(s,o,x,y):
        x,y=int(x),int(y); h,w=o.a.shape[:2]
        sx0,sy0=max(0,-x),max(0,-y); dx0,dy0=max(0,x),max(0,y)
        ww=min(w-sx0,s.w-dx0); hh=min(h-sy0,s.h-dy0)
        if ww<=0 or hh<=0: return
        src=o.a[sy0:sy0+hh,sx0:sx0+ww]; dst=s.a[dy0:dy0+hh,dx0:dx0+ww]
        al=src[...,3:4].astype(float)/255
        dst[...,:3]=(src[...,:3]*al+dst[...,:3]*(1-al)).astype(np.uint8)
        dst[...,3]=np.maximum(dst[...,3],src[...,3])
    def shade_mask(s,m,f):
        v=s.a[m,:3].astype(float)*f; s.a[m,:3]=v.clip(0,255).astype(np.uint8)
    def save(s,p): Image.fromarray(s.a,'RGBA').save(p)
    def img(s): return Image.fromarray(s.a,'RGBA')
def shadow(cv,cx,cy,rx,ry,alpha=70):
    m=cv.ell_mask(cx,cy,rx,ry)&(cv.a[...,3]==0)
    cv.a[m,:3]=(20,30,20); cv.a[m,3]=alpha
def checker(m):
    h,w=m.shape; yy,xx=np.mgrid[0:h,0:w]; return m&(((xx+yy)%2)==0)
