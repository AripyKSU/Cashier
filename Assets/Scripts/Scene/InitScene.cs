using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// InitScene – 게임 실행 시 가장 먼저 로드되는 Boot 씬.
/// 부트를 마치면 Hub 메뉴로 이동하며 메뉴의 새 게임 요청일 때만 게임 씬에 진입합니다.
/// </summary>
public class InitScene : MonoBehaviour
{
    [SerializeField] private GameSceneManager.SceneName nextScene = GameSceneManager.SceneName.Hub;
    [Tooltip("최종일 정산 진입 전에 시민권을 보유했다면 그날 미납 게임오버만 면제합니다. 새 게임부터 적용됩니다.")]
    [SerializeField] private bool exemptFinalDayPreownedCitizenship = true;

    private async void Start()
    {
        Debug.Log("<color=cyan><b>[InitScene] 게임 부팅 프로세스 시작...</b></color>");

        // 1. 필수 매니저 GameObject 생성 보장
        this.ensureManagerExists<GameSceneManager>("GameSceneManager");
        this.ensureManagerExists<ResourceManager>("ResourceManager");
        this.ensureManagerExists<DataTableManager>("DataTableManager");
        this.ensureGameSessionManagerExists();

        // 2. ResourceManager 초기화 (Addressables 및 카탈로그 수신)
        if (ResourceManager.Instance != null)
        {
            await ResourceManager.Instance.InitAsync(null, this.GetCancellationTokenOnDestroy());
            Debug.Log("[InitScene] ResourceManager 초기화 완료.");
        }

        // 3. DataTableManager CSV 데이터가 완전히 로드/캐싱 완료될 때까지 안전 대기
        if (DataTableManager.Instance != null)
        {
            await DataTableManager.Instance.EnsureDataLoadedAsync();
            Debug.Log("[InitScene] DataTableManager 모든 CSV 데이터 로드 완료.");
        }

        // 검증된 CSV 데이터를 사용해 Scene 전환 전에 새 게임 세션을 구성합니다.
        if (GameSessionManager.Instance == null || DataTableManager.Instance == null)
        {
            Debug.LogError("[InitScene] 게임 세션 초기화에 필요한 Manager가 없습니다.");
            return;
        }

        // 부트 씬 로드와 데이터 준비가 성공한 뒤에만 이전 종료 결과를 버린다.
        GameSessionManager.Instance.ResetSession();
        GameSessionManager.Instance.InitializeNewGame(DataTableManager.Instance, this.exemptFinalDayPreownedCitizenship);
        Debug.Log("[InitScene] GameSessionManager 새 게임 세션 초기화 완료.");

        Debug.Log($"<color=green><b>[InitScene] 부팅 프로세스 완료! {nextScene} 씬으로 전환합니다.</b></color>");

        // 4. 최초 실행은 Hub 메뉴, 메뉴에서 요청한 새 게임은 선택된 게임 씬으로 전환한다.
        await GameSceneManager.Instance.TransitionAfterBootAsync(nextScene);
    }

    private void ensureManagerExists<T>(string gameObjectName) where T : MonoBehaviour
    {
        if (UnityEngine.Object.FindFirstObjectByType<T>() == null)
        {
            Debug.LogWarning($"[InitScene] '{typeof(T).Name}' 매니저가 씬 상에 사전 배치되어 있지 않습니다.");
        }
    }

    /// <summary>
    /// 새 게임 세션을 소유할 GameSessionManager가 없으면 부트 시점에 생성합니다.
    /// </summary>
    private void ensureGameSessionManagerExists()
    {
        if (GameSessionManager.Instance != null
            || UnityEngine.Object.FindFirstObjectByType<GameSessionManager>() != null)
        {
            return;
        }

        // 별도 Scene 또는 Prefab 수정 없이 전역 게임 세션 수명 객체를 한 번만 생성합니다.
        GameObject managerObject = new GameObject("GameSessionManager");
        managerObject.AddComponent<GameSessionManager>();
    }
}
