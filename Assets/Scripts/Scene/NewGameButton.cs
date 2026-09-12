using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>종료 화면에서 Hub 메뉴로 돌아가는 버튼. 새 세션은 메뉴의 새 게임 선택 시 시작한다.</summary>
[RequireComponent(typeof(Button))]
public sealed class NewGameButton : MonoBehaviour
{
    private Button button;
    /// <summary>표시 수명에 클릭 리스너를 연결한다.</summary>
    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(restart);
    }
    /// <summary>중복 입력을 잠근 후 Hub 메뉴 복귀를 요청한다.</summary>
    private void restart() => restartAsync().Forget();
    /// <summary>이전 결과를 유지한 채 Hub 메뉴로 전환한다.</summary>
    /// <returns>메뉴 전환 또는 재시도 안내 완료.</returns>
    private async UniTask restartAsync()
    {
        if (!button.interactable) return;
        button.interactable = false;
        try
        {
            await GameSceneManager.Instance.ReturnToHubAsync();
        }
        catch (Exception exception)
        {
            Debug.LogError($"메뉴 복귀 실패: {exception}");
            if (this != null)
            {
                button.interactable = true;
                button.GetComponentInChildren<TMP_Text>().text = "메뉴 복귀 다시 시도";
            }
        }
    }
}
