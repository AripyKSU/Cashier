import re, subprocess, difflib, json
from pathlib import Path
ROOT=Path('F:/UnityProject/Cashier')
OUT=ROOT/'output/customer-handoff'
def head(path):return subprocess.check_output(['git','show','HEAD:'+path],cwd=ROOT).decode('utf-8-sig').replace('\r\n','\n')
def work(path):return (ROOT/path).read_text(encoding='utf-8-sig')
patches=[]
def patch(path,new):
    old=head(path)
    assert new!=old,path
    patches.extend(difflib.unified_diff(old.splitlines(True),new.splitlines(True),fromfile='a/'+path,tofile='b/'+path))
    dest=OUT/'commit-files'/path;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_text(new,encoding='utf-8')
# Keep the unrelated pouring-container assignment in the working tree only.
p='Assets/DystopiaPrototype/TopDownTest/Scripts/DystopiaTopDownTest.cs'
s=work(p).replace('if (!hasPlacedUi) pouringContainerImage.sprite = tiltedContainer;','pouringContainerImage.sprite = tiltedContainer;')
patch(p,s)
# Only the customer normal-map lookup, not render resolution changes.
p='Assets/DystopiaPrototype/Scripts/DystopiaPixelStage.cs';old=head(p);current=work(p)
changes=list(difflib.SequenceMatcher(None,old.splitlines(True),current.splitlines(True)).get_opcodes())
a=old.splitlines(True);b=current.splitlines(True);result=[]
for kind,i,j,k,l in changes:
    delta=''.join(a[i:j]+b[k:l])
    customer=any(word in delta for word in ['normalVariants','NormalVariant','NormalFor','Texture2D normal =','source.name == "Customer"'])
    result.extend(b[k:l] if kind=='equal' or customer else a[i:j])
patch(p,''.join(result))
# Serialize existing customer arrays and child controls only; never write/reload the scene file.
p='Assets/DystopiaPrototype/Scenes/DystopiaVerticalSlice.unity';old=head(p);current=work(p)
def replace_fields(old,current,field):
    pattern=r'^  '+field+r':[^\n]*\n(?:  - [^\n]*\n)*'
    originals=list(re.finditer(pattern,old,re.M)); replacements=list(re.finditer(pattern,current,re.M))
    assert len(originals)==len(replacements),(field,len(originals),len(replacements))
    for src,dst in reversed(list(zip(originals,replacements))):old=old[:src.start()]+dst[0]+old[src.end():]
    return old
for field in ['maleCustomers','femaleCustomers','maleBreathing','femaleBreathing']:
    old=replace_fields(old,current,field)
for field in ['childPortraitScale','childPortraitRise']:
    value=re.search(r'^  '+field+r': [^\n]+\n',current,re.M)[0]
    if re.search(r'^  '+field+r':',old,re.M):old=re.sub(r'^  '+field+r': [^\n]+\n',value,old,flags=re.M)
    else:old=old.replace('  maleBreathing:',value+'  maleBreathing:',1)
patch(p,old)
(OUT/'customer-only.patch').write_text(''.join(patches),encoding='utf-8')
print('Prepared customer-only index patch:',len(''.join(patches)),'bytes')
