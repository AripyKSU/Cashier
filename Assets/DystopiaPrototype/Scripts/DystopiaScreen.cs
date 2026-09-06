using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>전용 Scene의 독립 Sprite 화면, 실제 버튼 입력 및 런 수명을 관리합니다.</summary>
public sealed class DystopiaScreen : MonoBehaviour
{
    /// <summary>이 Scene에만 저장되는 조정 가능한 초기값입니다.</summary>
    [SerializeField] private DystopiaSettings settings = new DystopiaSettings();
    /// <summary>프로젝트 안에 저장된 배경, 가판, 딸, 감독관 참조입니다.</summary>
    [SerializeField] private Sprite background, counter, daughter, inspector;
    /// <summary>같은 기준선으로 표시하는 손님 외형 4종입니다.</summary>
    [SerializeField] private Sprite[] customers = new Sprite[4];
    private Font font;
    private RectTransform root, modal, basketRoot;
    private Text cashText, dayText, statusText, gaugeText, inputText, dialogue, reasonText, queueText;
    private Image gaugeFill, portrait;
    private Image[] waiting = new Image[2];
    private Button confirmButton;
    private string amount = "";
    // 잘못된 부호/소수 입력을 정수 금액으로 오인하여 결제하지 않게 합니다.
    private bool invalidAmount;
    private int drawnRevision = -1;
    private float alertUntil;
    // 같은 손님은 결과·일시정지 갱신에도 물건 위치를 유지합니다. 거래 난수와 분리합니다.
    private DystopiaCustomer laidOutCustomer;
    private int basketLayoutSeed;
    public DystopiaSession Session { get; private set; }
    public DystopiaSettings Settings => settings;

