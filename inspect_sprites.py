from PIL import Image
import os

assets_dir = r"C:\Code\NPC\NPC.UI.Isometric\Assets"
files_to_check = ['iso_villager_sleeping.png', 'iso_gravestone.png', 'iso_villager_farmer_sleeping.png']

for f in files_to_check:
    path = os.path.join(assets_dir, f)
    if os.path.exists(path):
        img = Image.open(path).convert("RGBA")
        pixels = img.load()
        w, h = img.size
        print(f"File: {f}")
        print(f"  Size: {w}x{h}")
        print(f"  Top-left: {pixels[0,0]}")
        print(f"  Bottom-right: {pixels[w-1, h-1]}")
        print(f"  Center: {pixels[w//2, h//2]}")
