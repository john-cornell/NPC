from PIL import Image
import os

assets_dir = r'C:\Code\NPC\NPC.UI.Isometric\Assets'
base_img_path = os.path.join(assets_dir, 'iso_wall_x.png')

base_img = Image.open(base_img_path).convert('RGBA')
flipped_img = base_img.transpose(Image.FLIP_LEFT_RIGHT)

# Arms (all are 64x128)
arm_bl = base_img.crop((0, 0, 64, 128)) # Original left (Bottom-Left)
arm_tr = base_img.crop((64, 0, 128, 128)) # Original right (Top-Right)
arm_tl = flipped_img.crop((0, 0, 64, 128)) # Flipped left (Top-Left)
arm_br = flipped_img.crop((64, 0, 128, 128)) # Flipped right (Bottom-Right)

def make_corner(name, arm1, pos1, arm2, pos2):
    corner = Image.new('RGBA', (128, 128), (0, 0, 0, 0))
    corner.paste(arm1, pos1, mask=arm1)
    corner.paste(arm2, pos2, mask=arm2)
    
    path = os.path.join(assets_dir, name + '.png')
    corner.save(path)
    
    bin_path = os.path.join(r'C:\Code\NPC\NPC.UI.Isometric\bin\Debug\net9.0\Assets', name + '.png')
    if os.path.exists(os.path.dirname(bin_path)):
        corner.save(bin_path)

# Top corner (^ shape, connects BL and BR)
make_corner('iso_wall_corner_top', arm_bl, (0, 0), arm_br, (64, 0))

# Bottom corner (v shape, connects TL and TR)
make_corner('iso_wall_corner_bottom', arm_tl, (0, 0), arm_tr, (64, 0))

# Left corner (> shape, connects TR and BR)
make_corner('iso_wall_corner_left', arm_tr, (64, 0), arm_br, (64, 0))

# Right corner (< shape, connects TL and BL)
make_corner('iso_wall_corner_right', arm_tl, (0, 0), arm_bl, (0, 0))

print("Created 4 corner sprites!")
