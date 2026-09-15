import re, json, subprocess
from pathlib import Path
from PIL import Image

ROOT=Path('F:/UnityProject/Cashier')
OUT=ROOT/'output/customer-handoff'
OUT.mkdir(parents=True,exist_ok=True)
def blocks(text):
    return {m[1]:m[2] for m in re.findall(r'^--- !u!(\d+) &(\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',text,re.M|re.S)}
prefab=blocks((ROOT/'Assets/DystopiaPrototype/Prefabs/FrontView.prefab').read_text(encoding='utf-8-sig'))
targets={}
for fid,b in prefab.items():
    n=re.search(r'^  m_Name: (.+)$',b,re.M)
    if n and n[1] in ['Customer','WaitingLeft','WaitingRear','FrontView']:
        comps=re.findall(r'component: \{fileID: (\d+)\}',b)
        targets[n[1]]={'object':fid,'components':comps}
fields=['m_AnchoredPosition','m_SizeDelta','m_LocalScale','m_AnchorMin','m_AnchorMax','m_Pivot','m_LocalRotation','m_PreserveAspect','m_Sprite']
scenes=['Assets/DystopiaPrototype/Scenes/DystopiaVerticalSlice.unity','Assets/DystopiaPrototype/Editor/References/Stage1Reference.unity','Assets/DystopiaPrototype/Editor/References/Stage2Reference.unity','Assets/DystopiaPrototype/Editor/References/Stage3Reference.unity']
report={}
for scene in scenes:
    s=(ROOT/scene).read_text(encoding='utf-8-sig')
    overrides={}
    for fid,key,value,ref in re.findall(r'    - target: \{fileID: (\d+), guid: f7d7176ddb30caf46b5519836f319e93, type: 3\}\n      propertyPath: ([^\n]+)\n      value:([^\n]*)\n      objectReference: ([^\n]+)',s):
        overrides.setdefault(fid,{})[key]=value.strip() or ref
    rows={}
    for name,t in targets.items():
        values={}
        for fid in t['components']:
            b=prefab[fid]
            for f in fields:
                m=re.search(r'^  '+f+r': (.+)$',b,re.M)
                if m: values[f]=m[1]
            for key,value in overrides.get(fid,{}).items():
                if key.split('.')[0] in fields: values['override.'+key]=value
        rows[name]=dict(t,values=values)
    rows['childFields']={key:(re.search(r'^  '+key+r': (.+)$',s,re.M).group(1) if re.search(r'^  '+key+r': (.+)$',s,re.M) else 'absent; code default') for key in ['childPortraitScale','childPortraitRise','useCustomerNormalMap','matchScreenResolution']}
    report[scene]=rows
(OUT/'layout.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
inventory=[]
for gender in ['Male','Female']:
    for p in sorted((ROOT/'Assets/Textures/art/Customer'/gender).glob('*.png')):
        im=Image.open(p).convert('RGBA'); a=im.getchannel('A'); bbox=a.point(lambda v:255 if v>8 else 0).getbbox()
        meta=Path(str(p)+'.meta').read_text(encoding='utf-8-sig')
        inventory.append(dict(name=p.stem,gender=gender,width=im.width,height=im.height,bbox=bbox,guid=re.search(r'^guid: (\w+)',meta,re.M)[1],bytes=p.stat().st_size))
(OUT/'inventory.json').write_text(json.dumps(inventory,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2))
print('Portraits:',len(inventory),'PNG bytes:',sum(i['bytes'] for i in inventory))
