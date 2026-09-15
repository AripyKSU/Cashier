import pathlib,json,re
root=pathlib.Path.cwd();m=json.loads((root/'output/asset-cleanup/manifest.json').read_text());deleted={e['guid']:e['source'] for e in m['items'] if not e['destination']};refs=[]
for p in (root/'Assets').rglob('*'):
 if p.is_file() and p.suffix in ('.unity','.prefab','.asset','.mat','.controller','.anim','.meta'):
  t=p.read_text(encoding='utf-8-sig',errors='replace')
  for g in re.findall(r'guid: ([0-9a-f]{32})',t):
   if g in deleted:refs.append((str(p),deleted[g]))
print('Deleted GUID references:',refs)
print('Remaining old art roots:',[(x,(root/x).exists()) for x in ['Assets/DystopiaPrototype/Art','Assets/DystopiaPrototype/TopDownTest/Art']])
p=root/'doc/CHECKOUT_ASSET_LAYOUT.md';s=p.read_text();s=s.replace('`Assets/Textures/Checkout/Effects/` 및 해당 하위 분류','`Assets/Textures/Checkout/Background/`, `Assets/Textures/Checkout/Effects/`');p.write_text(s,encoding='utf-8')
