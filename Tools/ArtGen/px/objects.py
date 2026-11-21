import numpy as np, random, math
from px import *
BASE=3   # pivot rows from bottom (feet line)
OUTG=(30,56,40)
def finish(cv,name,outline=True,oc=OUT):
    if outline: cv.outline(oc)
    cv.save(f'out/{name}.png'); return cv
# ------------------------------------------------------------------ building
def roof(cv,x0,y0,w,h,pal,ridge=True):
    R0,R1,R2,R3=pal
    cv.rect(x0+2,y0,w-4,1,R1); cv.rect(x0+1,y0+1,w-2,1,R1); cv.rect(x0,y0+2,w,h-2,R1)
    for y in range(y0+4,y0+h-2,6):
        cv.hline(x0,x0+w-1,y,R0); cv.hline(x0+1,x0+w-2,y+1,R2)
        off=0 if ((y-y0)//6)%2 else 5
        for x in range(x0+3+off,x0+w-2,10): cv.vline(x,y+1,y+5,R0)
    if ridge:
        cv.rect(x0+2,y0,w-4,2,R3); cv.rect(x0,y0+2,w,1,R2)
    # right-side shade, eave
    cv.shade_mask(_box(cv,x0+int(w*0.82),y0,w-int(w*0.82),h),0.86)
    cv.rect(x0,y0+h-3,w,1,R2); cv.rect(x0,y0+h-2,w,2,R0)
def _box(cv,x,y,w,h):
    m=np.zeros(cv.a.shape[:2],bool); m[max(0,y):y+h,max(0,x):x+w]=True; return m
def wall(cv,x0,y0,w,h,style):
    if style=='plaster':
        cv.rect(x0,y0,w,h,PAL['P1']); cv.rect(x0,y0+h-4,w,4,PAL['P0'])
        cv.rect(x0,y0,3,h,PAL['WD1']); cv.rect(x0+w-3,y0,3,h,PAL['WD0'])
        for x in range(x0+30,x0+w-20,30): cv.rect(x,y0,3,h,PAL['WD1'])
        cv.rect(x0,y0+h//2-1,w,3,PAL['WD1'])
    elif style=='wood':
        cv.rect(x0,y0,w,h,PAL['WD2'])
        for y in range(y0+5,y0+h,6): cv.hline(x0,x0+w-1,y,PAL['WD1'])
        for y in range(y0+1,y0+h,6): cv.hline(x0,x0+w-1,y,PAL['WD3'])
        cv.rect(x0,y0,3,h,PAL['WD0']); cv.rect(x0+w-3,y0,3,h,PAL['WD0'])
    elif style in ('stone','stonelight'):
        a,b,c=(PAL['S1'],PAL['S0'],PAL['S2']) if style=='stone' else (PAL['C2'],PAL['C0'],PAL['C3'])
        cv.rect(x0,y0,w,h,a)
        for i,y in enumerate(range(y0,y0+h,7)):
            cv.hline(x0,x0+w-1,y,b); cv.hline(x0,x0+w-1,y+1,c)
            for x in range(x0+(i%2)*7,x0+w,14): cv.vline(x,y,y+6,b)
    elif style=='brick':
        cv.rect(x0,y0,w,h,(176,92,70))
        for i,y in enumerate(range(y0,y0+h,4)):
            cv.hline(x0,x0+w-1,y,(122,64,56))
            for x in range(x0+(i%2)*5,x0+w,10): cv.vline(x,y,y+3,(122,64,56))
    cv.rect(x0,y0,w,3,(0,0,0,0)); cv.a[y0:y0+3,x0:x0+w,:3]=(cv.a[y0+3:y0+6,x0:x0+w,:3]*0.55).astype(np.uint8); cv.a[y0:y0+3,x0:x0+w,3]=255
def door(cv,cx,by,w=16,h=24,col=None):
    col=col or PAL['WD1']; x=cx-w//2; y=by-h
    cv.rect(x-2,y+3,w+4,h-3,PAL['WD0']); cv.ellipse(cx,y+5,w/2+2,6,PAL['WD0'])
    cv.rect(x,y+4,w,h-4,col); cv.ellipse(cx,y+6,w/2,5,col)
    for xx in range(x+4,x+w,4): cv.vline(xx,y+3,by-1,PAL['WD0'])
    cv.put(x+w-4,y+h//2+2,(232,196,90)); cv.rect(x-3,by,w+6,2,PAL['S2'])
def window(cv,cx,cy,w=12,h=12,lit=True,shutter=None):
    x=cx-w//2; y=cy-h//2
    cv.rect(x-2,y-2,w+4,h+4,PAL['WD0'])
    cv.rect(x,y,w,h,PAL['WIN'] if lit else PAL['GL'])
    cv.rect(x,y,w//2-1,h//2-1,PAL['WIN2'] if lit else (120,160,200))
    cv.rect(cx-1,y,2,h,PAL['WD0']); cv.rect(x,cy-1,w,2,PAL['WD0'])
    cv.rect(x-3,y+h+1,w+6,2,PAL['WD2'])
    if shutter:
        cv.rect(x-8,y-2,5,h+4,shutter); cv.rect(x+w+3,y-2,5,h+4,shutter)
        for yy in range(y,y+h+2,3): cv.hline(x-8,x-4,yy,tuple(int(v*0.7) for v in shutter)); cv.hline(x+w+3,x+w+7,yy,tuple(int(v*0.7) for v in shutter))
def chimney(cv,x,y,h=16):
    cv.rect(x,y,10,h,PAL['S1']); cv.rect(x-1,y,12,3,PAL['S2']); cv.rect(x+7,y+3,3,h-3,PAL['S0'])
def building(name,W,WH,RH,roofpal,style,doorx=None,windows=(),chim=None,extra=None,margin=(10,14),dormer=False):
    e=5; mx,mt=margin
    w=W+2*e+2*mx; h=mt+RH+WH+BASE+2
    cv=Canvas(w,h); x0=mx+e; ry=mt; wy=mt+RH-3; by=h-BASE
    wall(cv,x0,wy,W,by-wy,style)
    if chim is not None: chimney(cv,x0+chim,ry-10,16)
    roof(cv,x0-e,ry,W+2*e,RH,roofpal)
    if dormer:
        dw=34; dx=x0+W//2-dw//2+(dormer if isinstance(dormer,int) and dormer is not True else 0)
        cv.poly([(dx-3,ry+RH-1),(dx+dw//2,ry+RH-30),(dx+dw+3,ry+RH-1)],roofpal[0])
        cv.poly([(dx+1,ry+RH-1),(dx+dw//2,ry+RH-26),(dx+dw-1,ry+RH-1)],PAL['P1'] if style!='stone' else PAL['S2'])
        window(cv,dx+dw//2,ry+RH-10,8,8)
    for (cx,cy,*rest) in windows: window(cv,x0+cx,wy+cy,*rest)
    if doorx is not None: door(cv,x0+doorx,by)
    if extra: extra(cv,x0,wy,by,ry)
    cv.outline()
    # ground shadow + contact
    shadow(cv,w/2,by+1,W/2+e+4,4,80)
    cv.save(f'out/{name}.png')
    return cv,(x0,wy,by,ry)
def sign(cv,x,y,w,h,col,icon=None):
    cv.rect(x,y,w,h,PAL['WD0']); cv.rect(x+1,y+1,w-2,h-2,col)
    if icon: icon(cv,x+w//2,y+h//2)
def awning(cv,x,y,w,c1,c2,depth=10):
    for i in range(0,w,8):
        cv.rect(x+i,y,8,depth,c1 if (i//8)%2==0 else c2)
    for i in range(0,w,8):
        cv.ellipse(x+i+4,y+depth,4,3,c1 if (i//8)%2==0 else c2)
    cv.rect(x,y,w,2,tuple(int(v*0.8) for v in c1))
# ------------------------------------------------------------------ buildings
def make_buildings():
    out={}
    def shop_extra(cv,x0,wy,by,ry):
        awning(cv,x0+12,wy+8,40,(200,60,60),(244,236,220))
        awning(cv,x0+92,wy+8,40,(200,60,60),(244,236,220))
        sign(cv,x0+50,wy+4,42,12,(90,56,140)); 
        for i,cx in enumerate((x0+60,x0+71,x0+82)): cv.ellipse(cx,wy+10,3,3,[(250,210,90),(150,220,250),(240,140,200)][i])
        for bx in (x0+6,x0+124): cv.rect(bx,by-14,11,14,PAL['WD2']); cv.rect(bx,by-14,11,2,PAL['WD3']); cv.rect(bx,by-8,11,1,PAL['WD0'])
    out['Bld_Shop']=building('Bld_Shop',141,58,74,ROOFS['red'],'plaster',doorx=70,windows=[(32,34,12,12,True),(110,34,12,12,True)],chim=100,extra=shop_extra,dormer=True)
    def lib_extra(cv,x0,wy,by,ry):
        for cx in (16,138): cv.rect(x0+cx-5,wy+2,10,by-wy-2,PAL['C3']); cv.rect(x0+cx-6,wy+2,12,4,PAL['C2']); cv.rect(x0+cx+2,wy+6,3,by-wy-6,PAL['C1'])
        cv.ellipse(x0+77,wy+16,9,9,PAL['WD0']); cv.ellipse(x0+77,wy+16,7,7,PAL['WIN']); cv.rect(x0+76,wy+9,2,14,PAL['WD0']); cv.rect(x0+70,wy+15,14,2,PAL['WD0'])
        sign(cv,x0+60,by-48,34,10,(60,90,160))
        # books icon
        for i,c in enumerate([(220,80,70),(240,200,90),(90,170,110)]): cv.rect(x0+68+i*6,by-46,4,6,c)
    out['Bld_Library']=building('Bld_Library',154,74,84,ROOFS['blue'],'stonelight',doorx=77,windows=[(40,26,12,16,True),(114,26,12,16,True),(40,56,12,12,True),(114,56,12,12,True)],chim=20,extra=lib_extra)
    def barber_extra(cv,x0,wy,by,ry):
        # barber pole
        px=x0+20; cv.rect(px-3,wy+8,7,30,PAL['WD0']); 
        for i in range(26): cv.hline(px-2,px+2,wy+10+i,(220,60,60) if ((i//3)%3==0) else ((70,110,200) if (i//3)%3==1 else (244,244,240)))
        cv.ellipse(px,wy+8,4,3,(232,196,90)); cv.ellipse(px,wy+38,4,3,(232,196,90))
        awning(cv,x0+40,wy+6,48,(70,150,140),(240,240,230))
        sign(cv,x0+52,wy-2,26,8,(240,240,230))
        cv.line(x0+58,wy+1,x0+64,wy+4,(60,60,70)); cv.line(x0+58,wy+4,x0+64,wy+1,(60,60,70))
    out['Bld_Barber']=building('Bld_Barber',104,56,66,ROOFS['teal'],'plaster',doorx=72,windows=[(48,32,12,12,True)],chim=80,extra=barber_extra)
    def tavern_extra(cv,x0,wy,by,ry):
        cv.rect(x0,wy+34,147,3,PAL['WD0'])
        # hanging sign with mug
        cv.rect(x0+100,wy+38,20,2,PAL['WD0']); cv.vline(x0+104,wy+40,wy+43,PAL['WD0']); cv.vline(x0+116,wy+40,wy+43,PAL['WD0'])
        sign(cv,x0+100,wy+43,20,16,(196,140,70))
        cv.rect(x0+105,wy+47,7,8,(250,240,200)); cv.rect(x0+105,wy+50,7,5,(232,170,60)); cv.rect(x0+112,wy+49,2,4,(250,240,200))
        for bx in (x0+8,x0+20): 
            cv.rect(bx,by-15,11,15,PAL['WD1']); cv.rect(bx,by-11,11,2,PAL['S1']); cv.rect(bx,by-5,11,2,PAL['S1'])
    out['Bld_Tavern']=building('Bld_Tavern',147,76,78,ROOFS['orange'],'wood',doorx=72,windows=[(30,16,14,12,True,(64,120,90)),(117,16,14,12,True,(64,120,90)),(36,52,12,12,True),(110,52,12,12,True)],chim=24,extra=tavern_extra,dormer=True)
    def smith_extra(cv,x0,wy,by,ry):
        # open forge bay
        cv.rect(x0+60,wy+12,40,by-wy-12,(40,30,30)); cv.rect(x0+64,by-18,32,18,PAL['S0']); cv.rect(x0+68,by-14,24,8,(250,120,40)); cv.rect(x0+72,by-12,16,4,(255,220,120))
        cv.rect(x0+58,wy+10,44,3,PAL['WD0'])
        for x in (x0+58,x0+99): cv.rect(x,wy+10,3,by-wy-10,PAL['WD0'])
        sign(cv,x0+14,wy+6,24,12,(90,90,100)); cv.rect(x0+20,wy+12,12,3,(60,60,70)); cv.rect(x0+23,wy+9,6,3,(60,60,70))
    out['Bld_Smithy']=building('Bld_Smithy',116,58,64,ROOFS['slate'],'stone',doorx=34,windows=[],chim=88,extra=smith_extra)
    out['Bld_HouseA']=building('Bld_HouseA',100,52,64,ROOFS['orange'],'plaster',doorx=50,windows=[(22,26,12,12,True,(170,70,60)),(78,26,12,12,True,(170,70,60))],chim=76)
    out['Bld_HouseB']=building('Bld_HouseB',92,50,62,ROOFS['red'],'wood',doorx=30,windows=[(66,24,12,12,True,(70,110,170))],chim=14)
    out['Bld_HouseC']=building('Bld_HouseC',96,52,62,ROOFS['blue'],'plaster',doorx=62,windows=[(26,26,12,12,True,(200,150,60))],chim=70,dormer=-20)
    return out
if __name__=='__main__':
    make_buildings()
