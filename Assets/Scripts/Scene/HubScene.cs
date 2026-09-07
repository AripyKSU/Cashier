using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>부트스트랩을 마친 Hub의 진입점. 게임 씬 선택과 전환을 GameSceneManager에 위임한다.</summary>
public class HubScene : MonoBehaviour
{
    /// <summary>Hub 진입 후 개인 설정 또는 MainScene으로 전환하고 실패를 Console에 보고한다.</summary>
    private async void Start()
    {
        try
        {
            if (GameSceneManager.Instance == null)
                throw new InvalidOperationException("Start Play Mode from InitScene to bootstrap managers.");
            // Hub의 Start는 이전 Single 씬 로드가 완료되는 프레임에 실행될 수 있다.
            await UniTask.NextFrame(this.GetCancellationTokenOnDestroy());
            await GameSceneManager.Instance.TransitionToGameplayAsync();
        }
        catch (OperationCanceledException) when (this == null)
        {
            // 전환 전 Play Mode 종료는 정상 취소다.
        }
        catch (Exception exception)
        {
            Debug.LogError($"[HubScene] Gameplay transition failed: {exception}");
        }
    }
}
