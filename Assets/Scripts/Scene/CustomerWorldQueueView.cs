using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>실제 방문별 SpriteRenderer를 이동한다. 모델·거래·시계·로드 수명은 변경하지 않는다.</summary>
[DefaultExecutionOrder(200)]
public sealed class CustomerWorldQueueView : MonoBehaviour
{
    /// <summary>카메라 정렬과 준비된 Controller를 제공하는 같은 프리팹의 표시 소유자.</summary>
    [SerializeField] private WorldSceneView world;
    /// <summary>좌상단 기준 픽셀 authoring 공간의 방문 루트와 입장·계산대·퇴장 anchors. leftExit는 기존 prefab 직렬화 호환용이다.</summary>
    [SerializeField] private Transform visualRoot, counter, entrance, leftExit, rightExit;
    /// <summary>FIFO 순서 10개 위치. 인원수와 무관하게 슬롯별 원근 배율을 적용한다.</summary>
    [SerializeField] private Transform[] slots;
    /// <summary>계산대 높이 대비 Slot05·Slot10 배율. Stage2Reference의 550:340:240 기준.</summary>
    [SerializeField] private float middleSlotScale = 340f / 550f, rearSlotScale = 240f / 550f;
    [SerializeField] private TMP_FontAsset font;
    /// <summary>노멀맵과 손님 표면 조명을 받는 body 전용 material.</summary>
    [SerializeField] private Material bodyMaterial;
    /// <summary>거래 반응 전용 일반 Sprite material.</summary>
    [SerializeField] private Material reactionMaterial;
    /// <summary>거래 결과 순서: Satisfied, Delighted, Reluctant, Refused.</summary>
    [SerializeField] private Sprite[] tradeReactionSprites;
    /// <summary>계산대 성인 기준 높이·하단 가림 보정(전면 로컬 픽셀)과 이동 초.</summary>
    [SerializeField] private float heightPixels = 550, bottomCoverPixels = 12, moveSeconds = .65f;
    /// <summary>Child 외형의 성인 기준 표시 배율.</summary>
    [SerializeField, Range(CustomerPortraitLayout.MinChildPortraitScale, CustomerPortraitLayout.MaxChildPortraitScale)]
    private float childPortraitScale = CustomerPortraitLayout.DefaultChildPortraitScale;
    /// <summary>Child를 성인 기준 높이에서 위로 올리는 비율.</summary>
    [SerializeField, Range(CustomerPortraitLayout.MinChildPortraitRise, CustomerPortraitLayout.MaxChildPortraitRise)]
    private float childPortraitRise = CustomerPortraitLayout.DefaultChildPortraitRise;
    /// <summary>성인에게 추가할 하향량. 전면 로컬 픽셀 단위이며 0이면 기존 위치를 유지한다.</summary>
    [Header("연령별 추가 하향 오프셋 (0 = 현재 위치)")]
    [SerializeField, Min(0), Tooltip("성인 표시를 아래로 내릴 고정 픽셀 값")]
    private float adultDownOffsetPixels;
    /// <summary>노인에게 추가할 하향량. 크기·원근 배율과 독립된 전면 로컬 픽셀이다.</summary>
    [SerializeField, Min(0), Tooltip("노인 표시를 아래로 내릴 고정 픽셀 값")]
    private float elderlyDownOffsetPixels;
    /// <summary>아이에게 기존 크기·상승·하단 보정에 더해 적용할 하향량. 0이면 현재 아이 위치를 유지한다.</summary>
    [SerializeField, Min(0), Tooltip("아이 표시를 아래로 내릴 고정 픽셀 값")]
    private float childDownOffsetPixels;

    private readonly Dictionary<CustomerVisit, Visual> visuals = new Dictionary<CustomerVisit, Visual>();
    private readonly HashSet<CustomerVisit> seen = new HashSet<CustomerVisit>();
    private readonly List<CustomerVisit> remove = new List<CustomerVisit>();
    private DayProgress day;

