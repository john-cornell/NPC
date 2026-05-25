from PIL import Image
import os
import sys

img_name = 'iso_wall_corner.png'
assets_dir = r'C:\Code\NPC\NPC.UI.Isometric\Assets'
path = os.path.join(assets_dir, img_name)

if os.path.exists(path):
    img = Image.open(path).convert('RGBA')
    img = img.resize((128, 128), Image.Resampling.LANCZOS)
    
    data = img.getdata()
    new_data = []
    
    bg_colors = set()
    for y in range(16):
        for x in range(16):
            r, g, b, a = img.getpixel((x, y))
            bg_colors.add((r//5, g//5, b//5))
            
    for item in data:
        r, g, b, a = item
        quant = (r//5, g//5, b//5)
        
        if quant in bg_colors or (abs(r-g)<15 and abs(g-b)<15 and r>190):
            new_data.append((255, 255, 255, 0))
        else:
            new_data.append(item)
            
    img.putdata(new_data)
    img.save(path, 'PNG')
    
    bin_path = os.path.join(r'C:\Code\NPC\NPC.UI.Isometric\bin\Debug\net9.0\Assets', img_name)
    if os.path.exists(os.path.dirname(bin_path)):
        img.save(bin_path, 'PNG')
    print("Fixed corner image")
else:
    print(f"Could not find {path}")
