import os
import glob
from PIL import Image
from rembg import remove

assets_dir = r"C:\Code\NPC\NPC.UI.Isometric\Assets"
bin_dir = r"C:\Code\NPC\NPC.UI.Isometric\bin\Debug\net9.0\Assets"
sprites = glob.glob(os.path.join(assets_dir, "*.png"))

for sprite_path in sprites:
    try:
        print(f"Processing {os.path.basename(sprite_path)} with rembg...")
        input_img = Image.open(sprite_path).convert("RGBA")
        
        # Check if the image is mostly transparent. Wait, rembg will just work.
        img = remove(input_img)
        
        # After rembg, open it and resize it to 128x128
        img.thumbnail((128, 128), Image.Resampling.LANCZOS)
        
        # Pad to exactly 128x128 bounding box
        padded = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        paste_x = (128 - img.width) // 2
        paste_y = (128 - img.height) // 2
        
        padded.paste(img, (paste_x, paste_y), img)
        
        # Save to source folder
        padded.save(sprite_path, "PNG")
        
        # Save to bin folder
        bin_path = os.path.join(bin_dir, os.path.basename(sprite_path))
        if os.path.exists(bin_dir):
            padded.save(bin_path, "PNG")
            
        print(f"Finished {os.path.basename(sprite_path)}")
    except Exception as e:
        print(f"Failed to remove background for {sprite_path}: {e}")
