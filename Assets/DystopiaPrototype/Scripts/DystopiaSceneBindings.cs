using UnityEngine;
using UnityEngine.UI;

/// <summary>편집된 Scene 오브젝트를 정면 화면의 동작 참조에 연결합니다.</summary>
public sealed partial class DystopiaScreen
{
    /// <summary>대사를 박스 내부에 고정하여 크기 변경 후에도 중앙 정렬과 여백을 유지합니다.</summary>
    /// <param name="text">표시할 대사입니다.</param>
    /// <param name="panel">9-slice 배경 박스입니다.</param>
    internal static void FitDialogue(Text text, RectTransform panel)
    {
        var rect = text.rectTransform;
        rect.SetParent(panel, false);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = new Vector2(18, 10);
        rect.offsetMax = new Vector2(-18, -10);
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMaxSize = text.fontSize;
        text.resizeTextMinSize = Mathf.Min(12, text.fontSize);
    }
    /// <summary>정면과 탑다운이 같은 세션을 초기화 순서와 무관하게 공유합니다.</summary>
    public void InitializePlacedScreen() { if (Session == null) Awake(); }

    /// <summary>Scene에 저장된 정면 Canvas를 연결하며 위치·이미지·크기를 다시 만들지 않습니다.</summary>
    private bool BindPlacedScreen()
    {
        root = transform.Find("DystopiaCanvas") as RectTransform;
        if (root == null) return false;
        portrait = root.Find("Customer").GetComponent<Image>();
        waiting[0] = root.Find("WaitingLeft").GetComponent<Image>();
        waiting[1] = root.Find("WaitingRear").GetComponent<Image>();
        idlePeople[0]=waiting[0].rectTransform; idlePeople[1]=waiting[1].rectTransform; idlePeople[2]=portrait.rectTransform;
        for(int i=0;i<3;i++) { idleOrigins[i]=idlePeople[i].anchoredPosition; placedPeopleScales[i]=idlePeople[i].localScale; }
        for(int i=0;i<3;i++) { animatedCrowd[i]=root.Find("CrowdRow"+i).GetComponent<DystopiaCrowdImage>(); animatedCrowd[i].Configure(i); }
        string[] sides={"Left","Right"};
        for(int i=0;i<2;i++)
        {
            chimneySmoke[i]=root.Find(sides[i]+"ChimneySmoke").GetComponent<Image>();
            chimneySmokeOrigins[i]=chimneySmoke[i].rectTransform.anchoredPosition;
            watchGuards[i]=(RectTransform)root.Find(sides[i]+"WatchGuard");
            watchGuardOrigins[i]=watchGuards[i].anchoredPosition;
            watchMuzzleFlashes[i]=(RectTransform)root.Find("WatchMuzzleFlash"+i);
            watchMuzzleFlashes[i].gameObject.SetActive(false);
        }
        basketRoot=(RectTransform)root.Find("Basket");
        inputText=root.Find("PriceInput").GetComponent<Text>();
        dialogue=(root.Find("DialoguePanel/Dialogue") ?? root.Find("Dialogue")).GetComponent<Text>(); reasonText=root.Find("Feedback").GetComponent<Text>();
        FitDialogue(dialogue, (RectTransform)root.Find("DialoguePanel"));
        confirmButton=root.Find("Confirm").GetComponent<Button>(); confirmButtonText=confirmButton.GetComponentInChildren<Text>(true);
        dailyInstructionButton=root.Find("DailyInstructionButton").GetComponent<Button>();
        dailyInstructionButton.gameObject.SetActive(false);
        dailyInstructionButton.onClick.RemoveAllListeners(); dailyInstructionButton.onClick.AddListener(OpenDailyInstruction);
        confirmButton.onClick.RemoveAllListeners(); confirmButton.onClick.AddListener(Confirm);
        foreach(string name in new[]{"DailyInstruction","DailyLedger","Modal"})
        { var view=root.Find(name); if(view!=null) view.gameObject.SetActive(false); }
        root.gameObject.SetActive(true);
        return true;
    }
}

/// <summary>탑다운의 편집된 UI, 물품 Prefab과 판정 Collider를 연결합니다.</summary>
public sealed partial class DystopiaTopDownTest
{
    [Header("Scene 오브젝트 연결")]
    /// <summary>정면 화면과 공유할 거래 세션입니다.</summary>
    [SerializeField] private DystopiaScreen placedHostScreen;
    /// <summary>Scene에서 위치와 Size를 조절하는 판매 및 제외 판정 영역입니다.</summary>
    [SerializeField] private BoxCollider2D placedSaleZone, placedExcludedZone;
    /// <summary>물품 중심이 이동할 수 있는 범위입니다. 벽 Collider와 함께 편집합니다.</summary>
    [SerializeField] private BoxCollider2D placedMovementZone;
    /// <summary>상품 ID 순서의 물리 물품 Prefab입니다. 이미지와 크기는 각 Prefab에서 편집합니다.</summary>
    [SerializeField] private GameObject[] placedItemPrefabs;
    private bool hasPlacedUi;
    private Rect placedCalculatorLayout, placedClockLayout;
    private RectTransform keypadMotion;
    private Vector2 placedPourPosition;
    private float placedPourAngle;

