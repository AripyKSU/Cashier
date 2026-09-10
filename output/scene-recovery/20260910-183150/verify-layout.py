import json,re
from pathlib import Path
folder=Path('output/scene-recovery/20260910-183150')
scene=Path('Assets/DystopiaPrototype/Scenes/DystopiaVerticalSlice.unity').read_text()
prefab=Path('Assets/DystopiaPrototype/Prefabs/FrontView.prefab').read_text()
def blocks(text):return {int(i): (int(c),v) for c,i,v in re.findall(r'^--- !u!(\d+) &(\d+)[^\n]*\n([\s\S]*?)(?=^--- !u!|\Z)',text,re.M)}
sb,pb=blocks(scene),blocks(prefab)
names={i:re.search(r'^  m_Name: (.*)$',v,re.M)[1] for i,(c,v) in pb.items() if c==1}
back=json.loads((folder/'backup-transforms.json').read_text(encoding='utf8'))
report=[]
for name in ['Canopy','Counter','Stage3LeftPillar','Stage3RightPillar','Stage3CeilingLamp']:
 d=next(x for x in back if x['name']==name);text=sb.get(d['id'],(0,''))[1];ref=None
 if not text:
  ref,text=next((i,v) for i,(c,v) in pb.items() if c==224 and names.get(int(re.search(r'm_GameObject: \{fileID: (\d+)',v)[1]))==name)
 fields={}
 for field in ['m_AnchoredPosition','m_LocalScale']:
  values={k:float(v) for k,v in re.findall(r'([xyz]): ([-\d.eE+]+)',re.search(r'^  '+field+r': (.*)$',text,re.M)[1])}
  if ref:
   for k in values:
    pattern=r'- target: \{fileID: '+str(ref)+r', guid: f7d7176ddb30caf46b5519836f319e93, type: 3\}\s+propertyPath: '+field+r'\.'+k+r'\s+value: ([^\n]+)'
    m=re.search(pattern,scene)
    if m:values[k]=float(m[1])
  fields[field]={'saved':values,'backup':d[field],'matches':all(abs(values[k]-d[field][k])<.0002 for k in values)}
 report.append({'name':name,'fields':fields})
print(json.dumps(report,ensure_ascii=False,indent=2))
(folder/'saved-layout-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
