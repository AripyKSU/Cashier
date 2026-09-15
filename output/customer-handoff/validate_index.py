import subprocess,re,json,collections
from pathlib import Path
ROOT=Path('F:/UnityProject/Cashier')
def git(*args):return subprocess.check_output(['git',*args],cwd=ROOT)
paths=git('ls-files','-z').decode().split('\0'); pathset=set(paths)
metas=[p for p in paths if p.endswith('.meta')]
# Batch-read exactly what is about to be committed, including pre-existing metadata.
proc=subprocess.run(['git','cat-file','--batch'],cwd=ROOT,input=''.join(':'+p+'\n' for p in metas).encode(),stdout=subprocess.PIPE,check=True)
buf=proc.stdout;off=0;guids=collections.defaultdict(list)
for path in metas:
    end=buf.index(b'\n',off);hdr=buf[off:end].decode();size=int(hdr.split()[-1]);off=end+1
    body=buf[off:off+size].decode('utf-8-sig');off+=size+1
    m=re.search(r'^guid: (\w+)',body,re.M)
    if m:guids[m[1]].append(path)
classes=[('Normal',12),('Hasty',3),('PriceSensitive',3),('Wealthy',3),('Poor',3),('Child',3),('Elder',3)]
newroot='Assets/Textures/art/Customer/'
expected={}
for gender in ['Male','Female']:
    names=[f'{gender}{cls}_{i:02}' for cls,count in classes for i in range(1,count+1)]
    expected[gender]=[]
    for name in names:
        p=f'{newroot}{gender}/{name}.png';normal=f'{newroot}NormalMap/{name}_Normal.png'
        for asset in [p,normal]:
            assert asset in pathset and asset+'.meta' in pathset,asset
            g=re.search(rb'(?m)^guid: (\w+)',git('show',':'+asset+'.meta'))[1].decode()
            assert len(guids[g])==1,(g,guids[g])
        expected[gender].append(re.search(rb'(?m)^guid: (\w+)',git('show',':'+p+'.meta'))[1].decode())
scene=git('show',':Assets/DystopiaPrototype/Scenes/DystopiaVerticalSlice.unity').decode()
for gender in expected:
    arrays=re.findall(r'^  '+gender.lower()+r'Customers:\n((?:  - [^\n]*\n)+)',scene,re.M)
    assert len(arrays)==2,(gender,len(arrays))
    for arr in arrays:assert re.findall(r'guid: (\w+)',arr)==expected[gender],gender
doc=git('show',':doc/CUSTOMER_ARTWORK_GUIDE.md').decode()
for dest in re.findall(r'\]\(([^)]+)\)',doc):
    if not dest.startswith(('http:','https:')):
        p=(Path('doc')/dest).as_posix()
        import posixpath
        assert posixpath.normpath(p) in pathset,dest
staged=git('diff','--cached','--name-only').decode().splitlines()
allowed={'Assets/Textures/art/Customer.meta','doc/CUSTOMER_ARTWORK_GUIDE.md','Assets/DystopiaPrototype/Scenes/DystopiaVerticalSlice.unity','Assets/DystopiaPrototype/Scripts/DystopiaSession.cs','Assets/DystopiaPrototype/Scripts/DystopiaScreen.cs','Assets/DystopiaPrototype/Scripts/DystopiaPixelStage.cs','Assets/DystopiaPrototype/TopDownTest/Scripts/DystopiaTopDownTest.cs'}
allowed.update('Assets/DystopiaPrototype/Editor/'+n+ext for n in ['DystopiaCustomerTools','DystopiaCustomerVariety'] for ext in ['.cs','.cs.meta'])
assert all(p in allowed or p.startswith(newroot) for p in staged)
assert not git('diff','--cached','--diff-filter=D','--name-only').strip()
result=dict(stagedFiles=len(staged),portraits=60,normalMaps=60,duplicateCustomerGuids=0,sceneArrays=4,entriesPerArray=30,brokenDocLinks=0,unrelatedPaths=0,deletions=0)
(ROOT/'output/customer-handoff/verification.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result,indent=2))
