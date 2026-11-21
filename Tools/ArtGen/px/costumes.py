import cairo, math
from PIL import Image
from compose import compose, P
S=4  # supersample
OUT='out'
import os; os.makedirs(OUT,exist_ok=True)
def hexc(h): h=h.lstrip('#'); return tuple(int(h[i:i+2],16)/255 for i in (0,2,4))
def shade(c,f): return tuple(max(0,min(1,v*f)) for v in c)
def surf(w,h):
    s=cairo.ImageSurface(cairo.FORMAT_ARGB32,w*S,h*S); c=cairo.Context(s); c.scale(S,S); return s,c
def to_pil(s,w,h):
    s.flush(); im=Image.frombuffer('RGBA',(w*S,h*S),bytes(s.get_data()),'raw','BGRA',0,1)
    # premultiplied -> straight
    im=im.copy(); px=im.load()
    W,H=im.size
    import numpy as np
    a=np.array(im).astype(float); al=a[...,3:4]/255.0
    a[...,:3]=np.where(al>0,a[...,:3]/np.maximum(al,1e-6),0)
    im=Image.fromarray(a.clip(0,255).astype('uint8'),'RGBA')
    return im.resize((w,h),Image.LANCZOS)
BODY=Image.open(P+'Character/Default/Body.png').convert('RGBA')
# ---------- chest ----------
def chest(name, painter):
    """painter(ctx) paints in body-local coords (44x48), body interior x 4..40, y 4..44"""
    s,c=surf(44,48); painter(c); pat=to_pil(s,44,48)
    import numpy as np
    b=np.array(BODY).astype(float); p=np.array(pat).astype(float)
    lum=b[...,0:1]/255.0
    out=np.zeros_like(b)
    out[...,:3]=p[...,:3]*lum
    out[...,3]=b[...,3]
    im=Image.fromarray(out.clip(0,255).astype('uint8'),'RGBA')
    can=Image.new('RGBA',(78,70),(0,0,0,0)); can.alpha_composite(im,(17,11))
    can.save(f'{OUT}/Chest_{name}.png'); return can
def fill(c,col): c.set_source_rgb(*col); c.paint()
def rect(c,x,y,w,h,col): c.set_source_rgb(*col); c.rectangle(x,y,w,h); c.fill()
def poly(c,pts,col,line=None,lw=1.4):
    c.move_to(*pts[0]);[c.line_to(*p) for p in pts[1:]]; c.close_path()
    c.set_source_rgb(*col)
    if line: c.fill_preserve(); c.set_source_rgb(*line); c.set_line_width(lw); c.stroke()
    else: c.fill()
def circ(c,x,y,r,col): c.set_source_rgb(*col); c.arc(x,y,r,0,6.2832); c.fill()
def bodyshade(c,col):
    # darker crescent bottom-left like the package
    c.save(); c.set_source_rgba(*shade(col,0.0),0.18); c.arc(10,40,16,0,6.2832); c.fill(); c.restore()
K=(0.08,0.06,0.06)
def p_merchant(c):
    shirt=hexc('f1ead8'); vest=hexc('7b4bb0')
    fill(c,shirt); poly(c,[(0,0),(19,0),(23,26),(17,48),(0,48)],vest); poly(c,[(44,0),(29,0),(25,26),(30,48),(44,48)],vest)
    rect(c,0,30,44,5,hexc('c8963c'))  # belt sash
    for y in (12,20): circ(c,22.5,y,1.3,hexc('e8c050'))
    bodyshade(c,vest)
def p_librarian(c):
    robe=hexc('2f5fa8'); fill(c,robe)
    poly(c,[(17,0),(27,0),(24,48),(20,48)],hexc('e8c050'))
    poly(c,[(10,0),(34,0),(22,12)],hexc('f2efe6'))
    bodyshade(c,robe)
def p_barber(c):
    coat=hexc('f4f4f0'); fill(c,coat)
    poly(c,[(12,0),(32,0),(22,10)],hexc('3d7fc4'))
    rect(c,21,10,2,38,hexc('cfd2d6'))
    for y in (16,24,32): circ(c,25,y,1.2,hexc('3d7fc4'))
    rect(c,6,26,8,7,hexc('d9dbe0'))  # pocket
    poly(c,[(8,22),(9,27),(10,22)],hexc('c03a3a'))  # comb tip
    bodyshade(c,hexc('c0c4cc'))
