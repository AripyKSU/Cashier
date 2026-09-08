using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3.6 PriceInputPresenter
/// 키패드와 가격 입력값을 화면에 표시하고, 숫자 조작 및 검증 결과를 보여주는 Presenter.
/// 거래 성립 여부, 손님 예산과 가격 적정성을 UI에서 판단하지 않고, 가격 확정 요청/취소 요청 이벤트만 외부에 전달합니다.
/// </summary>
public class PriceInputPresenter : MonoBehaviour
{
    [Header("UI Text Displays")]
    [Tooltip("현재 입력된 금액 표시 텍스트")]
    [SerializeField] private TextMeshProUGUI priceDisplayText;

    [Tooltip("유효성 검증 및 안내 메시지 텍스트")]
    [SerializeField] private TextMeshProUGUI validationMessageText;

    [Header("UI Controls")]
    [Tooltip("결제/확정 버튼")]
    [SerializeField] private Button confirmButton;

    [Tooltip("취소/클리어 버튼")]
    [SerializeField] private Button cancelButton;

    [Tooltip("키패드 컨트롤러 (선택 연동)")]
    [SerializeField] private KeypadController keypadController;

    /// <summary>사용자가 가격을 확정했을 때 외부로 전달하는 요청 이벤트 (입력 가격 전달)</summary>
    public event Action<long> OnPriceConfirmed;

    /// <summary>사용자가 가격 입력을 취소/클리어했을 때 외부로 전달하는 요청 이벤트</summary>
    public event Action OnInputCancelled;

    private void Awake()
    {
        if (this.confirmButton != null)
        {
            this.confirmButton.onClick.AddListener(this.handleConfirmClicked);
        }

        if (this.cancelButton != null)
        {
            this.cancelButton.onClick.AddListener(this.handleCancelClicked);
        }

        if (this.keypadController != null)
        {
            this.keypadController.OnPriceConfirmed += this.handleKeypadPriceConfirmed;
        }
    }

    private void OnDestroy()
    {
        if (this.keypadController != null)
        {
            this.keypadController.OnPriceConfirmed -= this.handleKeypadPriceConfirmed;
        }
    }

    /// <summary>
    /// 외부 시스템에서 전달된 입력 상태 스냅샷을 기반으로 UI를 갱신합니다.
    /// </summary>
    public void UpdateView(PriceInputViewData viewData)
    {
        if (this.priceDisplayText != null)
        {
            if (viewData.InputAmount.HasValue)
            {
                this.priceDisplayText.text = $"{viewData.InputAmount.Value:N0} G";
            }
            else
            {
                this.priceDisplayText.text = "0 G";
            }
        }

        if (this.validationMessageText != null)
        {
            this.validationMessageText.text = !string.IsNullOrEmpty(viewData.ValidationMessage) ? viewData.ValidationMessage : string.Empty;
        }

        if (this.confirmButton != null)
        {
            this.confirmButton.interactable = viewData.CanConfirm && viewData.IsInputEnabled;
        }

        if (this.cancelButton != null)
        {
            this.cancelButton.interactable = viewData.IsInputEnabled;
        }
    }

    /// <summary>
    /// 코드로 동적 생성된 UI 요소를 바인딩할 때 사용하는 헬퍼 메서드
    /// </summary>
    public void Bind(TextMeshProUGUI priceDisplay, TextMeshProUGUI validationDisplay = null, Button confirmBtn = null, Button cancelBtn = null, KeypadController keypad = null)
    {
        this.priceDisplayText = priceDisplay;
        this.validationMessageText = validationDisplay;
        this.confirmButton = confirmBtn;
        this.cancelButton = cancelBtn;

        if (this.keypadController != null)
        {
            this.keypadController.OnPriceConfirmed -= this.handleKeypadPriceConfirmed;
        }

        this.keypadController = keypad;

        if (this.keypadController != null)
        {
            this.keypadController.OnPriceConfirmed += this.handleKeypadPriceConfirmed;
        }

        if (this.confirmButton != null)
        {
            this.confirmButton.onClick.RemoveAllListeners();
            this.confirmButton.onClick.AddListener(this.handleConfirmClicked);
        }

        if (this.cancelButton != null)
        {
            this.cancelButton.onClick.RemoveAllListeners();
            this.cancelButton.onClick.AddListener(this.handleCancelClicked);
        }
    }

    private void handleConfirmClicked()
    {
        if (this.keypadController != null)
        {
            this.OnPriceConfirmed?.Invoke(this.keypadController.CurrentPrice);
        }
    }

    private void handleCancelClicked()
    {
        if (this.keypadController != null)
        {
            this.keypadController.OnClearButtonClick();
        }
        this.OnInputCancelled?.Invoke();
    }

    private void handleKeypadPriceConfirmed(long price)
    {
        this.OnPriceConfirmed?.Invoke(price);
    }
}
