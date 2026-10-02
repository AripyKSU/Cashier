using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>기존 IntroScene의 정산/라면 구간과 픽셀 UI만 연결하는 제한된 저작 도구.</summary>
public static class IntroMealSetup
{
    private const string UiPath = "Assets/Textures/UI/Intro/";
    private const string Sounds = "Assets/Sounds/Intro/NineCut/";
    private static readonly string[] Dialogue = { "하루야, 또 라면 먹게 해서 미안해.",
        "왜 맨날 미안하다고 해요. 나는 좋은데.", "맨날 먹는데도 안 질려?", "나는 세상에서 라면이 제일 맛있어요!" };

    /// <summary>테스트 전에 저장 상태와 개인 시작 씬 override를 읽기 전용으로 확인한다.</summary>
    [MenuItem("Cashier/Intro/Check Meal Test State")]
    public static void CheckTestState()
    {
        var scene=EditorSceneManager.GetActiveScene();
        Directory.CreateDirectory("Temp/IntroMealRevision");
        File.WriteAllText("Temp/IntroMealRevision/editor-state.txt",$"playing={EditorApplication.isPlaying}\ncompiling={EditorApplication.isCompiling}\ndirty={scene.isDirty}\nscene={scene.path}\nplayStart={AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene)}\n");
    }

