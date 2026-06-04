import os
import glob
from PIL import Image

assets_dir = r"c:\Code\NPC\NPC.UI.Isometric\Assets"
sprites = glob.glob(os.path.join(assets_dir, "*.png"))

def is_background(pixel):
    r, g, b = pixel[:3]
    if r > 240 and g > 240 and b > 240:
        return True
    if 140 < r < 220 and 140 < g < 220 and 140 < b < 220:
        if abs(r-g) < 15 and abs(r-b) < 15 and abs(g-b) < 15:
            return True
    return False

def remove_background_floodfill(img):
    img = img.convert("RGBA")
    width, height = img.size
    pixels = img.load()
    
    corners = [(0,0), (width-1, 0), (0, height-1), (width-1, height-1)]
    queue = []
    visited = set()
    
    for c in corners:
        if is_background(pixels[c[0], c[1]]):
            queue.append(c)
            visited.add(c)
            
    directions = [(0,1), (0,-1), (1,0), (-1,0)]
    while queue:
        x, y = queue.pop(0)
        pixels[x, y] = (0, 0, 0, 0)
        
        for dx, dy in directions:
            nx, ny = x + dx, y + dy
            if 0 <= nx < width and 0 <= ny < height:
                if (nx, ny) not in visited:
                    if is_background(pixels[nx, ny]):
                        visited.add((nx, ny))
                        queue.append((nx, ny))
                        
    return img

for sprite_path in sprites:
    try:
        img = Image.open(sprite_path)
        img = img.convert("RGBA")
        pixels = img.load()
        r, g, b, a = pixels[0, 0]
        
        if a == 0:
            continue
            
        print(f"Processing: {os.path.basename(sprite_path)}")
        img = remove_background_floodfill(img)
        
        img.thumbnail((128, 128), Image.Resampling.LANCZOS)
        
        padded = Image.new('RGBA', (128, 128), (0,0,0,0))
        offset = ((128 - img.width) // 2, (128 - img.height) // 2)
        padded.paste(img, offset)
        
        padded.save(sprite_path, "PNG")
        print(f"Fixed: {os.path.basename(sprite_path)}")
    except Exception as e:
        print(f"Error on {sprite_path}: {e}")
