import pathlib,json,hashlib,re,collections
root=pathlib.Path.cwd(); m=json.loads((root/'output/asset-cleanup/manifest.json').read_text(encoding='utf-8')); errors=[]
for e in m['items']:
 src=root/e['source'];dest=root/e['destination'] if e['destination'] else None
 if src.exists() or pathlib.Path(str(src)+'.meta').exists():errors.append('Source remains '+e['source'])
 if dest:
  if not dest.exists():errors.append('Missing '+e['destination']);continue
  meta=pathlib.Path(str(dest)+'.meta');g=re.search(r'^guid: (\w+)',meta.read_text(encoding='utf-8-sig'),re.M)[1]
  if g!=e['guid']:errors.append('GUID mismatch '+str(dest))
  if hashlib.sha256(dest.read_bytes()).hexdigest()!=e['sha256']:errors.append('Content changed '+str(dest))
print('Completed marker:',(root/m['backupRoot']/'completed.txt').exists());print('Validation errors:',errors)
print('Korean filenames:',[str(p) for p in (root/'Assets').rglob('*') if re.search('[가-힣]',p.name)])
paths=[]
for p in (root/'Assets/DystopiaPrototype').rglob('*.cs'):
 if p.name=='AssetCleanupOnce.cs':continue
 for q in re.findall(r'"(Assets/(?:Textures|Materials|Shaders|Fonts)/Checkout/[^"{}]*\.(?:png|mat|shader|otf))"',p.read_text(encoding='utf-8')):
  if not (root/q).exists():paths.append((str(p),q))
print('Missing literal paths:',paths)
(root/'output/asset-cleanup/verification.json').write_text(json.dumps({'errors':errors,'missingLiteralPaths':paths,'moves':160,'deletes':20},indent=2),encoding='utf-8')
