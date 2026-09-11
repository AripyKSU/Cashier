using System;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 게임 화면의 키보드 입력(Enter, Escape)을 현재 UI 상태에 맞는 단일 요청으로 변환합니다.
/// 도메인 상태를 직접 변경하지 않고 Confirm, Continue, Pause 또는 Resume 이벤트만 발생시킵니다.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class GameInputRouter : MonoBehaviour
{
    private bool canConfirm;
    private bool canContinue;
    private bool canPause;
    private bool canResume;

    /// <summary>현재 가격 확정을 요청할 때 발생합니다.</summary>
    public event Action OnConfirmRequested;

    /// <summary>현재 거래 결과 확인을 요청할 때 발생합니다.</summary>
    public event Action OnContinueRequested;

    /// <summary>현재 영업 일시정지를 요청할 때 발생합니다.</summary>
    public event Action OnPauseRequested;

    /// <summary>현재 영업 재개를 요청할 때 발생합니다.</summary>
    public event Action OnResumeRequested;

    private void Update()
    {
        this.processEnterInput();
        this.processEscapeInput();
    }

    /// <summary>게임 진행과 입력값에서 계산된 입력 가능 상태를 적용합니다.</summary>
    /// <param name="canConfirm">현재 가격 확정 요청 가능 여부입니다.</param>
    /// <param name="canContinue">현재 거래 결과 확인 요청 가능 여부입니다.</param>
    /// <param name="canPause">현재 일시정지 요청 가능 여부입니다.</param>
    /// <param name="canResume">현재 일시정지 재개 요청 가능 여부입니다.</param>
    public void SetState(bool canConfirm, bool canContinue, bool canPause = false, bool canResume = false)
    {
        this.canConfirm = canConfirm;
        this.canContinue = canContinue;
        this.canPause = canPause;
        this.canResume = canResume;
    }

    /// <summary>이번 프레임의 Enter 입력을 판정하고 Confirm 또는 Continue 요청을 발생시킵니다.</summary>
    private void processEnterInput()
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

    /// <summary>이번 프레임의 Escape 키 입력을 판정하고 일시정지 또는 재개 요청을 발생시킵니다.</summary>
    private void processEscapeInput()
    {
        if (!this.wasEscapePressedThisFrame())
        {
            return;
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        // 일시정지 중이면 재개를 우선하고, 영업 중이면 일시정지를 처리합니다.
        if (this.canResume)
        {
            this.OnResumeRequested?.Invoke();
        }
        else if (this.canPause)
        {
            this.OnPauseRequested?.Invoke();
        }
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

    /// <summary>현재 프레임에 Escape 키가 눌렸는지 확인합니다.</summary>
    /// <returns>이번 프레임에 Escape 입력이 시작됐으면 true입니다.</returns>
    private bool wasEscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}
