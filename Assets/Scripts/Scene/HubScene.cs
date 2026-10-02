using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>새 게임·끝내기 입력을 받는 Hub 메뉴. 저장 및 이어하기는 아직 구현하지 않는다.</summary>
public class HubScene : MonoBehaviour
{
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text pressGuideText;
    private Tweener pressGuideFadeTween;

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
        SoundManager.Instance?.StopBgm();
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