    /// <summary>편집 가능한 Trigger Collider가 있으면 그 로컬 사각형으로 판정합니다.</summary>
    private bool ZoneContains(BoxCollider2D zone, Rect fallback, Vector2 point)
    {
        if(zone==null) return fallback.Contains(point);
        Vector2 local=zone.transform.InverseTransformPoint(itemRoot.TransformPoint(point));
        return new Rect(zone.offset-zone.size*.5f,zone.size).Contains(local);
    }

    /// <summary>편집된 이동 영역을 기준으로 물품 중심만 제한합니다.</summary>
    /// <param name="item">경계를 벗어나지 않게 할 물품입니다.</param>
    private void ClampPlacedItem(DystopiaTopDownItem item)
    {
        Vector3 local=placedMovementZone.transform.InverseTransformPoint(item.transform.position);
        Vector2 min=placedMovementZone.offset-placedMovementZone.size*.5f;
        Vector2 max=placedMovementZone.offset+placedMovementZone.size*.5f;
        local.x=Mathf.Clamp(local.x,min.x,max.x); local.y=Mathf.Clamp(local.y,min.y,max.y);
        item.transform.position=placedMovementZone.transform.TransformPoint(local);
    }

    /// <summary>미리 배치된 작업대와 물품 부모를 연결합니다.</summary>
    private bool BindPlacedWorld()
    {
        var cameraTransform=transform.Find("TopDownCamera");
        if(cameraTransform==null) return false;
        worldCamera=cameraTransform.GetComponent<Camera>();
        workbenchObject=transform.Find("TopDownWorkbench").gameObject;
        itemRoot=transform.Find("Items");
        worldCamera.gameObject.SetActive(false); workbenchObject.SetActive(false);
        return true;
    }

    /// <summary>Scene에 저장된 계산기 키의 이벤트만 연결하고 편집된 배치를 기준값으로 사용합니다.</summary>
    private bool BindPlacedUi()
    {
        var canvas=transform.Find("TopDownTestCanvas");
        if(canvas==null) return false;
        hasPlacedUi=true;
        frontRoot=canvas.Find("FrontView").gameObject; workUiRoot=canvas.Find("WorkViewUI").gameObject;
        frontContainerImage=frontRoot.transform.Find("FrontContainer").GetComponent<Image>();
        var customer=frontRoot.transform.Find("Customer"); if(customer!=null) customerImage=customer.GetComponent<Image>();
        var dialogueObject=frontRoot.transform.Find("DialoguePanel/Dialogue") ?? frontRoot.transform.Find("Dialogue"); if(dialogueObject!=null) dialogueText=dialogueObject.GetComponent<Text>();
        if (dialogueText != null && frontRoot.transform.Find("DialoguePanel") is RectTransform panel) DystopiaScreen.FitDialogue(dialogueText, panel);
        for(int i=0;i<landingDust.Length;i++) landingDust[i]=frontRoot.transform.Find("LandingDust"+i).GetComponent<Image>();
        var work=workUiRoot.transform;
        clueText=work.Find("CustomerClue").GetComponent<Text>(); noticeText=work.Find("Notice").GetComponent<Text>();
        keypadMotion=(RectTransform)work.Find("RegisterMotion");
        keypadRect=(RectTransform)(keypadMotion != null ? keypadMotion.Find("Register") : work.Find("Register")); inputText=keypadRect.Find("PriceInput").GetComponent<Text>();
        placedCalculatorLayout=new Rect(keypadRect.anchoredPosition.x,-keypadRect.anchoredPosition.y,360*keypadRect.localScale.x,360*keypadRect.localScale.y);
        clockRoot=(RectTransform)canvas.Find("CounterClock"); clockText=clockRoot.Find("BusinessClock").GetComponent<Text>();
        placedClockLayout=new Rect(clockRoot.anchoredPosition.x,-clockRoot.anchoredPosition.y,180*clockRoot.localScale.x,180*clockRoot.localScale.y);
        for(int i=1;i<=9;i++) { string digit=i.ToString(); BindKey("Digit"+digit,()=>Digit(digit)); }
        BindKey("Digit00",()=>Digit("00")); BindKey("Digit000",()=>Digit("000"));
        BindKey("Backspace",Backspace); BindKey("Clear",ClearAmount); BindKey("Confirm",ConfirmSale);
        calculatorToggleRect=(RectTransform)work.Find("CalculatorToggle");
        var toggle=calculatorToggleRect.GetComponent<Button>(); toggle.onClick.RemoveAllListeners(); toggle.onClick.AddListener(()=>calculatorOpen=!calculatorOpen);
        pouringContainerImage=work.Find("PouringContainer").GetComponent<Image>();
        placedPourPosition=pouringContainerImage.rectTransform.anchoredPosition;
        placedPourAngle=pouringContainerImage.rectTransform.localEulerAngles.z;
        transitionBlock=canvas.Find("Transition").gameObject; transitionBlock.SetActive(false);
        var events=transform.Find("EventSystem"); ownedEventSystem=events!=null?events.gameObject:null;
        if(ownedEventSystem!=null) ownedEventSystem.SetActive(!isEmbeddedInFrontScene);
        workUiRoot.SetActive(false);
        return true;
    }

    /// <summary>저장된 버튼에 실행 동작을 한 번만 연결합니다.</summary>
    private void BindKey(string name, UnityEngine.Events.UnityAction action)
    {
        var button=keypadRect.Find(name).GetComponent<Button>();
        button.onClick.RemoveAllListeners(); button.onClick.AddListener(action);
    }
}
