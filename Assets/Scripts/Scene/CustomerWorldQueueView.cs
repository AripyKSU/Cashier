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
    /// <summary>좌상단 기준 픽셀 authoring 공간의 방문 루트와 입장·계산대·퇴장 anchors.</summary>
    [SerializeField] private Transform visualRoot, counter, entrance, leftExit, rightExit;
    /// <summary>FIFO 순서 10개 위치. 원근 크기 변화는 적용하지 않는다.</summary>
    [SerializeField] private Transform[] slots;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Material spriteMaterial;
    /// <summary>전면 로컬 픽셀 기준 높이·하단 가림 보정과 이동 초. 원근 배율은 없다.</summary>
    [SerializeField] private float heightPixels = 430, bottomCoverPixels = 12, moveSeconds = .65f;
    private readonly Dictionary<CustomerVisit, Visual> visuals = new Dictionary<CustomerVisit, Visual>();
    private readonly HashSet<CustomerVisit> seen = new HashSet<CustomerVisit>();
    private readonly List<CustomerVisit> remove = new List<CustomerVisit>();
    private readonly System.Random exitRandom = new System.Random();
    private DayProgress day;

    /// <summary>소유 중인 방문별 표시 객체 수.</summary>
    public int VisualCount => visuals.Count;

    /// <summary>필수 연결·시간을 검사한다.</summary>
    private void Start()
    {
        if (world == null || visualRoot == null || counter == null || entrance == null || leftExit == null || rightExit == null ||
            font == null || spriteMaterial == null || slots == null || slots.Length != CustomerQueue.Capacity ||
            Array.Exists(slots, x => x == null) || !isPositive(heightPixels) || !isPositive(moveSeconds) ||
            float.IsNaN(bottomCoverPixels) || float.IsInfinity(bottomCoverPixels) || bottomCoverPixels < 0)
        {
            Debug.LogError("[CustomerWorldQueueView] 월드·슬롯·폰트·시간 연결을 확인하세요.", this);
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
            seen.Add(entry.Visit);
            retarget(visual, slots[i]);
            visual.Body.sortingOrder = 100 - i;
            setSpeech(visual, day.GetQueueSpeech(entry));
        }
        foreach (var entry in day.LeavingCustomers)
        {
            var visual = getVisual(entry.Visit, slots[0]);
            seen.Add(entry.Visit);
            beginExit(visual, true);
            setSpeech(visual, day.GetQueueSpeech(entry));
        }
        if (day.CurrentVisit != null)
        {
            var visual = getVisual(day.CurrentVisit, entrance);
            seen.Add(day.CurrentVisit);
            retarget(visual, counter);
            visual.Body.sortingOrder = 200;
            setSpeech(visual, 0);
        }
        float delta = controller.IsPresentationPaused ? 0 : Time.deltaTime;
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
            Vector3 target = visualRoot.InverseTransformPoint(visual.Target.position);
            Vector3 position = Vector3.Lerp(visual.Start, target, t);
            position.y = visualRoot.InverseTransformPoint(counter.position).y - bottomCoverPixels - 3 * heightPixels / 550f;
            visual.Root.localPosition = position;
            bool idle = !visual.Leaving && visual.Elapsed >= duration;
            float breath = idle ? (Mathf.Sin(visual.IdleSeconds * Mathf.PI * 2 / visual.BreathPeriod + visual.Phase) + 1) * .5f : 0;
            float scale = heightPixels / visual.Body.sprite.bounds.size.y;
            visual.Body.transform.localScale = new Vector3(scale * (1 + breath * .007f), scale * (1 + breath * .018f), 1);
            var bounds = visual.Body.sprite.bounds;
            Vector3 bottom = Vector3.Scale(new Vector3(bounds.center.x, bounds.min.y, 0), visual.Body.transform.localScale);
            visual.Body.transform.localPosition = -bottom + new Vector3(idle ? Mathf.Sin(visual.IdleSeconds * 2.1f / visual.BreathPeriod + visual.Phase) * .3f : 0, breath * 3 * heightPixels / 550f, 0);
            // 환경색과 퇴장 alpha는 같은 경로에서 합성한다.
            visual.Body.color = ComposeColor(world.PeopleTint, visual.Leaving ? t : 0, visual.Alpha, world.Opacity);
            visual.Speech.color = new Color(1, 1, 1, world.Opacity * (visual.Abandoned ? 1 : visual.Alpha));
            // 불만은 이탈 당시 위치에 남겨 이미지의 .45초 퇴장과 모델의 3초 대사를 분리한다.
            visual.Speech.transform.localPosition = (visual.Abandoned ? visual.SpeechPosition : position) + new Vector3(0, heightPixels + 4, 0);
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

    /// <summary>거래 완료 방문의 동일 외형을 퇴장시킨다.</summary>
    /// <param name="visit">반납된 방문.</param>
    private void handleDeparture(CustomerVisit visit) => beginExit(getVisual(visit, counter), false);

    /// <summary>퇴장 목표와 불만 위치를 한 번만 확정한다.</summary>
    /// <param name="visual">외형.</param><param name="abandoned">대기 만료.</param>
    private void beginExit(Visual visual, bool abandoned)
    {
        if (visual.Leaving) return;
        visual.Leaving = true;
        visual.Abandoned = abandoned;
        visual.SpeechPosition = visual.Root.localPosition;
        retarget(visual, exitRandom.Next(2) == 0 ? leftExit : rightExit);
    }

    /// <summary>준비된 Sprite를 빌려 방문별 월드 객체를 만든다. 독립 로딩·해제는 하지 않는다.</summary>
    /// <param name="visit">방문 identity.</param><param name="start">첫 위치.</param><returns>방문별 표시.</returns>
    private Visual getVisual(CustomerVisit visit, Transform start)
    {
        if (visuals.TryGetValue(visit, out var visual)) return visual;
        var sprite = world.Controller.GetCustomerAppearanceSprite(visit.AppearanceIdx);
        var root = new GameObject("Visit " + visit.AppearanceIdx + "/" + visit.DispositionIdx).transform;
        root.SetParent(visualRoot, false);
        root.position = start.position;
        var body = new GameObject("Appearance", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        body.transform.SetParent(root, false);
        body.sprite = sprite;
        body.sharedMaterial = spriteMaterial;
        body.color = Color.clear;
        var speech = new GameObject("Queue Speech", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
        speech.transform.SetParent(visualRoot, false);
        speech.font = font;
        speech.fontSize = 160; // 월드 TMP의 1/10 단위 보정: authoring 좌표 16px.
        speech.alignment = TextAlignmentOptions.Center;
        speech.rectTransform.sizeDelta = new Vector2(220, 48);
        speech.rectTransform.pivot = new Vector2(.5f, 0);
        speech.text = string.Empty;
        speech.renderer.sortingOrder = 300;
        float phase = visuals.Count * .37f + visit.AppearanceIdx * .618f;
        visual = new Visual { Root = root, Body = body, Speech = speech, Phase = phase,
            BreathPeriod = 2.9f * Mathf.Lerp(.88f, 1.12f, Mathf.Repeat(phase, 1)) };
        visuals.Add(visit, visual);
        return visual;
    }

    /// <summary>목표가 바뀔 때 현재 위치부터 이어간다.</summary>
    /// <param name="visual">표시.</param><param name="target">새 anchor.</param>
    private void retarget(Visual visual, Transform target)
    {
        if (visual.Target == target) return;
        visual.Start = visual.Root.localPosition;
        visual.Target = target;
        visual.Elapsed = 0;
    }

    /// <summary>대사 FK가 바뀔 때만 기존 테이블을 조회한다.</summary>
    /// <param name="visual">표시.</param><param name="idx">Text FK 또는 0.</param>
    private void setSpeech(Visual visual, uint idx)
    {
        if (visual.SpeechIdx == idx) return;
        visual.SpeechIdx = idx;
        visual.Speech.text = idx == 0 ? string.Empty : DataTableManager.Instance.GetDB<TextDataTable>(DataTableType.Text).Rows[idx].Text;
    }

    /// <summary>지연 Destroy 전에 숨겨 중복 표시를 차단한다.</summary>
    /// <param name="visit">제거할 방문.</param>
    private void removeVisual(CustomerVisit visit)
    {
        var visual = visuals[visit];
        visual.Root.gameObject.SetActive(false);
        visual.Speech.gameObject.SetActive(false);
        Destroy(visual.Root.gameObject);
        Destroy(visual.Speech.gameObject);
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
    }

    /// <summary>유한 양수 검사.</summary>
    /// <param name="value">검사값.</param><returns>유한 양수 여부.</returns>
    private static bool isPositive(float value) => value > 0 && !float.IsInfinity(value) && !float.IsNaN(value);

    /// <summary>모델과 분리된 방문 표현 상태.</summary>
    private sealed class Visual
    {
        public Transform Root, Target;
        public SpriteRenderer Body;
        public TextMeshPro Speech;
        public Vector3 Start, SpeechPosition;
        public float Alpha, Elapsed, IdleSeconds, Phase, BreathPeriod;
        public bool Leaving, Abandoned;
        public uint SpeechIdx;
    }
}
