from PIL import Image, ImageDraw
import os

assets_dir = r'C:\Code\NPC\NPC.UI.Isometric\Assets'
base_img_path = os.path.join(assets_dir, 'iso_wall_x.png')

base_img = Image.open(base_img_path).convert('RGBA')
flipped_img = base_img.transpose(Image.FLIP_LEFT_RIGHT)

arm_bl = base_img.crop((0, 0, 64, 128)) 
arm_tr = base_img.crop((64, 0, 128, 128)) 
arm_tl = flipped_img.crop((0, 0, 64, 128)) 
arm_br = flipped_img.crop((64, 0, 128, 128)) 

def make_corner(name, arm1, pos1, arm2, pos2, draw_post=True):
    corner = Image.new('RGBA', (128, 128), (0, 0, 0, 0))
    corner.paste(arm1, pos1, mask=arm1)
    corner.paste(arm2, pos2, mask=arm2)
    
    if draw_post:
        draw = ImageDraw.Draw(corner)
        draw.rectangle([58, 28, 70, 92], fill=(112, 75, 43, 255), outline=(59, 36, 17, 255))
        draw.line([(64, 28), (64, 92)], fill=(80, 50, 25, 255), width=2)
    
    path = os.path.join(assets_dir, name + '.png')
    corner.save(path)
    
    bin_path = os.path.join(r'C:\Code\NPC\NPC.UI.Isometric\bin\Debug\net9.0\Assets', name + '.png')
    if os.path.exists(os.path.dirname(bin_path)):
        corner.save(bin_path)

make_corner('iso_wall_corner_top', arm_bl, (0, 0), arm_br, (64, 0))
make_corner('iso_wall_corner_bottom', arm_tl, (0, 0), arm_tr, (64, 0))
make_corner('iso_wall_corner_left', arm_tr, (64, 0), arm_br, (64, 0))
make_corner('iso_wall_corner_right', arm_tl, (0, 0), arm_bl, (0, 0))

print("Fixed corners with posts!")
