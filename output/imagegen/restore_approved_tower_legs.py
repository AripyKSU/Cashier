from pathlib import Path
from PIL import Image
import numpy as np

root = Path('F:/UnityProject/Cashier')
generated = Path('C:/Users/imsoh/.codex/generated_images/01a07b95-2bcc-7e52-9a9f-a41c5509c088')
for name, source, box in [
    ('LeftWatchTower','exec-0646b820-dffd-46b4-8415-3628e89620c1.png',(104,302,240,941)),
    ('RightWatchTower','exec-4b5b8eec-cb21-4019-91cc-be7a62ffade3.png',(1478,342,1575,941)),
]:
    path = root/'Assets/DystopiaPrototype/Art'/(name+'.png')
    target = Image.open(path).convert('RGBA')
    before = np.array(target)
    target.save(root/'output/imagegen'/(name+'-before-approved-legs.png'))
    pixels = np.array(Image.open(generated/source).convert('RGBA').crop(box))
    # Preserve the approved image's actual dark RGB values. Discard bright checkerboard
    # and its blended white edge pixels instead of tinting or recoloring the structure.
    pixels[:,:,3] = np.where(np.max(pixels[:,:,:3],axis=2) <= 105,255,0)
    patch = Image.fromarray(pixels)
    target.paste(patch,box[:2])
    target.save(path)
    after = np.array(target)
    changed = np.any(before != after,axis=2)
    changed[box[1]:box[3],box[0]:box[2]] = False
    assert not changed.any(), 'Unexpected changes outside legs'
    preview = Image.new('RGBA',target.size,(94,104,112,255)); preview.alpha_composite(target)
    preview.crop((80,270,255,680) if name.startswith('Left') else (1460,320,1590,710)).resize((350,820),Image.Resampling.NEAREST).save(root/'output/imagegen'/(name+'-approved-legs-preview.png'))
    print(name, 'approved RGB preserved; changes restricted to legs; size',target.size)
