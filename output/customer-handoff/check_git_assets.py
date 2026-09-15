import subprocess,re,json
from pathlib import Path
root=Path('F:/UnityProject/Cashier')
def git(*args):return subprocess.check_output(['git',*args],cwd=root).decode('utf-8-sig')
new={}
for p in (root/'Assets/Textures/art/Customer').rglob('*.meta'):
    m=re.search(r'^guid: (\w+)',p.read_text(encoding='utf-8-sig'),re.M)
    if m:new[m[1]]=str(p.relative_to(root)).replace('\\','/')
old=[]
for p in git('ls-files','Assets/Textures/Checkout/Characters/Customers').splitlines():
    if p.endswith('.meta') and not p.startswith('"'):
        text=git('show','HEAD:'+p)
        m=re.search(r'^guid: (\w+)',text,re.M)
        if m:old.append({'old':p,'guid':m[1],'new':new.get(m[1])})
print(json.dumps(old,ensure_ascii=False,indent=2))
(root/'output/customer-handoff/legacy-guid-map.json').write_text(json.dumps(old,ensure_ascii=False,indent=2),encoding='utf-8')
