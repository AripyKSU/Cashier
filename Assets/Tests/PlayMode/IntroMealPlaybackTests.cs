#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>저장된 인트로의 실제 컴포넌트와 입력 장치로 정산·라면·스킵 수명을 검사한다.</summary>
public sealed class IntroMealPlaybackTests
{
    private IntroDialogueController controller;
    private IntroNineCutPlayer player;
    private IntroMealSequence meal;
    private IntroSequenceBeat[] authored;
    private Keyboard keyboard;
    private AudioClip testEating;
    private bool background;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    /// <summary>테스트 로드 인스턴스에서만 영업 전환과 자동 시작을 분리한다. 씬 파일은 저장하지 않는다.</summary>
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        background=Application.runInBackground; Application.runInBackground=true;
        SceneManager.sceneLoaded+=loaded;
        EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/IntroScene.unity",new LoadSceneParameters(LoadSceneMode.Single));
        SceneManager.sceneLoaded-=loaded;
        yield return null;
        player=controller.GetComponent<IntroNineCutPlayer>(); meal=controller.GetComponent<IntroMealSequence>();
        Assert.That(meal,Is.Not.Null);
        authored=get<IntroSequenceBeat[]>(player,"beats");
        keyboard=InputSystem.AddDevice<Keyboard>();
        Directory.CreateDirectory("Temp/IntroMealRevision");
    }

    /// <summary>씬의 Awake 이후 Start 전 테스트 인스턴스의 외부 진입만 차단한다.</summary>
    private void loaded(Scene scene,LoadSceneMode mode)
    {
        controller=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<IntroDialogueController>(true)).Single();
        set(controller,"playOnStart",false);
        var entry=controller.GetComponent<IntroSceneEntry>(); entry.enabled=false; Object.Destroy(entry);
    }

    /// <summary>테스트 소유 입력 장치와 음원, 런타임 변경을 정리한다.</summary>
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        var result=TestContext.CurrentContext.Result;
        File.WriteAllText("Temp/IntroMealRevision/"+TestContext.CurrentContext.Test.Name+".txt",result.Outcome+"\n"+result.Message+"\n"+result.StackTrace);
        SceneManager.sceneLoaded-=loaded;
        if(controller!=null) controller.gameObject.SetActive(false);
        if(keyboard!=null) InputSystem.RemoveDevice(keyboard);
        if(testEating!=null) Object.Destroy(testEating);
        Application.runInBackground=background;
        yield return null;
    }

    /// <summary>실제 재생 시간에서 7개 행, 5번의 오디오 시작과 끊기지 않는 프리랩을 검사한다.</summary>
    [UnityTest]
    public IEnumerator SettlementTimingsColumnsAndContinuousPrelap()
    {
        int card=Array.FindIndex(authored,b=>b.kind==IntroBeatKind.Card && b.index==4);
        start(authored.Skip(card-1).ToArray());
        var group=get<CanvasGroup>(meal,"settlement");
        yield return until(()=>group.gameObject.activeSelf,2);
        float zero=Time.realtimeSinceStartup;
        var rows=get<CanvasGroup[]>(meal,"rows");
        var fx=get<AudioSource>(player,"effects"); var simmer=get<AudioSource>(meal,"simmer");
        float[] seen=Enumerable.Repeat(-1f,7).ToArray();
        int sfxStarts=0,simmerStarts=0; bool fxWas=false,potWas=false; float potAt=-1; bool captured=false;
        while(!player.IsTyping && Time.realtimeSinceStartup-zero<8)
        {
            float elapsed=Time.realtimeSinceStartup-zero;
            if(elapsed<3f) InputSystem.QueueStateEvent(keyboard,Time.frameCount%2==0?new KeyboardState(Key.Space):new KeyboardState());
            for(int i=0;i<7;i++) if(seen[i]<0 && rows[i].alpha>.99f) seen[i]=elapsed;
            if(fx.isPlaying && !fxWas) sfxStarts++;
            if(simmer.isPlaying && !potWas){simmerStarts++;potAt=elapsed;}
            fxWas=fx.isPlaying; potWas=simmer.isPlaying;
            if(!captured && elapsed>3.6f)
            { ScreenCapture.CaptureScreenshot("Temp/IntroMealRevision/settlement.png"); captured=true; }
            yield return null;
        }
        float[] expected={.3f,.8f,1.3f,1.8f,2.3f,2.6f,3.3f};
        for(int i=0;i<7;i++) Assert.That(seen[i],Is.EqualTo(expected[i]).Within(.16f),"row "+i);
        Assert.That(sfxStarts,Is.EqualTo(5)); Assert.That(simmerStarts,Is.EqualTo(1));
        Assert.That(potAt,Is.EqualTo(4.1f).Within(.16f)); Assert.That(simmer.isPlaying,Is.True);
        Assert.That(Time.realtimeSinceStartup-zero,Is.EqualTo(6.3f).Within(.25f));
        string[] amounts={"+32,000원","-14,000원","-7,000원","-9,000원","+2,000원"};
        int[] indexes={0,1,2,3,5};
        for(int n=0;n<indexes.Length;n++)
        {
            var t=rows[indexes[n]].transform.Find("Amount").GetComponent<TextMeshProUGUI>();
            Assert.That(t.text,Is.EqualTo(amounts[n])); Assert.That(t.alignment,Is.EqualTo(TextAlignmentOptions.MidlineRight));
            Assert.That(t.color.g>t.color.r,Is.EqualTo(n==0 || n==4));
        }
        Assert.That(32000-14000-7000-9000,Is.EqualTo(2000));
        Assert.That(rows[4].transform.Find("Divider").GetComponent<RectTransform>().rect.height,Is.EqualTo(2));
        Assert.That(group.GetComponent<Image>(),Is.Null);
        ScreenCapture.CaptureScreenshot("Temp/IntroMealRevision/meal.png");
        File.WriteAllText("Temp/IntroMealRevision/timing.txt",string.Join(",",seen)+$"\nSfxStarts={sfxStarts}\nPrelap={potAt}\nSimmerStarts={simmerStarts}\n");
        yield return new WaitForSecondsRealtime(.2f);
    }

    /// <summary>실제 Space 입력이 문장 완성과 진행을 분리하며 마지막 식사 이벤트와 암전 효과음을 잇는다.</summary>
    [UnityTest]
    public IEnumerator FourExactLinesInputEatingOnceAndCollapseOnBlack()
    {
        int index=Array.FindIndex(authored,b=>b.kind==IntroBeatKind.MealDialogue);
        start(new[]{new IntroSequenceBeat{kind=IntroBeatKind.Image,index=3},new IntroSequenceBeat{kind=IntroBeatKind.Fade,value=1}}.Concat(authored.Skip(index)).ToArray());
        // 실제 면 먹는 클립은 미확보. 이벤트 검증에만 테스트 소유 짧은 음원을 주입한다.
        testEating=AudioClip.Create("TestEatingEvent",2400,1,24000,false);
        set(meal,"noodleSlurp",testEating);
        var body=get<TextMeshProUGUI>(controller,"bodyText"); var speaker=get<TextMeshProUGUI>(controller,"speakerText");
        string[] speakers={"아빠","하루","아빠","하루"};
        string[] lines={"하루야, 또 라면 먹게 해서 미안해.","왜 맨날 미안하다고 해요. 나는 좋은데.","맨날 먹는데도 안 질려?","나는 세상에서 라면이 제일 맛있어요!"};
        for(int i=0;i<4;i++)
        {
            string displayed=$"{speakers[i]} : {lines[i]}";
            yield return until(()=>body.text==displayed && player.IsTyping,2);
            Assert.That(speaker.gameObject.activeSelf,Is.False);
            yield return pressSpace();
            Assert.That(player.IsTyping,Is.False); Assert.That(body.text,Is.EqualTo(displayed));
            if(i==3) Assert.That(get<AudioSource>(player,"effects").isPlaying,Is.True,"last typed callback");
            yield return pressSpace();
        }
        var fx=get<AudioSource>(player,"effects"); var alpha=get<CanvasGroup>(player,"pictureGroup");
        yield return new WaitForSecondsRealtime(.15f);
        yield return until(()=>fx.isPlaying,1);
        Assert.That(alpha.alpha,Is.EqualTo(0).Within(.001f),"chopsticks after blackout");
        yield return until(()=>body.text=="아빠 : 하루야…?",2);
        Assert.That(alpha.alpha,Is.EqualTo(0));
    }

    /// <summary>자동 진행 중 확인창은 타이밍을 멈추고, 예는 한 번 완료하며 재시작/비활성화에 예약음이 남지 않는다.</summary>
    [UnityTest]
    public IEnumerator ConfirmSkipPauseResumeRestartAndDisableCleanup()
    {
        int card=Array.FindIndex(authored,b=>b.kind==IntroBeatKind.Card && b.index==4);
        int completed=0; controller.IntroCompleted+=()=>completed++;
        start(authored.Skip(card).ToArray());
        var rows=get<CanvasGroup[]>(meal,"rows");
        yield return new WaitForSecondsRealtime(.15f);
        get<Button>(player,"skipButton").onClick.Invoke();
        Assert.That(get<GameObject>(player,"skipConfirmation").activeSelf,Is.True);
        ScreenCapture.CaptureScreenshot("Temp/IntroMealRevision/skip-confirmation.png");
        yield return new WaitForSecondsRealtime(.6f);
        Assert.That(rows[0].alpha,Is.EqualTo(0)); Assert.That(completed,Is.Zero);
        get<Button>(player,"confirmNo").onClick.Invoke();
        yield return until(()=>rows[0].alpha==1,1);
        get<Button>(player,"skipButton").onClick.Invoke();
        get<Button>(player,"confirmYes").onClick.Invoke(); get<Button>(player,"confirmYes").onClick.Invoke();
        Assert.That(completed,Is.EqualTo(1)); Assert.That(controller.IsPlaying,Is.False);
        yield return new WaitForSecondsRealtime(.4f);
        Assert.That(controller.GetComponentsInChildren<AudioSource>(true).All(a=>!a.isPlaying),Is.True);
        controller.gameObject.SetActive(true); controller.Play();
        yield return new WaitForSecondsRealtime(.15f); Assert.That(rows[0].alpha,Is.Zero);
        controller.gameObject.SetActive(false);
        yield return new WaitForSecondsRealtime(.7f);
        Assert.That(controller.IsPlaying,Is.False); Assert.That(completed,Is.EqualTo(1));
        Assert.That(controller.GetComponentsInChildren<AudioSource>(true).All(a=>!a.isPlaying),Is.True);
    }

    /// <summary>검사할 구간만 런타임 인스턴스에 지정한다.</summary>
    private void start(IntroSequenceBeat[] beats){set(player,"beats",beats);controller.Play();}
    /// <summary>새 입력 장치에서 눌림/해제 프레임을 각각 보낸다.</summary>
    private IEnumerator pressSpace()
    {InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;}
    /// <summary>유한 시간 안에 상태 전이를 기다린다.</summary>
    private static IEnumerator until(Func<bool> condition,float timeout)
    {float end=Time.realtimeSinceStartup+timeout;while(!condition() && Time.realtimeSinceStartup<end)yield return null;Assert.That(condition(),Is.True,"state timed out");}
    /// <summary>테스트에서만 직렬화된 상태를 읽는다.</summary>
    private static T get<T>(object target,string name)=>(T)target.GetType().GetField(name,Flags).GetValue(target);
    /// <summary>테스트 인스턴스에만 필드 값을 지정한다.</summary>
    private static void set(object target,string name,object value)=>target.GetType().GetField(name,Flags).SetValue(target,value);
}
#endif
