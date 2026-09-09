"""Approved deterministic customer edge cleanup; source backups are immutable."""
from pathlib import Path
import json
import shutil
import sys
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as nd

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'output/customer-cleanup'
ART = ROOT / 'Assets/DystopiaPrototype/Art/Customers'
ORIGINALS = OUT / 'originals'
CANDIDATES = OUT / 'candidates'
ORIGINALS.mkdir(exist_ok=True)
CANDIDATES.mkdir(exist_ok=True)


def exterior(mask):
    """Find only background connected to the canvas edge, protecting internal detail."""
    padded = np.pad(mask, 1)
    seed = np.zeros_like(padded)
    seed[0, :] = seed[-1, :] = True
    seed[:, 0] = seed[:, -1] = True
    return nd.binary_propagation(seed, mask=~padded)[1:-1, 1:-1]


def backup(source, name):
    target = ORIGINALS / name
    if not target.exists():
        shutil.copy2(source, target)
    return np.array(Image.open(target).convert('RGBA'))


report = []
for path in sorted(ART.glob('*Customer_*.png')):
    if path.stem.endswith('_Normal') or int(path.stem.rsplit('_', 1)[1]) > (24 if path.name.startswith('Male') else 16):
        continue
    original = backup(path, path.name)
    result = original.copy()
    mask = original[:, :, 3] > 0
    edge = mask & nd.binary_dilation(exterior(mask), structure=np.ones((3, 3)))
    labels, _ = nd.label(mask, np.ones((3, 3)))
    sizes = np.bincount(labels.ravel())
    detached = mask & (sizes[labels] <= 5)
    neighbors = nd.convolve(mask.astype(int), np.ones((3, 3), int)) - mask
    # Single-pass pruning only: no repeated erosion, blur, or recoloring.
    tips = edge & (neighbors <= 2)
    luminance = original[:, :, :3].mean(2)
    nearby = nd.maximum_filter(np.where(mask & ~edge, luminance, 0), size=5)
    white = edge & (luminance > 120) & (luminance > nearby + 30)
    removed = detached | tips | white
    result[removed, 3] = 0
    protected = mask & ~edge & ~detached
    assert np.array_equal(result[protected], original[protected])
    assert np.array_equal(result[:, :, :3], original[:, :, :3])
    Image.fromarray(result).save(CANDIDATES / path.name)
    report.append(dict(file=path.name, removed_pixels=int(removed.sum()), interior_changed=0, rgb_changed=0))

sources = [
    ('아이 1.PNG', 'MaleCustomer_25.png', 'Child', 'Male'),
    ('남자아이2.png', 'MaleCustomer_26.png', 'Child', 'Male'),
    ('여자아이1.PNG', 'FemaleCustomer_17.png', 'Child', 'Female'),
    ('여자아이2.PNG', 'FemaleCustomer_18.png', 'Child', 'Female'),
    ('할아버지1.png', 'MaleCustomer_27.png', 'Elderly', 'Male'),
]
reference = np.array(Image.open(Path('N:/개인/정총무/할아버지1.png')).convert('RGBA'))
weights = np.array([.2126, .7152, .0722])
target_mean = (reference[:, :, :3] @ weights)[reference[:, :, 3] == 255].mean()
for source_name, asset_name, kind, gender in sources:
    original = backup(Path('N:/개인/정총무') / source_name, source_name)
    result = original.copy()
    solid = original[:, :, 3] >= 240
    labels, _ = nd.label(solid, np.ones((3, 3)))
    sizes = np.bincount(labels.ravel())
    solid &= sizes[labels] >= 64
    # At source resolution the drawn pixel blocks are about 15 px wide;
    # these sub-block alpha fragments are cutting residue, not drawn features.
    ext = exterior(solid)
    boundary = nd.binary_dilation(ext, iterations=2)
    result[ext | (boundary & ~solid), 3] = 0
    result[solid & boundary, 3] = 255
    # Remove only abnormally bright fringe, never a whole silhouette ring.
    rgb = result[:, :, :3].astype(float)
    lum = rgb @ weights
    med = nd.median_filter(lum, size=5)
    fringe = solid & boundary & (lum > med + 35) & (lum > 110)
    result[fringe, 3] = 0
    gain = 1.0
    if kind == 'Child':
        gain = float(np.clip(target_mean / lum[result[:, :, 3] == 255].mean(), .68, .90))
        result[:, :, :3] = np.clip(np.rint((lum[:, :, None] + .35 * (rgb - lum[:, :, None])) * gain), 0, 255).astype(np.uint8)
    else:
        assert np.array_equal(result[:, :, :3], original[:, :, :3])
    # Keep original pixel grid and dimensions. No resampling or redraw.
    Image.fromarray(result).save(CANDIDATES / asset_name)
    report.append(dict(file=asset_name, source=source_name, type=kind, gender=gender,
                       saturation=.35 if kind == 'Child' else 1, brightness_gain=gain,
                       dimensions=list(Image.fromarray(result).size),
                       alpha_changed=int(np.count_nonzero(result[:, :, 3] != original[:, :, 3]))))

(OUT / 'report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
sheet = Image.new('RGB', (1500, 620), (105, 115, 125))
draw = ImageDraw.Draw(sheet)
for i, (source_name, asset_name, kind, gender) in enumerate(sources):
    bbox = Image.open(CANDIDATES / asset_name).getbbox()
    for row, path in enumerate([ORIGINALS / source_name, CANDIDATES / asset_name]):
        im = Image.open(path).convert('RGBA')
        im = im.crop(bbox)
        im.thumbnail((290, 280), Image.Resampling.NEAREST)
        sheet.paste(im, (i * 300 + (300 - im.width) // 2, row * 310 + 25), im)
        draw.text((i * 300 + 5, row * 310 + 5), ('BEFORE ' if row == 0 else 'AFTER ') + asset_name, fill='white')
sheet.save(OUT / 'new-customers-comparison.png')
print('Inspected 40 existing, prepared 5 new; existing removed pixels:', sum(r.get('removed_pixels', 0) for r in report))

if '--apply' in sys.argv:
    new_names = {item[1] for item in sources}
    for path in CANDIDATES.glob('*.png'):
        result = Image.open(path).convert('RGBA')
        if path.name in new_names:
            # Only transparent canvas is trimmed. Pixel coordinates within the drawing
            # remain one-to-one; the grandfather's full body is retained.
            bbox = result.getbbox()
            cropped = result.crop(bbox)
            result = Image.new('RGBA', (cropped.width + 10, cropped.height + 10))
            result.paste(cropped, (5, 5))
            next(r for r in report if r['file'] == path.name)['trim_rect'] = list(bbox)
        target = ART / path.name
        if not target.exists() or not np.array_equal(np.array(Image.open(target).convert('RGBA')), np.array(result)):
            result.save(target)
    (OUT / 'report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
