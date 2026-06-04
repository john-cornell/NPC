import os
from PIL import Image

assets_dir = r"C:\Code\NPC\NPC.UI.Isometric\Assets"
f = os.path.join(assets_dir, 'iso_villager_farmer_sleeping.png')

img = Image.open(f).convert("RGBA")
pixels = img.load()
w, h = img.size
print(f"Size: {w}x{h}")
for y in range(0, h, 10):
    for x in range(0, w, 10):
        r, g, b, a = pixels[x, y]
        if a > 0:
            print(f"Non-transparent at {x},{y}: {r},{g},{b},{a}")
