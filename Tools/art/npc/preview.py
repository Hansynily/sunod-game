import sys
from npc_style import C
from PIL import Image, ImageDraw
G=C["green"][4]+(255,); Gs=C["green"][3]+(255,)
def sheet(paths, out, sc=12):
    c=Image.new("RGBA",(len(paths)*(16*sc+40)+40,32*sc+80),G); d=ImageDraw.Draw(c)
    for n,p in enumerate(paths):
        im=Image.open(p).resize((16*sc,32*sc),Image.NEAREST)
        x=40+n*(16*sc+40); d.ellipse((x+3*sc,40+29*sc,x+13*sc,40+31*sc),fill=Gs); c.alpha_composite(im,(x,40))
    c.convert("RGB").save(out)
def gif(paths, out, dur, sc=12):
    fr=[]
    for p in paths:
        f=Image.new("RGBA",(16*sc+80,32*sc+80),G); d=ImageDraw.Draw(f)
        d.ellipse((40+3*sc,40+29*sc,40+13*sc,40+31*sc),fill=Gs)
        f.alpha_composite(Image.open(p).resize((16*sc,32*sc),Image.NEAREST),(40,40)); fr.append(f.convert("RGB"))
    fr[0].save(out,save_all=True,append_images=fr[1:],duration=dur,loop=0)
if __name__=="__main__":
    sheet(sys.argv[2:], sys.argv[1])
