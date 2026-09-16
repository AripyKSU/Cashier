using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>손님 원화를 임포트하고, 알파 볼륨에서 노멀맵을 만들고, 기존 Scene의 외형 배열에 연결합니다.</summary>
public static class DystopiaCustomerTools
{
    private const string Root="Assets/DystopiaPrototype/";
    private const string Customer="Assets/Textures/Customer/Dystopia/";
    private const string Male=Customer;
    private const string Female=Customer;
    private const string Normals=Customer+"NormalMaps/";
    /// <summary>손님 임포트 해상도 상한입니다. 픽셀 스테이지 해상도를 올릴 때 여기서 병목이 생기지 않도록 원본을 보존합니다.</summary>
    private const int DisplayMaxSize=2048;
    /// <summary>노멀맵 계산에 사용하는 최대 변 길이입니다. Sprite와 같은 UV를 공유합니다.</summary>
    private const int NormalSize=256;

    /// <summary>원본 PNG를 바꾸지 않고 손님 60종을 기존 손님과 같은 설정으로 임포트합니다.</summary>
    [MenuItem("Dystopia/손님/1. Import Customer Artwork")]
    public static void ImportArtwork()
    {
        int count=0;
        foreach(string path in CustomerPaths()) { ImportCustomerSprite(path); count++; }
        AssetDatabase.Refresh();
        Debug.Log("Customer artwork imported: "+count+" sprites at max "+DisplayMaxSize+"px.");
    }

    /// <summary>손님 60종의 알파 실루엣과 명암에서 tangent-space 노멀맵을 생성합니다.</summary>
    [MenuItem("Dystopia/손님/2. Generate Normal Maps")]
    public static void GenerateNormalMaps()
    {
        Directory.CreateDirectory(Normals);
        var paths=CustomerPaths().ToArray();
        try
        {
            for(int i=0;i<paths.Length;i++)
            {
                string name=Path.GetFileNameWithoutExtension(paths[i]);
                EditorUtility.DisplayProgressBar("손님 노멀맵 생성",name,(float)i/paths.Length);
                WriteNormalMap(paths[i],Normals+name+"_Normal.png");
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        AssetDatabase.Refresh();
        foreach(string path in paths) ImportNormalMap(Normals+Path.GetFileNameWithoutExtension(path)+"_Normal.png");
        AssetDatabase.Refresh();
        Debug.Log("Customer normal maps generated: "+paths.Length+" files in "+Normals);
    }

    /// <summary>현재 Scene의 남녀 외형 배열, 호흡 스타일, 손님 레이어의 노멀맵 대응표를 새 손님으로 교체합니다.</summary>
    [MenuItem("Dystopia/손님/3. Register Customers In Scene")]
    public static void RegisterCustomers()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var scene=SceneManager.GetActiveScene();
        if(scene.path!=Root+"Scenes/DystopiaVerticalSlice.unity") throw new InvalidOperationException("Open DystopiaVerticalSlice first.");
        var males=LoadRange(Male,true,DystopiaSession.MaleAppearanceCount);
        var females=LoadRange(Female,false,DystopiaSession.FemaleAppearanceCount);
        string directory="output/customer-swap/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(directory);
        if(!EditorSceneManager.SaveScene(scene,directory+"/before.unity",true)) throw new IOException("Backup failed.");

        int owners=0;
        foreach(var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(behaviour==null || behaviour.gameObject.scene!=scene) continue;
            var serialized=new SerializedObject(behaviour);
            var maleProperty=serialized.FindProperty("maleCustomers");
            var femaleProperty=serialized.FindProperty("femaleCustomers");
            if(maleProperty==null || femaleProperty==null) continue;
            FillSpriteArray(maleProperty,males);
            FillSpriteArray(femaleProperty,females);
            FillBreathing(serialized.FindProperty("maleBreathing"),males.Length);
            FillBreathing(serialized.FindProperty("femaleBreathing"),females.Length);
            serialized.ApplyModifiedProperties();
            owners++;
        }
        if(owners==0) throw new InvalidOperationException("No component with maleCustomers/femaleCustomers was found in the scene.");

        var stage=UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        int linked=0;
        if(stage!=null)
        {
            var variants=new List<DystopiaPixelStage.NormalVariant>();
            foreach(var sprite in males.Concat(females))
            {
                var map=AssetDatabase.LoadAssetAtPath<Texture2D>(Normals+sprite.name+"_Normal.png");
                if(map!=null) variants.Add(new DystopiaPixelStage.NormalVariant { sprite=sprite,map=map });
            }
            Undo.RecordObject(stage,"Link customer normal maps");
            foreach(var layer in stage.layers)
            {
                if(layer.source==null || layer.surface!=DystopiaPixelStage.Surface.Person) continue;
                layer.normalVariants=variants.ToArray();
                // 옛 단일 연결은 삭제된 손님을 가리키므로 대응표로만 판정하게 비웁니다.
                layer.normalSprite=null; layer.normalMap=null;
                linked++;
            }
            EditorUtility.SetDirty(stage);
            PrefabUtility.RecordPrefabInstancePropertyModifications(stage);
        }

        // 앞 손님 슬롯이 삭제된 Sprite를 들고 있으면 첫 프레임까지 빈 칸으로 보입니다.
        foreach(var image in UnityEngine.Object.FindObjectsByType<Image>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(image.gameObject.scene!=scene) continue;
            if(image.name!="Customer" && image.name!="WaitingLeft" && image.name!="WaitingRear") continue;
            if(image.sprite!=null) continue;
            Undo.RecordObject(image,"Restore customer sprite");
            image.sprite=image.name=="WaitingLeft" ? females[0] : males[0];
            EditorUtility.SetDirty(image); PrefabUtility.RecordPrefabInstancePropertyModifications(image);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"Registered {males.Length} male and {females.Length} female customers on {owners} component(s); normal variants linked to {linked} person layer(s). Backup: {directory}");
    }

    /// <summary>손님 레이어의 노멀맵 연결을 모두 끊습니다. 생성된 파일은 지우지 않으므로 3번 메뉴로 다시 연결할 수 있습니다.</summary>
    [MenuItem("Dystopia/손님/Unlink Normal Maps")]
    public static void UnlinkNormalMaps()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var stage=UnityEngine.Object.FindFirstObjectByType<DystopiaPixelStage>();
        if(stage==null) throw new InvalidOperationException("Open DystopiaVerticalSlice first.");
        Undo.RecordObject(stage,"Unlink customer normal maps");
        int cleared=0;
        foreach(var layer in stage.layers)
        {
            if(layer.source==null) continue;
            if(layer.normalVariants.Length==0 && layer.normalMap==null) continue;
            layer.normalVariants=Array.Empty<DystopiaPixelStage.NormalVariant>();
            layer.normalSprite=null; layer.normalMap=null;
            cleared++;
        }
        EditorUtility.SetDirty(stage);
        PrefabUtility.RecordPrefabInstancePropertyModifications(stage);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        Debug.Log($"Normal maps unlinked from {cleared} layer(s). Files kept in {Normals}");
    }

