using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 인트로 9컷 연출에 감독관의 "딸 수명 3주" 대사와, 마지막 검은 화면의 "주어진 하루 D-21 → -1 → D-20" 카운트다운을 넣는 도구.
/// 씬에 저장된 beats 배열을 직접 고친다. 이미 넣었으면 다시 넣지 않는다.
/// </summary>
public static class IntroCountdownSetup
{
    private const string ScenePath = "Assets/Scenes/IntroScene.unity";
    private const string DoctorLine = "아까 의사한테 얼핏 들었는데… 자네 딸아이, 길어봤자 3주 정도밖에 안 남았다더군.";
    private const string AfterLine = "돈부터 마련해.";
    private const string ChoiceLine = "선택은 자네 몫이야.";
    private const string CardLabel = "Countdown";
    // 검은 화면 한 장에서 글자만 바뀐다: D-21 → -1 → D-20. "||"가 바뀌는 지점이다.
    private const string CountdownText =
        "하루에게 남은 날\n<size=160%>D-21</size>" +
        "||하루에게 남은 날\n<size=160%>D-21 <color=#b3261e>-1</color></size>" +
        "||하루에게 남은 날\n<size=160%><color=#e8c27a>D-20</color></size>";

    /// <summary>대사와 카운트다운을 IntroScene에 넣고 저장합니다.</summary>
    [MenuItem("Cashier/Intro/Insert Doctor Line And Countdown")]
    public static void Install()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        IntroNineCutPlayer player = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            player = root.GetComponentInChildren<IntroNineCutPlayer>(true);
            if (player != null) break;
        }

        if (player == null) throw new System.InvalidOperationException("IntroNineCutPlayer를 찾을 수 없습니다.");
        var serialized = new SerializedObject(player);
        SerializedProperty beats = serialized.FindProperty("beats");

        if (find(beats, DoctorLine) < 0)
        {
            int after = find(beats, AfterLine);
            if (after < 0) throw new System.InvalidOperationException($"'{AfterLine}' 대사를 찾을 수 없습니다.");
            insert(beats, after + 1, "Line", IntroBeatKind.Line, 0, "감독관", DoctorLine, 0f, 0f);
        }

        if (findLabel(beats, CardLabel) < 0)
        {
            int choice = find(beats, ChoiceLine);
            if (choice < 0) throw new System.InvalidOperationException($"'{ChoiceLine}' 대사를 찾을 수 없습니다.");
            // 선택 대사 뒤 화면이 검게 페이드된 직후에 카운트다운을 넣는다.
            int fade = choice + 1;
            while (fade < beats.arraySize && beats.GetArrayElementAtIndex(fade).FindPropertyRelative("kind").enumValueIndex != (int)IntroBeatKind.Fade)
                fade++;
            int at = Mathf.Min(fade + 1, beats.arraySize);
            // value = 글자 페이드 시간, seconds = 다 보인 채 머무는 시간.
            insert(beats, at++, "Wait", IntroBeatKind.Wait, 0, "", "", 0.6f, 0f);
            insert(beats, at++, CardLabel, IntroBeatKind.Card, 9, "", CountdownText, 1.6f, 0.8f);
            insert(beats, at, "Wait", IntroBeatKind.Wait, 0, "", "", 0.6f, 0f);
        }
        else
        {
            // 예전처럼 카드 여러 장으로 나뉘어 있으면 지우고, 첫 카드 자리에 한 장짜리를 다시 넣는다.
            int first = findLabel(beats, CardLabel);
            if (beats.GetArrayElementAtIndex(first).FindPropertyRelative("text").stringValue != CountdownText)
            {
                int card;
                while ((card = findLabel(beats, CardLabel)) >= 0) beats.DeleteArrayElementAtIndex(card);
                insert(beats, first, CardLabel, IntroBeatKind.Card, 9, "", CountdownText, 1.6f, 0.8f);
            }
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[IntroCountdownSetup] 인트로 대사·카운트다운 저장 완료");
    }

    private static int find(SerializedProperty beats, string text)
    {
        for (int index = 0; index < beats.arraySize; index++)
            if (beats.GetArrayElementAtIndex(index).FindPropertyRelative("text").stringValue == text) return index;
        return -1;
    }

    private static int findLabel(SerializedProperty beats, string label)
    {
        for (int index = 0; index < beats.arraySize; index++)
            if (beats.GetArrayElementAtIndex(index).FindPropertyRelative("label").stringValue == label) return index;
        return -1;
    }

    private static void insert(SerializedProperty beats, int at, string label, IntroBeatKind kind, int index, string speaker,
        string text, float seconds, float value)
    {
        beats.InsertArrayElementAtIndex(at);
        var beat = beats.GetArrayElementAtIndex(at);
        beat.FindPropertyRelative("label").stringValue = label;
        beat.FindPropertyRelative("kind").enumValueIndex = (int)kind;
        beat.FindPropertyRelative("index").intValue = index;
        beat.FindPropertyRelative("speaker").stringValue = speaker;
        beat.FindPropertyRelative("text").stringValue = text;
        beat.FindPropertyRelative("seconds").floatValue = seconds;
        beat.FindPropertyRelative("value").floatValue = value;
        beat.FindPropertyRelative("fadeMusic").boolValue = false;
        beat.FindPropertyRelative("musicTarget").floatValue = 0f;
    }
}
