using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>편집된 손잡이 위치에서 청소기를 잡고 뒤집어 물품을 기존 제외 경로로 흡입합니다.</summary>
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
    public float gripRadius = .4f, suctionRadius = 1.5f, captureRadius = .3f;
    /// <summary>물리 흡입 가속도와 물건이 줄어들어 사라지는 시간(초)입니다.</summary>
    public float suctionAcceleration = 22, swallowSeconds = .24f;
    /// <summary>잡은 손이 머무를 화면 아래 영역의 상단 비율입니다.</summary>
    [Range(.05f,.5f)] public float gripTopViewport = .25f;
    /// <summary>원래 배치를 복원하며 편집 상태에 연출을 저장하지 않습니다.</summary>
    private Vector3 restPosition;
    private Quaternion restRotation;
    private SpriteRenderer visual;
    private Camera worldCamera;
    private Vector2 virtualPointer;
    private float flip, windSeconds;
    private bool isHeld;
    private readonly LineRenderer[] wind = new LineRenderer[12];
    /// <summary>삼키는 중 취소되면 복원할 물품의 물리·외형 상태입니다.</summary>
    private DystopiaTopDownItem captured;
    private Vector3 capturePosition, captureScale;
    private Quaternion captureRotation;
    private bool captureSimulated;
    private TopDownItemState captureState;
    private float captureElapsed;
    /// <summary>입력을 소유하고 있는지 여부입니다.</summary>
    internal bool IsHeld => isHeld;
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
        if (!allowed || mouse==null || !mouse.leftButton.isPressed) Release();
        else if (!isHeld && canPickup && mouse.leftButton.wasPressedThisFrame)
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
        flip=Mathf.MoveTowards(flip,isHeld?1:0,dt/.18f);
        if (isHeld)
        {
            Rect viewport=camera.pixelRect;
            virtualPointer.x=Mathf.Clamp(virtualPointer.x,viewport.xMin,viewport.xMax);
            virtualPointer.y=Mathf.Clamp(virtualPointer.y,viewport.yMin-viewport.height*.45f,viewport.yMin+viewport.height*gripTopViewport);
            transform.localRotation=restRotation*Quaternion.Euler(0,0,180*Mathf.SmoothStep(0,1,flip));
            transform.position+=(Vector3)PointerWorld(virtualPointer)-grip.position;
        }
        else
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

    /// <summary>앞쪽 부채꼴 안의 물건만 당기고 노즐 가까이에 도달한 한 개를 삼킵니다.</summary>
    private void FixedUpdate()
    {
        if (!isHeld || flip<.85f || checkout==null || !checkout.IsSorting || checkout.Session==null || checkout.Session.IsPaused || !Application.isFocused) return;
        Vector2 forward=-transform.up;
        foreach (var item in checkout.Items)
        {
            if (item==null || !item.gameObject.activeInHierarchy || item.IsBeingVacuumed || item.State==TopDownItemState.Excluded || !item.Body.simulated) continue;
            Vector2 delta=(Vector2)nozzle.position-item.Body.position;
            float distance=delta.magnitude;
            if (distance>suctionRadius || distance>.05f && Vector2.Dot(-delta/distance,forward)<.35f) continue;
            if (captured==null && distance<captureRadius)
            {
                captured=item; capturePosition=item.transform.position; captureScale=item.transform.localScale;
                captureRotation=item.transform.rotation; captureSimulated=item.Body.simulated; captureState=item.State; captureElapsed=0;
                item.IsBeingVacuumed=true; item.State=TopDownItemState.Working;
                item.Body.linearVelocity=Vector2.zero; item.Body.angularVelocity=0; item.Body.simulated=false;
                continue;
            }
            item.WasStirred=true;
            item.Body.AddForce(delta/Mathf.Max(.01f,distance)*suctionAcceleration*item.Body.mass,ForceMode2D.Force);
        }
    }

    /// <summary>흡입구로 휘말려 줄어드는 연출이 끝난 뒤 기존 장바구니 제외 API를 한 번 호출합니다.</summary>
    /// <param name="dt">일시정지를 제외한 경과 초입니다.</param>
    private void AdvanceCapture(float dt)
    {
        if (captured==null) return;
        captureElapsed+=dt;
        float t=Mathf.Clamp01(captureElapsed/Mathf.Max(.05f,swallowSeconds));
        captured.transform.position=Vector3.Lerp(capturePosition,nozzle.position,t*t);
        captured.transform.localScale=captureScale*Mathf.Max(.02f,1-t*t);
        captured.transform.rotation=captureRotation*Quaternion.Euler(0,0,t*150);
        if (t<1) return;
        if (!checkout.TryClassify(captured,TopDownItemState.Excluded)) { CancelCapture(); return; }
        captured.IsBeingVacuumed=false;
        captured.transform.localScale=captureScale;
        captured=null;
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

    /// <summary>취소된 삼키기를 되돌립니다. 장바구니 수량은 완료 전까지 바꾸지 않습니다.</summary>
    private void CancelCapture()
    {
        if(captured==null) return;
        captured.transform.SetPositionAndRotation(capturePosition,captureRotation);
        captured.transform.localScale=captureScale; captured.Body.simulated=captureSimulated;
        captured.State=captureState; captured.IsBeingVacuumed=false; captured=null;
    }

    /// <summary>홀드·흡입을 종료하며 취소 중인 물건을 보존합니다.</summary>
    internal void Release()
    { isHeld=false; CancelCapture(); SetWindVisible(false); }

    /// <summary>화면 전환·비활성화 때 원래 배치와 물품 상태를 복원합니다.</summary>
    private void OnDisable()
    {
        Release();
        if(visual==null) return;
        visual.enabled=false; flip=0;
        transform.localPosition=restPosition; transform.localRotation=restRotation;
    }
}
