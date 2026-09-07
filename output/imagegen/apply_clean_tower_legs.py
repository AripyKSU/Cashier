from pathlib import Path
from collections import deque
from PIL import Image
import numpy as np

root = Path('F:/UnityProject/Cashier')
generated = Path('C:/Users/imsoh/.codex/generated_images/01a07b95-2bcc-7e52-9a9f-a41c5509c088')
for name, file, box in [
    ('LeftWatchTower','exec-84239778-d64e-450a-81ab-3317baf51c8d.png',(104,302,248,941)),
    ('RightWatchTower','exec-f107926c-e0d9-4f70-9eec-761a46f0922c.png',(1478,342,1575,941)),
]:
    path = root/'Assets/DystopiaPrototype/Art'/(name+'.png')
    target = Image.open(path).convert('RGBA')
    before = np.array(target)
    target.save(root/'output/imagegen'/(name+'-before-clean-legs.png'))
    pixels = np.array(Image.open(generated/file).convert('RGBA').crop(box))
    rgb = pixels[:,:,:3].astype(int)
    background = rgb[:,:,1] - np.maximum(rgb[:,:,0],rgb[:,:,2]) > 16
    pixels[background] = 0
    pixels[~background,3] = 255
    solid = pixels[:,:,3] == 255
    assert not ((pixels[:,:,:3].max(axis=2)>120) & solid).any(), 'Bright background residue'
    visited = np.zeros(solid.shape,dtype=bool)
    components=[]
    for y,x in zip(*np.where(solid)):
        if visited[y,x]: continue
        queue=deque([(y,x)]); visited[y,x]=True; count=0
        while queue:
            cy,cx=queue.popleft(); count+=1
            for dy,dx in [(-1,0),(1,0),(0,-1),(0,1),(-1,-1),(-1,1),(1,-1),(1,1)]:
                ny,nx=cy+dy,cx+dx
                if 0<=ny<solid.shape[0] and 0<=nx<solid.shape[1] and solid[ny,nx] and not visited[ny,nx]:
                    visited[ny,nx]=True; queue.append((ny,nx))
        components.append(count)
    target.paste(Image.fromarray(pixels),box[:2])
    target.save(path)
    after=np.array(Image.open(path).convert('RGBA'))
    change=np.any(before!=after,axis=2); change[box[1]:box[3],box[0]:box[2]]=False
    assert not change.any()
    # Composite previews, rather than an image viewer's handling of hidden RGB.
    for label,color in [('dark',(35,42,49,255)),('light',(185,195,202,255))]:
        crop=target.crop(box)
        preview=Image.new('RGBA',crop.size,color); preview.alpha_composite(crop)
        preview.resize((crop.width*2,crop.height*2),Image.Resampling.NEAREST).save(root/'output/imagegen'/(name+'-clean-'+label+'.png'))
    print(name, 'bright residue=0, components=',sorted(components,reverse=True), 'outside edit unchanged=True')
