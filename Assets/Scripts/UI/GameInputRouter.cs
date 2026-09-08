using System;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 게임 화면의 Enter 입력을 현재 UI 상태에 맞는 단일 요청으로 변환합니다.
/// 도메인 상태를 직접 변경하지 않고 Confirm 또는 Continue 이벤트만 발생시킵니다.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class GameInputRouter : MonoBehaviour
{
    private bool canConfirm;
    private bool canContinue;

    /// <summary>현재 가격 확정을 요청할 때 발생합니다.</summary>
    public event Action OnConfirmRequested;

    /// <summary>현재 거래 결과 확인을 요청할 때 발생합니다.</summary>
    public event Action OnContinueRequested;

    private void Update()
    {
        if (!this.wasEnterPressedThisFrame())
        {
            return;
        }

        // 같은 Enter가 EventSystem Submit으로 다시 처리되지 않도록 선택 대상을 먼저 해제합니다.
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        // 입력 시점의 상태에서 요청 하나만 발생시켜 Confirm 후 즉시 Continue되는 재진입을 막습니다.
        if (this.canContinue)
        {
            this.OnContinueRequested?.Invoke();
        }
        else if (this.canConfirm)
        {
            this.OnConfirmRequested?.Invoke();
        }
    }

    /// <summary>게임 진행과 입력값에서 계산된 Enter 입력 가능 상태를 적용합니다.</summary>
    /// <param name="canConfirm">현재 가격 확정 요청 가능 여부입니다.</param>
    /// <param name="canContinue">현재 거래 결과 확인 요청 가능 여부입니다.</param>
    public void SetState(bool canConfirm, bool canContinue)
    {
        this.canConfirm = canConfirm;
        this.canContinue = canContinue;
    }

    /// <summary>현재 프레임에 Enter 또는 Numpad Enter가 눌렸는지 확인합니다.</summary>
    /// <returns>이번 프레임에 Enter 입력이 시작됐으면 true입니다.</returns>
    private bool wasEnterPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        return keyboard != null
            && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }
}
