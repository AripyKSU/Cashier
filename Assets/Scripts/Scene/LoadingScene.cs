using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

/// <summary>로딩 진행 UI를 표시하고 씬 수명에 맞춰 tween과 등록을 정리한다.</summary>
public class LoadingScene : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image progressBar;
    [SerializeField] private TextMeshProUGUI progressText;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
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
