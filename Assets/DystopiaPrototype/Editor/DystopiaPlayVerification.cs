using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>Play Mode에서 Input System 이벤트를 실제 키보드·마우스 경로에 전달하는 검사입니다.</summary>
public static class DystopiaPlayVerification
{
    private static IEnumerator routine;
    private static double nextStep;
    private static double deadline;
    private static string reportName;
    private static readonly StringBuilder Report = new StringBuilder();

    /// <summary>Game View에 입력을 보내고 화면과 판정 결과를 함께 검사합니다.</summary>
    [MenuItem("Dystopia/Verify Play Input")]
    public static void Begin()
    {
        if(!EditorApplication.isPlaying) throw new InvalidOperationException("Start the dedicated Play scene first.");
        if(routine!=null) throw new InvalidOperationException("Verification already running.");
        var type=typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        EditorWindow.GetWindow(type).Focus();
        Report.Clear();
        Report.AppendLine("Actual Play Mode; synthetic device events through Input System and uGUI raycasts. Not physical user input.");
        routine=Run(); nextStep=0; deadline=EditorApplication.timeSinceStartup+180;
        reportName="PlayInput";
        InputSystem.onBeforeUpdate+=Step;
        EditorApplication.update+=KeepUpdating;
    }

    /// <summary>Keeps the dedicated test player loop scheduled while the Editor tool runs.</summary>
    private static void KeepUpdating()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup>deadline)
        {
            Report.AppendLine("INCOMPLETE: verification interrupted or exceeded its deadline.");
            Finish();
        }
    }

    /// <summary>프레임 간 입력 down/up을 분리하여 실제 처리 시간을 제공합니다.</summary>
    private static void Step()
    {
        if(InputState.currentUpdateType != InputUpdateType.Dynamic) return;
        if(EditorApplication.timeSinceStartup<nextStep) return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline) throw new TimeoutException("Play input verification exceeded 180 seconds.");
            if(!EditorApplication.isPlaying || !routine.MoveNext()) { Finish(); return; }
            nextStep=EditorApplication.timeSinceStartup+(routine.Current is float seconds?seconds:.12f);
        }
        catch(Exception exception)
        {
            Report.AppendLine("FAIL: "+exception);
            Debug.LogException(exception);
            Finish();
        }
    }

    /// <summary>검사 구독과 입력 상태를 정리하고 증거를 저장합니다.</summary>
    private static void Finish()
    {
        InputSystem.onBeforeUpdate-=Step; routine=null;
        EditorApplication.update-=KeepUpdating;
        if(Keyboard.current!=null) InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
        if(Mouse.current!=null) InputSystem.QueueStateEvent(Mouse.current,new MouseState());
        File.WriteAllText("Assets/DystopiaPrototype/Evidence/"+reportName+".txt",Report.ToString());
        Debug.Log("[Dystopia] Play input verification finished. "+Report);
    }

    /// <summary>Uses the same real input path to reach tribute, citizenship and insolvency screens.</summary>
    [MenuItem("Dystopia/Verify Full Run Input")]
    public static void BeginFullRun()
    {
        Begin();
        routine=FullRun(); reportName="FullRunInput";
        deadline=EditorApplication.timeSinceStartup+600;
    }

    /// <summary>Traverses production states without changing cash, dates, budgets or timer values.</summary>
    private static IEnumerator FullRun()
    {
        var screen=UnityEngine.Object.FindFirstObjectByType<DystopiaScreen>();
        for(int run=0;run<2;run++)
        {
            screen.Restart(); yield return .3f;
            bool paid=false;
            while(screen.Session.Phase!=DystopiaPhase.Goal && screen.Session.Phase!=DystopiaPhase.Failed)
            {
                Require(screen.Session.Day<=21,"run remains within 21 days");
                string button=null;
                switch(screen.Session.Phase)
                {
                    case DystopiaPhase.PriceGuide:
                        if(run==0 && screen.Session.Day>1)
                        {
                            ScreenCapture.CaptureScreenshot("Assets/DystopiaPrototype/Evidence/Guide-Day"+screen.Session.Day+".png");
                            yield return .2f;
                        }
                        button="OpenShop"; break;
                    case DystopiaPhase.Trading:
                        int amount=run==0?Math.Min(screen.Session.Customer.total,screen.Session.Customer.budget):1;
                        foreach(char digit in amount.ToString())
                        { KeyDown(DigitKey(digit)); yield return .035f; KeyUp(); yield return .035f; }
                        button="Confirm"; break;
                    case DystopiaPhase.Tribute:
                        ScreenCapture.CaptureScreenshot("Assets/DystopiaPrototype/Evidence/Tribute-Run"+run+".png");
                        yield return .2f;
                        int before=screen.Session.Cash;
                        int due=screen.Session.TributeAmount;
                        Click("PayTribute",true); yield return .12f; Click("PayTribute",false); yield return .2f;
                        if(run==0)
                        {
                            Require(screen.Session.Phase==DystopiaPhase.Settlement && screen.Session.Cash==before-due,"weekly tribute deducted once via UI");
                            paid=true;
                        }
                        else Require(screen.Session.Phase==DystopiaPhase.Failed,"insufficient tribute reaches failure via UI");
                        break;
                    case DystopiaPhase.Settlement:
                        button=run==0 && paid && screen.Session.CanBuy?"BuyCitizenship":"NextDay";
                        break;
                }
                if(button!=null) { Click(button,true); yield return .12f; Click(button,false); }
                yield return screen.Session.Phase==DystopiaPhase.Result?1f:.2f;
            }
            Require(screen.Session.Phase==(run==0?DystopiaPhase.Goal:DystopiaPhase.Failed),run==0?"citizenship completion via UI":"failure run complete");
            ScreenCapture.CaptureScreenshot("Assets/DystopiaPrototype/Evidence/"+(run==0?"08-Citizenship":"09-Failure")+".png");
            yield return .3f;
            Click("Restart",true); yield return .12f; Click("Restart",false); yield return .2f;
            Require(screen.Session.Day==1 && screen.Session.Cash==0,"ending restart via UI");
        }
    }

    /// <summary>결제·거절·일시정지·시간 경과와 재시작을 실제 화면에서 검증합니다.</summary>
    private static IEnumerator Run()
    {
        var screen=UnityEngine.Object.FindFirstObjectByType<DystopiaScreen>();
        Require(screen!=null,"dedicated screen active");
        screen.Restart(); yield return .3f;
        ScreenCapture.CaptureScreenshot("Assets/DystopiaPrototype/Evidence/01-PriceGuide.png");
        yield return .3f;
        Click("OpenShop",true); yield return .15f; Click("OpenShop",false); yield return .3f;
        Require(screen.Session.Phase==DystopiaPhase.Trading,"mouse opens shop");
        KeyDown(Key.Enter); yield return .15f; KeyUp(); yield return .2f;
        Require(screen.Session.Cash==0 && screen.Session.Sold==0,"empty Enter does not sell");
        KeyDown(Key.Minus); yield return .1f; KeyUp(); yield return .1f;
        KeyDown(Key.Digit1); yield return .1f; KeyUp(); yield return .1f;
        KeyDown(Key.Enter); yield return .1f; KeyUp(); yield return .1f;
        Require(screen.Session.Cash==0 && GameObject.Find("PriceInput").GetComponent<Text>().text.Contains("정수만"),"negative input cannot become a positive sale");
        KeyDown(Key.Backspace); yield return .1f; KeyUp(); yield return .1f;
        Click("Digit1",true); yield return .12f; Click("Digit1",false); yield return .15f;
        Require(GameObject.Find("PriceInput").GetComponent<Text>().text=="1 원","mouse keypad digit");
        Click("Erase",true); yield return .12f; Click("Erase",false); yield return .15f;
        Require(GameObject.Find("PriceInput").GetComponent<Text>().text=="금액 입력","mouse erase");
        // 가격은 Editor 검사에서만 읽고 입력합니다. 실제 UI에는 정답을 표시하지 않습니다.
        int price=Math.Min(screen.Session.Customer.total,screen.Session.Customer.budget);
        foreach(char digit in price.ToString())
        {
            KeyDown(DigitKey(digit)); yield return .08f; KeyUp(); yield return .08f;
        }
        KeyDown(Key.Digit9); yield return .1f; KeyUp(); yield return .1f;
        KeyDown(Key.Backspace); yield return .1f; KeyUp(); yield return .1f;
        var input=GameObject.Find("PriceInput").GetComponent<Text>();
        Require(input.text==$"{price:N0} 원","keyboard amount and Backspace");
        ScreenCapture.CaptureScreenshot("Assets/DystopiaPrototype/Evidence/02-Trading.png");
        yield return .2f;
        KeyDown(Key.Enter); yield return .1f; KeyUp(); yield return .1f;
        Require(screen.Session.LastAccepted && screen.Session.Cash==price,"Enter routes to accepted transaction");
        KeyDown(Key.Enter); yield return .1f; KeyUp(); yield return .1f;
        Require(screen.Session.Cash==price && screen.Session.Sold==1,"rapid Enter cannot duplicate sale");
        ScreenCapture.CaptureScreenshot("Assets/DystopiaPrototype/Evidence/03-SaleResult.png");
        yield return 1.1f;
        for(int i=0;i<7;i++) { KeyDown(Key.Digit9); yield return .07f; KeyUp(); yield return .07f; }
        KeyDown(Key.Enter); yield return .12f; KeyUp(); yield return .12f;
        Require(!screen.Session.LastAccepted && screen.Session.Refused==1 && screen.Session.Cash==price,"overpriced transaction refused");
        ScreenCapture.CaptureScreenshot("Assets/DystopiaPrototype/Evidence/04-Refusal.png");
        yield return 1.1f;
        KeyDown(Key.Escape); yield return .15f; KeyUp(); yield return .2f;
        Require(screen.Session.IsPaused,"Escape pauses");
        float gauge=screen.Session.Gauge; yield return 1f;
        Require(screen.Session.Gauge==gauge,"real elapsed pause preserves gauge");
        Click("Resume",true); yield return .15f; Click("Resume",false); yield return .25f;
        Require(!screen.Session.IsPaused,"mouse resumes");
        // 실제 시간으로 기다립니다. 날짜·게이지를 변경하는 디버그 우회를 사용하지 않습니다.
        while(screen.Session.Gauge>18) yield return .25f;
        ScreenCapture.CaptureScreenshot("Assets/DystopiaPrototype/Evidence/05-WaitingPressure.png");
        int departures=screen.Session.Departed;
        while(screen.Session.Departed==departures) yield return .25f;
        Require(screen.Session.Departed>departures,"real time causes waiting customers to leave");
        ScreenCapture.CaptureScreenshot("Assets/DystopiaPrototype/Evidence/06-Departure.png");
        yield return .25f;
        while(screen.Session.Phase!=DystopiaPhase.Settlement)
        {
            if(screen.Session.Phase==DystopiaPhase.Trading)
            {
                foreach(char digit in "9999999")
                { KeyDown(DigitKey(digit)); yield return .06f; KeyUp(); yield return .06f; }
                Click("Confirm",true); yield return .12f; Click("Confirm",false);
            }
            yield return 1f;
        }
        Require(screen.Session.Sold+screen.Session.Refused+screen.Session.Departed==screen.Session.Visitors,"day consumes each visitor once");
        ScreenCapture.CaptureScreenshot("Assets/DystopiaPrototype/Evidence/07-Settlement.png");
        yield return .3f;
        int cash=screen.Session.Cash;
        Click("NextDay",true); yield return .15f; Click("NextDay",false); yield return .2f;
        Require(screen.Session.Day==2 && screen.Session.Cash==cash,"next-day mouse input preserves cash");
        KeyDown(Key.Escape); yield return .1f; KeyUp(); yield return .2f;
        Click("Restart",true); yield return .15f; Click("Restart",false); yield return .3f;
        Require(screen.Session.Day==1 && screen.Session.Cash==0 && screen.Session.Reputation==50,"restart resets run");
        Require(UnityEngine.Object.FindObjectsByType<DystopiaScreen>(FindObjectsSortMode.None).Length==1,"restart does not duplicate screen");
        Require(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length==1,"one EventSystem after restart");
    }

    /// <summary>현재 실제 버튼의 중앙으로 마우스 입력을 보냅니다.</summary>
    private static void Click(string name,bool down)
    {
        if(!down) { InputSystem.QueueStateEvent(Mouse.current,new MouseState {position=Mouse.current.position.ReadValue()}); return; }
        var button=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name==name && b.gameObject.activeInHierarchy);
        var rect=(RectTransform)button.transform;
        var position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        InputSystem.QueueStateEvent(Mouse.current,new MouseState {position=position}.WithButton(MouseButton.Left));
    }

    /// <summary>키보드 장치의 실제 프레임 입력 큐를 사용합니다.</summary>
    private static void KeyDown(Key key) { InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(key)); }
    /// <summary>키가 다음 프레임에도 눌린 상태로 남지 않게 합니다.</summary>
    private static void KeyUp() { InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState()); }
    /// <summary>숫자를 Input System 키 코드로 변환합니다.</summary>
    private static Key DigitKey(char digit) => digit=='0'?Key.Digit0:Key.Digit1+(digit-'1');
    /// <summary>실패를 로그와 보고서에 명시합니다.</summary>
    private static void Require(bool value,string label)
    {
        if(!value) throw new InvalidOperationException(label);
        Report.AppendLine("OK: "+label);
    }
}
