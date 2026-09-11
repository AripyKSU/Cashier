using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>공유 게임 씬의 실제 방문을 관찰해 줄·계산대·퇴장 외형을 이동한다. 모델·거래·시계는 변경하지 않는다.</summary>
public sealed class CustomerQueueView : MonoBehaviour
{
    /// <summary>대기열 옵션을 켠 기존 화면 조립자.</summary>
    [SerializeField] private GameUIController controller;
    /// <summary>정면 화면의 외형 뒤에 있는 시각 루트. component 자체는 비활성 정면 밖에 둔다.</summary>
    [SerializeField] private RectTransform visualRoot;
    /// <summary>기존 계산대 외형. Image 렌더만 대신하며 자식 debuglabel은 보존한다.</summary>
    [SerializeField] private Image counterAppearance;
    /// <summary>FIFO 순서대로 편집할 슬롯 10개.</summary>
    [SerializeField] private RectTransform[] slots;
    /// <summary>신규 방문의 시작 위치.</summary>
    [SerializeField] private RectTransform entrance;
    /// <summary>임의 좌우 퇴장 위치.</summary>
    [SerializeField] private RectTransform leftExit;
    /// <summary>임의 좌우 퇴장 위치.</summary>
    [SerializeField] private RectTransform rightExit;
    /// <summary>기존 한국어 TMP 폰트.</summary>
    [SerializeField] private TMP_FontAsset font;
    /// <summary>현재 위치에서 새 슬롯까지 이동하는 초.</summary>
    [SerializeField, Min(0.01f)] private float moveSeconds = 0.65f;
    /// <summary>visualRoot 로컬 픽셀. 현재45외형 최대 투명 여백(430높이·1.018배 호흡에서9.435px 미만)의 올림+2px. 외형/최대높이 변경 시 재검증한다.</summary>
    [SerializeField, Min(0)] private float bottomCoverPixels = 12f;

    private readonly Dictionary<CustomerVisit, Visual> visuals = new Dictionary<CustomerVisit, Visual>();
    private readonly HashSet<CustomerVisit> seen = new HashSet<CustomerVisit>();
    private readonly List<CustomerVisit> remove = new List<CustomerVisit>();
    private readonly System.Random exitRandom = new System.Random();
    private DayProgress day;
    private bool originalImageEnabled;
    private bool isBound;

    /// <summary>필수 연결과 Inspector 시간을 검사하고 원래 renderer 상태를 보존한다.</summary>
    private void Awake()
    {
        if (controller == null || visualRoot == null || counterAppearance == null || entrance == null ||
            leftExit == null || rightExit == null || font == null || slots == null || slots.Length != CustomerQueue.Capacity ||
            Array.Exists(slots, x => x == null) || !isPositive(moveSeconds) ||
            float.IsNaN(bottomCoverPixels) || float.IsInfinity(bottomCoverPixels) || bottomCoverPixels < 0)
        {
            Debug.LogError("[CustomerQueueView] 슬롯/anchor/폰트/양수 시간을 확인하세요.", this);
            enabled = false;
            return;
        }
        originalImageEnabled = counterAppearance.enabled;
        isBound = true;
    }

