import subprocess,re,json
from pathlib import Path
ROOT=Path('F:/UnityProject/Cashier')
def run(*args,data=None):return subprocess.check_output(['git',*args],cwd=ROOT,input=data)
def put(path,text):
    oid=run('hash-object','-w','--stdin',data=text.encode('utf-8')).decode().strip()
    run('update-index','--cacheinfo','100644',oid,path)
p='Assets/DystopiaPrototype/TopDownTest/Scripts/DystopiaTopDownTest.cs'
s=run('show','HEAD:'+p).decode().replace('\r\n','\n')
for gender,boolean,plural in [('Male','true','male'),('Female','false','female')]:
    old=f'$"Assets/Textures/Checkout/Characters/Customers/{gender}Customer_{{i + 1:00}}.png"'
    new=f'"Assets/Textures/art/Customer/{gender}/" + DystopiaSession.AppearanceFileName({boolean}, i) + ".png"'
    assert s.count(old)==1
    s=s.replace(old,new)
put(p,s)
for p in ['Assets/DystopiaPrototype/Scripts/DystopiaPixelStage.cs','Assets/DystopiaPrototype/Scenes/DystopiaVerticalSlice.unity']:
    put(p,(ROOT/'output/customer-handoff/commit-files'/p).read_text(encoding='utf-8-sig'))
print('Index narrowed to customer changes. Working scene/code files untouched.')
