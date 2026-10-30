using UnityEngine;

/// <summary>
/// 씬마다 계산 화면 손맛 연출을 켜고 끄는 Scene 수명 컴포넌트입니다.
/// 체감 확인용 브랜치에서는 연출 기본값이 켜짐이라, 이 컴포넌트로 항목별로 끄거나 다시 켤 수 있습니다.
/// 같은 씬의 SaleSortingPanel과 KeypadController를 찾아 연출 사용 여부만 바꾸며, 분류·거래 규칙과 데이터는 건드리지 않습니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CheckoutFeedbackSwitch : MonoBehaviour
{
    [Tooltip("상품이 하나씩 포물선으로 굴러 나와 튕기고 착지음을 내는 쏟기 연출을 켭니다.")]
    [SerializeField] private bool spillFeedback = true;

    [Tooltip("가격 확정 시 금전등록기 서랍 소리를 냅니다.")]
    [SerializeField] private bool registerClunk = true;

    [Tooltip("상품 집기·끌기·놓기 손맛(들림·그림자·지연·기울기·찌그러짐·히트스톱·흔들림)과 판매 구역 황동색 틴트를 켭니다.")]
    [SerializeField] private bool dragFeedback = true;

    [Tooltip("키패드 숫자 입력 눌림과 가격 확정 흔들림을 켭니다.")]
    [SerializeField] private bool keypadFeedback = true;

    /// <summary>같은 씬의 분류 화면을 찾아 연출 설정을 적용합니다. 첫 손님 쏟기 전에 한 번 실행됩니다.</summary>
    private void Start()
    {
        foreach (SaleSortingPanel panel in FindObjectsByType<SaleSortingPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            panel.SpillFeedbackEnabled = this.spillFeedback;
            panel.DragFeedbackEnabled = this.dragFeedback;
        }

        foreach (KeypadController keypad in FindObjectsByType<KeypadController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            keypad.RegisterClunkEnabled = this.registerClunk;
            keypad.PressFeedbackEnabled = this.keypadFeedback;
        }
    }
}
