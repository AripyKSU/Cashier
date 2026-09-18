using TMPro;
using UnityEngine;

/// <summary>손님 외형과 독립된 3초 대기열 말풍선을 풀 수명으로 소유합니다.</summary>
public class WorldQueueSpeech : MonoBehaviour
{
    /// <summary>현재 대여에서 사용할 월드 TextMeshPro입니다.</summary>
    public TextMeshPro TMP { get; private set; }

    [SerializeField]
    private TextMeshPro tmp = null;

    private void Awake()
    {
        tmp = tmp ?? GetComponent<TextMeshPro>();
        TMP = tmp;
    }

    /// <summary>대여한 말풍선을 월드 표현 부모와 폰트에 연결합니다.</summary>
    /// <param name="visualRoot">손님 외형과 같은 좌표계의 부모.</param><param name="font">월드 대사용 폰트.</param>
    public void Init(Transform visualRoot, TMP_FontAsset font)
    {
        tmp.transform.SetParent(visualRoot, false);
        tmp.font = font;
        tmp.fontSize = 160; // 월드 TMP의 1/10 단위 보정: authoring 좌표 16px.
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.rectTransform.sizeDelta = new Vector2(220, 48);
        tmp.rectTransform.pivot = new Vector2(.5f, 0);
        tmp.text = string.Empty;
        tmp.renderer.sortingOrder = 300;
    }

    /// <summary>풀 반환 전에 방문별 문구와 표시 상태를 지웁니다.</summary>
    public void ResetForPool()
    {
        tmp.text = string.Empty;
        tmp.color = Color.clear;
        tmp.transform.localPosition = Vector3.zero;
        tmp.transform.localScale = Vector3.one;
    }
}
