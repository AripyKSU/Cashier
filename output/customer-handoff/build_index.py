import subprocess, xml.etree.ElementTree as ET
from pathlib import Path
ROOT=Path('F:/UnityProject/Cashier'); OUT=ROOT/'output/customer-handoff/build'
OUT.mkdir(parents=True,exist_ok=True)
paths=set(subprocess.check_output(['git','ls-files','-z'],cwd=ROOT).decode().split('\0'))
sources=[p for p in paths if p.endswith('.cs')]
subprocess.run(['git','checkout-index','--prefix='+OUT.as_posix()+'/', '--',*sources],cwd=ROOT,check=True)
for project in ROOT.glob('*.csproj'):
    tree=ET.parse(project);root=tree.getroot();ns=root.tag.partition('}')[0]+'}' if '}' in root.tag else ''
    for group in root:
        for elem in list(group):
            if elem.tag==ns+'Compile' and 'Include' in elem.attrib:
                p=elem.attrib['Include'].replace('\\','/')
                if p in paths:elem.set('Include',str(OUT/p))
                elif p.startswith('Assets/') and not p.startswith('Assets/Plugins/'):
                    group.remove(elem)
                else:elem.set('Include',str(ROOT/p) if not Path(p).is_absolute() else p)
            if elem.tag in [ns+'OutputPath',ns+'BaseIntermediateOutputPath',ns+'IntermediateOutputPath']:
                elem.text=str(OUT/('bin' if elem.tag==ns+'OutputPath' else 'obj')/project.stem)+'/'
            if elem.tag==ns+'ProjectReference':
                elem.set('Include',str(OUT/Path(elem.attrib['Include'].replace('\\','/')).name))
            for child in elem:
                if child.tag==ns+'HintPath' and child.text and not Path(child.text).is_absolute():
                    child.text=str(ROOT/child.text.replace('\\','/'))
    tree.write(OUT/project.name,encoding='utf-8',xml_declaration=True)
print('Exported index-only C# sources:',len(sources))
