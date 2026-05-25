import os
import glob
from PIL import Image
from rembg import remove

brain_dir = r"C:\Users\John Cornell\.gemini\antigravity\brain\36d67a62-dd9e-45a8-9e0a-e95d5c25dc22"
assets_dir = r"c:\Code\NPC\NPC.UI.Isometric\Assets"

sprites = glob.glob(os.path.join(brain_dir, "iso_*.png"))

for sprite_path in sprites:
    filename = os.path.basename(sprite_path)
    clean_name = filename.split('_177')[0] + ".png"
    out_path = os.path.join(assets_dir, clean_name)
    
    print(f"Processing {clean_name} with rembg...")
    if os.path.exists(out_path):
        os.remove(out_path)

    # Run rembg
    try:
        input_img = Image.open(sprite_path)
        img = remove(input_img)
        img.save(out_path, "PNG")
    except Exception as e:
        print(f"Failed to remove background: {e}")
        continue
    
    # After rembg, open it and resize it to 128x128
    if os.path.exists(out_path):
        img = Image.open(out_path)
        
        # Center in 128x128
        new_img = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        img.thumbnail((128, 128), Image.Resampling.LANCZOS)
        
        paste_x = (128 - img.width) // 2
        paste_y = (128 - img.height) // 2
        
        new_img.paste(img, (paste_x, paste_y), img)
        new_img.save(out_path, "PNG")
        
        print(f"Finished {clean_name}")
