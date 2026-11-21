"""Compose a package character preview (front, facing right) from part sprites, for designing costumes."""
from PIL import Image, ImageOps
P='/mnt/user-data/uploads/Project-R/Assets/Layer Lab/2D Minimal-CharacterMaker/Extenstions/Parts Pack Base/'
def tint(im, col):
    im=im.convert('RGBA'); r,g,b,a=im.split()
    lum=im.convert('L')
    out=Image.merge('RGBA',(lum.point(lambda v:v*col[0]//255),lum.point(lambda v:v*col[1]//255),lum.point(lambda v:v*col[2]//255),a))
    return out
def place(can, im, cx, cy):
    can.alpha_composite(im,(int(round(cx-im.width/2)),int(round(cy-im.height/2))))
def compose(chest=None, helmet=None, hair='Character/Hair/Body_Hair_007.png', hair_col=(120,80,60), skin=(250,214,190), beard=None, scale=1):
    can=Image.new('RGBA',(260,300),(0,0,0,0))
    ox,oy=130,250
    bx,by=ox+0.6,oy-17.6
    hx,hy=bx-3,by-31.5
    place(can,tint(Image.open(P+'Character/Default/Body.png'),skin),bx,by)
    if chest: place(can,chest if isinstance(chest,Image.Image) else Image.open(P+chest).convert('RGBA'),bx,by)
    place(can,tint(Image.open(P+'Character/Default/Head.png'),skin),hx,hy)
    if helmet:
        hp=hair.replace('Character/Hair/Body_Hair','Character/Helmet_Hair/Body_Helmet_Hair')
        place(can,tint(Image.open(P+hp),hair_col),hx+2.5,hy-2.5)
    else:
        place(can,tint(Image.open(P+hair),hair_col),hx+2.5,hy-2.5)
    place(can,Image.open(P+'Character/Eye/Body_Eye_001.png').convert('RGBA'),hx+13.5,hy+5.5)
    if beard: place(can,tint(Image.open(P+beard),hair_col),hx+13.5,hy+20.5)
    if helmet: place(can,helmet if isinstance(helmet,Image.Image) else Image.open(P+helmet).convert('RGBA'),hx+4,hy-6.5)
    return can
if __name__=='__main__':
    ims=[compose(),compose('Parts/Chest/FA_Chest_014_BlueGold.png','Parts/Helmet/FA_Helmet_009_Red.png'),
         compose('Parts/Chest/FA_Chest_026_Gold.png','Parts/Helmet/FA_Helmet_025_Red.png',beard='Character/Beard/Body_Beard_003.png'),
         compose('Parts/Chest/FA_Chest_012_Brown.png','Parts/Helmet/FA_Helmet_001_Brown.png')]
    sh=Image.new('RGBA',(260*len(ims),300),(120,140,90,255))
    for i,im in enumerate(ims): sh.alpha_composite(im,(260*i,0))
    sh.resize((sh.width*2,sh.height*2),Image.NEAREST).save('ref.png')
