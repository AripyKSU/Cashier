"""Add four user-provided elders without resampling or changing interior colors."""
from pathlib import Path
import json
import shutil
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as nd

root = Path(__file__).resolve().parents[2]
out = root / 'output/customer-cleanup/elders'
out.mkdir(exist_ok=True)
(out / 'originals').mkdir(exist_ok=True)
art = root / 'Assets/DystopiaPrototype/Art/Customers'
mapping = [('할아버지.png', 'MaleCustomer_28.png'), ('할아버지2.png', 'MaleCustomer_29.png'),
           ('할머니.png', 'FemaleCustomer_19.png'), ('할머니2.png', 'FemaleCustomer_20.png')]
sheet = Image.new('RGB', (1024, 600), (100, 110, 120))
draw = ImageDraw.Draw(sheet)
report = []
for i, (name, asset) in enumerate(mapping):
    source = Path('N:/개인/정총무') / name
    backup = out / 'originals' / name
    if not backup.exists():
        shutil.copy2(source, backup)
    original = np.array(Image.open(backup).convert('RGBA'))
    result = original.copy()
    mask = original[:, :, 3] >= 128
    padded = np.pad(mask, 1)
    seed = np.zeros_like(padded)
    seed[0, :] = seed[-1, :] = True
    seed[:, 0] = seed[:, -1] = True
    exterior = nd.binary_propagation(seed, mask=~padded)[1:-1, 1:-1]
    edge = mask & nd.binary_dilation(exterior, structure=np.ones((3, 3)))
    labels, _ = nd.label(mask, np.ones((3, 3)))
    sizes = np.bincount(labels.ravel())
    detached = mask & (sizes[labels] <= 2)
    bright = edge & (original[:, :, :3].min(2) > 210)
    result[exterior | detached | bright, 3] = 0
    result[edge & ~detached & ~bright, 3] = 255
    protected = mask & ~edge & ~detached
    assert np.array_equal(result[protected], original[protected])
    assert np.array_equal(result[:, :, :3], original[:, :, :3])
    im = Image.fromarray(result)
    bbox = im.getbbox()
    cropped = im.crop(bbox)
    final = Image.new('RGBA', (cropped.width + 10, cropped.height + 10))
    final.paste(cropped, (5, 5))
    final.save(art / asset)
    for row, a in enumerate([original, result]):
        preview = Image.fromarray(a).crop(bbox)
        preview.thumbnail((230, 260), Image.Resampling.NEAREST)
        factor = min(230 // preview.width, 260 // preview.height)
        if factor > 1:
            preview = preview.resize((preview.width * factor, preview.height * factor), Image.Resampling.NEAREST)
        sheet.paste(preview, (i * 256 + (256 - preview.width) // 2, row * 300 + 30), preview)
        draw.text((i * 256 + 5, row * 300 + 6), ('Before ' if row == 0 else 'After ') + asset, fill='white')
    report.append(dict(source=name, asset=asset, type='Elderly',
                       alpha_changes=int(np.count_nonzero(original[:, :, 3] != result[:, :, 3])),
                       interior_changes=0, rgb_changes=0, trim_rect=bbox, size=final.size))
(out / 'report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
sheet.save(out / 'comparison.png')
print(json.dumps(report, ensure_ascii=True))
