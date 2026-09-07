from pathlib import Path
from PIL import Image
import numpy as np

root = Path('F:/UnityProject/Cashier')
source = Path('C:/Users/imsoh/.codex/generated_images/01a07b95-2bcc-7e52-9a9f-a41c5509c088/exec-400fd014-3388-4e6d-b94c-93b63b246196.png')
pixels = np.array(Image.open(source).convert('RGBA'))
rgb = pixels[:, :, :3].astype(int)
background = rgb[:, :, 1] - np.maximum(rgb[:, :, 0], rgb[:, :, 2]) >= 24
pixels[background] = 0
pixels[:, :, 1] = np.minimum(pixels[:, :, 1], np.maximum(pixels[:, :, 0], pixels[:, :, 2]))
sprite = Image.fromarray(pixels)
sprite = sprite.crop(sprite.getbbox())
sprite.save(root / 'Assets/DystopiaPrototype/Art/WatchGuard.png')
sprite.save(root / 'output/imagegen/WatchGuard-transparent.png')
print('Armed guard:', sprite.size, 'RGBA; green background removed')
