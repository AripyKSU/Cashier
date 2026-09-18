using TMPro;
using UnityEngine;

public class WorldQueueSpeech : MonoBehaviour
{
    public TextMeshPro TMP { get; private set; }

    [SerializeField]
    private TextMeshPro tmp = null;

    private void Awake()
    {
        TMP = tmp ?? GetComponent<TextMeshPro>();
    }

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
}
