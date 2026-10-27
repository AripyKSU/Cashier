using UnityEngine;

/// <summary>
/// 별도 테스트 씬에서만 계산 화면 손맛 연출을 켜는 Scene 수명 컴포넌트입니다.
/// MainScene에는 두지 않으므로 MainScene과 공용 GameUI 프리팹의 기본 동작은 바뀌지 않습니다.
/// 같은 씬의 SaleSortingPanel을 찾아 연출 사용 여부만 바꾸며, 분류·거래 규칙과 데이터는 건드리지 않습니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CheckoutFeedbackSwitch : MonoBehaviour
{
    [Tooltip("상품이 하나씩 포물선으로 굴러 나와 튕기고 착지음을 내는 쏟기 연출을 켭니다.")]
    [SerializeField] private bool spillFeedback = true;

    /// <summary>같은 씬의 분류 화면을 찾아 연출 설정을 적용합니다. 첫 손님 쏟기 전에 한 번 실행됩니다.</summary>
    private void Start()
    {
        foreach (SaleSortingPanel panel in FindObjectsByType<SaleSortingPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            panel.SpillFeedbackEnabled = this.spillFeedback;
        }
    }
}