    /// <summary>소유 중인 방문별 표시 객체 수.</summary>
    public int VisualCount => visuals.Count;

    /// <summary>필수 연결·시간을 검사한다.</summary>
    private void Start()
    {
        if (world == null || visualRoot == null || counter == null || entrance == null || rightExit == null ||
            font == null || bodyMaterial == null || reactionMaterial == null || slots == null || slots.Length != CustomerQueue.Capacity ||
            Array.Exists(slots, x => x == null) || tradeReactionSprites == null || tradeReactionSprites.Length != 4 ||
            Array.Exists(tradeReactionSprites, x => x == null) || !isPositive(heightPixels) ||
            !isPositive(middleSlotScale) || middleSlotScale > 1 || !isPositive(rearSlotScale) || rearSlotScale > middleSlotScale ||
            !CustomerPortraitLayout.AreParametersValid(heightPixels, childPortraitScale, childPortraitRise) || !isPositive(moveSeconds) ||
            float.IsNaN(bottomCoverPixels) || float.IsInfinity(bottomCoverPixels) || bottomCoverPixels < 0 ||
            !isNonNegativeFinite(adultDownOffsetPixels) || !isNonNegativeFinite(elderlyDownOffsetPixels) ||
            !isNonNegativeFinite(childDownOffsetPixels))
        {
            Debug.LogError("[CustomerWorldQueueView] 월드·슬롯·폰트·시간·거래 이모지 연결을 확인하세요.", this);
            enabled = false;
        }
    }