    /// <summary>열린 씬의 해당 구간만 갱신한 뒤 저장한다.</summary>
    [MenuItem("Cashier/Intro/Apply Meal and Pixel UI")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/IntroScene.unity" || scene.isDirty)
            throw new InvalidOperationException("저장된 IntroScene 편집 모드가 필요합니다.");
        var player = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<IntroNineCutPlayer>(true)).Single();
        if (player.GetComponent<IntroMealSequence>() != null) throw new InvalidOperationException("이미 적용됨. Inspector 변경은 덮어쓰지 않습니다.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var frame = importSprite("DialogueFrame",new Rect(39,145,2094,451),new Vector4(95,70,95,70));
        var skipSprite = importSprite("SkipButton",new Rect(94,162,1985,430),new Vector4(95,70,95,70));
        var yesSprite = importSprite("YesButton",new Rect(92,148,1989,447),new Vector4(100,75,100,75));
        var noSprite = importSprite("NoButton",new Rect(122,139,1929,464),new Vector4(100,75,100,75));
        var so = new SerializedObject(player);
        var controller = player.GetComponent<IntroDialogueController>();
        var legacy = new SerializedObject(controller);
        var body = (TextMeshProUGUI)legacy.FindProperty("bodyText").objectReferenceValue;
        var box = (GameObject)legacy.FindProperty("dialogueBoxObject").objectReferenceValue;
        style(box.GetComponent<Image>(),frame);
        var boxData = new SerializedObject(box.GetComponent<AutoSizeNineSliceDialogueBox>());
        boxData.FindProperty("minWidth").floatValue=480;
        boxData.FindProperty("minHeight").floatValue=100;
        boxData.FindProperty("horizontalPadding").floatValue=38;
        boxData.FindProperty("verticalPadding").floatValue=24;
        boxData.ApplyModifiedPropertiesWithoutUndo();
        var skip=(Button)so.FindProperty("skipButton").objectReferenceValue;
        style(skip.GetComponent<Image>(),skipSprite);
        var overlay=new GameObject("SkipConfirmation",typeof(RectTransform),typeof(Image));
        overlay.transform.SetParent(player.transform,false);
        rect((RectTransform)overlay.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        overlay.GetComponent<Image>().color=new Color(0,0,0,.7f);
        var panel=new GameObject("Panel",typeof(RectTransform),typeof(Image)); panel.transform.SetParent(overlay.transform,false);
        rect((RectTransform)panel.transform,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(850,320));
        style(panel.GetComponent<Image>(),frame);
        var question=text("Question",panel.transform,body.font,36,"정말 건너 뛰시겠습니까?");
        rect(question.rectTransform,Vector2.one*.5f,Vector2.one*.5f,new Vector2(0,60),new Vector2(750,90));
        var yes=button("Yes",panel.transform,body.font,"예",yesSprite,new Vector2(-155,-65));
        var no=button("No",panel.transform,body.font,"아니요",noSprite,new Vector2(155,-65));
        overlay.SetActive(false);
        set(so,"skipConfirmation",overlay); set(so,"confirmYes",yes); set(so,"confirmNo",no);

        var meal=player.gameObject.AddComponent<IntroMealSequence>();
        var mealSo=new SerializedObject(meal);
        var group=new GameObject("IntroSettlement",typeof(RectTransform),typeof(CanvasGroup));
        group.transform.SetParent(player.transform,false);
        group.transform.SetSiblingIndex(overlay.transform.GetSiblingIndex());
        rect((RectTransform)group.transform,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(780,520));
        set(mealSo,"settlement",group.GetComponent<CanvasGroup>());
        string[] labels={"오늘 판매수익","물품대금","무료나눔","차량 유지비","","오늘 순이익","오늘도 저녁 라면 확정!"};
        string[] amounts={"+32,000원","-14,000원","-7,000원","-9,000원","","+2,000원",""};
        float[] y={190,125,60,-5,-55,-110,-210};
        var rows=mealSo.FindProperty("rows"); rows.arraySize=7;
        var clips=mealSo.FindProperty("rowClips"); clips.arraySize=7;
        for(int i=0;i<7;i++)
        {
            var row=new GameObject("Row"+i,typeof(RectTransform),typeof(CanvasGroup)); row.transform.SetParent(group.transform,false);
            rect((RectTransform)row.transform,Vector2.one*.5f,Vector2.one*.5f,new Vector2(0,y[i]),new Vector2(780,58));
            rows.GetArrayElementAtIndex(i).objectReferenceValue=row.GetComponent<CanvasGroup>();
            row.GetComponent<CanvasGroup>().alpha=0;
            if(i==4)
            {
                var line=new GameObject("Divider",typeof(RectTransform),typeof(Image)); line.transform.SetParent(row.transform,false);
                rect((RectTransform)line.transform,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(780,2));
                line.GetComponent<Image>().color=new Color(.75f,.75f,.72f,.8f); line.GetComponent<Image>().raycastTarget=false;
            }
            else
            {
                var label=text("Label",row.transform,body.font,i==5?40:34,labels[i]);
                rect(label.rectTransform,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(235,0),new Vector2(470,58));
                label.alignment=TextAlignmentOptions.MidlineLeft;
                if(i==6)
                {
                    rect(label.rectTransform,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(780,58));
                    label.alignment=TextAlignmentOptions.Center; label.color=new Color(1,.93f,.79f);
                }
                else
                {
                    var amount=text("Amount",row.transform,body.font,i==5?40:34,amounts[i]);
                    rect(amount.rectTransform,new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-145,0),new Vector2(290,58));
                    amount.alignment=TextAlignmentOptions.MidlineRight;
                    amount.color=(i==0 || i==5)?new Color(.56f,.76f,.56f):new Color(.8f,.48f,.46f);
                }
            }
            string sound=i==0?Sounds+"SettlementCoinSoft.wav":i==5?Sounds+"SettlementPachinko.ogg":i==6?Sounds+"SettlementWinJingle.ogg":i<4?Sounds+"LedgerTick.wav":null;
            clips.GetArrayElementAtIndex(i).objectReferenceValue=sound==null?null:AssetDatabase.LoadAssetAtPath<AudioClip>(sound);
        }
        var fx=(AudioSource)so.FindProperty("effects").objectReferenceValue;
        var mixer=AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Settings/SoundMixer.mixer");
        var sfxGroup=mixer.FindMatchingGroups("SFX").Single();
        var bgmGroup=mixer.FindMatchingGroups("BGM").Single();
        ((AudioSource)so.FindProperty("music").objectReferenceValue).outputAudioMixerGroup=bgmGroup;
        ((AudioSource)so.FindProperty("ambience").objectReferenceValue).outputAudioMixerGroup=sfxGroup;
        fx.outputAudioMixerGroup=sfxGroup;
        set(mealSo,"effects",fx);
        set(mealSo,"simmer",loopSource("MealSimmer",player.transform,"BoilingWater.mp3",sfxGroup));
        set(mealSo,"nightWind",loopSource("MealNightWind",player.transform,"Wind.wav",sfxGroup));
        mealSo.ApplyModifiedPropertiesWithoutUndo();
        group.SetActive(false);
        set(so,"mealSequence",meal);
        var dialogue=so.FindProperty("confirmedMealDialogue"); dialogue.arraySize=4;
        for(int i=0;i<4;i++)
        {
            var d=dialogue.GetArrayElementAtIndex(i); d.FindPropertyRelative("speaker").stringValue=i%2==0?"아빠":"하루";
            d.FindPropertyRelative("body").stringValue=Dialogue[i];
        }
        // 데이터 배열 전체를 교체하지 않고 3→4 구간의 타이밍만 수정한다.
        var beats=so.FindProperty("beats");
        int card=Enumerable.Range(0,beats.arraySize).Single(i=>beats.GetArrayElementAtIndex(i).FindPropertyRelative("kind").enumValueIndex==(int)IntroBeatKind.Card && beats.GetArrayElementAtIndex(i).FindPropertyRelative("index").intValue==4);
        beats.GetArrayElementAtIndex(card-2).FindPropertyRelative("seconds").floatValue=0;
        var fade=beats.GetArrayElementAtIndex(card-1); fade.FindPropertyRelative("seconds").floatValue=.6f;
        fade.FindPropertyRelative("fadeMusic").boolValue=true; fade.FindPropertyRelative("musicTarget").floatValue=.15f;
        // 기존 끓는 one-shot과 선행 대기는 정산의 연속 루프가 대체한다.
        var oldPot=beats.GetArrayElementAtIndex(card+3); oldPot.FindPropertyRelative("kind").enumValueIndex=(int)IntroBeatKind.Wait; oldPot.FindPropertyRelative("seconds").floatValue=0;
        beats.GetArrayElementAtIndex(card+4).FindPropertyRelative("seconds").floatValue=0;
        beats.GetArrayElementAtIndex(card+5).FindPropertyRelative("seconds").floatValue=.8f;
        beats.GetArrayElementAtIndex(card+5).FindPropertyRelative("fadeMusic").boolValue=true;
        beats.GetArrayElementAtIndex(card+5).FindPropertyRelative("musicTarget").floatValue=.55f;
        // 기존 MealDialogue 직전에 한 개의 대기 beat를 삽입한다.
        beats.InsertArrayElementAtIndex(card+6);
        var wait=beats.GetArrayElementAtIndex(card+6); wait.FindPropertyRelative("kind").enumValueIndex=(int)IntroBeatKind.Wait;
        wait.FindPropertyRelative("label").stringValue="Meal image hold before first line"; wait.FindPropertyRelative("seconds").floatValue=.4f;
        beats.GetArrayElementAtIndex(card+8).FindPropertyRelative("seconds").floatValue=0;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("[IntroMeal] Applied: seven timed rows, four exact lines, continuous simmer, pixel frame and confirmed skip. Noodle/dish clips missing.");
    }

