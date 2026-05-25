from PIL import Image
import os

images = ['iso_wall_x.png', 'iso_wall_y.png', 'iso_door_x.png', 'iso_door_y.png']
assets_dir = r'C:\Code\NPC\NPC.UI.Isometric\Assets'

for img_name in images:
    path = os.path.join(assets_dir, img_name)
    if not os.path.exists(path): continue
    
    img = Image.open(path).convert('RGBA')
    # Resize to 128x128
    img = img.resize((128, 128), Image.Resampling.LANCZOS)
    
    data = img.getdata()
    new_data = []
    
    # Let's sample the top-left 16x16 pixels to find the background colors
    bg_colors = set()
    for y in range(16):
        for x in range(16):
            r, g, b, a = img.getpixel((x, y))
            # Quantize slightly to group similar colors
            bg_colors.add((r//5, g//5, b//5))
            
    for item in data:
        r, g, b, a = item
        quant = (r//5, g//5, b//5)
        
        # If it matches a background color, or is generally white/light-gray (checkerboard)
        if quant in bg_colors or (abs(r-g)<15 and abs(g-b)<15 and r>190):
            new_data.append((255, 255, 255, 0))
        else:
            new_data.append(item)
            
    img.putdata(new_data)
    img.save(path, 'PNG')
    
    bin_path = os.path.join(r'C:\Code\NPC\NPC.UI.Isometric\bin\Debug\net9.0\Assets', img_name)
    if os.path.exists(os.path.dirname(bin_path)):
        img.save(bin_path, 'PNG')