    /// <summary>실제 그림 영역과 여백 비율을 보고해 손님 사이의 크기 편차를 확인합니다.</summary>
    [MenuItem("Dystopia/손님/Report Framing")]
    public static void ReportFraming()
    {
        var lines=new List<string> { "name\tsource\tbbox\toffset\tfill\taspect\tbodyHeightRatio" };
        foreach(string path in CustomerPaths())
        {
            var texture=LoadSource(path);
            try
            {
                var bounds=OpaqueBounds(texture,out int width,out int height);
                lines.Add($"{Path.GetFileNameWithoutExtension(path)}\t{width}x{height}\t{bounds.width}x{bounds.height}\t({bounds.x},{bounds.y})\t{(float)bounds.width*bounds.height/(width*height):P0}\t{(float)bounds.width/bounds.height:N2}\t{(float)bounds.height/height:P0}");
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
        Directory.CreateDirectory("output/customer-swap");
        string report="output/customer-swap/framing.tsv";
        File.WriteAllLines(report,lines);
        Debug.Log("Customer framing report: "+report+"\n"+string.Join("\n",lines.Take(8)));
    }

    /// <summary>남녀 폴더의 번호 순 손님 경로를 모두 반환합니다.</summary>
    /// <returns>존재하는 PNG 경로입니다.</returns>
    private static IEnumerable<string> CustomerPaths()
    {
        for(int i=0;i<DystopiaSession.MaleAppearanceCount;i++)
        {
            string path=Male+DystopiaSession.AppearanceFileName(true,i)+".png";
            if(File.Exists(path)) yield return path;
        }
        for(int i=0;i<DystopiaSession.FemaleAppearanceCount;i++)
        {
            string path=Female+DystopiaSession.AppearanceFileName(false,i)+".png";
            if(File.Exists(path)) yield return path;
        }
    }

    /// <summary>번호 순 Sprite를 모두 읽고 누락을 즉시 알립니다.</summary>
    /// <param name="folder">남성 또는 여성 폴더입니다.</param>
    /// <param name="isMale">남성 외형 묶음이면 true입니다.</param>
    /// <param name="count">기대하는 외형 수입니다.</param>
    /// <returns>0번부터 순서대로 채운 Sprite 배열입니다.</returns>
    /// <exception cref="InvalidOperationException">번호가 비어 있으면 발생합니다.</exception>
    private static Sprite[] LoadRange(string folder,bool isMale,int count)
    {
        var sprites=new Sprite[count];
        for(int i=0;i<count;i++)
        {
            string path=folder+DystopiaSession.AppearanceFileName(isMale,i)+".png";
            sprites[i]=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(sprites[i]==null) throw new InvalidOperationException("Missing customer sprite: "+path+". Run Import Customer Artwork first.");
        }
        return sprites;
    }

    /// <summary>직렬화된 Sprite 배열을 새 목록으로 교체합니다.</summary>
    /// <param name="property">대상 배열 프로퍼티입니다.</param>
    /// <param name="sprites">순서대로 넣을 Sprite입니다.</param>
    private static void FillSpriteArray(SerializedProperty property,Sprite[] sprites)
    {
        property.arraySize=sprites.Length;
        for(int i=0;i<sprites.Length;i++) property.GetArrayElementAtIndex(i).objectReferenceValue=sprites[i];
    }

    /// <summary>외형 번호 구간에 맞는 호흡 스타일을 설정합니다. 삭제된 손님 기준의 옛 값은 유지하지 않습니다.</summary>
    /// <param name="property">BreathStyle 배열 프로퍼티이며 없으면 무시합니다.</param>
    /// <param name="count">외형 수입니다.</param>
    private static void FillBreathing(SerializedProperty property,int count)
    {
        if(property==null) return;
        property.arraySize=count;
        for(int i=0;i<count;i++)
        {
            // BreathStyle은 0=Normal, 1=Heavy, 2=Elderly입니다. 절박·가난한 손님은 숨이 가쁩니다.
            var customerClass=DystopiaSession.AppearanceClass(i);
            int style=customerClass==DystopiaCustomerClass.Elder ? 2
                : customerClass==DystopiaCustomerClass.Hasty || customerClass==DystopiaCustomerClass.Poor ? 1 : 0;
            property.GetArrayElementAtIndex(i).enumValueIndex=style;
        }
    }

    /// <summary>임포트 설정과 무관하게 원본 PNG 픽셀을 읽습니다.</summary>
    /// <param name="path">프로젝트 내 PNG 경로입니다.</param>
    /// <returns>호출자가 파기해야 하는 임시 텍스처입니다.</returns>
    private static Texture2D LoadSource(string path)
    {
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        if(!texture.LoadImage(File.ReadAllBytes(path))) { UnityEngine.Object.DestroyImmediate(texture); throw new IOException("Could not read "+path); }
        return texture;
    }

    /// <summary>알파가 있는 영역의 외접 사각형을 계산합니다.</summary>
    /// <param name="texture">원본 픽셀입니다.</param>
    /// <param name="width">원본 너비입니다.</param>
    /// <param name="height">원본 높이입니다.</param>
    /// <returns>불투명 영역의 사각형입니다.</returns>
    private static RectInt OpaqueBounds(Texture2D texture,out int width,out int height)
    {
        width=texture.width; height=texture.height;
        var pixels=texture.GetPixels32();
        int minX=width,minY=height,maxX=-1,maxY=-1;
        for(int y=0;y<height;y++) for(int x=0;x<width;x++)
            if(pixels[y*width+x].a>8) { if(x<minX) minX=x; if(x>maxX) maxX=x; if(y<minY) minY=y; if(y>maxY) maxY=y; }
        if(maxX<minX) return new RectInt(0,0,width,height);
        return new RectInt(minX,minY,maxX-minX+1,maxY-minY+1);
    }

    /// <summary>기존 손님과 같은 Sprite 설정으로 임포트하고 표시 해상도에 맞춰 축소합니다.</summary>
    /// <param name="path">프로젝트 내 PNG 경로입니다.</param>
    private static void ImportCustomerSprite(string path)
    {
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=100; importer.filterMode=FilterMode.Bilinear; importer.mipmapEnabled=false;
        importer.wrapMode=TextureWrapMode.Clamp; importer.npotScale=TextureImporterNPOTScale.None;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency=true; importer.sRGBTexture=true;
        // 원본은 표시 크기의 4~6배라 Point 축소에서 계단이 생깁니다. 임포트 축소로 원본을 보존한 채 해상도를 맞춥니다.
        importer.maxTextureSize=DisplayMaxSize;
        importer.SaveAndReimport();
    }

    /// <summary>노멀맵을 선형 색 공간의 일반 텍스처로 임포트합니다.</summary>
    /// <param name="path">생성된 노멀맵 경로입니다.</param>
    private static void ImportNormalMap(string path)
    {
        if(!File.Exists(path)) return;
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        // 플랫폼별 노멀맵 압축 디코딩을 쓰지 않고 셰이더에서 직접 읽습니다.
        importer.textureType=TextureImporterType.Default; importer.sRGBTexture=false;
        importer.filterMode=FilterMode.Bilinear; importer.mipmapEnabled=false;
        importer.wrapMode=TextureWrapMode.Clamp; importer.npotScale=TextureImporterNPOTScale.None;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency=false; importer.maxTextureSize=NormalSize;
        importer.SaveAndReimport();
    }

    /// <summary>알파 실루엣의 두께와 원화 명암을 높이로 삼아 노멀맵 PNG를 씁니다.</summary>
    /// <param name="sourcePath">손님 원본 PNG 경로입니다.</param>
    /// <param name="targetPath">생성할 노멀맵 PNG 경로입니다.</param>
    private static void WriteNormalMap(string sourcePath,string targetPath)
    {
        var source=LoadSource(sourcePath);
        Texture2D output=null;
        try
        {
            // Sprite와 같은 비율을 유지한 채 계산 해상도를 맞춥니다.
            int width=source.width, height=source.height;
            float shrink=Mathf.Min(1f,(float)NormalSize/Mathf.Max(width,height));
            int w=Mathf.Max(8,Mathf.RoundToInt(width*shrink)), h=Mathf.Max(8,Mathf.RoundToInt(height*shrink));
            var pixels=source.GetPixels32();
            var alpha=new float[w*h];
            var luminance=new float[w*h];
            // 상자 평균으로 축소해 Point 축소의 계단 없이 부드러운 높이를 얻습니다.
            for(int y=0;y<h;y++)
            {
                int y0=y*height/h, y1=Mathf.Max(y0+1,(y+1)*height/h);
                for(int x=0;x<w;x++)
                {
                    int x0=x*width/w, x1=Mathf.Max(x0+1,(x+1)*width/w);
                    float a=0,l=0; int n=0;
                    for(int sy=y0;sy<y1;sy++) for(int sx=x0;sx<x1;sx++)
                    {
                        var p=pixels[sy*width+sx];
                        float pa=p.a/255f;
                        a+=pa;
                        l+=pa*(p.r*.299f+p.g*.587f+p.b*.114f)/255f;
                        n++;
                    }
                    int index=y*w+x;
                    alpha[index]=a/n;
                    luminance[index]=a>0 ? l/a : 0;
                }
            }
            // 실루엣을 크게 번지게 하면 가장자리에서 0으로 떨어지는 몸통 부피가 됩니다.
            var dome=Blur(alpha,w,h,Mathf.Max(3,Mathf.RoundToInt(Mathf.Max(w,h)*.06f)),3);
            // 원화 명암은 옷 주름 같은 세부만 남기고 큰 색 덩어리는 지웁니다.
            var detail=Blur(luminance,w,h,Mathf.Max(1,Mathf.RoundToInt(Mathf.Max(w,h)*.012f)),2);
            var height01=new float[w*h];
            for(int i=0;i<w*h;i++)
            {
                float body=Mathf.Sqrt(Mathf.Clamp01(dome[i]));
                height01[i]=alpha[i]>.02f ? body*.82f+detail[i]*.18f : 0;
            }
            output=new Texture2D(w,h,TextureFormat.RGBA32,false);
            var normals=new Color32[w*h];
            for(int y=0;y<h;y++) for(int x=0;x<w;x++)
            {
                int i=y*w+x;
                if(alpha[i]<=.02f) { normals[i]=new Color32(128,128,255,255); continue; }
                // Sobel로 높이의 기울기를 구합니다. X는 오른쪽, Y는 위쪽이 양수입니다.
                float dx=Sample(height01,w,h,x+1,y-1)+2*Sample(height01,w,h,x+1,y)+Sample(height01,w,h,x+1,y+1)
                        -Sample(height01,w,h,x-1,y-1)-2*Sample(height01,w,h,x-1,y)-Sample(height01,w,h,x-1,y+1);
                float dy=Sample(height01,w,h,x-1,y+1)+2*Sample(height01,w,h,x,y+1)+Sample(height01,w,h,x+1,y+1)
                        -Sample(height01,w,h,x-1,y-1)-2*Sample(height01,w,h,x,y-1)-Sample(height01,w,h,x+1,y-1);
                var n=new Vector3(-dx*NormalScale(w,h),-dy*NormalScale(w,h),1).normalized;
                normals[i]=new Color32((byte)Mathf.RoundToInt((n.x*.5f+.5f)*255),(byte)Mathf.RoundToInt((n.y*.5f+.5f)*255),(byte)Mathf.RoundToInt((n.z*.5f+.5f)*255),255);
            }
            output.SetPixels32(normals);
            File.WriteAllBytes(targetPath,output.EncodeToPNG());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
            if(output!=null) UnityEngine.Object.DestroyImmediate(output);
        }
    }

    /// <summary>해상도가 달라도 기울기의 세기가 같아지도록 Sobel 결과를 보정합니다.</summary>
    /// <param name="w">계산 너비입니다.</param>
    /// <param name="h">계산 높이입니다.</param>
    /// <returns>기울기에 곱할 배율입니다.</returns>
    private static float NormalScale(int w,int h) => Mathf.Max(w,h)*.055f;

    /// <summary>범위를 벗어난 좌표는 가장자리 값으로 읽습니다.</summary>
    /// <param name="field">높이 배열입니다.</param>
    /// <param name="w">너비입니다.</param>
    /// <param name="h">높이입니다.</param>
    /// <param name="x">읽을 X입니다.</param>
    /// <param name="y">읽을 Y입니다.</param>
    /// <returns>해당 위치의 높이입니다.</returns>
    private static float Sample(float[] field,int w,int h,int x,int y) => field[Mathf.Clamp(y,0,h-1)*w+Mathf.Clamp(x,0,w-1)];

    /// <summary>분리형 상자 흐림을 여러 번 적용해 가우시안에 가까운 결과를 만듭니다.</summary>
    /// <param name="field">입력 배열이며 변경하지 않습니다.</param>
    /// <param name="w">너비입니다.</param>
    /// <param name="h">높이입니다.</param>
    /// <param name="radius">한 번의 상자 반경입니다.</param>
    /// <param name="passes">반복 횟수입니다.</param>
    /// <returns>흐려진 새 배열입니다.</returns>
    private static float[] Blur(float[] field,int w,int h,int radius,int passes)
    {
        var current=(float[])field.Clone();
        var buffer=new float[w*h];
        for(int pass=0;pass<passes;pass++)
        {
            for(int y=0;y<h;y++)
            {
                float sum=0;
                for(int x=-radius;x<=radius;x++) sum+=current[y*w+Mathf.Clamp(x,0,w-1)];
                for(int x=0;x<w;x++)
                {
                    buffer[y*w+x]=sum/(radius*2+1);
                    sum+=current[y*w+Mathf.Clamp(x+radius+1,0,w-1)]-current[y*w+Mathf.Clamp(x-radius,0,w-1)];
                }
            }
            for(int x=0;x<w;x++)
            {
                float sum=0;
                for(int y=-radius;y<=radius;y++) sum+=buffer[Mathf.Clamp(y,0,h-1)*w+x];
                for(int y=0;y<h;y++)
                {
                    current[y*w+x]=sum/(radius*2+1);
                    sum+=buffer[Mathf.Clamp(y+radius+1,0,h-1)*w+x]-buffer[Mathf.Clamp(y-radius,0,h-1)*w+x];
                }
            }
        }
        return current;
    }
}
