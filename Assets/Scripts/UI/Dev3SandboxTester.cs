using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 개발자 런타임 디버그 툴 (Developer 3).
/// 게임 중 단축키(~ + U + I)를 누르면 열리는 경량 IMGUI 디버그 창입니다.
/// 도덕성, 자금, 날짜, 타임스케일 조작을 지원합니다.
/// </summary>
public sealed class Dev3SandboxTester : MonoBehaviour
{
    private const int WindowId = 987654;
    private Rect windowRect = new Rect(20, 20, 330, 490);
    private Vector2 scrollPos = Vector2.zero;
    private bool isOpen = false;

    [Header("Settings")]
    [Tooltip("~, U, I 키를 동시에 눌러야 열리도록 할지 여부입니다.")]
    [SerializeField] private bool requireAllThreeKeys = true;

    /// <summary>
    /// 게임 실행 시 자동으로 디버그 인스턴스를 생성해 상주(DontDestroyOnLoad)시킵니다.
    /// 별도의 씬 연결이나 프리팹 설치 없이 언제든 단축키로 호출할 수 있습니다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        if (FindFirstObjectByType<Dev3SandboxTester>() == null)
        {
            GameObject debugGo = new GameObject("[Dev3DebugMenu]");
            debugGo.AddComponent<Dev3SandboxTester>();
            DontDestroyOnLoad(debugGo);
        }
    }

    private void Update()
    {
        this.handleKeyboardInput();
    }

    private void handleKeyboardInput()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;

        bool isBackquotePressed = kb.backquoteKey.isPressed;
        bool isUPressed = kb.uKey.isPressed;
        bool isIPressed = kb.iKey.isPressed;

        if (this.requireAllThreeKeys)
        {
            bool anyPressedThisFrame = kb.backquoteKey.wasPressedThisFrame ||
                                       kb.uKey.wasPressedThisFrame ||
                                       kb.iKey.wasPressedThisFrame;

            if (isBackquotePressed && isUPressed && isIPressed && anyPressedThisFrame)
            {
                this.isOpen = !this.isOpen;
            }
        }
        else
        {
            if (kb.backquoteKey.wasPressedThisFrame || kb.uKey.wasPressedThisFrame || kb.iKey.wasPressedThisFrame)
            {
                this.isOpen = !this.isOpen;
            }
        }
#else
        bool isBackquotePressed = Input.GetKey(KeyCode.BackQuote) || Input.GetKey(KeyCode.Tilde);
        bool isUPressed = Input.GetKey(KeyCode.U);
        bool isIPressed = Input.GetKey(KeyCode.I);

        if (this.requireAllThreeKeys)
        {
            bool anyPressedThisFrame = Input.GetKeyDown(KeyCode.BackQuote) || Input.GetKeyDown(KeyCode.Tilde) ||
                                       Input.GetKeyDown(KeyCode.U) || Input.GetKeyDown(KeyCode.I);

            if (isBackquotePressed && isUPressed && isIPressed && anyPressedThisFrame)
            {
                this.isOpen = !this.isOpen;
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.BackQuote) || Input.GetKeyDown(KeyCode.Tilde) ||
                Input.GetKeyDown(KeyCode.U) || Input.GetKeyDown(KeyCode.I))
            {
                this.isOpen = !this.isOpen;
            }
        }
#endif
    }

    private void OnGUI()
    {
        if (!this.isOpen) return;

        // 화면 밖으로 창이 나가지 않도록 고정
        this.windowRect.x = Mathf.Clamp(this.windowRect.x, 0, Screen.width - this.windowRect.width);
        this.windowRect.y = Mathf.Clamp(this.windowRect.y, 0, Screen.height - this.windowRect.height);

        this.windowRect = GUI.Window(WindowId, this.windowRect, this.drawDebugWindow, "개발자 디버그 메뉴 (~, U, I)");
    }

    private void drawDebugWindow(int windowId)
    {
        this.scrollPos = GUILayout.BeginScrollView(this.scrollPos);

        GUILayout.Space(4);

        // 현재 게임 상태 요약
        GameSessionManager session = GameSessionManager.Instance;
        GameUIController gameUI = FindFirstObjectByType<GameUIController>();

        decimal morality = session != null ? session.CurrentMorality : 0m;
        long balance = session?.Economy?.FinanceService != null ? session.Economy.FinanceService.CurrentBalance : 0;
        int currentDay = session != null ? (int)session.ElapsedDays + 1 : 1;
        float currentScale = Time.timeScale;
        bool isPreOpen = gameUI != null && gameUI.CurrentDayProgress?.State == DayProgressState.PreOpen;

        GUILayout.Label($"<b>[현재 상태]</b>");
        GUILayout.Label($"· 날짜: {currentDay}일차 (상태: {(gameUI?.CurrentDayProgress != null ? gameUI.CurrentDayProgress.State.ToString() : "없음")})");
        GUILayout.Label($"· 도덕성: {morality:F1} | 자금: {balance:N0} G");
        GUILayout.Label($"· 타임스케일: {currentScale:0.#}x");

        GUILayout.Space(6);
        GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1)); // 구분선
        GUILayout.Space(4);

        // 1. 도덕성 조작
        GUILayout.Label("<b>도덕성</b>");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+10", GUILayout.Height(28)))
        {
            if (session != null)
            {
                session.DebugAddMorality(10m);
            }
        }
        if (GUILayout.Button("-10", GUILayout.Height(28)))
        {
            if (session != null)
            {
                session.DebugAddMorality(-10m);
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        // 2. 자금 조작
        GUILayout.Label("<b>자금</b>");
        if (GUILayout.Button("+100,000 G", GUILayout.Height(28)))
        {
            if (gameUI != null)
            {
                gameUI.GrantTestFunds();
            }
            else if (session?.Economy?.FinanceService != null)
            {
                session.Economy.FinanceService.AddIncome(100_000, FinanceChangeReason.None);
            }
        }

        GUILayout.Space(6);

        // 3. 날짜 조작 (일일지침 화면에서만 활성화)
        GUILayout.Label("<b>날짜</b>");
        GUI.enabled = isPreOpen && session != null;
        if (GUILayout.Button($"날짜 +1 ({currentDay + 1}일차로 이동)", GUILayout.Height(28)))
        {
            if (gameUI != null && session != null)
            {
                gameUI.DebugJumpToDay(currentDay + 1);
            }
        }
        GUI.enabled = true;
        if (!isPreOpen)
        {
            GUILayout.Label("<color=yellow>※ 날짜 +1은 일일지침(영업 전) 화면에서만 가능합니다.</color>");
        }

        GUILayout.Space(6);

        // 4. 타임스케일
        GUILayout.Label("<b>타임스케일</b>");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("1배", GUILayout.Height(28)))
        {
            Time.timeScale = 1f;
        }
        if (GUILayout.Button("2배", GUILayout.Height(28)))
        {
            Time.timeScale = 2f;
        }
        if (GUILayout.Button("5배", GUILayout.Height(28)))
        {
            Time.timeScale = 5f;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1)); // 구분선
        GUILayout.Space(4);

        // 5. 창 닫기 버튼
        if (GUILayout.Button("창 닫기", GUILayout.Height(30)))
        {
            this.isOpen = false;
        }

        GUILayout.EndScrollView();

        // 창 드래그 이동 가능
        GUI.DragWindow();
    }
}
