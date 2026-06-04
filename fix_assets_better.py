import os
import glob
from PIL import Image

assets_dir = r"C:\Code\NPC\NPC.UI.Isometric\Assets"
bin_dir = r"C:\Code\NPC\NPC.UI.Isometric\bin\Debug\net9.0\Assets"
sprites = glob.glob(os.path.join(assets_dir, "*.png"))

def is_background(pixel):
    r, g, b, a = pixel
    if a == 0: return True
    if r > 240 and g > 240 and b > 240: return True
    if 140 < r < 220 and 140 < g < 220 and 140 < b < 220:
        if abs(r-g) < 15 and abs(r-b) < 15 and abs(g-b) < 15:
            return True
    return False

def remove_background_floodfill(img):
    img = img.convert("RGBA")
    width, height = img.size
    pixels = img.load()
    
    queue = []
    visited = set()
    
    for x in range(width):
        for y in [0, height-1]:
            if is_background(pixels[x, y]):
                queue.append((x, y))
                visited.add((x, y))
    for y in range(height):
        for x in [0, width-1]:
            if (x, y) not in visited and is_background(pixels[x, y]):
                queue.append((x, y))
                visited.add((x, y))
                
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
        
        img = remove_background_floodfill(img)
        
        img.thumbnail((128, 128), Image.Resampling.LANCZOS)
        
        padded = Image.new('RGBA', (128, 128), (0,0,0,0))
        offset = ((128 - img.width) // 2, (128 - img.height) // 2)
        padded.paste(img, offset)
        
        padded.save(sprite_path, "PNG")
        
        bin_path = os.path.join(bin_dir, os.path.basename(sprite_path))
        if os.path.exists(bin_dir):
            padded.save(bin_path, "PNG")
            
        print(f"Fixed: {os.path.basename(sprite_path)}")
    except Exception as e:
        print(f"Error on {sprite_path}: {e}")