def p_guard(c):
    mail=hexc('9aa2ad'); fill(c,mail)
    c.set_source_rgb(*hexc('77808c'))
    for y in range(2,48,4):
        for x in range(0,44,4): c.arc(x+(2 if (y//4)%2 else 0),y,1.1,0,6.3); c.fill()
    tab=hexc('b8383a'); poly(c,[(11,0),(33,0),(32,48),(12,48)],tab)
    poly(c,[(11,0),(33,0),(32,48),(12,48)],tab)
    rect(c,11,30,22,4,hexc('5a3a22')); rect(c,20,29.5,4,5,hexc('e8c050'))
    poly(c,[(22,10),(26,14),(22,22),(18,14)],hexc('e8c050'))
    bodyshade(c,tab)
def p_smith(c):
    shirt=hexc('4a4f5a'); fill(c,shirt)
    apron=hexc('8a5a32'); poly(c,[(9,8),(35,8),(37,48),(7,48)],apron)
    rect(c,13,0,3,10,apron); rect(c,28,0,3,10,apron)
    rect(c,15,24,14,8,hexc('6e4626'))  # pocket
    circ(c,10,9,1.3,hexc('d0d0d0')); circ(c,34,9,1.3,hexc('d0d0d0'))
    bodyshade(c,apron)
def p_elder(c):
    coat=hexc('4e7d46'); fill(c,coat)
    poly(c,[(0,24),(44,30),(44,35),(0,29)],hexc('d9b048'))
    poly(c,[(14,0),(30,0),(22,9)],hexc('e9e2cf'))
    rect(c,21,9,2,15,hexc('3a5f34'))
    bodyshade(c,coat)
def p_grocer(c):
    dress=hexc('d4823a'); fill(c,dress)
    poly(c,[(10,14),(34,14),(37,48),(7,48)],hexc('f4f0e4'))
    rect(c,7,18,30,3,hexc('e9e2cf'))
    rect(c,14,30,8,6,hexc('e6ddc9'))
    bodyshade(c,dress)
def p_baker(c):
    jac=hexc('fbfbf6'); fill(c,jac)
    poly(c,[(14,0),(30,0),(22,6)],hexc('e3e3dc'))
    for y in (13,21,29):
        circ(c,17,y,1.3,hexc('9aa0a8')); circ(c,27,y,1.3,hexc('9aa0a8'))
    rect(c,0,36,44,12,hexc('e8e4da'))
    rect(c,0,36,44,2,hexc('c95b4a'))
    bodyshade(c,hexc('c9ccd2'))
def p_villager(c):
    dress=hexc('d86f8a'); fill(c,dress)
    poly(c,[(12,16),(32,16),(35,48),(9,48)],hexc('fbf6ee'))
    poly(c,[(13,0),(31,0),(22,8)],hexc('fbf6ee'))
    for x,y in ((5,30),(38,24),(6,12),(37,40)): circ(c,x,y,1.4,hexc('f6c6d2'))
    bodyshade(c,dress)
def p_farmer(c):
    shirt=hexc('c0423a'); fill(c,shirt)
    c.set_source_rgba(*hexc('7a1f1c'),0.6)
    for x in range(0,44,6): c.rectangle(x,0,2,48); c.fill()
    for y in range(0,48,6): c.rectangle(0,y,44,2); c.fill()
    ov=hexc('3f6ea8'); poly(c,[(10,18),(34,18),(36,48),(8,48)],ov)
    rect(c,11,0,4,19,ov); rect(c,29,0,4,19,ov)
    circ(c,13,19,1.4,hexc('e8c050')); circ(c,31,19,1.4,hexc('e8c050'))
    rect(c,17,24,10,7,hexc('35608f'))
    bodyshade(c,ov)
def p_kid(c):
    fill(c,hexc('f4f4f0'))
    for y in range(0,48,8): rect(c,0,y,44,4,hexc('d24848'))
    rect(c,0,36,44,12,hexc('5a6fa0'))
    bodyshade(c,hexc('d24848'))
def p_youth(c):
    tun=hexc('3c9a8c'); fill(c,tun)
    rect(c,0,31,44,4,hexc('6b4a2c')); rect(c,19,30.5,6,5,hexc('d9b048'))
    poly(c,[(4,0),(40,0),(40,9),(4,11)],hexc('e98a3a'))
    poly(c,[(8,8),(15,8),(13,24),(7,22)],hexc('d4762c'))
    bodyshade(c,tun)
CHESTS=dict(merchant=p_merchant,librarian=p_librarian,barber=p_barber,guard=p_guard,smith=p_smith,elder=p_elder,
            grocer=p_grocer,baker=p_baker,villager=p_villager,farmer=p_farmer,kid=p_kid,youth=p_youth)
# ---------- hats ----------
# helmet canvas 120x108; head ellipse centre (56,60.5), size 67x52 -> top of head y~35.5, x 23..89
LW=3.6
def outline_fill(c,path,col,lw=LW):
    path(c); c.set_source_rgb(*col); c.fill_preserve(); c.set_source_rgb(*K); c.set_line_width(lw); c.set_line_join(cairo.LINE_JOIN_ROUND); c.stroke()
def band(c,path,clip,col):
    c.save(); clip(c); c.clip(); path(c); c.set_source_rgb(*col); c.fill(); c.restore()
def ell(cx,cy,rx,ry):
    def f(c): c.save(); c.translate(cx,cy); c.scale(rx,ry); c.arc(0,0,1,0,6.2832); c.restore()
    return f
def hat(name, painter):
    s,c=surf(120,108); painter(c); im=to_pil(s,120,108); im.save(f'{OUT}/Helmet_{name}.png'); return im
def h_beret(c):
    col=hexc('7b4bb0')
    outline_fill(c,ell(52,42,32,12),col)
    band(c,ell(52,42,32,12),ell(44,48,30,8),shade(col,0.75))
    c.set_source_rgb(*K); c.set_line_width(LW); c.move_to(52,30); c.line_to(54,24); c.stroke()
def h_scholar(c):
    col=hexc('2f5fa8')
    def cap(c):
        c.move_to(24,50); c.curve_to(26,26,86,26,88,50); c.close_path()
    outline_fill(c,cap,col)
    band(c,cap,lambda c:c.rectangle(0,42,120,10),shade(col,0.7))
    def top(c): c.move_to(20,30); c.line_to(56,20); c.line_to(94,30); c.line_to(56,40); c.close_path()
    outline_fill(c,top,shade(col,0.85))
    c.set_source_rgb(*K); c.set_line_width(2.4); c.move_to(56,30); c.line_to(90,34); c.line_to(92,46); c.stroke()
    circ(c,92,48,3.2,hexc('e8c050'))
def h_barber(c):
    col=hexc('f4f4f0')
    def cap(c): c.move_to(26,48); c.curve_to(28,22,84,22,86,48); c.close_path()
    outline_fill(c,cap,col)
    band(c,cap,lambda c:c.rectangle(0,41,120,8),hexc('3d7fc4'))
    def brim(c): c.move_to(80,44); c.curve_to(92,42,104,44,104,49); c.curve_to(98,52,86,51,80,49); c.close_path()
    outline_fill(c,brim,hexc('3d7fc4'))
def h_guard(c):
    steel=hexc('b4bcc6')
    def dome(c): c.move_to(22,52); c.curve_to(22,16,90,16,90,52); c.close_path()
    # plume behind
    def plume(c): c.move_to(54,22); c.curve_to(44,4,24,6,16,18); c.curve_to(30,14,40,18,46,26); c.close_path()
    outline_fill(c,plume,hexc('c8383a'))
    outline_fill(c,dome,steel)
    band(c,dome,ell(40,50,26,22),shade(steel,0.78))
    def rim(c): c.move_to(14,50); c.curve_to(40,44,74,44,98,50); c.curve_to(98,56,14,56,14,50); c.close_path()
    outline_fill(c,rim,shade(steel,0.9))
    rect(c,54,22,4,24,hexc('e8c050'))
def h_bandana(c):
    col=hexc('c8383a')
    def b(c): c.move_to(24,52); c.curve_to(24,28,86,26,88,48); c.curve_to(70,44,40,46,24,52); c.close_path()
    outline_fill(c,b,col)
    def knot(c): c.move_to(26,46); c.line_to(12,40); c.line_to(14,54); c.close_path()
    outline_fill(c,knot,shade(col,0.8))
    for x,y in ((44,38),(60,34),(74,38),(52,44)): circ(c,x,y,1.8,hexc('f4f0e4'))
def h_scarf(c):
    col=hexc('3f8a5a')
    def s(c): c.move_to(20,62); c.curve_to(16,22,94,18,92,54); c.curve_to(80,44,40,44,30,64); c.close_path()
    outline_fill(c,s,col)
    band(c,s,ell(30,56,16,18),shade(col,0.75))
    for x,y in ((40,34),(58,30),(74,36),(32,48)): circ(c,x,y,2,hexc('f4e6b4'))
def h_toque(c):
    col=hexc('fbfbf6')
    def puff(c):
        c.move_to(32,46); c.line_to(32,30)
        c.curve_to(18,26,22,6,38,10); c.curve_to(42,-2,70,-2,74,10); c.curve_to(90,6,94,26,80,30)
        c.line_to(80,46); c.close_path()
    outline_fill(c,puff,col)
    band(c,puff,ell(38,22,14,20),hexc('dcdcd6'))
    def bandp(c): c.rectangle(28,38,56,12)
    outline_fill(c,bandp,hexc('ececE6'.lower()))
def h_sunhat(c):
    col=hexc('ecd08a')
    def brim(c): c.save(); c.translate(56,46); c.scale(46,10); c.arc(0,0,1,0,6.2832); c.restore()
    outline_fill(c,brim,col)
    def crown(c): c.move_to(34,46); c.curve_to(34,18,78,18,78,46); c.close_path()
    outline_fill(c,crown,col)
    band(c,crown,lambda c:c.rectangle(0,36,120,8),hexc('d86f8a'))
    for dx,dy in ((0,-3),(3,0),(0,3),(-3,0)): circ(c,40+dx,40+dy,2.6,hexc('fbf6ee'))
    circ(c,40,40,1.8,hexc('e8c050'))
def h_straw(c):
    col=hexc('e2c066')
    def brim(c):
        c.move_to(8,50); c.curve_to(20,40,92,40,106,50); c.curve_to(92,58,22,58,8,50); c.close_path()
    outline_fill(c,brim,col)
    def crown(c): c.move_to(32,46); c.curve_to(30,20,82,20,80,46); c.close_path()
    outline_fill(c,crown,col)
    band(c,crown,lambda c:c.rectangle(0,38,120,7),hexc('a8743a'))
    c.set_source_rgba(*hexc('a88a3a'),0.8); c.set_line_width(1.2)
    for x in range(16,100,8): c.move_to(x,48); c.line_to(x+3,53); c.stroke()
def h_kidcap(c):
    col=hexc('3d7fc4')
    def cap(c): c.move_to(26,48); c.curve_to(26,20,86,20,86,48); c.close_path()
    def brim(c): c.move_to(80,44); c.curve_to(94,40,108,42,108,48); c.curve_to(100,52,88,51,80,49); c.close_path()
    outline_fill(c,brim,shade(col,0.8))
    outline_fill(c,cap,col)
    band(c,cap,ell(40,46,20,18),shade(col,0.8))
    circ(c,56,23,3,hexc('d24848'))
def h_headband(c):
    col=hexc('e98a3a')
    def b(c): c.move_to(23,44); c.curve_to(36,36,76,34,89,42); c.line_to(89,49); c.curve_to(76,42,36,44,23,52); c.close_path()
    outline_fill(c,b,col)
    def tail(c): c.move_to(25,46); c.curve_to(14,44,8,52,6,60); c.curve_to(14,56,18,54,26,52); c.close_path()
    outline_fill(c,tail,shade(col,0.85))
HATS=dict(merchant=h_beret,librarian=h_scholar,barber=h_barber,guard=h_guard,smith=h_bandana,grocer=h_scarf,
          baker=h_toque,villager=h_sunhat,farmer=h_straw,kid=h_kidcap,youth=h_headband)
LOOK=dict(merchant=((96,64,40),None),librarian=((60,50,50),None),barber=((40,40,40),'Character/Beard/Body_Beard_003.png'),
          guard=((90,60,40),None),smith=((40,30,30),'Character/Beard/Body_Beard_003.png'),elder=((220,220,215),'Character/Beard/Body_Beard_003.png'),
          grocer=((150,80,40),None),baker=((200,150,90),None),villager=((240,200,110),None),farmer=((120,80,50),None),kid=((230,170,80),None),youth=((70,40,30),None))
if __name__=='__main__':
    tiles=[]
    for k,f in CHESTS.items():
        ch=chest(k,f); h=hat(k,HATS[k]) if k in HATS else None
        hc,beard=LOOK[k]
        tiles.append(compose(ch,h,hair_col=hc,beard=beard))
    W=200;sh=Image.new('RGBA',(W*6,2*230),(110,140,90,255))
    for i,t in enumerate(tiles): sh.alpha_composite(t.crop((30,70,230,300)),((i%6)*W,(i//6)*230))
    sh.resize((sh.width*2//1,sh.height*2//1),Image.LANCZOS).save('costumes.png')
