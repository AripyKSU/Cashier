using UnityEngine;
using static CustomerWorldQueueView;

/// <summary>풀에서 대여하는 손님 외형과 거래 반응 SpriteRenderer를 소유합니다.</summary>
public class WorldVisit : MonoBehaviour
{
    [SerializeField] private SpriteRenderer appearance = null;
    [SerializeField] private SpriteRenderer reaction = null;
    private MaterialPropertyBlock bodyProperties;

    /// <summary>대여된 렌더러를 방문별 표현 상태로 초기화합니다.</summary>
    /// <param name="visit">표시할 방문.</param><param name="visualRoot">월드 표현 부모.</param>
    /// <param name="start">최초 위치.</param><param name="sprite">손님 외형.</param>
    /// <param name="normalTexture">외형 노멀맵.</param><param name="world">환경 조명 공급자.</param>
    /// <param name="layout">최초 외형 배치.</param><param name="speech">별도 대여한 말풍선.</param>
    /// <param name="phase">방문별 보행·호흡 위상.</param><returns>대기열 표현 상태.</returns>
    public Visual ToVisual(CustomerVisit visit, Transform visualRoot, Transform start, Sprite sprite, Texture2D normalTexture, WorldSceneView world, CustomerPortraitLayout layout, WorldQueueSpeech speech, float phase)
    {
        transform.SetParent(visualRoot, false);
        transform.position = start.position;
        appearance.sprite = sprite;
        appearance.transform.localScale = Vector3.one * (layout.DisplayHeight / sprite.bounds.size.y);
        Vector3 bodyBounds = sprite.bounds.min;
        appearance.transform.localPosition = -Vector3.Scale(new Vector3(sprite.bounds.center.x, bodyBounds.y, 0), appearance.transform.localScale);
        bodyProperties ??= new MaterialPropertyBlock();
        bodyProperties.Clear();
        world.ApplyCustomerLighting(bodyProperties);
        bodyProperties.SetTexture("_NormalMap", normalTexture);
        bodyProperties.SetFloat("_NormalStrength", 1f);
        bodyProperties.SetFloat("_Surface", 2f);
        appearance.SetPropertyBlock(bodyProperties);
        appearance.color = Color.clear;
        reaction.sortingOrder = 220;
        reaction.gameObject.SetActive(false);

        Visual visual = new Visual
        {
            Root = transform,
            Body = appearance,
            BodyProperties = bodyProperties,
            Reaction = reaction,
            Speech = speech,
            SpeechTMP = speech.TMP,
            Phase = phase,
            Attributes = visit.Attributes,
            DisplayHeight = layout.DisplayHeight,
            RisePixels = layout.RisePixels,
            BreathPeriod = 2.9f * Mathf.Lerp(.88f, 1.12f, Mathf.Repeat(phase, 1))
        };

        return visual;
    }

    /// <summary>풀 반환 전에 렌더러의 방문별 참조와 표시 상태를 지웁니다.</summary>
    public void ResetForPool()
    {
        appearance.sprite = null;
        appearance.color = Color.clear;
        appearance.SetPropertyBlock(null);
        bodyProperties?.Clear();
        appearance.transform.localPosition = Vector3.zero;
        appearance.transform.localScale = Vector3.one;
        reaction.sprite = null;
        reaction.color = Color.clear;
        reaction.transform.localPosition = Vector3.zero;
        reaction.transform.localScale = Vector3.one;
        reaction.gameObject.SetActive(false);
        transform.localPosition = Vector3.zero;
        transform.localScale = Vector3.one;
    }
}
