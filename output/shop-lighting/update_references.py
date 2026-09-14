"""Patch only verified shop lighting fields, preserving all other scene bytes."""
from pathlib import Path
import re
import json
import shutil

root = Path('Assets/DystopiaPrototype')
backup = Path('output/shop-lighting/reference-backup')
backup.mkdir(parents=True, exist_ok=True)
normal_guid = re.search(r'^guid: (\w+)', (root/'Art/Stage3ContainerNormal.png.meta').read_text(), re.M)[1]
report = {}
for number in (2, 3):
    path = root/f'Editor/References/Stage{number}Reference.unity'
    before = path.read_bytes()
    saved = backup/path.name
    if saved.exists():
        assert saved.read_bytes() == before, 'Backup already exists for another revision'
    else:
        shutil.copyfile(path, saved)
    text = before.decode('utf-8')
    changes = []
    def tune(match):
        block = match[0]
        fields = {}
        if number == 2 and 'guid: 5483abef4a4d8994ba8bcf82666a5da9' in block:
            fields = dict(normalResponse='0.4' if 'fileID: 1711071217,' in block else '0.25', rimResponse='0.05', specularResponse='0.08')
        if 'source: {fileID: 2356696958130715508}' in block:
            if number == 2:
                assert 'guid: 44cdeba4a972958468be0ec121357197' in block
                fields = dict(normalResponse='0.65', highlightResponse='0.78', rimResponse='0.03', specularResponse='0.08', bottomShade='0.55')
            else:
                assert 'normalMap: {fileID: 0}' in block
                fields = dict(normalSprite='{fileID: 5187440768154225832, guid: ba80f4bbc4fb47b4caa16e46e22ab9cb, type: 3}', normalMap='{fileID: 2800000, guid: '+normal_guid+', type: 3}', normalResponse='0.45', highlightResponse='0.8', specularResponse='0.08', bottomShade='0.45')
        for field, value in fields.items():
            block, count = re.subn(r'(?m)^(    '+field+r': )[^\r\n]+', lambda m: m[1]+value, block)
            assert count == 1, field
        if fields:
            changes.append({'source': re.search(r'source: \{fileID: ([^}]+)', block)[1], 'fields': fields})
        return block
    result = re.sub(r'(?m)^  - source:.*?(?=^  - source:|^  lightingShader:)', tune, text, flags=re.S)
    assert len(changes) == (5 if number == 2 else 1), changes
    # All Transform and RectTransform serialized objects must remain byte-identical.
    layout = lambda s: re.findall(r'(?ms)^--- !u!(?:4|224) &.*?(?=^--- !u!|\Z)', s)
    assert layout(text) == layout(result)
    path.write_bytes(result.encode('utf-8'))
    report[str(number)] = changes
(backup/'changes.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