    /// <summary>모델 갱신 이후 같은 Visit의 목표만 변경하고 프레임 경과 시간으로 이동한다.</summary>
    private void LateUpdate()
    {
        if (controller == null || visualRoot == null || counterAppearance == null) return;
        var nextDay = controller.CurrentDayProgress;
        if (!ReferenceEquals(day, nextDay))
        {
            detach();
            day = nextDay;
            if (day != null) day.CustomerDeparted += handleDeparture;
        }
        if (day == null || !day.UsesCustomerQueue)
        {
            counterAppearance.enabled = originalImageEnabled;
            return;
        }
        counterAppearance.enabled = false;
        if (day.State == DayProgressState.PreOpen || (day.State == DayProgressState.Settlement && !controller.IsSettlementPresentationPending) || day.State == DayProgressState.Completed)
        {
            clearVisuals();
            return;
        }
        seen.Clear();
        for (int i = 0; i < day.WaitingCustomers.Count; i++)
        {
            var entry = day.WaitingCustomers[i];
            var visual = getVisual(entry.Visit, entrance.position);
            seen.Add(entry.Visit);
            // 원근 배율 없이 계산대와 대기열의 표시 높이를 통일한다.
            retarget(visual, slots[i], counterSize());
            visual.Rect.SetAsFirstSibling(); // 뒤쪽 번호가 앞 손님의 외형을 가리지 않는다.
            setSpeech(visual, day.GetQueueSpeech(entry));
        }
        foreach (var entry in day.LeavingCustomers)
        {
            var visual = getVisual(entry.Visit, slots[0].position);
            seen.Add(entry.Visit);
            beginExit(visual, true);
            setSpeech(visual, day.GetQueueSpeech(entry));
        }
        if (day.CurrentVisit != null)
        {
            var visual = getVisual(day.CurrentVisit, entrance.position);
            seen.Add(day.CurrentVisit);
            retarget(visual, counterAppearance.rectTransform, counterSize());
            setSpeech(visual, 0); // 실제 계산대 대사와 debuglabel은 기존 Presenter가 표시한다.
            visual.Rect.SetAsLastSibling();
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
            // 위치 재지정과 독립적으로 현재 alpha에서 이어가며 퇴장 중 이미지만 검게 전환한다.
            visual.Fade.alpha = Mathf.MoveTowards(visual.Fade.alpha, visual.Leaving ? 0 : 1, delta / duration);
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(visual.Elapsed / duration));
            if (visual.Leaving) visual.Image.color = Color.Lerp(visual.ExitColor, Color.black, t);
            Vector3 target = visual.Target.TransformPoint(new Vector3(visual.Target.rect.center.x, visual.Target.rect.yMin, 0));
            visual.Rect.sizeDelta = Vector2.Lerp(visual.StartSize, visual.TargetSize, t);
            visual.Rect.position = coveredBottom(Vector3.Lerp(visual.Start, target, t), visual.Rect.rect.height);
            // 이미지 child는 기존 호흡을 유지하며 root는 최대 상승폭만큼 가림선 아래에 둔다.
            bool idle = !visual.Leaving && visual.Elapsed >= duration;
            float breath = idle ? (Mathf.Sin(visual.IdleSeconds * Mathf.PI * 2 / visual.BreathPeriod + visual.Phase) + 1) * .5f : 0;
            visual.Body.localScale = new Vector3(1 + breath * .007f, 1 + breath * .018f, 1);
            visual.Body.anchoredPosition = idle ? new Vector2(Mathf.Sin(visual.IdleSeconds * 2.1f / visual.BreathPeriod + visual.Phase) * .3f, breath * 3 * visual.Rect.rect.height / 550f) : Vector2.zero;
            if (visual.Leaving && visual.Elapsed >= duration && (!visual.Abandoned || !seen.Contains(pair.Key))) remove.Add(pair.Key);
        }
        foreach (var visit in remove) removeVisual(visit);
    }

    /// <summary>논리적 소유권 반납을 관찰해 원래 외형을 퇴장시킨다. 모델 callback은 호출하지 않는다.</summary>
    /// <param name="visit">거래 완료 후 퇴장하는 동일 방문.</param>
    private void handleDeparture(CustomerVisit visit)
    {
        if (day?.UsesCustomerQueue != true) return;
        beginExit(getVisual(visit, counterAppearance.rectTransform.position), false);
    }

    /// <summary>현재 위치에서 임의 exit로 한 번 이동하며 불만 표시 수명을 따로 확보한다.</summary>
    /// <param name="visual">방문별 단일 외형.</param><param name="abandoned">대기 만료 여부.</param>
    private void beginExit(Visual visual, bool abandoned)
    {
        if (visual.Leaving) return;
        visual.Leaving = true;
        visual.Abandoned = abandoned;
        visual.ExitColor = visual.Image.color;
        retarget(visual, exitRandom.Next(2) == 0 ? leftExit : rightExit, visual.Rect.sizeDelta);
    }

    /// <summary>실제 외형 CSV를 읽어 방문 하나에 시각 객체 하나만 생성한다.</summary>
    /// <param name="visit">생성 시점에 확정된 방문.</param><param name="start">최초 화면 위치.</param>
    /// <returns>재사용할 방문별 외형.</returns>
    private Visual getVisual(CustomerVisit visit, Vector3 start)
    {
        if (visuals.TryGetValue(visit, out var visual)) return visual;
        var sprite = controller.GetCustomerAppearanceSprite(visit.AppearanceIdx);
        var obj = new GameObject("Visit " + visit.AppearanceIdx + "/" + visit.DispositionIdx, typeof(RectTransform), typeof(CanvasGroup));
        var body = new GameObject("Appearance", typeof(RectTransform), typeof(Image));
        var fade = obj.GetComponent<CanvasGroup>();
        fade.alpha = 0;
        fade.interactable = false;
        fade.blocksRaycasts = false;
        var rect = (RectTransform)obj.transform;
        rect.SetParent(visualRoot, false);
        rect.pivot = new Vector2(.5f, 0);
        float aspect = sprite.rect.width / sprite.rect.height;
        float height = counterSize().y;
        rect.sizeDelta = new Vector2(height * aspect, height);
        rect.position = coveredBottom(start, rect.sizeDelta.y);
        var bodyRect = (RectTransform)body.transform;
        bodyRect.SetParent(rect, false);
        bodyRect.anchorMin = Vector2.zero; bodyRect.anchorMax = Vector2.one;
        bodyRect.pivot = new Vector2(.5f, 0); bodyRect.sizeDelta = Vector2.zero;
        var image = body.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = false;
        var speech = new GameObject("Speech", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        speech.transform.SetParent(rect, false);
        speech.font = font;
        speech.text = string.Empty;
        speech.fontSize = 16;
        speech.alignment = TextAlignmentOptions.Center;
        speech.raycastTarget = false;
        speech.rectTransform.anchorMin = speech.rectTransform.anchorMax = new Vector2(0.5f, 1);
        speech.rectTransform.pivot = new Vector2(0.5f, 0);
        speech.rectTransform.anchoredPosition = new Vector2(0, 4);
        speech.rectTransform.sizeDelta = new Vector2(220, 48);
        float phase = visuals.Count * .37f + visit.AppearanceIdx * .618f;
        visual = new Visual { Rect = rect, Body = bodyRect, Image = image, Speech = speech, Fade = fade,
            Aspect = aspect,
            Phase = phase, BreathPeriod = 2.9f * Mathf.Lerp(.88f, 1.12f, Mathf.Repeat(phase, 1)) };
        visuals.Add(visit, visual);
        return visual;
    }

    /// <summary>목표 슬롯이 달라졌을 때 현재 위치부터 다시 이동한다.</summary>
    /// <param name="visual">외형.</param><param name="target">새 목표.</param><param name="size">도착 크기.</param>
    private void retarget(Visual visual, RectTransform target, Vector2 size)
    {
        size.x = size.y * visual.Aspect;
        if (visual.Target == target && visual.TargetSize == size) return;
        visual.Start = visual.Rect.position;
        visual.StartSize = visual.Rect.sizeDelta;
        visual.Target = target;
        visual.TargetSize = size;
        visual.Elapsed = 0;
    }

    /// <summary>계산대 외형 크기를 대기열의 로컬 단위로 변환한다.</summary>
    /// <returns>현재 손님 크기의 기준.</returns>
    private Vector2 counterSize()
    {
        var rect = counterAppearance.rectTransform;
        Vector3 size = visualRoot.InverseTransformVector(rect.TransformVector(rect.rect.size));
        return new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
    }

    /// <summary>좌우 경로는 유지하고 보간 중인 높이의 최대 bob을 고려해 잘린 하단을 계산대 뒤에 고정한다.</summary>
    /// <param name="position">좌우 이동의 world 위치.</param><param name="height">현재 visualRoot 로컬 높이.</param>
    /// <returns>Canvas 배율을 보존한 world 하단 위치.</returns>
    private Vector3 coveredBottom(Vector3 position, float height)
    {
        var anchor = counterAppearance.rectTransform;
        var cutoff = visualRoot.InverseTransformPoint(anchor.TransformPoint(new Vector3(anchor.rect.center.x, anchor.rect.yMin, 0)));
        var local = visualRoot.InverseTransformPoint(position);
        local.y = cutoff.y - 3f * height / 550f - bottomCoverPixels;
        return visualRoot.TransformPoint(local);
    }

    /// <summary>대사 PK가 바뀔 때만 텍스트를 조회한다. 외형 PK를 방문 식별자로 쓰지 않는다.</summary>
    /// <param name="visual">대사 대상.</param><param name="idx">Text FK 또는 0.</param>
    private void setSpeech(Visual visual, uint idx)
    {
        if (visual.SpeechIdx == idx) return;
        visual.SpeechIdx = idx;
        visual.Speech.text = idx == 0 ? string.Empty : DataTableManager.Instance.GetDB<TextDataTable>(DataTableType.Text).Rows[idx].Text;
    }

    /// <summary>동일 프레임의 재활성화에도 중복 렌더가 남지 않도록 숨긴 뒤 제거한다.</summary>
    /// <param name="visit">제거할 방문 키.</param>
    private void removeVisual(CustomerVisit visit)
    {
        var visual = visuals[visit];
        visual.Rect.gameObject.SetActive(false);
        Destroy(visual.Rect.gameObject);
        visuals.Remove(visit);
    }

    /// <summary>하루 종료·화면 제거 시 모든 시각 객체를 정리한다.</summary>
    private void clearVisuals()
    {
        foreach (var visual in visuals.Values)
            if (visual.Rect != null) { visual.Rect.gameObject.SetActive(false); Destroy(visual.Rect.gameObject); }
        visuals.Clear();
    }

    /// <summary>이전 하루 구독과 외형을 해제한다.</summary>
    private void detach()
    {
        if (day != null) day.CustomerDeparted -= handleDeparture;
        day = null;
        clearVisuals();
    }

    /// <summary>비활성·씬 파괴 시 연출과 구독을 정리하고 원래 renderer를 복원한다.</summary>
    private void OnDisable()
    {
        detach();
        if (isBound && counterAppearance != null) counterAppearance.enabled = originalImageEnabled;
    }

    /// <summary>Inspector 수치의 유한 양수 여부를 확인한다.</summary>
    /// <param name="value">초 또는 픽셀.</param><returns>유한 양수이면 true.</returns>
    private static bool isPositive(float value) => value > 0 && !float.IsInfinity(value) && !float.IsNaN(value);

    /// <summary>모델을 변경하지 않는 방문별 이동·대사 상태.</summary>
    private sealed class Visual
    {
        public RectTransform Rect, Target;
        public RectTransform Body;
        /// <summary>퇴장 중 이미지 tint만 변경하며 텍스트 색은 보존한다.</summary>
        public Image Image;
        public Color ExitColor;
        public float Aspect, IdleSeconds, Phase, BreathPeriod;
        public TextMeshProUGUI Speech;
        /// <summary>외형·대사를 함께 표시하며 이동 목표 변경에도 현재 투명도를 유지한다.</summary>
        public CanvasGroup Fade;
        public Vector3 Start;
        public Vector2 StartSize, TargetSize;
        public float Elapsed;
        public bool Leaving, Abandoned;
        public uint SpeechIdx;
    }

}
