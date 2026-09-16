import pathlib,json,re,shutil
root=pathlib.Path.cwd(); out=root/'output/asset-cleanup'; m=json.loads((out/'manifest.json').read_text(encoding='utf-8')); moved={e['source']:e['destination'] for e in m['items'] if e['destination']}
for p in (root/'Assets/DystopiaPrototype').rglob('*.cs'):
 if p.name=='AssetCleanupOnce.cs':continue
 s=p.read_text(encoding='utf-8-sig'); old=s
 for a,b in sorted(moved.items(),key=lambda x:-len(x[0])):
  s=s.replace('"'+a+'"','"'+b+'"')
  if a.startswith('Assets/DystopiaPrototype/'):
   rel=a[len('Assets/DystopiaPrototype/'):]
   s=re.sub(r'Root\s*\+\s*"'+re.escape(rel)+'"','"'+b+'"',s)
 s=s.replace('Assets/DystopiaPrototype/Art/Customers/','Assets/Textures/Checkout/Characters/Customers/')
 s=re.sub(r'Root\s*\+\s*"Art/Products/"','"Assets/Textures/Checkout/Products/"',s)
 s=s.replace('Assets/DystopiaPrototype/TopDownTest/Art/','Assets/Textures/Checkout/Workbench/')
 s=s.replace('Assets/DystopiaPrototype/Art/ChimneySmoke','Assets/Textures/Checkout/Effects/ChimneySmoke')
 s=re.sub(r'Root\s*\+\s*"Art/RearWatchGuard"','"Assets/Textures/Checkout/Characters/RearWatchGuard"',s)
 s=s.replace('Root+"Art/"+(i==0 ? "Stage2Container" : "Stage2Clock")','"Assets/Textures/Checkout/Shop/"+(i==0 ? "Stage2Container" : "Stage2Clock")')
 s=s.replace('"Assets/DystopiaPrototype/Art/"+name+".png"','FindCheckoutArtwork(name)')
 s=s.replace('Root+"Art/"+name+".png"','FindCheckoutArtwork(name)')
 s=s.replace('Directory.GetFiles(Root+"Art","*.png")','Directory.GetFiles("Assets/Textures/Checkout", "*.png", SearchOption.AllDirectories)')
 s=s.replace('art + (showClock ? "Art/BoothCounter.png" : "TopDownTest/Art/TopDownWorkbench.png")','showClock ? "Assets/Textures/Checkout/Shop/BoothCounter.png" : "Assets/Textures/Checkout/Workbench/TopDownWorkbench.png"')
 if p.name=='DystopiaTopDownTestTools.cs':
  for n in ['WorkbenchLighting.mat','VacuumWind.mat']:
   s=s.replace('art+"'+n+'"','"Assets/Materials/Checkout/'+n+'"')
 if p.name=='DystopiaTools.cs':
  # Retired application helpers must point to the currently supported replacements, not deleted imports.
  aliases={'Assets/Textures/Checkout/Workbench/Stage1Container.png':'Assets/Textures/Checkout/Shop/Stage1WoodContainerCompact.png','Assets/Textures/Checkout/Workbench/Stage1ContainerNormal.png':'Assets/Textures/Checkout/Shop/Stage1WoodContainerCompactNormal.png','Assets/DystopiaPrototype/Art/Stage1Clock.png':'Assets/Textures/Checkout/Shop/Stage1BasicClock.png','Assets/DystopiaPrototype/Art/Stage1ClockNormal.png':'Assets/Textures/Checkout/Shop/Stage1BasicClockNormal.png'}
  for a,b in aliases.items():s=s.replace(a,b)
  # This obsolete table apply entry is no longer on the menu; reject it explicitly before accessing removed data.
  s=s.replace('const string path=Root+"Art/Stage2Table.png";','const string path="Assets/Textures/Checkout/Shop/Stage2Shop.png";')
  marker='    private static Sprite Art(string name)'
  pos=s.index(marker)
  helper='''    private static string FindCheckoutArtwork(string name)
    {
        // Editor 전용 기존 이름 조회입니다. 이동된 폴더에서 정확히 같은 파일 하나만 허용합니다.
        return Directory.GetFiles("Assets/Textures/Checkout", name + ".png", SearchOption.AllDirectories).SingleOrDefault()?.Replace('\\\\', '/');
    }

    /// <summary>정리된 이미지 폴더에서 기존 아트 이름의 Sprite를 읽습니다.</summary>
'''
  s=s[:pos]+helper+s[pos:]
 if s!=old:
  backup=root/m['backupRoot']/'code'/p.relative_to(root);backup.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,backup)
  p.write_text(s,encoding='utf-8',newline='\r\n')
  print(p.relative_to(root))
