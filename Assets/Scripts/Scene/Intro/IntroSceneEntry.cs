using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 인트로 씬의 수명 조립 컴포넌트. Hub의 새 게임 이후 세션이 이미 준비된 상태로 진입하며,
/// 인트로가 끝나면 기존 규칙(개인 씬 또는 MainScene)대로 Gameplay 전환을 요청한다.
/// </summary>
public sealed class IntroSceneEntry : MonoBehaviour
{
    [SerializeField] private IntroDialogueController intro;

    private bool hasRequestedTransition;

    /// <summary>인트로 완료를 구독하고 부트 없이 열린 경우를 즉시 알린다.</summary>
    private void Awake()
    {
        if (intro == null)
        {
            Debug.LogError("[IntroSceneEntry] IntroDialogueController 참조가 필요합니다.", this);
            return;
        }
        intro.IntroCompleted += handleIntroCompleted;
    }

    private void Start()
    {
        if (GameSceneManager.Instance == null)
            Debug.LogError("[IntroSceneEntry] InitScene부터 실행해야 씬 전환 런타임이 준비됩니다.", this);
    }

    private void OnDestroy()
    {
        if (intro != null) intro.IntroCompleted -= handleIntroCompleted;
    }

    /// <summary>인트로가 끝나면 한 번만 Gameplay 전환을 요청한다.</summary>
    private void handleIntroCompleted()
    {
        if (hasRequestedTransition) return;
        hasRequestedTransition = true;
        transitionAsync().Forget();
    }

    /// <summary>개인 씬 설정을 포함한 기존 Gameplay 전환 경로를 그대로 사용한다.</summary>
    private async UniTask transitionAsync()
    {
        try
        {
            if (GameSceneManager.Instance == null)
                throw new InvalidOperationException("GameSceneManager가 없습니다. InitScene부터 실행하세요.");
            await GameSceneManager.Instance.TransitionToGameplayAsync();
        }
        catch (Exception exception)
        {
            hasRequestedTransition = false;
            Debug.LogError($"[IntroSceneEntry] Gameplay 전환 실패: {exception}", this);
        }
    }
}
