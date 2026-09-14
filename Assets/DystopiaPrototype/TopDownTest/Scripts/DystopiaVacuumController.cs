using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>편집된 손잡이 위치에서 청소기를 잡고 뒤집어 여러 물품을 흡입하고 같은 입구로 배출합니다.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class DystopiaVacuumController : MonoBehaviour
{
    /// <summary>물품과 거래 수량을 소유하는 체크아웃입니다.</summary>
    public DystopiaTopDownTest checkout;
    /// <summary>Inspector에서 배치하는 손잡이 접점과 흡입구입니다.</summary>
    public Transform grip, nozzle;
    /// <summary>흡입 바람선이 공유하는 프로젝트 재질입니다.</summary>
    public Material windMaterial;
    /// <summary>월드 단위의 손잡이 클릭 반경·흡입 거리·삼키기 반경입니다.</summary>
    public float gripRadius = .4f, suctionRadius = 1.9f, captureRadius = .36f;
    /// <summary>물리 흡입 가속도와 물건이 줄어들어 사라지는 시간(초)입니다.</summary>
    public float suctionAcceleration = 52, swallowSeconds = .16f;
    /// <summary>흡입을 놓았을 때 노즐 앞에서 물건을 내보내는 월드 초당 속도입니다.</summary>
    public float spitSpeed = 4.8f;
    /// <summary>여러 물건을 같은 출입구에서 차례로 내보내는 시간 간격(초)입니다.</summary>
    [Min(.02f)] public float spitInterval = .07f;
    /// <summary>뒤집기 시작과 끝의 속도를 부드럽게 연결하는 감쇠 시간(초)입니다.</summary>
    [Min(.04f)] public float turnSmoothSeconds = .11f;
    /// <summary>잡은 손이 머무를 화면 아래 영역의 상단 비율입니다.</summary>
    [Range(.05f,.5f)] public float gripTopViewport = .25f;
    /// <summary>원래 배치를 복원하며 편집 상태에 연출을 저장하지 않습니다.</summary>
    private Vector3 restPosition;
    private Quaternion restRotation;
    private SpriteRenderer visual;
    private Camera worldCamera;
    private Vector2 virtualPointer;
    private float flip, flipVelocity, windSeconds, spitElapsed;
    private int spitIndex;
    private bool isHeld, isSpitting;
    private readonly LineRenderer[] wind = new LineRenderer[12];
    /// <summary>흡입 중이거나 내부에 보관된 모든 물건입니다. 개수 제한을 두지 않습니다.</summary>
    private readonly List<StoredItem> storedItems = new List<StoredItem>();
    /// <summary>입력을 소유하고 있는지 여부입니다.</summary>
    internal bool IsHeld => isHeld || isSpitting;
    /// <summary>손 커서가 붙을 실제 손잡이 접점입니다.</summary>
    internal Vector3 GripWorldPoint => grip.position;

    /// <summary>저장된 시작 상태와 바람선만 준비합니다. 기존 Transform은 변경하지 않습니다.</summary>
    private void Awake()
    {
        visual = GetComponent<SpriteRenderer>();
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
        for (int i=0;i<wind.Length;i++)
        {
            var go = new GameObject("SuctionWind"+i) { hideFlags=HideFlags.DontSave };
            go.transform.SetParent(transform,false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial=windMaterial; line.useWorldSpace=true; line.positionCount=6;
            line.startWidth=.025f; line.endWidth=.008f;
            line.startColor=new Color(.68f,.87f,1,0); line.endColor=new Color(.85f,.95f,1,.65f);
            line.sortingLayerID=visual.sortingLayerID; line.sortingOrder=visual.sortingOrder+1;
            line.enabled=false; wind[i]=line;
        }
    }

    /// <summary>상품·막대보다 먼저 손잡이 클릭을 판정하고 홀드를 유지합니다.</summary>
    /// <param name="camera">탑다운 카메라입니다.</param>
    /// <param name="allowed">분류 중이고 포커스·일시정지 조건을 통과한 경우입니다.</param>
    /// <param name="canPickup">다른 물건이나 UI를 잡고 있지 않은 경우입니다.</param>
    internal void SampleInput(Camera camera,bool allowed,bool canPickup)
    {
        worldCamera=camera;
        if (visual==null || grip==null || nozzle==null) return;
        visual.enabled=camera!=null && camera.isActiveAndEnabled;
        var mouse=Mouse.current;
        if (!allowed || mouse==null) Release();
        else if (!mouse.leftButton.isPressed) BeginSpitAndRelease();
        else if (!isSpitting && !isHeld && canPickup && mouse.leftButton.wasPressedThisFrame)
        {
            Vector2 pointer=PointerWorld(mouse.position.ReadValue());
            if (Vector2.Distance(pointer,grip.position)<=gripRadius)
            {
                isHeld=true;
                virtualPointer=mouse.position.ReadValue();
            }
        }
        else if (isHeld)
        {
            // 델타를 누적해 OS 커서가 화면 하단에 닿아도 손을 더 아래로 움직일 수 있습니다.
            virtualPointer+=mouse.delta.ReadValue();
        }
        if (!allowed) { SetWindVisible(false); return; }
        float dt=Time.unscaledDeltaTime;
        float turnTarget=isHeld || isSpitting?1:0;
        flip=Mathf.SmoothDamp(flip,turnTarget,ref flipVelocity,turnSmoothSeconds,10,dt);
        if(Mathf.Abs(flip-turnTarget)<.001f) { flip=turnTarget; flipVelocity=0; }
        if (isHeld)
        {
            Rect viewport=camera.pixelRect;
            virtualPointer.x=Mathf.Clamp(virtualPointer.x,viewport.xMin,viewport.xMax);
            virtualPointer.y=Mathf.Clamp(virtualPointer.y,viewport.yMin-viewport.height*.45f,viewport.yMin+viewport.height*gripTopViewport);
            transform.localRotation=restRotation*Quaternion.Euler(0,0,180*Mathf.SmoothStep(0,1,flip));
            transform.position+=(Vector3)PointerWorld(virtualPointer)-grip.position;
        }
        else if (!isSpitting)
        {
            transform.localRotation=Quaternion.Slerp(transform.localRotation,restRotation,1-Mathf.Exp(-18*dt));
            transform.localPosition=Vector3.Lerp(transform.localPosition,restPosition,1-Mathf.Exp(-18*dt));
            if (flip==0) { transform.localRotation=restRotation; transform.localPosition=restPosition; }
        }
        bool sucking=isHeld && flip>.85f;
        SetWindVisible(sucking);
        if (sucking)
        {
            windSeconds+=dt;
            DrawWind();
            AdvanceCapture(dt);
        }
        if(isSpitting) AdvanceSpit(dt);
    }

    /// <summary>현재 물리 평면에 화면 좌표를 투영하며 하단 바깥 좌표도 허용합니다.</summary>
    /// <param name="pointer">화면 픽셀 좌표입니다.</param>
    /// <returns>청소기와 같은 Z 평면의 월드 좌표입니다.</returns>
    private Vector2 PointerWorld(Vector2 pointer)
    {
        Ray ray=worldCamera.ScreenPointToRay(pointer);
        var plane=new Plane(Vector3.forward,transform.position);
        return plane.Raycast(ray,out float distance)?ray.GetPoint(distance):transform.position;
    }

    /// <summary>앞쪽 부채꼴 안의 물건을 당기고 노즐에 닿는 모든 물건을 차례로 삼킵니다.</summary>
    private void FixedUpdate()
    {
        if (!isHeld || flip<.85f || checkout==null || !checkout.IsSorting || checkout.Session==null || checkout.Session.IsPaused || !Application.isFocused) return;
        Vector2 forward=-transform.up;
        foreach (var item in checkout.Items)
        {
            if (item==null || !item.gameObject.activeInHierarchy || item.IsBeingVacuumed || item.State==TopDownItemState.Excluded || !item.Body.simulated) continue;
            Vector2 delta=(Vector2)nozzle.position-item.Body.position;
            float distance=delta.magnitude;
            // 입구 정면뿐 아니라 바로 아래쪽 가장자리의 물건도 놓치지 않도록 부채꼴을 100도까지 엽니다.
            if (distance>suctionRadius || distance>.05f && Vector2.Dot(-delta/distance,forward)<-.17f) continue;
            if (distance<captureRadius)
            {
                storedItems.Add(new StoredItem(item));
                item.IsBeingVacuumed=true; item.State=TopDownItemState.Working;
                item.Body.linearVelocity=Vector2.zero; item.Body.angularVelocity=0; item.Body.simulated=false;
                continue;
            }
            item.WasStirred=true;
            item.Body.AddForce(delta/Mathf.Max(.01f,distance)*suctionAcceleration*item.Body.mass,ForceMode2D.Force);
        }
    }

    /// <summary>각 물건을 독립적으로 흡입구까지 휘말리게 한 뒤 청소기 안에 보관합니다.</summary>
    /// <param name="dt">일시정지를 제외한 경과 초입니다.</param>
    private void AdvanceCapture(float dt)
    {
        foreach(var stored in storedItems)
        {
            if(stored.hasSwallowed) continue;
            stored.elapsed+=dt;
            float t=Mathf.Clamp01(stored.elapsed/Mathf.Max(.05f,swallowSeconds));
            stored.item.transform.position=Vector3.Lerp(stored.position,nozzle.position,t*t);
            stored.item.transform.localScale=stored.scale*Mathf.Max(.02f,1-t*t);
            stored.item.transform.rotation=stored.rotation*Quaternion.Euler(0,0,t*150);
            if(t<1) continue;
            stored.hasSwallowed=true;
            stored.item.gameObject.SetActive(false);
        }
    }

    /// <summary>휘어진 짧은 바람선을 반복해서 노즐 쪽으로 이동시킵니다.</summary>
    private void DrawWind()
    {
        Vector3 forward=-transform.up, side=transform.right;
        for(int i=0;i<wind.Length;i++)
        {
            float phase=Mathf.Repeat(windSeconds*1.7f+i/(float)wind.Length,1);
            float lane=(i%5-2)*.22f;
            for(int j=0;j<6;j++)
            {
                float distance=Mathf.Clamp01(1-phase-j*.035f)*suctionRadius;
                float offset=lane*distance+Mathf.Sin(distance*5+windSeconds*7+i)*.035f*distance;
                wind[i].SetPosition(j,nozzle.position+forward*distance+side*offset);
            }
        }
    }

    /// <summary>바람선을 홀드 중에만 표시합니다.</summary>
    /// <param name="visible">흡입 중이면 true입니다.</param>
    private void SetWindVisible(bool visible)
    { foreach(var line in wind) if(line!=null) line.enabled=visible; }

    /// <summary>같은 출입구에서 저장 순서대로 하나씩 배출합니다.</summary>
    /// <param name="dt">프레임과 무관한 배출 간격에 사용할 경과 초입니다.</param>
    private void AdvanceSpit(float dt)
    {
        spitElapsed-=dt;
        while(isSpitting && spitElapsed<=0)
        {
            Spit(storedItems[spitIndex],spitIndex);
            spitIndex++;
            if(spitIndex>=storedItems.Count)
            {
                storedItems.Clear(); spitIndex=0; isSpitting=false;
                break;
            }
            spitElapsed+=spitInterval;
        }
    }

    /// <summary>한 물건에 작은 방향·속도·회전 변주를 주어 같은 출입구에서 내보냅니다.</summary>
    /// <param name="stored">배출할 물건의 저장 상태입니다.</param>
    /// <param name="sequence">이번 연속 배출의 순서입니다.</param>
    private void Spit(StoredItem stored,int sequence)
    {
        float sample=sequence+1;
        float angle=Mathf.Sin(sample*2.17f)*7f;
        float speed=spitSpeed*Mathf.Lerp(.9f,1.1f,Mathf.Repeat(sample*.37f,1));
        Vector2 direction=Quaternion.Euler(0,0,angle)*-transform.up;
        var item=stored.item;
        item.transform.position=nozzle.position+(Vector3)(direction*.2f);
        item.transform.localScale=stored.scale; item.transform.rotation=stored.rotation;
        item.State=TopDownItemState.Working; item.WasStirred=true;
        item.gameObject.SetActive(true); item.Body.simulated=stored.simulated;
        item.Body.linearVelocity=direction*speed;
        item.Body.angularVelocity=Mathf.Sin(sample*1.31f)*75f;
        item.IsBeingVacuumed=false;
    }

    /// <summary>한 물건의 흡입 전 Transform·물리·분류 상태를 복원합니다.</summary>
    /// <param name="stored">복원할 저장 상태입니다.</param>
    private static void Restore(StoredItem stored)
    {
        var item=stored.item;
        if(item==null) return;
        item.transform.SetPositionAndRotation(stored.position,stored.rotation);
        item.transform.localScale=stored.scale; item.State=stored.state;
        item.gameObject.SetActive(true); item.Body.simulated=stored.simulated;
        item.Body.linearVelocity=stored.velocity; item.Body.angularVelocity=stored.angularVelocity;
        item.IsBeingVacuumed=false;
    }

    /// <summary>버튼을 놓으면 덜 들어간 물건은 복원하고 삼킨 물건의 연속 배출을 시작합니다.</summary>
    private void BeginSpitAndRelease()
    {
        if(!isHeld) return;
        isHeld=false; SetWindVisible(false);
        for(int i=storedItems.Count-1;i>=0;i--)
        {
            if(storedItems[i].hasSwallowed) continue;
            Restore(storedItems[i]); storedItems.RemoveAt(i);
        }
        if(storedItems.Count==0) return;
        spitIndex=0; spitElapsed=0; isSpitting=true;
    }

    /// <summary>홀드·흡입을 종료하며 취소 중인 물건을 보존합니다.</summary>
    internal void Release()
    {
        isHeld=isSpitting=false;
        for(int i=spitIndex;i<storedItems.Count;i++) Restore(storedItems[i]);
        storedItems.Clear(); spitIndex=0; spitElapsed=0; SetWindVisible(false);
    }

    /// <summary>화면 전환·비활성화 때 원래 배치와 물품 상태를 복원합니다.</summary>
    private void OnDisable()
    {
        Release();
        if(visual==null) return;
        visual.enabled=false; flip=flipVelocity=0;
        transform.localPosition=restPosition; transform.localRotation=restRotation;
    }

    /// <summary>물건마다 독립적인 흡입 진행도와 취소 복원값을 보관합니다.</summary>
    private sealed class StoredItem
    {
        internal readonly DystopiaTopDownItem item;
        internal readonly Vector3 position,scale;
        internal readonly Quaternion rotation;
        // 취소 시 흡입 직전 운동량을 물품별로 복원합니다.
        internal readonly Vector2 velocity;
        internal readonly float angularVelocity;
        internal readonly bool simulated;
        internal readonly TopDownItemState state;
        internal float elapsed;
        internal bool hasSwallowed;

        /// <summary>흡입 직전의 물건 상태를 캡처합니다.</summary>
        /// <param name="item">흡입구에 도달한 물건입니다.</param>
        internal StoredItem(DystopiaTopDownItem item)
        {
            this.item=item; position=item.transform.position; scale=item.transform.localScale;
            rotation=item.transform.rotation; simulated=item.Body.simulated; state=item.State;
            velocity=item.Body.linearVelocity; angularVelocity=item.Body.angularVelocity;
        }
    }
}
