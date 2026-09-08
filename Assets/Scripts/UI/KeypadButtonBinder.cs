using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 씬에 배치된 키패드 버튼을 기존 KeypadController의 입력 API에 연결합니다.
/// 버튼이나 가격 판정 규칙을 생성하지 않고, UI 이벤트 배선만 담당합니다.
/// </summary>
public sealed class KeypadButtonBinder : MonoBehaviour
{
    [Header("Keypad target")]
    [SerializeField] private KeypadController keypadController;

    [Header("Digit buttons; index 0-9")]
    [SerializeField] private Button[] numberButtons;

    [Header("Editing buttons")]
    [SerializeField] private Button doubleZeroButton;
    [SerializeField] private Button backspaceButton;

    private UnityAction[] numberActions;
    private UnityAction doubleZeroAction;
    private UnityAction backspaceAction;

    /// <summary>씬 버튼 이벤트를 키패드 입력 API에 연결합니다.</summary>
    private void Awake()
    {
        if (this.keypadController == null)
        {
            return;
        }

        if (this.numberButtons != null)
        {
            this.numberActions = new UnityAction[Mathf.Min(this.numberButtons.Length, 10)];
            for (int number = 0; number < this.numberButtons.Length && number <= 9; number++)
            {
                Button button = this.numberButtons[number];
                if (button == null)
                {
                    continue;
                }

                int capturedNumber = number;
                UnityAction action = () => this.keypadController.OnNumberButtonClick(capturedNumber);
                this.numberActions[number] = action;
                button.onClick.AddListener(action);
            }
        }

        if (this.doubleZeroButton != null)
        {
            this.doubleZeroAction = this.keypadController.OnDoubleZeroButtonClick;
            this.doubleZeroButton.onClick.AddListener(this.doubleZeroAction);
        }

        if (this.backspaceButton != null)
        {
            this.backspaceAction = this.keypadController.OnBackspaceButtonClick;
            this.backspaceButton.onClick.AddListener(this.backspaceAction);
        }
    }

    /// <summary>씬이 제거될 때 버튼 이벤트 구독을 해제합니다.</summary>
    private void OnDestroy()
    {
        if (this.keypadController == null)
        {
            return;
        }

        if (this.numberButtons != null)
        {
            for (int number = 0; number < this.numberButtons.Length && number <= 9; number++)
            {
                Button button = this.numberButtons[number];
                if (button != null && this.numberActions != null && number < this.numberActions.Length && this.numberActions[number] != null)
                {
                    button.onClick.RemoveListener(this.numberActions[number]);
                }
            }
        }

        if (this.doubleZeroButton != null && this.doubleZeroAction != null)
        {
            this.doubleZeroButton.onClick.RemoveListener(this.doubleZeroAction);
        }

        if (this.backspaceButton != null && this.backspaceAction != null)
        {
            this.backspaceButton.onClick.RemoveListener(this.backspaceAction);
        }
    }
}
