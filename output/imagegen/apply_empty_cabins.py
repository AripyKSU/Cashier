from pathlib import Path
from PIL import Image
import numpy as np

root = Path('F:/UnityProject/Cashier')
generated = Path('C:/Users/imsoh/.codex/generated_images/01a07b95-2bcc-7e52-9a9f-a41c5509c088')
edits = [
    ('LeftWatchTower', 'exec-32d03f7a-4e62-46d3-8380-8560ee87adba.png', (108,166,224,234), (123,177,173,224)),
    ('RightWatchTower', 'exec-eeb5888d-0cad-45f4-8bef-f2b97bbba688.png', (1485,265,1575,307), (1508,266,1549,305)),
]
for name, file, crop, region in edits:
    path = root / 'Assets/DystopiaPrototype/Art' / (name+'.png')
    original = Image.open(path).convert('RGBA')
    original.save(root/'output/imagegen'/(name+'-before-silhouette-removal.png'))
    patch = Image.open(generated/file).convert('RGBA').resize((crop[2]-crop[0],crop[3]-crop[1]),Image.Resampling.NEAREST)
    data = np.array(patch)
    rgb = data[:,:,:3].astype(int)
    green = rgb[:,:,1] - np.maximum(rgb[:,:,0],rgb[:,:,2]) > 24
    data[green] = 0
    patch = Image.fromarray(data)
    local = (region[0]-crop[0],region[1]-crop[1],region[2]-crop[0],region[3]-crop[1])
    original.paste(patch.crop(local),region[:2])
    original.save(path)
    preview = original.crop(crop)
    bg = Image.new('RGBA',preview.size,(110,125,135,255)); bg.alpha_composite(preview)
    bg.resize((preview.width*8,preview.height*8),Image.Resampling.NEAREST).save(root/'output/imagegen'/(name+'-empty-cabin.png'))
    print(name, 'only changed inside',region)
