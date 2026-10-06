using UnityEngine;

/// <summary>
/// 배치된 씬에서만 작업대 분류 손맛 연출을 켭니다. MainScene에는 배치하지 않으며
/// Assets/Scenes/Local/ 개인 테스트 씬에서 연출을 검증할 때 사용합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SaleSortingFeedbackEnabler : MonoBehaviour
{
    /// <summary>씬의 모든 분류 패널에 연출을 켭니다. 패널 Awake 이후에 실행됩니다.</summary>
    private void Start()
    {
        SaleSortingPanel[] panels = FindObjectsByType<SaleSortingPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (panels.Length == 0)
        {
            Debug.LogWarning("[SaleSortingFeedbackEnabler] 씬에서 SaleSortingPanel을 찾지 못했습니다.", this);
            return;
        }

        foreach (SaleSortingPanel panel in panels) panel.EnableSortingFeedback();
    }
}
