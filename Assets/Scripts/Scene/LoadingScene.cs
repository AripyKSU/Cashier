using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;
using Cysharp.Threading.Tasks;

/// <summary>로딩 화면 애니메이션과 실패 재시도 UI의 수명을 관리한다.</summary>
public class LoadingScene : MonoBehaviour
{
    private const float MinimumDisplayDurationSeconds = 2.7f;
    private const float FadeInDurationSeconds = 0.3f;

    [Header("Loading Root")]
    [SerializeField] private CanvasGroup canvasGroup;
    /// <summary>첫 프레임부터 재생하고 unscaled time으로 갱신하는 로딩 애니메이터.</summary>
    [Header("Loading Animation")]
    [SerializeField] private Animator loadingAnimator;
    /// <summary>정상 로딩 중에는 숨기고 전환 실패 때만 표시하는 오류 문구.</summary>
    [Header("Failure UI")]
    [SerializeField] private TextMeshProUGUI errorText;
    /// <summary>엔딩 전환 실패 때만 표시하는 명시적 재시도 버튼.</summary>
    [SerializeField] private Button endingRetryButton;
    private GameSceneManager.SceneName? retryDestination;
    private CancellationTokenSource minimumDisplayCancellation;

    private void Awake()
    {
        if (endingRetryButton != null)
        {
            endingRetryButton.gameObject.SetActive(false);
            endingRetryButton.onClick.AddListener(retryEnding);
        }
        if (errorText != null)
            errorText.gameObject.SetActive(false);
        if (canvasGroup == null)
        {
            // LoadingScene 오브젝트와 Canvas가 별도 root인 기존 씬 구조를 유지하되,
            // 다른 씬의 Canvas를 잡지 않도록 현재 씬 root 안에서만 찾는다.
            var roots = gameObject.scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length && canvasGroup == null; i++)
            {
                var canvas = roots[i].GetComponentInChildren<Canvas>(true);
                if (canvas == null) continue;

                canvasGroup = canvas.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            }
        }
        if (canvasGroup == null)
        {
            Debug.LogError("LoadingScene requires a CanvasGroup on its loading Canvas.", this);
            enabled = false;
            return;
        }
        canvasGroup.alpha = 0f; // start invisible
    }

    /// <summary>화면 진입 때 애니메이션을 초기화하고 unscaled fade-in을 시작한다.</summary>
    private void OnEnable()
    {
        minimumDisplayCancellation?.Cancel();
        minimumDisplayCancellation?.Dispose();
        minimumDisplayCancellation = new CancellationTokenSource();
        RestartLoadingAnimation();
        if (canvasGroup == null)
            return;

        // 로딩 화면은 이전 씬의 timeScale이 0이어도 지정된 속도로 나타나야 한다.
        DOTween.Kill(canvasGroup);
        canvasGroup.alpha = 0f;
        DOTween.To(() => canvasGroup.alpha, value => canvasGroup.alpha = value, 1f, FadeInDurationSeconds)
            .SetTarget(canvasGroup).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    /// <summary>목적지 씬을 활성화하기 전에 로딩 화면을 최소 시간 동안 표시한다.</summary>
    /// <returns>최소 표시 시간이 지난 뒤 완료되는 작업.</returns>
    public UniTask WaitForMinimumDisplayAsync()
    {
        var cancellationToken = minimumDisplayCancellation?.Token ?? this.GetCancellationTokenOnDestroy();
        return UniTask.Delay(TimeSpan.FromSeconds(MinimumDisplayDurationSeconds), ignoreTimeScale: true,
            cancellationToken: cancellationToken);
    }

    /// <summary>정산을 재실행하지 않는 엔딩 전환 재시도를 안내한다.</summary>
    public void ShowEndingRetry()
    {
        retryDestination = null;
        ShowError("엔딩을 불러오지 못했습니다. 결과는 보존됩니다.");
        if (endingRetryButton != null)
        {
            endingRetryButton.gameObject.SetActive(true);
            endingRetryButton.interactable = true;
        }
    }

    /// <summary>새 게임 씬 로딩 실패 후 Gameplay 전환만 재시도한다.</summary>
    public void ShowNewGameRetry()
    {
        ShowEndingRetry();
        retryDestination = GameSceneManager.SceneName.Init;
        ShowError("새 게임을 불러오지 못했습니다. 다시 시도해 주세요.");
        SetRetryButtonLabel("새 게임 다시 불러오기");
    }

    /// <summary>엔딩에서 메뉴로 돌아가는 전환만 재시도한다. 새 게임은 자동 시작하지 않는다.</summary>
    public void ShowHubRetry()
    {
        ShowEndingRetry();
        retryDestination = GameSceneManager.SceneName.Hub;
        ShowError("메뉴를 불러오지 못했습니다. 다시 시도해 주세요.");
        SetRetryButtonLabel("메뉴 다시 불러오기");
    }

    /// <summary>동일 종료 결과로 씬 전환만 다시 요청한다.</summary>
    private void retryEnding()
    {
        if (endingRetryButton == null || GameSceneManager.Instance == null)
            return;
        endingRetryButton.interactable = false;
        if (retryDestination == GameSceneManager.SceneName.Init) GameSceneManager.Instance.RestartGameAsync().Forget();
        else if (retryDestination == GameSceneManager.SceneName.Hub) GameSceneManager.Instance.ReturnToHubAsync().Forget();
        else GameSceneManager.Instance.TransitionToFinalEndingAsync().Forget();
    }

    /// <summary>오류 문구를 활성화하고 지정한 메시지를 표시한다.</summary>
    /// <param name="message">사용자에게 표시할 전환 실패 메시지.</param>
    private void ShowError(string message)
    {
        if (errorText != null)
        {
            errorText.gameObject.SetActive(true);
            errorText.text = message;
        }
    }

    /// <summary>재시도 버튼의 자식 TMP 문구를 지정한다.</summary>
    /// <param name="label">버튼에 표시할 재시도 문구.</param>
    private void SetRetryButtonLabel(string label)
    {
        if (endingRetryButton == null)
            return;

        var labelText = endingRetryButton.GetComponentInChildren<TMP_Text>();
        if (labelText != null)
            labelText.text = label;
    }

    /// <summary>로딩 애니메이션을 첫 프레임에서 다시 시작한다.</summary>
    private void RestartLoadingAnimation()
    {
        if (loadingAnimator == null)
            return;

        loadingAnimator.enabled = true;
        loadingAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        loadingAnimator.Rebind();
        loadingAnimator.Update(0f);
    }

    /// <summary>Single 씬 전환으로 파괴되기 전에 tween과 애니메이터를 정리한다.</summary>
    private void OnDisable()
    {
        // 비활성화 이후의 callback이 파괴된 CanvasGroup과 UI를 접근하지 않게 한다.
        if (canvasGroup != null) DOTween.Kill(canvasGroup);
        if (loadingAnimator != null) loadingAnimator.enabled = false;
        minimumDisplayCancellation?.Cancel();
        minimumDisplayCancellation?.Dispose();
        minimumDisplayCancellation = null;
    }
}
