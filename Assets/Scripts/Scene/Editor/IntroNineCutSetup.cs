using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>열린 IntroScene에만 9컷 리소스를 연결한다. 기존 4컷과 공용 씬 전환은 보존한다.</summary>
public static class IntroNineCutSetup
{
    private const string ScenePath = "Assets/Scenes/IntroScene.unity";
    private const string ArtPath = "Assets/Textures/art/Intro/NineCut";
    private const string AudioPath = "Assets/Sounds/Intro/NineCut";
    private static readonly string[] ClipNames = { "LonelyTheme", "OminousDrone", "Wind", "Burner",
        "SlowStoneSteps", "PaperRustle", "MetalContainer", "PotSimmer", "ChopsticksDrop", "BodyFall",
        "RunningStop", "BodyCollision", "DullImpact", "ApproachingShoes", "LabouredBreath", "LedgerTick" };

    /// <summary>Editor JSON 입력. 런타임에는 씬에 저장한 Inspector 데이터만 사용한다.</summary>
    [Serializable]
    private sealed class SequenceFile { public IntroSequenceBeat[] beats; }

    /// <summary>리소스를 가져와 현재 인트로에 참조를 연결하고 검사 후 저장한다.</summary>
    [MenuItem("Cashier/Intro/Apply Nine Cut Sequence")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath || scene.isDirty)
            throw new InvalidOperationException("저장된 IntroScene을 편집 모드에서 열어야 합니다. 미저장 씬을 덮어쓰지 않습니다.");
        var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<IntroDialogueController>(true)).Single();
        if (controller.GetComponent<IntroNineCutPlayer>() != null)
            throw new InvalidOperationException("이미 연결된 9컷을 자동으로 덮어쓰지 않습니다. Inspector에서 편집하세요.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var sprites = new Sprite[9];
        for (int i = 0; i < sprites.Length; i++)
        {
            string path = $"{ArtPath}/Scene{i + 1:00}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprites[i] == null) throw new InvalidOperationException(path);
        }
        var clips = ClipNames.Select(name => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioPath}/{name}.wav")).ToArray();
        if (clips.Any(clip => clip == null)) throw new InvalidOperationException("오디오 import 누락");
        var file = JsonUtility.FromJson<SequenceFile>(File.ReadAllText("Assets/Datas/Intro/IntroNineCutSequence.json"));
        var existing = new SerializedObject(controller);
        var font = ((TextMeshProUGUI)existing.FindProperty("bodyText").objectReferenceValue).font;
        var parent = controller.transform;
        var imageObject = new GameObject("NineCutArtwork", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        imageObject.transform.SetParent(parent, false);
        imageObject.transform.SetSiblingIndex(1);
        var picture = imageObject.GetComponent<Image>();
        rect(picture.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        picture.sprite = sprites[0];
        picture.preserveAspect = true;
        picture.raycastTarget = false;
        controller.GetComponent<Canvas>().pixelPerfect = true;
        var card = text("NineCutCard", parent, font, 48);
        rect(card.rectTransform, new Vector2(.5f,.5f), new Vector2(.5f,.5f), Vector2.zero, new Vector2(1200,500));
        card.gameObject.SetActive(false);
        var skipObject = new GameObject("NineCutSkip", typeof(RectTransform), typeof(Image), typeof(Button));
        skipObject.transform.SetParent(parent, false);
        rect((RectTransform)skipObject.transform, Vector2.one, Vector2.one, new Vector2(-125,-48), new Vector2(190,56));
        skipObject.GetComponent<Image>().color = new Color(.04f,.045f,.06f,.8f);
        var skip = skipObject.GetComponent<Button>();
        skip.targetGraphic = skipObject.GetComponent<Image>();
        var skipLabel = text("Label", skip.transform, font, 25);
        rect(skipLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        skipLabel.text = "건너뛰기";
        var player = controller.gameObject.AddComponent<IntroNineCutPlayer>();
        var so = new SerializedObject(player);
        set(so,"picture",picture); set(so,"pictureGroup",imageObject.GetComponent<CanvasGroup>());
        set(so,"cardText",card); set(so,"skipButton",skip); set(so,"controller",controller);
        set(so,"music",source("IntroMusic",parent));
        set(so,"ambience",source("IntroAmbience",parent));
        set(so,"effects",source("IntroEffects",parent));
        array(so.FindProperty("artwork"),sprites);
        array(so.FindProperty("clips"),clips);
        var beats = so.FindProperty("beats");
        beats.arraySize = file.beats.Length;
        for (int i = 0; i < file.beats.Length; i++)
        {
            var p = beats.GetArrayElementAtIndex(i); var b = file.beats[i];
            p.FindPropertyRelative("label").stringValue = b.label;
            p.FindPropertyRelative("kind").enumValueIndex = (int)b.kind;
            p.FindPropertyRelative("index").intValue = b.index;
            p.FindPropertyRelative("speaker").stringValue = b.speaker;
            p.FindPropertyRelative("text").stringValue = b.text;
            p.FindPropertyRelative("seconds").floatValue = b.seconds;
            p.FindPropertyRelative("value").floatValue = b.value;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        set(existing,"nineCutSequence",player);
        existing.ApplyModifiedPropertiesWithoutUndo();
        // 기존 4컷 데이터/연결은 그대로 두고 새 프리뷰에 겹치지 않게 표시만 끈다.
        var cuts = existing.FindProperty("cuts");
        for (int i = 0; i < cuts.arraySize; i++)
            ((GameObject)cuts.GetArrayElementAtIndex(i).FindPropertyRelative("root").objectReferenceValue)?.SetActive(false);
        ((GameObject)existing.FindProperty("dialogueBoxObject").objectReferenceValue).SetActive(false);
        ((GameObject)existing.FindProperty("speakerObject").objectReferenceValue).SetActive(false);
        validate(player);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[IntroNineCut] SAVED: 9 images, 16 audio clips, 35 lines, 104 beats; original 4 cuts retained. Scene 04 dialogue/settlement unconfirmed.");
    }

    /// <summary>저장된 씬의 참조와 순서, 원본 컷 보존 및 오디오 수를 검사한다.</summary>
    [MenuItem("Cashier/Intro/Validate Nine Cut Sequence")]
    public static void Validate()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) throw new InvalidOperationException("IntroScene이 아닙니다.");
        var player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<IntroNineCutPlayer>(true)).Single();
        validate(player);
        Debug.Log("[IntroNineCut] STATIC VALIDATION OK; input/audio listening/Play Mode not tested.");
    }

    /// <summary>필수 리소스와 9컷 순서를 검증하고 결과를 Temp에 기록한다.</summary>
    private static void validate(IntroNineCutPlayer player)
    {
        var so = new SerializedObject(player);
        foreach (string field in new[]{"picture","pictureGroup","cardText","skipButton","controller","music","ambience","effects"})
            if (so.FindProperty(field).objectReferenceValue == null) throw new InvalidOperationException(field);
        foreach (string field in new[]{"artwork","clips"})
        {
            var a=so.FindProperty(field);
            if (a.arraySize != (field == "artwork" ? 9 : 16)) throw new InvalidOperationException(field + " count");
            for(int i=0;i<a.arraySize;i++)
                if(a.GetArrayElementAtIndex(i).objectReferenceValue==null) throw new InvalidOperationException(field+" missing "+i);
        }
        int image=0, lines=0, impacts=0;
        var beats=so.FindProperty("beats");
        for(int i=0;i<beats.arraySize;i++)
        {
            var b=beats.GetArrayElementAtIndex(i);
            int kind=b.FindPropertyRelative("kind").enumValueIndex;
            int index=b.FindPropertyRelative("index").intValue;
            if(kind==(int)IntroBeatKind.Image && index!=image++) throw new InvalidOperationException("image order");
            if(kind==(int)IntroBeatKind.Line) lines++;
            if(kind==(int)IntroBeatKind.Effect && index==12) impacts++;
        }
        if(image!=9 || lines!=35 || impacts!=2) throw new InvalidOperationException($"counts {image}/{lines}/{impacts}");
        var legacy=new SerializedObject(player.GetComponent<IntroDialogueController>());
        if(legacy.FindProperty("cuts").arraySize!=4) throw new InvalidOperationException("original cuts changed");
        foreach(var source in player.GetComponentsInChildren<AudioSource>(true))
            if(source.playOnAwake || source.spatialBlend!=0) throw new InvalidOperationException("audio source setup");
        Directory.CreateDirectory("Temp/IntroNineCut");
        File.WriteAllText("Temp/IntroNineCut/validation.txt",$"Images={image}\nLines={lines}\nImpacts={impacts}\nBeats={beats.arraySize}\nOriginalCuts=4\nScene04=PENDING source\nRuntime=PENDING user test\n");
    }

    /// <summary>로컬 2D 음원 하나를 생성한다.</summary>
    private static AudioSource source(string name, Transform parent)
    {
        var go=new GameObject(name,typeof(AudioSource)); go.transform.SetParent(parent,false);
        var source=go.GetComponent<AudioSource>(); source.playOnAwake=false; source.spatialBlend=0; return source;
    }

    /// <summary>기존 한글 폰트로 간단한 표시 텍스트를 만든다.</summary>
    private static TextMeshProUGUI text(string name,Transform parent,TMP_FontAsset font,float size)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(parent,false);
        var t=go.GetComponent<TextMeshProUGUI>(); t.font=font; t.fontSize=size;
        t.alignment=TextAlignmentOptions.Center; t.raycastTarget=false; t.text=""; return t;
    }

    /// <summary>새로 추가하는 UI의 저장 레이아웃을 지정한다.</summary>
    private static void rect(RectTransform r,Vector2 min,Vector2 max,Vector2 pos,Vector2 size)
    { r.anchorMin=min; r.anchorMax=max; r.pivot=new Vector2(.5f,.5f); r.anchoredPosition=pos; r.sizeDelta=size; }

    /// <summary>정확한 직렬화 필드에 참조를 기록한다.</summary>
    private static void set(SerializedObject so,string name,UnityEngine.Object value) => so.FindProperty(name).objectReferenceValue=value;

    /// <summary>배열에 리소스 참조를 기록한다.</summary>
    private static void array(SerializedProperty p,UnityEngine.Object[] values)
    { p.arraySize=values.Length; for(int i=0;i<values.Length;i++) p.GetArrayElementAtIndex(i).objectReferenceValue=values[i]; }
}
