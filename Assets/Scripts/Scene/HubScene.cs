using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 타이틀 Hub 메뉴. 처음에는 "아무 키나 누르기" 안내만 보이고, 첫 입력에 왼쪽 메뉴
/// (새 장사 / 이어하기 / 설정 / 가게 문 닫기)를 띄운다. 저장 및 이어하기는 아직 구현하지 않아 잠가 둔다.
/// 설정 버튼은 같은 씬의 OptionsPanelPresenter가 직접 여닫는다.
/// </summary>
public class HubScene : MonoBehaviour
{
    private const float MenuFadeSeconds = .5f;

    [SerializeField] private Button newGameButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text pressGuideText;

    [Tooltip("첫 입력 뒤 나타나는 왼쪽 메뉴 묶음 (배경 띠 + 버튼들)")]
    [SerializeField] private CanvasGroup menuGroup;

    [Tooltip("저장 기능 전까지 잠가 두는 이어하기 버튼")]
    [SerializeField] private Button continueButton;

    private Tweener pressGuideFadeTween;
    private Tweener menuFadeTween;
    private bool isMenuShown;

    /// <summary>활성화된 안내 문구의 알파 PingPong 반복을 시작한다.</summary>
    private void OnEnable()
    {
        if (pressGuideText == null) return;
        pressGuideFadeTween?.Kill();
        Color color = pressGuideText.color;
        color.a = 1f;
        pressGuideText.color = color;
        pressGuideFadeTween = DOTween.ToAlpha(() => pressGuideText.color,
            value => pressGuideText.color = value, 0.2f, 0.8f)
            .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
    }

    /// <summary>씬의 메뉴 버튼을 연결하고 부트 준비 전 새 게임을 잠근다.</summary>
    private void Awake()
    {
        // 메뉴는 첫 입력 전까지 보이지도 눌리지도 않는다. 그 입력이 메뉴 항목을 함께 누르지 않게 한다.
        if (menuGroup != null)
        {
            menuGroup.alpha = 0f;
            menuGroup.interactable = false;
            menuGroup.blocksRaycasts = false;
        }
        if (continueButton != null) continueButton.interactable = false;
        newGameButton.interactable = false;
        newGameButton.onClick.AddListener(startNewGame);
        quitButton.onClick.AddListener(quitGame);
    }

    /// <summary>이전 씬 전환이 완료되면 자동 진행 없이 메뉴 입력을 기다린다.</summary>
    private async void Start()
    {
        try
        {
            if (GameSceneManager.Instance == null)
                throw new InvalidOperationException("Start Play Mode from InitScene to bootstrap managers.");
            await UniTask.NextFrame(this.GetCancellationTokenOnDestroy());
            SoundManager.Instance?.PlayBgm(SoundKeys.TitleBgm);
            newGameButton.interactable = true;
            statusText.text = string.Empty;
        }
        catch (OperationCanceledException) when (this == null)
        {
            // 전환 전 Play Mode 종료는 정상 취소다.
        }
        catch (Exception exception)
        {
            showError(exception);
        }
    }

    /// <summary>안내 문구가 떠 있는 동안 키보드·마우스·패드의 첫 입력을 기다렸다가 메뉴를 띄운다.</summary>
    private void Update()
    {
        if (isMenuShown || !wasAnyInputPressed()) return;
        showMenu();
    }

    /// <summary>중복 입력을 막고 준비된 런타임으로 새 세션을 만든 뒤 Gameplay 씬으로 이동한다.</summary>
    private async void startNewGame()
    {
        if (!newGameButton.interactable) return;
        newGameButton.interactable = false;
        statusText.text = "새 게임을 준비하고 있습니다.";
        SoundManager.Instance?.StopBgm();
        try { await GameSceneManager.Instance.RestartGameAsync(); }
        catch (Exception exception)
        {
            if (this == null) return;
            SoundManager.Instance?.PlayBgm(SoundKeys.TitleBgm);
            newGameButton.interactable = true;
            showError(exception);
        }
    }

    /// <summary>Hub가 비활성화되면 타이틀 BGM이 다음 씬으로 이어지지 않게 정지한다.</summary>
    private void OnDisable()
    {
        pressGuideFadeTween?.Kill();
        pressGuideFadeTween = null;
        menuFadeTween?.Kill();
        menuFadeTween = null;
        SoundManager.Instance?.StopBgm();
    }

    /// <summary>이번 프레임에 아무 키·마우스 버튼·패드 버튼이 눌렸는지 확인한다.</summary>
    /// <returns>눌렸으면 true.</returns>
    private static bool wasAnyInputPressed()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
        Mouse mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)) return true;
        Gamepad gamepad = Gamepad.current;
        return gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame);
    }

    /// <summary>안내 문구를 끄고 왼쪽 메뉴를 서서히 띄운다. 한 번만 실행된다.</summary>
    private void showMenu()
    {
        isMenuShown = true;
        pressGuideFadeTween?.Kill();
        pressGuideFadeTween = null;
        if (pressGuideText != null) pressGuideText.gameObject.SetActive(false);
        if (menuGroup == null) return;
        menuFadeTween = DOTween.To(() => menuGroup.alpha, value => menuGroup.alpha = value, 1f, MenuFadeSeconds).SetEase(Ease.OutSine).SetUpdate(true)
            .OnComplete(() =>
            {
                // 다 나타난 뒤에만 눌리게 해 메뉴를 띄운 입력이 항목을 누르지 않게 한다.
                menuGroup.interactable = true;
                menuGroup.blocksRaycasts = true;
            });
    }

    /// <summary>배포 실행을 종료하며 Editor에서는 Play만 종료한다.</summary>
    private void quitGame()
    {
        SoundManager.Instance?.StopBgm();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>메뉴 오류를 사용자와 Console에 함께 알린다.</summary>
    /// <param name="exception">부트 또는 전환 오류.</param>
    private void showError(Exception exception)
    {
        statusText.text = "게임을 시작하지 못했습니다. InitScene부터 실행하거나 다시 시도해 주세요.";
        Debug.LogError($"[HubScene] {exception}");
    }
}
