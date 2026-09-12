using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;
using Cysharp.Threading.Tasks;

/// <summary>로딩 진행 UI를 표시하고 씬 수명에 맞춰 tween과 등록을 정리한다.</summary>
public class LoadingScene : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image progressBar;
    [SerializeField] private TextMeshProUGUI progressText;
    /// <summary>엔딩 전환 실패 때만 표시하는 명시적 재시도 버튼.</summary>
    [SerializeField] private Button endingRetryButton;
    private CanvasGroup canvasGroup;
    private GameSceneManager.SceneName? retryDestination;

    private void Awake()
    {
        if (endingRetryButton != null)
        {
            endingRetryButton.gameObject.SetActive(false);
            endingRetryButton.onClick.AddListener(retryEnding);
        }
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        canvasGroup.alpha = 0f; // start invisible
    }

    /// <summary>DLL API로 같은 CanvasGroup 대상의 fade-in을 등록한다.</summary>
    private void OnEnable()
    {
        // fade‑in (0.3s)
        DOTween.To(() => canvasGroup.alpha, value => canvasGroup.alpha = value, 1f, 0.3f)
            .SetTarget(canvasGroup).SetEase(Ease.OutQuad);
        LoadingBarController.Instance?.Register(this);
    }

    /// <summary>정산을 재실행하지 않는 엔딩 전환 재시도를 안내한다.</summary>
    public void ShowEndingRetry()
    {
        retryDestination = null;
        if (progressText != null) progressText.text = "엔딩을 불러오지 못했습니다. 결과는 보존됩니다.";
        if (endingRetryButton != null)
        {
            endingRetryButton.gameObject.SetActive(true);
            endingRetryButton.interactable = true;
        }
    }

    /// <summary>부트 씬 로딩 실패 후 새 게임 전환만 재시도한다.</summary>
    public void ShowNewGameRetry()
    {
        ShowEndingRetry();
        retryDestination = GameSceneManager.SceneName.Init;
        if (progressText != null) progressText.text = "새 게임을 불러오지 못했습니다. 다시 시도해 주세요.";
        if (endingRetryButton != null)
            endingRetryButton.GetComponentInChildren<TMP_Text>().text = "새 게임 다시 불러오기";
    }

    /// <summary>엔딩에서 메뉴로 돌아가는 전환만 재시도한다. 새 게임은 자동 시작하지 않는다.</summary>
    public void ShowHubRetry()
    {
        ShowEndingRetry();
        retryDestination = GameSceneManager.SceneName.Hub;
        if (progressText != null) progressText.text = "메뉴를 불러오지 못했습니다. 다시 시도해 주세요.";
        if (endingRetryButton != null)
            endingRetryButton.GetComponentInChildren<TMP_Text>().text = "메뉴 다시 불러오기";
    }

    /// <summary>동일 종료 결과로 씬 전환만 다시 요청한다.</summary>
    private void retryEnding()
    {
        endingRetryButton.interactable = false;
        if (retryDestination == GameSceneManager.SceneName.Init) GameSceneManager.Instance.RestartGameAsync().Forget();
        else if (retryDestination == GameSceneManager.SceneName.Hub) GameSceneManager.Instance.ReturnToHubAsync().Forget();
        else GameSceneManager.Instance.TransitionToFinalEndingAsync().Forget();
    }

    public void SetProgress(float p)
    {
        if (progressBar != null) progressBar.fillAmount = Mathf.Clamp01(p);
        if (progressText != null) progressText.text = (p * 100f).ToString("F0") + "%";
    }

    /// <summary>Single 씬 전환으로 파괴되기 전에 tween과 진행률 등록을 해제한다.</summary>
    private void OnDisable()
    {
        // 비활성화 이후의 fade-out은 파괴된 CanvasGroup을 접근하므로 여기서는 정리만 한다.
        if (canvasGroup != null) DOTween.Kill(canvasGroup);
        LoadingBarController.Instance?.Unregister();
    }
}
