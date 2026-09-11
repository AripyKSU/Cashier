using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>독립 감독관 패널의 대사·입력·검은 틴트 페이드만 담당한다. 진행 이력은 세션 소유다.</summary>
public sealed class InspectorPresenter : MonoBehaviour
{
    /// <summary>감독관 초상 표시.</summary>
    [SerializeField] private Image portrait;
    /// <summary>대사 표시.</summary>
    [SerializeField] private TextMeshProUGUI dialogue;
    /// <summary>대사·버튼의 별도 알파. 초상 틴트를 적용하지 않는다.</summary>
    [SerializeField] private CanvasGroup dialogueGroup;
    /// <summary>현재 줄을 확인하는 버튼.</summary>
    [SerializeField] private Button nextButton;
    /// <summary>입장·퇴장 각각의 유한한 양수 초.</summary>
    [SerializeField, Min(0.01f)] private float fadeSeconds = 0.4f;
    private InspectorEventSnapshot current;
    private CancellationTokenSource presentationCancellation;
    private Func<bool> isPaused;
    private bool hasPresentation;
    private bool entered;
    private bool exiting;
    private bool canReveal;
    private Color originalColor;

    /// <summary>현재 줄에 대한 입력 요청. 날짜·이벤트·줄 번호를 함께 전달한다.</summary>
    public event Action<InspectorEventSnapshot> NextRequested;
    /// <summary>이번 패널 수명에서 실제 퇴장을 마친 요청.</summary>
    public event Action<InspectorEventSnapshot> ExitCompleted;
    /// <summary>표현 실패를 화면 소유자에게 전달한다.</summary>
    public event Action<Exception> Failed;

    /// <summary>직렬화된 색을 보관하고 단일 버튼 콜백을 연결한다.</summary>
    private void Awake()
    {
        if (portrait != null) originalColor = portrait.color;
        if (nextButton != null) nextButton.onClick.AddListener(handleNext);
    }

    /// <summary>부모의 바인딩이 자식 활성화보다 먼저 실행된 경우 준비된 연출을 재개한다.</summary>
    private void OnEnable()
    {
        if (hasPresentation && canReveal) Present(current, dialogue.text, portrait.sprite, true, isPaused);
    }

    /// <summary>패널을 숨기면 진행 중 연출과 늦은 완료 콜백을 취소한다.</summary>
    private void OnDisable()
    {
        cancelPresentation();
        hasPresentation = false;
        entered = false;
        exiting = false;
    }

    /// <summary>컴포넌트 수명 종료 시 버튼 구독을 정리한다.</summary>
    private void OnDestroy()
    {
        cancelPresentation();
        if (nextButton != null) nextButton.onClick.RemoveListener(handleNext);
    }

    /// <summary>필수 prefab 연결과 페이드 시간을 확인한다.</summary>
    /// <exception cref="InvalidOperationException">참조 또는 시간 설정 오류.</exception>
    public void ValidateReferences()
    {
        if (portrait == null || dialogue == null || dialogueGroup == null || nextButton == null ||
            float.IsNaN(fadeSeconds) || float.IsInfinity(fadeSeconds) || fadeSeconds <= 0)
            throw new InvalidOperationException("InspectorPanel: 필수 참조 및 양수 fadeSeconds 필요");
    }

    /// <summary>모델 스냅샷을 표시한다. 로딩 덮개 아래에서는 연출·입력을 시작하지 않는다.</summary>
    /// <param name="snapshot">세션의 현재 감독관 상태.</param>
    /// <param name="text">검증된 Text 문구.</param>
    /// <param name="sprite">검증된 초상.</param>
    /// <param name="reveal">전체 화면 준비가 끝났는지 여부.</param>
    /// <param name="pauseQuery">화면 소유자의 일시정지 질의.</param>
    /// <exception cref="InvalidOperationException">표시 데이터 누락.</exception>
    public void Present(InspectorEventSnapshot snapshot, string text, Sprite sprite, bool reveal, Func<bool> pauseQuery)
    {
        ValidateReferences();
        if (sprite == null || string.IsNullOrWhiteSpace(text) || snapshot.Phase == InspectorEventPhase.Completed)
            throw new InvalidOperationException("InspectorPanel: 표시할 대사·초상·진행 필요");
        bool newEvent = !hasPresentation || current.Day != snapshot.Day || current.EventIdx != snapshot.EventIdx;
        current = snapshot;
        isPaused = pauseQuery;
        canReveal = reveal;
        if (newEvent)
        {
            cancelPresentation();
            hasPresentation = true;
            entered = false;
            exiting = false;
            portrait.sprite = sprite;
            portrait.color = new Color(0, 0, 0, 0);
            dialogueGroup.alpha = 0;
            nextButton.interactable = false;
        }
        dialogue.text = text;
        if (!reveal || !isActiveAndEnabled) return;
        if (snapshot.Phase == InspectorEventPhase.AwaitingExit)
        {
            if (exiting) return;
            cancelPresentation();
            exiting = true;
            nextButton.interactable = false;
            startFade(false, snapshot);
        }
        else if (!entered && presentationCancellation == null) startFade(true, snapshot);
        else if (entered) nextButton.interactable = !(isPaused?.Invoke() ?? false);
    }

    /// <summary>현재 대사 스냅샷으로만 입력을 보낸다.</summary>
    private void handleNext()
    {
        if (!entered || exiting || !nextButton.interactable || (isPaused?.Invoke() ?? false)) return;
        nextButton.interactable = false;
        NextRequested?.Invoke(current);
    }

    /// <summary>기존 연출을 취소한 후 새 연출을 관찰한다.</summary>
    /// <param name="entering">입장이면 true.</param>
    /// <param name="snapshot">연출 시작 상태.</param>
    private void startFade(bool entering, InspectorEventSnapshot snapshot)
    {
        presentationCancellation = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        fadeAsync(entering, snapshot, presentationCancellation.Token).Forget(exception => Failed?.Invoke(exception));
    }

    /// <summary>경과 시간 기반으로 색과 알파를 바꾸고 유효한 수명에서만 완료한다.</summary>
    /// <param name="entering">입장 또는 퇴장.</param>
    /// <param name="snapshot">시작한 모델 상태.</param>
    /// <param name="token">비활성·파괴 시 취소.</param>
    /// <returns>연출 완료 또는 정상 취소.</returns>
    private async UniTask fadeAsync(bool entering, InspectorEventSnapshot snapshot, CancellationToken token)
    {
        Color from = portrait.color;
        Color to = entering ? originalColor : new Color(0, 0, 0, 0);
        float fromAlpha = dialogueGroup.alpha;
        float elapsed = 0;
        try
        {
            while (elapsed < fadeSeconds)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                token.ThrowIfCancellationRequested();
                if (isPaused?.Invoke() ?? false) continue;
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / fadeSeconds);
                portrait.color = Color.Lerp(from, to, progress);
                dialogueGroup.alpha = Mathf.Lerp(fromAlpha, entering ? 1 : 0, progress);
            }
            token.ThrowIfCancellationRequested();
            if (entering)
            {
                entered = true;
                nextButton.interactable = true;
            }
            else ExitCompleted?.Invoke(snapshot);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    /// <summary>진행·일시정지를 바꾸지 않고 표현 수명만 종료한다.</summary>
    private void cancelPresentation()
    {
        presentationCancellation?.Cancel();
        presentationCancellation?.Dispose();
        presentationCancellation = null;
    }
}
