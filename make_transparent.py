import sys
from PIL import Image

def make_transparent(image_path):
    try:
        img = Image.open(image_path).convert("RGBA")
        datas = img.getdata()
        
        # Get the color of the top-left pixel
        bg_color = datas[0]
        
        newData = []
        for item in datas:
            # If pixel matches the background color, make it transparent
            # Match RGB values (ignoring alpha)
            if item[0] == bg_color[0] and item[1] == bg_color[1] and item[2] == bg_color[2]:
                newData.append((255, 255, 255, 0))
            else:
                newData.append(item)
                
        img.putdata(newData)
        img.save(image_path, "PNG")
        print(f"Successfully processed {image_path}")
    except Exception as e:
        print(f"Error processing {image_path}: {e}")

if __name__ == "__main__":
    if len(sys.argv) > 1:
        make_transparent(sys.argv[1])
    else:
        print("Please provide an image path.")
