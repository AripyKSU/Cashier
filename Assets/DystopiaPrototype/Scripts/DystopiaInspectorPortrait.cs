using UnityEngine;
using UnityEngine.UI;

/// <summary>감독관 원본 한 장에 색상 변경 없이 밑단을 고정한 작은 호흡만 적용합니다.</summary>
public sealed class DystopiaInspectorPortrait : MonoBehaviour
{
    /// <summary>원본 그림을 표시할 몸통과 사용하지 않는 이전 분리 머리입니다.</summary>
    [SerializeField] private RectTransform body, head;
    /// <summary>기본 UI 머티리얼로 원본 전체를 표시합니다.</summary>
    [SerializeField] private RawImage bodyImage;
    /// <summary>목과 허벅지 정리 결과가 보존된 원본 전체 텍스처입니다.</summary>
    [SerializeField] private Texture2D portraitTexture;
    /// <summary>호흡으로 줄어드는 높이 비율입니다.</summary>
    [SerializeField, Range(0, .025f)] private float breathDepth = .012f;
    /// <summary>한 번 숨을 들이쉬고 내쉬는 시간, 단위 초입니다.</summary>
    [SerializeField, Range(2, 6)] private float breathPeriod = 3.7f;
    // 팝업을 새로 열 때마다 시작하는 호흡 시간입니다.
    private float elapsed;

    /// <summary>이전 분리 표시를 끄고 원본의 색상과 기본 머티리얼을 복원합니다.</summary>
    private void OnEnable()
    {
#if UNITY_EDITOR
        if (portraitTexture == null) portraitTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/DystopiaPrototype/Art/Inspector.png");
#endif
        if (head != null) head.gameObject.SetActive(false);
        if (bodyImage != null)
        {
            bodyImage.texture = portraitTexture;
            bodyImage.material = null;
            bodyImage.color = Color.white;
        }
        if (body != null) body.localRotation = Quaternion.identity;
        elapsed = 0;
        ApplyPose(0);
    }

    /// <summary>팝업 시간에 따라 작은 호흡을 진행합니다.</summary>
    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        ApplyPose(elapsed);
    }

    /// <summary>그림 전체를 밑단 기준으로만 늘이고 줄이며 회전과 색상은 바꾸지 않습니다.</summary>
    /// <param name="seconds">팝업 표시 후 경과 시간, 단위 초입니다.</param>
    public void ApplyPose(float seconds)
    {
        if (body == null) return;
        float breath = .5f + .5f * Mathf.Sin(seconds * Mathf.PI * 2 / breathPeriod);
        body.localScale = new Vector3(1 + (breath - .5f) * breathDepth * .25f, 1 - (1 - breath) * breathDepth, 1);
    }
}