    /// <summary>Windows PC 프로토타입의 설치된 한글 폰트와 UI를 구성합니다.</summary>
    private void Awake()
    {
        font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 24);
        if (font == null) throw new InvalidOperationException("맑은 고딕이 설치된 Windows PC가 필요합니다.");
        BuildScreen();
        Restart();
    }

    /// <summary>키보드와 실제 버튼은 동일한 입력 경로를 사용합니다.</summary>
    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.escapeKey.wasPressedThisFrame) Pause();
            if (Session.Phase == DystopiaPhase.Trading && !Session.IsPaused)
            {
                if (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame ||
                    keyboard.periodKey.wasPressedThisFrame || keyboard.numpadPeriodKey.wasPressedThisFrame)
                {
                    invalidAmount = true;
                    ShowAmount();
                }
                for (int i = 0; i < 10; i++)
                {
                    Key top = i == 0 ? Key.Digit0 : Key.Digit1 + (i - 1);
                    Key pad = Key.Numpad0 + i;
                    if (keyboard[top].wasPressedThisFrame || keyboard[pad].wasPressedThisFrame) Digit(i.ToString());
                }
                if (keyboard.backspaceKey.wasPressedThisFrame) Erase();
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) Confirm();
            }
        }
        Session.Tick(Time.unscaledDeltaTime);
        if (drawnRevision != Session.Revision) Refresh();
        gaugeFill.rectTransform.sizeDelta = new Vector2(300 * Session.Gauge / 100, 8);
        gaugeFill.color = Session.Gauge >= settings.greenThreshold ? new Color(.54f,.72f,.65f) : Session.Gauge > 30 ? new Color(.78f,.64f,.38f) : new Color(.85f,.35f,.30f);
        gaugeText.text = $"대기열  {Session.Gauge:0}%";
        if (Session.Phase == DystopiaPhase.Trading && Time.unscaledTime > alertUntil) reasonText.text = "숫자 입력 · Enter 판매 · Backspace 지우기 · Esc 일시정지";
    }

    /// <summary>런타임에 생성한 폰트 객체의 수명을 끝냅니다.</summary>
    private void OnDestroy() { if (font != null) Destroy(font); }

    /// <summary>기존 Scene을 참조하지 않고 모든 런 상태를 새로 시작합니다.</summary>
    public void Restart()
    {
        Session = new DystopiaSession(settings, Environment.TickCount);
        amount = "";
        invalidAmount = false;
        drawnRevision = -1;
        Refresh();
    }

    /// <summary>최대 일곱 자리 양의 정수 입력만 추가합니다.</summary>
    /// <param name="digit">한 자리 숫자 문자열.</param>
    public void Digit(string digit)
    {
        if (Session.Phase != DystopiaPhase.Trading || Session.IsPaused || invalidAmount || digit.Length != 1 || digit[0] < '0' || digit[0] > '9' || amount.Length >= 7) return;
        if (amount == "0") amount = "";
        amount += digit;
        alertUntil = 0;
        ShowAmount();
    }

    /// <summary>마지막 한 자리를 삭제합니다.</summary>
    public void Erase()
    {
        if (Session.Phase != DystopiaPhase.Trading || Session.IsPaused) return;
        if (invalidAmount) { invalidAmount = false; amount = ""; ShowAmount(); return; }
        if (amount.Length > 0) amount = amount.Substring(0, amount.Length - 1);
        ShowAmount();
    }

    /// <summary>표시된 금액을 확정하고 결과 동안 추가 입력을 차단합니다.</summary>
    public void Confirm()
    {
        if (invalidAmount) return;
        if (Session.Confirm(amount)) { amount = ""; Refresh(); }
        else if (Session.Phase == DystopiaPhase.Trading && !Session.IsPaused)
        {
            reasonText.text = "판매할 금액을 1원 이상 입력하세요.";
            alertUntil = Time.unscaledTime + 2;
        }
    }

    /// <summary>런의 시간과 입력을 함께 멈추거나 재개합니다.</summary>
    public void Pause() { Session.TogglePause(); Refresh(); }

    /// <summary>16:9 기준 좌표의 배경·독립 인물·가판·UI를 만듭니다.</summary>
    private void BuildScreen()
    {
        var canvasObject = new GameObject("DystopiaCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280,720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        root = canvasObject.GetComponent<RectTransform>();
        Picture(root,"Seoul",background,0,0,1280,720,false);
        waiting[0] = Picture(root,"WaitingLeft",customers[0],185,170,215,350,true);
        waiting[1] = Picture(root,"WaitingRight",customers[0],780,170,215,350,true);
        foreach (var item in waiting) item.color = new Color(.60f,.65f,.70f,1);
        portrait = Picture(root,"Customer",customers[0],405,82,400,550,true);
        Picture(root,"Counter",counter,0,215,1280,535,false);
        Picture(root,"Daughter",daughter,-65,565,400,620,true);
        Panel(root,"TopBar",0,0,1280,83,new Color(.045f,.065f,.075f,.97f));
        Label(root,"Title","프로젝트 디스토피아",25,12,270,28,22);
        Label(root,"Place","서울 · 임시 배급 가판",25,44,270,23,14,new Color(.60f,.67f,.69f));
        dayText = Label(root,"Day","",330,14,230,30,22);
        statusText = Label(root,"Status","",330,48,270,24,15);
        cashText = Label(root,"Cash","",655,14,240,30,25);
        Label(root,"GoalHint",$"안전구역 시민권  {settings.citizenshipPrice:N0}원",655,49,265,23,14);
        gaugeText = Label(root,"GaugeText","",945,14,240,25,18);
        Panel(root,"GaugeRail",945,51,300,8,new Color(.16f,.20f,.22f));
        gaugeFill = Panel(root,"GaugeFill",945,51,300,8,Color.white);
        queueText = Label(root,"Queue","",36,106,290,29,18);
        Panel(root,"DialoguePanel",320,359,560,70,new Color(.04f,.055f,.065f,.94f));
        dialogue = Label(root,"Dialogue","",337,370,525,49,19);
        reasonText = Label(root,"Feedback","",273,674,655,35,14);
        basketRoot = Rect(root,"Basket",275,442,635,138);
        Panel(root,"Register",947,405,305,300,new Color(.045f,.065f,.075f,.97f));
        Label(root,"PriceHeading","받을 금액",962,416,120,23,16);
        inputText = Label(root,"PriceInput","금액 입력",962,443,276,40,30);
        for (int i = 1; i <= 9; i++)
        {
            string value = i.ToString();
            MakeButton(root,"Digit"+value,value,961+((i-1)%3)*94,491+((i-1)/3)*38,88,33,()=>Digit(value),18);
        }
        MakeButton(root,"Erase","지우기",961,605,88,33,Erase,16);
        MakeButton(root,"Digit0","0",1055,605,88,33,()=>Digit("0"),18);
        MakeButton(root,"Pause","일시정지",1149,605,88,33,Pause,15);
        confirmButton = MakeButton(root,"Confirm","판매 확정  ↵",961,646,276,44,Confirm,20);
        var colors = confirmButton.colors;
        colors.normalColor = new Color(.35f,.48f,.45f); colors.highlightedColor = new Color(.46f,.60f,.55f);
        confirmButton.colors = colors;
        // 탐색 Submit은 숫자키 Enter 처리와 겹치므로 전용 모듈의 탐색 이벤트를 끕니다.
        var eventObject = new GameObject("DystopiaEventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
        eventObject.transform.SetParent(transform,false);
        eventObject.GetComponent<EventSystem>().sendNavigationEvents = false;
        eventObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    /// <summary>상태 전환 때만 상품·대화·안내창을 갱신합니다.</summary>
    private void Refresh()
    {
        drawnRevision = Session.Revision;
        dayText.text = $"{Session.Day:00}일차  /  영업 기록";
        cashText.text = $"{Session.Cash:N0} 원";
        statusText.text = $"명성 {Session.Reputation}   ·   도덕성 {Session.Morality}";
        queueText.text = $"오늘 방문 {Session.Visitors}명 · 남은 손님 {Session.Remaining}명";
        confirmButton.interactable = Session.Phase == DystopiaPhase.Trading && !Session.IsPaused;
        ShowAmount();
        portrait.sprite = customers[Mathf.Clamp(Session.Customer.appearance,0,customers.Length-1)];
        portrait.color = Session.Phase == DystopiaPhase.Result && !Session.LastAccepted ? new Color(.8f,.6f,.6f) : Color.white;
        for (int i=0;i<waiting.Length;i++)
        {
            waiting[i].gameObject.SetActive(Session.Remaining > i+1);
            waiting[i].sprite = customers[(Session.Customer.appearance+i+1)%customers.Length];
        }
        dialogue.text = Session.Phase == DystopiaPhase.Result ? Session.Feedback : Session.Customer.isPoor ?
            "집에 아이가 기다리고 있어요. 가진 돈이 얼마 없어요…" : "이 물건들로 주세요. 얼마나 드리면 될까요?";
        if (Session.Phase == DystopiaPhase.Result || Session.Departed > 0)
        {
            reasonText.text = Session.Reason;
            alertUntil = Time.unscaledTime + 3;
        }
        ClearChildren(basketRoot);
        if (laidOutCustomer != Session.Customer)
        {
            laidOutCustomer = Session.Customer;
            basketLayoutSeed = Environment.TickCount;
        }
        var layoutRandom = new System.Random(basketLayoutSeed);
        // 상품 아래쪽을 상판에 두되 위치·기울기를 흩뜨려 일부가 자연스럽게 겹치게 합니다.
        int unitCount = 0;
        foreach (var line in Session.Customer.basket) unitCount += line.quantity;
        int[] slots = new int[unitCount];
        var placedItems = new RectTransform[unitCount];
        for (int i = 0; i < slots.Length; i++) slots[i] = i;
        for (int i = slots.Length - 1; i > 0; i--)
        {
            int other = layoutRandom.Next(i + 1);
            int slot = slots[i]; slots[i] = slots[other]; slots[other] = slot;
        }
        int placed = 0;
        for (int i=0;i<Session.Customer.basket.Count;i++)
        {
            var line = Session.Customer.basket[i];
            for (int unit = 0; unit < line.quantity; unit++)
            {
                int slot = slots[placed++];
                int columns = Math.Min(5, unitCount - slot / 5 * 5);
                float x = 317.5f + (slot % 5 - (columns - 1) * .5f) * 92 + layoutRandom.Next(-22,23);
                float y = layoutRandom.Next(74,127);
                float angle = layoutRandom.Next(6,19) * (layoutRandom.Next(2) == 0 ? -1 : 1);
                Vector2 size = line.product.sprite.rect.size;
                size *= Mathf.Min(128 / size.x, 144 / size.y);
                if (ReferenceEquals(line.product, settings.products[3])) size *= 1.35f;
                // 회전한 아래쪽 모서리도 상판 앞 테두리를 넘지 않게 합니다.
                y = Mathf.Min(y, 130 - size.x * .5f * Mathf.Abs(Mathf.Sin(angle * Mathf.Deg2Rad)));
                var item = Picture(basketRoot,"Product"+i+"Unit"+unit,line.product.sprite,0,0,size.x,size.y,true);
                item.rectTransform.pivot = new Vector2(.5f,0);
                item.rectTransform.anchoredPosition = new Vector2(x,-y);
                item.rectTransform.localRotation = Quaternion.Euler(0,0,angle);
                placedItems[slot] = item.rectTransform;
            }
        }
        // 실제 접지점이 가까운 상품을 나중에 그려 겹침의 앞뒤 관계를 맞춥니다.
        Array.Sort(placedItems, (a, b) => b.anchoredPosition.y.CompareTo(a.anchoredPosition.y));
        foreach (var item in placedItems) item.SetAsLastSibling();
        if (modal != null) { modal.gameObject.SetActive(false); Destroy(modal.gameObject); }
        modal = null;
        if (Session.IsPaused)
        {
            Modal("일시정지","시간과 대기열이 멈췄습니다.",false);
            MakeButton(modal,"Resume","계속하기",190,320,360,48,Pause,21);
            MakeButton(modal,"Restart","처음부터",190,380,360,44,Restart,18);
            return;
        }
        switch (Session.Phase)
        {
            case DystopiaPhase.PriceGuide:
                Modal(Session.Day==1?"영업 전, 가격을 기억하세요":"새로 들어온 물품", "영업이 시작되면 가격표를 다시 볼 수 없습니다.",false);
                int column=0;
                foreach(var product in settings.products)
                {
                    if(product.firstDay!=Session.Day) continue;
                    float x=30+column*175;
                    Picture(modal,"GuideArt"+column,product.sprite,x,139,150,130,true);
                    Label(modal,"GuideName"+column,product.name,x,280,160,26,20);
                    Label(modal,"GuidePrice"+column,$"{product.price:N0}원",x,313,160,32,24);
                    column++;
                }
                Label(modal,"Narrative",$"{Session.DaysUntilTribute}일 뒤, 감독관이 상납금을 받으러 옵니다.\n딸과 함께 안전구역으로 갈 돈을 모으세요.",35,366,670,56,17);
                MakeButton(modal,"OpenShop","기억했어요 · 영업 시작",170,435,400,46,()=>{Session.OpenShop();Refresh();},21);
                break;
            case DystopiaPhase.Tribute:
                Modal("감독관의 방문",$"“물품대금, 자리값, 내 몫. 잊지는 않았겠지.”\n오늘 상납금  {Session.TributeAmount:N0}원",true);
                Label(modal,"TributeCash",$"보유 현금  {Session.Cash:N0}원\n상납 처리 후 시민권을 구매할 수 있습니다.",40,225,480,70,21);
                MakeButton(modal,"PayTribute","상납금 정산",80,420,400,48,()=>{Session.PayTribute();Refresh();},21);
                break;
            case DystopiaPhase.Settlement:
                Modal($"{Session.Day}일차 영업 종료",$"매출  {Session.Revenue:N0}원     ·     보유 현금  {Session.Cash:N0}원",false);
                Label(modal,"Summary",$"성공 {Session.Sold}건     거절 {Session.Refused}건     이탈 {Session.Departed}명\n\n명성 {Session.ReputationChange:+0;-0;0}      도덕성 {Session.MoralityChange:+0;-0;0}\n\n다음 상납까지 {Session.DaysUntilTribute}일\n시민권까지 {Math.Max(0,settings.citizenshipPrice-Session.Cash):N0}원",40,160,650,210,23);
                MakeButton(modal,"NextDay","다음 날",390,420,300,48,()=>{Session.NextDay();Refresh();},21);
                var buy=MakeButton(modal,"BuyCitizenship","시민권 구매",40,420,320,48,()=>{Session.BuyCitizenship();Refresh();},21);
                buy.interactable=Session.CanBuy;
                break;
            case DystopiaPhase.Goal:
                Modal("안전구역으로", "“아빠, 이제 우리 따뜻한 데서 잘 수 있어?”",false);
                Label(modal,"Ending","시민권을 손에 넣었습니다.\n\n당신이 매긴 가격들이 이곳까지 데려왔습니다.\n\n명성과 도덕성, 그리고 남겨진 사람들을 기억하며.",50,185,640,195,23);
                MakeButton(modal,"Restart","다시 시작",170,420,400,48,Restart,21);
                break;
            case DystopiaPhase.Failed:
                Modal("가판은 문을 닫았습니다", "“상납금이 없다고? 그럼 자리를 비워.”",true);
                Label(modal,"Failure",$"필요한 상납금 {Session.TributeAmount:N0}원\n보유 현금 {Session.Cash:N0}원\n\n기억과 선택을 되짚어 다시 시작하세요.",40,190,470,170,22);
                MakeButton(modal,"Restart","다시 시작",80,420,400,48,Restart,21);
                break;
        }
    }

    /// <summary>게임 아트 위에 읽기 쉬운 안내 영역을 배치합니다.</summary>
    private void Modal(string title,string subtitle,bool showInspector)
    {
        modal = Rect(root,"Modal",270,111,740,510);
        Panel(modal,"Border",0,0,740,510,new Color(.46f,.51f,.51f));
        Panel(modal,"Paper",2,2,736,506,new Color(.075f,.095f,.105f,.99f));
        Label(modal,"ModalTitle",title,35,28,670,44,31);
        Label(modal,"ModalSubtitle",subtitle,35,85,670,55,18);
        if(showInspector) Picture(modal,"Inspector",inspector,488,150,245,350,true);
    }

    /// <summary>입력 금액은 플레이어가 직접 입력한 값만 표시합니다.</summary>
    private void ShowAmount()
    {
        inputText.fontSize = invalidAmount ? 17 : 30;
        inputText.text = invalidAmount ? "정수만 입력하세요 · 지우기로 초기화" : amount.Length==0 ? "금액 입력" : $"{int.Parse(amount):N0} 원";
    }

    /// <summary>자식들을 즉시 숨긴 뒤 프레임 끝에 해제합니다.</summary>
    private void ClearChildren(Transform parent)
    {
        foreach(Transform child in parent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
    }

    /// <summary>좌상단 기준의 고정 reference-resolution 영역을 만듭니다.</summary>
    private RectTransform Rect(Transform parent,string name,float x,float y,float width,float height)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent,false); rect.anchorMin=rect.anchorMax=new Vector2(0,1); rect.pivot=new Vector2(0,1);
        rect.anchoredPosition=new Vector2(x,-y); rect.sizeDelta=new Vector2(width,height);
        return rect;
    }

    /// <summary>단색 UI 영역을 만듭니다.</summary>
    private Image Panel(Transform parent,string name,float x,float y,float width,float height,Color color)
    {
        var image=Rect(parent,name,x,y,width,height).gameObject.AddComponent<Image>(); image.color=color; image.raycastTarget=false; return image;
    }

    /// <summary>독립적인 Sprite 참조를 UI에 표시합니다.</summary>
    private Image Picture(Transform parent,string name,Sprite sprite,float x,float y,float width,float height,bool preserve)
    {
        var image=Panel(parent,name,x,y,width,height,Color.white); image.sprite=sprite; image.preserveAspect=preserve;
        if(sprite==null) image.color=Color.clear;
        return image;
    }

    /// <summary>한국어 텍스트를 이미지와 분리하여 표시합니다.</summary>
    private Text Label(Transform parent,string name,string value,float x,float y,float width,float height,int size,Color? color=null)
    {
        var text=Rect(parent,name,x,y,width,height).gameObject.AddComponent<Text>(); text.font=font; text.text=value; text.fontSize=size;
        text.color=color??new Color(.91f,.93f,.91f); text.raycastTarget=false; text.verticalOverflow=VerticalWrapMode.Overflow;
        return text;
    }

    /// <summary>실제 uGUI 버튼 이벤트를 연결합니다.</summary>
    private Button MakeButton(Transform parent,string name,string value,float x,float y,float width,float height,UnityEngine.Events.UnityAction action,int size)
    {
        var image=Panel(parent,name,x,y,width,height,Color.white); image.raycastTarget=true;
        var button=image.gameObject.AddComponent<Button>(); button.targetGraphic=image;
        var colors=button.colors; colors.normalColor=new Color(.19f,.25f,.28f); colors.highlightedColor=new Color(.30f,.39f,.42f); colors.pressedColor=new Color(.42f,.50f,.51f); button.colors=colors;
        button.navigation=new Navigation {mode=Navigation.Mode.None}; button.onClick.AddListener(action);
        var label=Label(image.transform,"Label",value,4,0,width-8,height,size); label.alignment=TextAnchor.MiddleCenter;
        return button;
    }
}
