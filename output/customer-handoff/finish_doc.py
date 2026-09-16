import json
from pathlib import Path
root=Path('F:/UnityProject/Cashier')
rows=json.loads((root/'output/customer-handoff/inventory.json').read_text(encoding='utf-8'))
doc=root/'doc/CUSTOMER_ARTWORK_GUIDE.md'
s=doc.read_text(encoding='utf-8')
child=['| 원화 | PNG 전체 | 알파 bbox 크기 | 실제 보이는 크기(약, Canvas 단위) |','|---|---|---|---|']
for i in rows:
    if 'Child' in i['name']:
        x,y,r,b=i['bbox'];w=r-x;h=b-y
        child.append(f"| {i['name']} | {i['width']}×{i['height']} | {w}×{h} | {w/i['width']*220:.1f}×{h/i['height']*220:.1f} |")
table=['| 성별/원화 | 원본 W×H | bbox 시작 X,Y | bbox W×H |','|---|---|---|---|']
classes=['Normal','Hasty','PriceSensitive','Wealthy','Poor','Child','Elder']
ordered=sorted(rows,key=lambda i:(i['gender']!='Male',next(j for j,c in enumerate(classes) if i['name'].startswith(i['gender']+c+'_')),i['name']))
for i in ordered:
    x,y,r,b=i['bbox'];name=i['name'];gender=i['gender']
    table.append(f"| [{name}](../Assets/Textures/art/Customer/{gender}/{name}.png) | {i['width']}×{i['height']} | {x},{y} | {r-x}×{b-y} |")
s=s.replace('<!-- CHILD_TABLE -->','\n'.join(child)).replace('<!-- INVENTORY_TABLE -->','\n'.join(table))
s=s.replace('Bilinear, Mipmap Off, Clamp, Uncompressed, Max Size 256이다.','생성 크기는 최대 변 256이며 Bilinear, Mipmap Off, Clamp, Uncompressed를 목표로 한다. 저장된 예제 .meta는 루트 maxTextureSize 2048, DefaultTexturePlatform 256을 함께 기록한다. 원화와 혼동하여 숫자 하나만 보고 import 상태를 판단하지 않는다.')
doc.write_text(s,encoding='utf-8')
print('Document:',len(s.splitlines()),'lines,',len(s.encode('utf-8')),'bytes; 60 inventory rows and 6 child measurements')