    /// <summary>생성 PNG의 투명 여백을 Sprite rect에서만 제외한다.</summary>
    private static Sprite importSprite(string name,Rect bounds,Vector4 border)
    {
        string path=UiPath+name+".png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(path); importer.textureType=TextureImporterType.Sprite;
        importer.spriteImportMode=SpriteImportMode.Multiple; importer.filterMode=FilterMode.Point;
        importer.textureCompression=TextureImporterCompression.Uncompressed; importer.mipmapEnabled=false;
        importer.maxTextureSize=4096; importer.npotScale=TextureImporterNPOTScale.None;
#pragma warning disable 618
        importer.spritesheet=new[]{new SpriteMetaData { name=name,rect=bounds,border=border,pivot=Vector2.one*.5f,alignment=0 }};
#pragma warning restore 618
        importer.SaveAndReimport(); return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single();
    }

    /// <summary>기존 믹서에 연결된 로컬 루프 음원을 만든다.</summary>
    private static AudioSource loopSource(string name,Transform parent,string clip,AudioMixerGroup group)
    {
        var go=new GameObject(name,typeof(AudioSource)); go.transform.SetParent(parent,false);
        var a=go.GetComponent<AudioSource>(); a.playOnAwake=false; a.loop=true; a.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Sounds+clip);
        a.outputAudioMixerGroup=group; return a;
    }

    /// <summary>생성된 픽셀 테두리를 아홉 조각으로 사용한다.</summary>
    private static void style(Image image,Sprite sprite) { image.sprite=sprite; image.type=Image.Type.Sliced; image.color=Color.white; image.pixelsPerUnitMultiplier=3f; }
    /// <summary>텍스트가 별도인 버튼을 생성한다.</summary>
    private static Button button(string name,Transform parent,TMP_FontAsset font,string label,Sprite sprite,Vector2 pos)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button)); go.transform.SetParent(parent,false);
        rect((RectTransform)go.transform,Vector2.one*.5f,Vector2.one*.5f,pos,new Vector2(240,76)); style(go.GetComponent<Image>(),sprite);
        var b=go.GetComponent<Button>(); b.targetGraphic=go.GetComponent<Image>();
        var t=text("Label",go.transform,font,32,label); rect(t.rectTransform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero); return b;
    }
    /// <summary>기존 한글 폰트로 UI 텍스트를 만든다.</summary>
    private static TextMeshProUGUI text(string name,Transform parent,TMP_FontAsset font,float size,string value)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(parent,false);
        var t=go.GetComponent<TextMeshProUGUI>(); t.font=font; t.fontSize=size; t.text=value; t.alignment=TextAlignmentOptions.Center; t.raycastTarget=false; return t;
    }
    /// <summary>새 UI에만 저작 좌표를 기록한다.</summary>
    private static void rect(RectTransform r,Vector2 min,Vector2 max,Vector2 pos,Vector2 size) {r.anchorMin=min;r.anchorMax=max;r.pivot=Vector2.one*.5f;r.anchoredPosition=pos;r.sizeDelta=size;}
    /// <summary>정확한 직렬화 참조 한 개를 지정한다.</summary>
    private static void set(SerializedObject so,string field,UnityEngine.Object value)=>so.FindProperty(field).objectReferenceValue=value;
}
