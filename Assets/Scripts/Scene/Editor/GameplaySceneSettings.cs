using UnityEditor;
using UnityEngine;

/// <summary>공유 Scene을 변경하지 않고 PC·프로젝트별 EditorPrefs에 개발 씬 GUID를 저장한다.</summary>
public class GameplaySceneSettings : EditorWindow
{
    /// <summary>게임 진입 대상 설정 창을 연다.</summary>
    [MenuItem("Cashier/Gameplay Scene Settings")]
    public static void Open()
    {
        GetWindow<GameplaySceneSettings>("Gameplay Scene");
    }

    /// <summary>Init부터 실행한 뒤 선택된 목적지와 부트스트랩 manager의 유지 여부를 검사한다.</summary>
    /// <exception cref="System.InvalidOperationException">PlayMode, 목적지 또는 manager 상태 불일치.</exception>
    [MenuItem("Cashier/Validate Gameplay Entry")]
    public static void ValidateGameplayEntry()
    {
        string guid = EditorPrefs.GetString(GameSceneManager.LocalScenePreferenceKey, string.Empty);
        string expected = string.IsNullOrEmpty(guid) ? "Assets/Scenes/MainScene.unity" : AssetDatabase.GUIDToAssetPath(guid);
        if (!EditorApplication.isPlaying || string.IsNullOrEmpty(expected)
            || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != expected
            || GameSceneManager.Instance == null || ResourceManager.Instance == null || DataTableManager.Instance == null)
            throw new System.InvalidOperationException("Gameplay entry validation failed. Start from InitScene and wait for the selected scene.");
        Debug.Log("[Gameplay Entry] PASS: " + expected + "; bootstrap managers retained.");
    }

    /// <summary>개인 씬 선택과 MainScene 복귀 설정을 표시한다.</summary>
    private void OnGUI()
    {
        string key = GameSceneManager.LocalScenePreferenceKey;
        string guid = EditorPrefs.GetString(key, string.Empty);
        SceneAsset current = AssetDatabase.LoadAssetAtPath<SceneAsset>(AssetDatabase.GUIDToAssetPath(guid));
        EditorGUILayout.HelpBox("Play from InitScene. Empty selection loads MainScene. "
            + "Personal scenes belong in Assets/Scenes/Local/. Builds always load MainScene.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            EditorGUI.BeginChangeCheck();
            SceneAsset selected = (SceneAsset)EditorGUILayout.ObjectField("Personal Scene", current, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                string path = AssetDatabase.GetAssetPath(selected);
                if (selected == null) EditorPrefs.DeleteKey(key);
                else if (path.StartsWith(GameSceneManager.LocalSceneFolder, System.StringComparison.Ordinal))
                    EditorPrefs.SetString(key, AssetDatabase.AssetPathToGUID(path));
                else Debug.LogError("Personal scenes must be saved under " + GameSceneManager.LocalSceneFolder);
            }
            if (GUILayout.Button("Use MainScene")) EditorPrefs.DeleteKey(key);
        }
        if (!string.IsNullOrEmpty(guid) && current == null)
            EditorGUILayout.HelpBox("The selected scene is missing. Select it again or use MainScene.", MessageType.Error);
    }
}
