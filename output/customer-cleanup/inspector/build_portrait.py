"""Split the approved portrait at the jaw; preserve source RGB and synthesize normals."""
from pathlib import Path
import shutil,json
import numpy as np
from scipy import ndimage as nd
from PIL import Image, ImageDraw

root=Path(__file__).resolve().parents[3]
art=root/'Assets/DystopiaPrototype/Art'
out=Path(__file__).resolve().parent
source=art/'Inspector.png'
backup=out/'before-rig.png'
if not backup.exists(): shutil.copy2(source,backup)
im=Image.open(backup).convert('RGBA');a=np.array(im);h,w=a.shape[:2]
maskim=Image.new('L',(w,h));ImageDraw.Draw(maskim).polygon([(0,0),(w,0),(w,46),(84,46),(80,59),(75,65),(67,68),(59,67),(51,62),(47,55),(46,46),(0,46)],fill=255)
mask=np.array(maskim)>0
head=a.copy();head[~mask,3]=0
body=a.copy();body[mask,3]=0
# A small underpaint hidden under the jaw keeps the collar closed at +/-3 degrees.
neckim=Image.new('L',(w,h));ImageDraw.Draw(neckim).polygon([(56,57),(73,57),(79,70),(70,77),(57,73),(51,65)],fill=255)
neck=(np.array(neckim)>0)&mask
yy,xx=np.indices((h,w));body[neck]=a[np.clip(yy[neck]+12,69,77),xx[neck]]
assert np.array_equal(head[:,:,:3],a[:,:,:3])
assert np.array_equal(body[~neck,:3],a[~neck,:3])
Image.fromarray(head).save(art/'InspectorHead.png');Image.fromarray(body).save(art/'InspectorBody.png')
# Broad face/torso volume plus restrained luminance relief. Do not bake source lighting twice.
lum=a[:,:,:3]@np.array([.2126,.7152,.0722])/255
relief=nd.gaussian_filter(lum,1.25)
gy,gx=np.gradient(relief)
nx=(xx-67)/80 + gx*2.0
ny=-(yy-165)/650 - gy*2.0
# Smoothly join head and torso normals across the neck and collar.
face_weight=np.clip((96-yy)/44,0,1)
face_weight=face_weight*face_weight*(3-2*face_weight)
nx=nx*(1-face_weight)+((xx-66)/38+gx*1.5)*face_weight
ny=ny*(1-face_weight)+(-(yy-40)/55-gy*1.5)*face_weight
nz=np.ones_like(nx)
normal=np.stack([nx,ny,nz],2);normal/=np.linalg.norm(normal,axis=2,keepdims=True)
normal=np.rint((normal*.5+.5)*255).astype(np.uint8)
Image.fromarray(normal).save(art/'InspectorNormal.png')
(out/'rig-report.json').write_text(json.dumps({'size':[w,h],'neck_pivot_pixels':[66,66],'head_source_rgb_changes':0,'body_rgb_changes_outside_neck_underpaint':0,'neck_underpaint_pixels':int(neck.sum()),'normal_method':'approximate face/torso curvature + smoothed luminance relief'},indent=2))
print('Head/body RGB preserved outside neck underpaint; source intact.')