    /// <summary>모델·월드 정렬 이후 같은 Visit의 목표만 바꾸고 경과 시간으로 이동한다.</summary>
    private void LateUpdate()
    {
        if (world == null || world.Controller == null) return;
        var controller = world.Controller;
        if (!ReferenceEquals(day, controller.CurrentDayProgress))
        {
            detach();
            day = controller.CurrentDayProgress;
            if (day != null) day.CustomerDeparted += handleDeparture;
        }
        if (day == null || day.State == DayProgressState.PreOpen || day.State == DayProgressState.Completed ||
            (day.State == DayProgressState.Settlement && !controller.IsSettlementPresentationPending))
        {
            clearVisuals();
            return;
        }
        seen.Clear();
        for (int i = 0; i < day.WaitingCustomers.Count; i++)
        {
            var entry = day.WaitingCustomers[i];
            var visual = getVisual(entry.Visit, entrance);
            if (visual == null) continue;
            seen.Add(entry.Visit);
            retarget(visual, slots[i]);
            visual.Body.sortingOrder = 100 - i;
            setSpeech(visual, day.GetQueueSpeech(entry));
        }
        foreach (var entry in day.LeavingCustomers)
        {
            var visual = getVisual(entry.Visit, slots[0]);
            if (visual == null) continue;
            seen.Add(entry.Visit);
            beginExit(visual, true);
            setSpeech(visual, day.GetQueueSpeech(entry));
        }
        if (day.CurrentVisit != null)
        {
            var visual = getVisual(day.CurrentVisit, entrance);
            if (visual != null)
            {
                seen.Add(day.CurrentVisit);
                retarget(visual, counter);
                visual.Body.sortingOrder = 200;
                setSpeech(visual, 0);
                showReaction(visual, day.CurrentVisit.Outcome);
            }
        }
        float delta = controller.IsPresentationBlocked ? 0 : Time.deltaTime;
        float reactionDelta = controller.IsPresentationBlocked || world.Opacity <= 0 ||
            !world.RenderRoot.gameObject.activeInHierarchy ? 0 : Time.deltaTime;
        remove.Clear();
        foreach (var pair in visuals)
        {
            var visual = pair.Value;
            if (!seen.Contains(pair.Key) && !visual.Leaving) { remove.Add(pair.Key); continue; }
            visual.Elapsed += delta;
            visual.IdleSeconds += delta;
            float duration = visual.Leaving ? controller.QueueExitSeconds : moveSeconds;
            visual.Alpha = Mathf.MoveTowards(visual.Alpha, visual.Leaving ? 0 : 1, delta / duration);
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(visual.Elapsed / duration));
            visual.DisplayHeight = Mathf.Lerp(visual.StartHeight, visual.TargetHeight, t);
            visual.RisePixels = Mathf.Lerp(visual.StartRise, visual.TargetRise, t);
            Vector3 target = visualRoot.InverseTransformPoint(visual.Target.position);
            Vector3 position = Vector3.Lerp(visual.Start, target, t);
            float envelope = Mathf.Sin(t * Mathf.PI);
            float step = visual.Elapsed * Mathf.PI * 10 + visual.Phase;
            Vector2 walk = new Vector2(Mathf.Sin(step) * 9,
                Mathf.Abs(Mathf.Cos(step)) * 15 * visual.DisplayHeight / 550f) * envelope;
            position.x += walk.x;
            position.y = visualRoot.InverseTransformPoint(counter.position).y - bottomCoverPixels -
                3 * visual.DisplayHeight / 550f + walk.y + visual.RisePixels - getDownOffset(visual.Attributes);
            visual.Root.localPosition = position;
            bool idle = !visual.Leaving && visual.Elapsed >= duration;
            float breath = idle ? (Mathf.Sin(visual.IdleSeconds * Mathf.PI * 2 / visual.BreathPeriod + visual.Phase) + 1) * .5f : 0;
            float scale = visual.DisplayHeight / visual.Body.sprite.bounds.size.y;
            float stride = Mathf.Sin(step * 2) * .025f * envelope;
            visual.Body.transform.localScale = new Vector3(scale * (1 + breath * .007f) * (1 - stride),
                scale * (1 + breath * .018f) * (1 + stride), 1);
            var bounds = visual.Body.sprite.bounds;
            Vector3 bottom = Vector3.Scale(new Vector3(bounds.center.x, bounds.min.y, 0), visual.Body.transform.localScale);
            visual.Body.transform.localPosition = -bottom + new Vector3(idle ? Mathf.Sin(visual.IdleSeconds * 2.1f / visual.BreathPeriod + visual.Phase) * .3f : 0, breath * 3 * visual.DisplayHeight / 550f, 0);
            // 환경색과 퇴장 alpha는 같은 경로에서 합성한다.
            world.ApplyCustomerLighting(visual.BodyProperties);
            visual.Body.SetPropertyBlock(visual.BodyProperties);
            visual.Body.color = ComposeColor(world.PeopleTint, visual.Leaving ? t : 0, visual.Alpha, world.Opacity);
            visual.SpeechTMP.color = new Color(1, 1, 1, world.Opacity * (visual.Abandoned ? 1 : visual.Alpha));
            // 불만은 외형 퇴장 alpha와 독립된 3초 수명을 유지하되 손님의 현재 위치를 따른다.
            visual.SpeechTMP.transform.localPosition = position + new Vector3(0, visual.DisplayHeight + 4, 0);
            updateReaction(visual, reactionDelta);
            if (visual.Leaving && visual.Elapsed >= duration && (!visual.Abandoned || !seen.Contains(pair.Key))) remove.Add(pair.Key);
        }
        foreach (var visit in remove) removeVisual(visit);
    }

    /// <summary>비활성화·씬 종료 구독 해제.</summary>
    private void OnDisable() => detach();

    /// <summary>시간대 색과 퇴장 검정·alpha를 단일 렌더 색으로 합성한다.</summary>
    /// <param name="tint">환경색.</param><param name="exit">검정 전환.</param><param name="alpha">방문 페이드.</param><param name="screenAlpha">전면 alpha.</param>
    /// <returns>최종 렌더 색.</returns>
    public static Color ComposeColor(Color tint, float exit, float alpha, float screenAlpha)
    {
        Color color = Color.Lerp(tint, Color.black, Mathf.Clamp01(exit));
        color.a = Mathf.Clamp01(alpha) * Mathf.Clamp01(screenAlpha);
        return color;
    }

    /// <summary>기존 거래 결과를 직렬화된 이모지 배열 인덱스로 변환합니다.</summary>
    /// <param name="outcome">확정 거래 결과.</param><returns>0~3 또는 표시하지 않는 -1.</returns>
    public static int GetReactionIndex(CustomerTradeOutcome outcome) => outcome switch
    {
        CustomerTradeOutcome.RegularSale => 0,
        CustomerTradeOutcome.DiscountSale => 1,
        CustomerTradeOutcome.ExploitativeSale => 2,
        CustomerTradeOutcome.PaymentRefused => 3,
        _ => -1
    };

    /// <summary>거래 완료 방문의 동일 외형을 퇴장시킨다.</summary>
    /// <param name="visit">반납된 방문.</param>
    private void handleDeparture(CustomerVisit visit) => beginExit(getVisual(visit, counter), false);

    /// <summary>퇴장 목표를 화면 오른쪽 출구로 한 번만 확정한다.</summary>
    /// <param name="visual">외형.</param><param name="abandoned">대기 만료.</param>
    private void beginExit(Visual visual, bool abandoned)
    {
        if (visual == null) return;
        if (visual.Leaving) return;
        visual.Leaving = true;
        visual.Reaction.gameObject.SetActive(false);
        visual.Abandoned = abandoned;
        retarget(visual, rightExit);
    }

    /// <summary>준비된 Sprite를 빌려 방문별 월드 객체를 만든다. 독립 로딩·해제는 하지 않는다.</summary>
    /// <param name="visit">방문 identity.</param><param name="start">첫 위치.</param><returns>방문별 표시.</returns>
    private Visual getVisual(CustomerVisit visit, Transform start)
    {
        if (visuals.TryGetValue(visit, out var visual)) return visual;

        if (!DataTableManager.Instance.GetDB<ResourceDataTable>(DataTableType.Resource).TryGetResource(visit.ResourceIdx, out var visitResData))
        {
            Debug.LogError($"[CustomerWorldQueueView] 방문 외형 리소스 FK {visit.ResourceIdx}이 ResourceDataTable에 없습니다.");
            return visual;
        }

        if (!DataTableManager.Instance.GetDB<ResourceDataTable>(DataTableType.Resource).TryGetResource(visit.SpeechIdx, out var speechResData))
        {
            Debug.LogError($"[CustomerWorldQueueView] 방문 말풍선 리소스 FK {visit.SpeechIdx}이 ResourceDataTable에 없습니다.");
            return visual;
        }

        var visitPooled = SimplePoolManager.Instance.Get<WorldVisit>(visitResData.Path);

        if(visitPooled == null)
        {
            Debug.LogError($"[CustomerWorldQueueView] 방문 외형 리소스 {visitResData.Path}을 SimplePoolManager에서 가져오지 못했습니다.");
            return visual;
        }

        var speechPooled = SimplePoolManager.Instance.Get<WorldQueueSpeech>(speechResData.Path);

        if(speechPooled == null)
        {
            SimplePoolManager.Instance.Release(visitResData.Path, visitPooled);
            Debug.LogError($"[CustomerWorldQueueView] 방문 말풍선 리소스 {speechResData.Path}을 SimplePoolManager에서 가져오지 못했습니다.");
            return visual;
        }

        var sprite = world.Controller.GetCustomerAppearanceSprite(visit.AppearanceIdx);
        var normalTexture = world.Controller.GetCustomerAppearanceNormalTexture(visit.AppearanceIdx);
        CustomerPortraitLayout layout = CustomerPortraitLayout.Calculate(visit.Attributes, heightPixels * getPerspectiveScale(start), childPortraitScale, childPortraitRise);

        speechPooled.Init(visualRoot, font);

        float phase = visuals.Count * .37f + visit.AppearanceIdx * .618f;
        visual = visitPooled.ToVisual(visit, visualRoot, start, sprite, normalTexture, world, layout, speechPooled, phase);
        visuals.Add(visit, visual);
        return visual;
    }

    /// <summary>목표가 바뀔 때 현재 위치·높이부터 이어간다. 퇴장 중에는 현재 크기를 고정한다.</summary>
    /// <param name="visual">표시.</param><param name="target">새 anchor.</param>
    private void retarget(Visual visual, Transform target)
    {
        if (visual.Target == target) return;
        visual.Start = visual.Root.localPosition;
        visual.StartHeight = visual.TargetHeight = visual.DisplayHeight;
        visual.StartRise = visual.TargetRise = visual.RisePixels;
        if (!visual.Leaving)
        {
            var layout = CustomerPortraitLayout.Calculate(visual.Attributes, heightPixels * getPerspectiveScale(target), childPortraitScale, childPortraitRise);
            visual.TargetHeight = layout.DisplayHeight;
            visual.TargetRise = layout.RisePixels;
        }
        visual.Target = target;
        visual.Elapsed = 0;
    }

    /// <summary>방문 시점의 연령 속성에 해당하는 고정 하향 오프셋을 반환한다.</summary>
    /// <param name="attributes">방문의 성별·연령 속성.</param>
    /// <returns>원근 배율을 적용하지 않는 authoring 픽셀 값.</returns>
    private float getDownOffset(CustomerAttributes attributes)
    {
        if ((attributes & CustomerAttributes.Child) != 0) return childDownOffsetPixels;
        if ((attributes & CustomerAttributes.Elderly) != 0) return elderlyDownOffsetPixels;
        return adultDownOffsetPixels;
    }

    /// <summary>Inspector 조정값이 유한한 0 이상인지 확인한다.</summary>
    /// <param name="value">하향 오프셋.</param><returns>유한한 0 이상이면 true.</returns>
    private static bool isNonNegativeFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;

    /// <summary>계산대→Slot05→Slot10의 고정 배율을 슬롯 번호로 선형 보간한다. 입구는 마지막 배율을 사용한다.</summary>
    /// <param name="anchor">계산대·대기 슬롯·입구.</param><returns>계산대 성인 높이 대비 배율.</returns>
    private float getPerspectiveScale(Transform anchor)
    {
        if (anchor == counter) return 1f;
        int slotNumber = Array.IndexOf(slots, anchor) + 1;
        if (slotNumber == 0) return rearSlotScale;
        int middle = CustomerQueue.Capacity / 2;
        return slotNumber <= middle
            ? Mathf.Lerp(1f, middleSlotScale, (float)slotNumber / middle)
            : Mathf.Lerp(middleSlotScale, rearSlotScale, (float)(slotNumber - middle) / (CustomerQueue.Capacity - middle));
    }

    /// <summary>대사 FK가 바뀔 때만 기존 테이블을 조회한다.</summary>
    /// <param name="visual">표시.</param><param name="idx">Text FK 또는 0.</param>
    private void setSpeech(Visual visual, uint idx)
    {
        if (visual.SpeechIdx == idx) return;
        visual.SpeechIdx = idx;
        visual.SpeechTMP.text = idx == 0 ? string.Empty : DataTableManager.Instance.GetDB<TextDataTable>(DataTableType.Text).Rows[idx].Text;
    }

    /// <summary>방문당 확정 결과를 한 번만 이모지 연출로 시작합니다.</summary>
    /// <param name="visual">현재 방문의 표시 상태.</param><param name="outcome">확정된 기존 거래 결과.</param>
    private void showReaction(Visual visual, CustomerTradeOutcome outcome)
    {
        int index = GetReactionIndex(outcome);
        if (index < 0 || visual.ReactionShown || visual.Leaving) return;
        visual.ReactionShown = true;
        visual.ReactionElapsed = 0;
        visual.Reaction.sprite = this.tradeReactionSprites[index];
        float scale = 64f / visual.Reaction.sprite.bounds.size.y;
        visual.Reaction.transform.localPosition = new Vector3(90, visual.DisplayHeight * .82f, 0);
        visual.Reaction.transform.localScale = Vector3.one * scale * .55f;
        visual.Reaction.color = new Color(1, 1, 1, this.world.Opacity);
        visual.Reaction.gameObject.SetActive(true);
    }

    /// <summary>1초 pop·상승·후반 fade를 표시 가능한 동안 진행합니다.</summary>
    /// <param name="visual">진행할 방문 표시.</param><param name="delta">표현 차단·전면 숨김을 제외한 표현 시간.</param>
    private void updateReaction(Visual visual, float delta)
    {
        if (!visual.Reaction.gameObject.activeSelf) return;
        visual.ReactionElapsed += delta;
        float t = Mathf.Clamp01(visual.ReactionElapsed);
        float pop = t < .2f ? Mathf.Lerp(.55f, 1.15f, t / .2f) : Mathf.Lerp(1.15f, 1, Mathf.Clamp01((t - .2f) / .2f));
        float scale = 64f / visual.Reaction.sprite.bounds.size.y;
        visual.Reaction.transform.localScale = Vector3.one * scale * pop;
        visual.Reaction.transform.localPosition = new Vector3(90, visual.DisplayHeight * .82f + 40 * Mathf.SmoothStep(0, 1, t), 0);
        visual.Reaction.color = new Color(1, 1, 1, this.world.Opacity * (1 - Mathf.Clamp01((t - .55f) / .45f)));
        if (t >= 1) visual.Reaction.gameObject.SetActive(false);
    }

    /// <summary>지연 Destroy 전에 숨겨 중복 표시를 차단한다.</summary>
    /// <param name="visit">제거할 방문.</param>
    private void removeVisual(CustomerVisit visit)
    {
        var visual = visuals[visit];
        visual.Root.gameObject.SetActive(false);
        visual.SpeechTMP.gameObject.SetActive(false);

        WorldVisit worldVisit = visual.Root.GetComponent<WorldVisit>();
        WorldQueueSpeech worldQueueSpeech = visual.Speech;
        string visitResPath = DataTableManager.Instance.GetDB<ResourceDataTable>(DataTableType.Resource).GetResourcePath(visit.ResourceIdx);
        string speechResPath = DataTableManager.Instance.GetDB<ResourceDataTable>(DataTableType.Resource).GetResourcePath(visit.SpeechIdx);

        if (worldVisit != null && !string.IsNullOrEmpty(visitResPath))
        {
            SimplePoolManager.Instance?.Release(visitResPath, worldVisit);
        }

        if(worldQueueSpeech != null && !string.IsNullOrEmpty(speechResPath))
        {
            SimplePoolManager.Instance?.Release(speechResPath, worldQueueSpeech);
        }


        visuals.Remove(visit);
    }

    /// <summary>소유 시각 객체만 정리한다.</summary>
    private void clearVisuals()
    {
        remove.Clear();
        remove.AddRange(visuals.Keys);
        foreach (var visit in remove) removeVisual(visit);
    }

    /// <summary>이전 날짜 구독과 표시를 정리한다.</summary>
    private void detach()
    {
        if (day != null) day.CustomerDeparted -= handleDeparture;
        day = null;
        clearVisuals();

        //todo : release all pooled objects
    }

    /// <summary>유한 양수 검사.</summary>
    /// <param name="value">검사값.</param><returns>유한 양수 여부.</returns>
    private static bool isPositive(float value) => value > 0 && !float.IsInfinity(value) && !float.IsNaN(value);

    /// <summary>모델과 분리된 방문 표현 상태.</summary>
    public sealed class Visual
    {
        public Transform Root, Target;
        public SpriteRenderer Body, Reaction;
        public MaterialPropertyBlock BodyProperties;
        public WorldQueueSpeech Speech;
        public TextMeshPro SpeechTMP;
        public Vector3 Start;
        public float Alpha, Elapsed, IdleSeconds, Phase, BreathPeriod, ReactionElapsed;
        public float DisplayHeight, RisePixels, StartHeight, TargetHeight, StartRise, TargetRise;
        public CustomerAttributes Attributes;
        public bool Leaving, Abandoned, ReactionShown;
        public uint SpeechIdx;

    }
}
