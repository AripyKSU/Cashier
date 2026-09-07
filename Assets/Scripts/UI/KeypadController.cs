using System;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 계산대 포스기 키패드 입력 및 가격 검증 컨트롤러 (개발자 3 담당).
/// 마우스 클릭/터치 버튼 입력뿐만 아니라 키보드(숫자키, NumPad, -, ., Backspace, Enter) 입력을 지원합니다.
/// </summary>
public class KeypadController : MonoBehaviour
{
    // =========================================================================
    // 1. SERIALIZED FIELDS
    // =========================================================================

    [Header("UI References")]
    [Tooltip("현재 입력 중인 금액을 표시하는 TextMeshPro 텍스트")]
    [SerializeField] private TextMeshProUGUI priceDisplayText;

    [Header("Settings")]
    [Tooltip("최대 입력 가능한 자릿수 (기본: 6자리 = 최대 999,999원)")]
    [SerializeField] private int maxDigits = 6;

    [Tooltip("키보드 입력 허용 여부")]
    [SerializeField] private bool allowKeyboardInput = true;


    // =========================================================================
    // 2. EVENTS & PROPERTIES
    // =========================================================================

    /// <summary>가격 확정 시 외부(판정 시스템 등)로 확정 금액을 전달하는 이벤트</summary>
    public event Action<long> OnPriceConfirmed;

    /// <summary>현재 입력된 금액</summary>
    public long CurrentPrice => this.currentPrice;


    // =========================================================================
    // 3. PRIVATE FIELDS
    // =========================================================================

    private long currentPrice = 0;


    // =========================================================================
    // 4. UNITY LIFECYCLE
    // =========================================================================

    private void Start()
    {
        this.updateDisplay();
    }

    private void Update()
    {
        if (this.allowKeyboardInput)
        {
            this.handleKeyboardInput();
        }
    }


    // =========================================================================
    // 5. PUBLIC UI METHODS (Buttons)
    // =========================================================================

    /// <summary>0~9 숫자 버튼 클릭 시 호출</summary>
    /// <param name="number">입력할 숫자 (0~9)</param>
    public void OnNumberButtonClick(int number)
    {
        if (number < 0 || number > 9) return;

        // 선행 0 방지 (현재 0원인데 또 0을 누르면 무시)
        if (this.currentPrice == 0 && number == 0) return;

        // 최대 자릿수 초과 검사
        long nextValue = (this.currentPrice * 10) + number;
        if (nextValue.ToString().Length > this.maxDigits)
        {
            Debug.LogWarning($"[Keypad] 최대 자릿수({this.maxDigits}자리)를 초과할 수 없습니다.");
            return;
        }

        this.currentPrice = nextValue;
        this.updateDisplay();
    }

    /// <summary>
    /// '00' 버튼 (또는 - / . 버튼) 클릭 시 호출.
    /// 현재 금액 뒤에 0을 2개 추가합니다.
    /// </summary>
    public void OnDoubleZeroButtonClick()
    {
        // 0원 상태에서는 00을 추가할 수 없음
        if (this.currentPrice == 0) return;

        // 2자리가 추가되었을 때 자릿수 초과 검사
        long nextValue = this.currentPrice * 100;
        if (nextValue.ToString().Length > this.maxDigits)
        {
            Debug.LogWarning($"[Keypad] '00' 입력 시 최대 자릿수({this.maxDigits}자리)를 초과합니다.");
            return;
        }

        this.currentPrice = nextValue;
        this.updateDisplay();
    }

    /// <summary>Backspace(한 자리 지우기) 버튼 클릭 시 호출</summary>
    public void OnBackspaceButtonClick()
    {
        if (this.currentPrice <= 0) return;

        this.currentPrice /= 10;
        this.updateDisplay();
    }

    /// <summary>전체 지우기 (Clear) 버튼 클릭 시 호출</summary>
    public void OnClearButtonClick()
    {
        this.currentPrice = 0;
        this.updateDisplay();
    }

    /// <summary>결제 / 가격 확정 (Enter) 버튼 클릭 시 호출</summary>
    public void OnConfirmButtonClick()
    {
        if (this.currentPrice <= 0)
        {
            Debug.LogWarning("[Keypad] 0원은 결제할 수 없습니다!");
            return;
        }

        Debug.Log($"<color=cyan><b>[Keypad] 가격 확정 전송: {this.currentPrice:N0}원</b></color>");
        this.OnPriceConfirmed?.Invoke(this.currentPrice);

        // 결제 완료 후 입력창은 자동으로 0으로 비움
        this.currentPrice = 0;
        this.updateDisplay();
    }


    // =========================================================================
    // 6. PRIVATE HELPER METHODS
    // =========================================================================

    private void updateDisplay()
    {
        if (this.priceDisplayText != null)
        {
            // Currency display in English (e.g., 1,500 G)
            this.priceDisplayText.text = $"{this.currentPrice:N0} G";
        }
    }

    private void handleKeyboardInput()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;

        // 1. 숫자키 0~9 (상단 숫자키 & NumPad)
        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) this.OnNumberButtonClick(1);
        else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) this.OnNumberButtonClick(2);
        else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) this.OnNumberButtonClick(3);
        else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) this.OnNumberButtonClick(4);
        else if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) this.OnNumberButtonClick(5);
        else if (kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame) this.OnNumberButtonClick(6);
        else if (kb.digit7Key.wasPressedThisFrame || kb.numpad7Key.wasPressedThisFrame) this.OnNumberButtonClick(7);
        else if (kb.digit8Key.wasPressedThisFrame || kb.numpad8Key.wasPressedThisFrame) this.OnNumberButtonClick(8);
        else if (kb.digit9Key.wasPressedThisFrame || kb.numpad9Key.wasPressedThisFrame) this.OnNumberButtonClick(9);
        else if (kb.digit0Key.wasPressedThisFrame || kb.numpad0Key.wasPressedThisFrame) this.OnNumberButtonClick(0);

        // 2. '00' 입력: - (Minus) 또는 . (Period)
        if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame ||
            kb.periodKey.wasPressedThisFrame || kb.numpadPeriodKey.wasPressedThisFrame)
        {
            this.OnDoubleZeroButtonClick();
        }

        // 3. Backspace (지우기)
        if (kb.backspaceKey.wasPressedThisFrame)
        {
            this.OnBackspaceButtonClick();
        }

        // 4. Enter / NumPad Enter (결제 확정)
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
        {
            this.OnConfirmButtonClick();
        }
#else
        // 레거시 입력 폴백
        for (int i = 0; i <= 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i))
            {
                this.OnNumberButtonClick(i);
                break;
            }
        }

        if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus) ||
            Input.GetKeyDown(KeyCode.Period) || Input.GetKeyDown(KeyCode.KeypadPeriod))
        {
            this.OnDoubleZeroButtonClick();
        }

        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            this.OnBackspaceButtonClick();
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            this.OnConfirmButtonClick();
        }
#endif
    }
}
