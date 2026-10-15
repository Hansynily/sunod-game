"""Pixel props for the tutorial kite quest. Run: python make_kite_props.py"""
from PIL import Image, ImageDraw
O=(46,34,24,255)
def save(im,n): im.save(n); print(n, im.size)

# kite: red/yellow diamond with cross spars and a ribbon tail
k=Image.new('RGBA',(14,22),(0,0,0,0)); d=k.load()
pts=[(7,0),(13,7),(7,14),(1,7)]
ImageDraw.Draw(k).polygon(pts, fill=(217,58,74,255), outline=O)
ImageDraw.Draw(k).polygon([(7,1),(12,7),(7,7)], fill=(255,204,66,255))
ImageDraw.Draw(k).polygon([(7,7),(7,13),(2,7)], fill=(255,204,66,255))
ImageDraw.Draw(k).line([(7,1),(7,13)], fill=O); ImageDraw.Draw(k).line([(2,7),(12,7)], fill=O)
for i,(x,y) in enumerate([(7,15),(8,16),(7,17),(6,18),(7,19),(8,20)]):
    d[x,y]=(47,111,214,255) if i%2==0 else (255,240,150,255)
save(k,'kite.png')

# ladder
l=Image.new('RGBA',(10,26),(0,0,0,0)); dr=ImageDraw.Draw(l)
dr.rectangle([0,0,1,25],fill=(168,108,60,255)); dr.rectangle([8,0,9,25],fill=(168,108,60,255))
for y in range(3,26,5): dr.rectangle([2,y,7,y+1],fill=(196,140,84,255))
dr.line([(0,0),(0,25)],fill=O); dr.line([(9,0),(9,25)],fill=O)
save(l,'ladder.png')

# hook pole
h=Image.new('RGBA',(8,30),(0,0,0,0)); dr=ImageDraw.Draw(h)
dr.rectangle([3,5,4,29],fill=(150,100,60,255)); dr.line([(2,5),(2,29)],fill=O)
dr.arc([0,0,7,8],180,360,fill=(120,120,130,255),width=2)
save(h,'hook_pole.png')

# cushion
c=Image.new('RGBA',(22,9),(0,0,0,0)); dr=ImageDraw.Draw(c)
dr.rounded_rectangle([0,1,21,8],radius=3,fill=(142,79,214,255),outline=O)
dr.line([(3,3),(18,3)],fill=(190,140,240,255))
save(c,'cushion.png')

# arrow (pointing right)
a=Image.new('RGBA',(11,11),(0,0,0,0)); dr=ImageDraw.Draw(a)
dr.polygon([(0,3),(5,3),(5,0),(10,5),(5,10),(5,7),(0,7)],fill=(34,179,196,255),outline=O)
save(a,'arrow.png')

# sparkle
s=Image.new('RGBA',(9,9),(0,0,0,0)); d=s.load()
for i in range(9): d[4,i]=(255,240,150,255); d[i,4]=(255,240,150,255)
for (x,y) in [(3,3),(5,3),(3,5),(5,5)]: d[x,y]=(255,204,66,255)
d[4,4]=(255,255,255,255)
save(s,'sparkle.png')

# heart (for heal / charm)
hr=Image.new('RGBA',(9,8),(0,0,0,0)); dr=ImageDraw.Draw(hr)
dr.polygon([(4,7),(0,3),(0,1),(1,0),(3,0),(4,1),(5,0),(7,0),(8,1),(8,3)],fill=(240,90,110,255),outline=O)
save(hr,'heart.png')
