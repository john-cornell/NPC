import os
import glob
from PIL import Image
from rembg import remove

assets_dir = r"C:\Code\NPC\NPC.UI.World\Assets"
bin_dir = r"C:\Code\NPC\NPC.UI.World\bin\Debug\net9.0\Assets"
sprites = glob.glob(os.path.join(assets_dir, "*.png"))

for sprite_path in sprites:
    try:
        filename = os.path.basename(sprite_path)
        
        # Only process images that are unusually large (likely haven't been resized)
        # or the ones we specifically know we missed like goblin, dagger, etc.
        # But to be safe, process everything with rembg and force 128x128 bounding box.
        # Wait, some are already processed by the previous scripts if they were copied here.
        # But let's just process all of them to be 100% sure.
        
        print(f"Processing {filename} with rembg...")
        input_img = Image.open(sprite_path).convert("RGBA")
        
        # rembg background removal
        img = remove(input_img)
        
        # Resize to 128x128 bounding box
        img.thumbnail((128, 128), Image.Resampling.LANCZOS)
        
        # Pad to exactly 128x128 bounding box
        padded = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        paste_x = (128 - img.width) // 2
        paste_y = (128 - img.height) // 2
        
        padded.paste(img, (paste_x, paste_y), img)
        
        # Save to source folder
        padded.save(sprite_path, "PNG")
        
        # Save to bin folder
        bin_path = os.path.join(bin_dir, filename)
        if os.path.exists(bin_dir):
            padded.save(bin_path, "PNG")
        elif not os.path.exists(os.path.dirname(bin_dir)):
            # Create bin if it doesn't exist? Actually let's create the Assets dir if we need to.
            os.makedirs(bin_dir, exist_ok=True)
            padded.save(bin_path, "PNG")
            
        print(f"Finished {filename}")
    except Exception as e:
        print(f"Failed to remove background for {sprite_path}: {e}")
