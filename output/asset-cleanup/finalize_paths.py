import pathlib,re,json
p=pathlib.Path('Assets/DystopiaPrototype/Editor/DystopiaTools.cs');s=p.read_text(encoding='utf-8');a=s.index('    public static void ApplyStage2Table()');b=s.index('    /// <summary>',a+20)
s=s[:a]+'''    public static void ApplyStage2Table()
    {
        throw new InvalidOperationException("폐기된 Stage2Table 원본입니다. Dystopia/Apply Stage 2 Shop을 사용하세요.");
    }

'''+s[b:]
s=s.replace('    /// <summary>プロジェクト内部の正確なパスからEditorでのみ取得します。</summary>\n    private static string FindCheckoutArtwork', '    /// <summary>Editor에서 정리된 하위 폴더의 정확한 이미지 이름을 찾습니다.</summary>\n    /// <param name="name">확장자를 제외한 기존 아트 이름입니다.</param>\n    /// <returns>동일 이름이 유일하면 해당 경로, 없으면 null입니다.</returns>\n    private static string FindCheckoutArtwork')
s=s.replace('    /// <summary>정리된 이미지 폴더에서 기존 아트 이름의 Sprite를 읽습니다.</summary>','    /// <summary>정리된 이미지 폴더에서 기존 아트 이름의 Sprite를 읽습니다.</summary>\n    /// <param name="name">확장자를 제외한 아트 이름입니다.</param>\n    /// <returns>등록된 Sprite 또는 null입니다.</returns>')
s=s.replace('        string art = "Assets/DystopiaPrototype/";\n','')
p.write_text(s,encoding='utf-8',newline='\r\n')
p=pathlib.Path('Assets/DystopiaPrototype/Scripts/DystopiaScreen.cs');s=p.read_text(encoding='utf-8');s=re.sub(r'^.*AssetDatabase.LoadAssetAtPath<Texture2D>\("Assets/DystopiaPrototype/Art/LedgerStamp[^\"]*"\);\n','',s,flags=re.M);p.write_text(s,encoding='utf-8',newline='\r\n')
