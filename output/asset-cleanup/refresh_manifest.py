import json,re,pathlib,hashlib,datetime
root=pathlib.Path.cwd(); out=root/'output/asset-cleanup'; m=json.loads((out/'manifest.json').read_text(encoding='utf-8-sig'))
texts={str(p.relative_to(root)).replace('\\','/'):p.read_text(encoding='utf-8-sig',errors='replace') for p in (root/'Assets').rglob('*') if p.is_file() and p.suffix in ('.unity','.prefab','.asset','.mat','.controller','.anim','.shader','.cs','.json')}
keep=[]
for e in m['items']:
 p=root/e['source']; meta=pathlib.Path(str(p)+'.meta')
 if not p.exists(): raise Exception('Missing '+str(p))
 guid=re.search(r'^guid: (\w+)',meta.read_text(encoding='utf-8-sig'),re.M)[1]
 if guid!=e['guid']: raise Exception('GUID changed '+str(p))
 refs=[n for n,s in texts.items() if n!=e['source'] and guid in s]
 if not e['destination'] and refs:
  e['destination']='Assets/Textures/Checkout/Legacy/'+p.name if p.suffix=='.png' else 'Assets/Editor/Checkout/'+p.name
  keep.append({'source':e['source'],'references':refs})
 e['sha256']=hashlib.sha256(p.read_bytes()).hexdigest(); e['metaSha256']=hashlib.sha256(meta.read_bytes()).hexdigest()
m['backupRoot']='output/asset-cleanup/'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S')
(out/'manifest.json').write_text(json.dumps(m,ensure_ascii=False,indent=2),encoding='utf-8')
(out/'retained-references.json').write_text(json.dumps(keep,ensure_ascii=False,indent=2),encoding='utf-8')
print('moves',sum(bool(e['destination']) for e in m['items']),'deletes',sum(not e['destination'] for e in m['items']),'retained',keep)
