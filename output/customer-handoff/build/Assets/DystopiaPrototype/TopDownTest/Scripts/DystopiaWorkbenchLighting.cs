using UnityEngine;

/// <summary>탑다운 가판의 노멀 조명과 넓은 중앙 조명을 기존 영업 시간에 맞춥니다. 배치는 변경하지 않습니다.</summary>
[ExecuteAlways, RequireComponent(typeof(SpriteRenderer))]
public sealed class DystopiaWorkbenchLighting : MonoBehaviour
{
    /// <summary>정면 화면과 공유하는 시간대 설정입니다.</summary>
    public DystopiaDayNight dayNight;
    /// <summary>가판 UV에서 조명의 중심입니다. Inspector에서 편집합니다.</summary>
    public Vector2 lightCenter = new Vector2(.5f,.5f);
    /// <summary>UV 높이 기준 조명의 넓이와 노멀 표면 반응입니다.</summary>
    [Range(.2f,2)] public float lightRadius = 1;
    [Range(0,2)] public float normalStrength = .55f;
    /// <summary>런타임 및 편집 미리보기에서만 사용하는 렌더 속성입니다.</summary>
    private SpriteRenderer visual;
    private MaterialPropertyBlock properties;

    /// <summary>공유 재질을 복제하지 않고 시간대 색과 조명값만 전달합니다.</summary>
    private void LateUpdate()
    {
        if (visual == null) visual = GetComponent<SpriteRenderer>();
        if (properties == null) properties = new MaterialPropertyBlock();
        float hour = dayNight != null ? Application.isPlaying && dayNight.clock != null
            ? dayNight.clock.BusinessMinute / 60f : dayNight.preview ? dayNight.previewHour : 12 : 12;
        float dawn = dayNight != null ? 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(dayNight.dawnStart,dayNight.dayStart,hour)) : 0;
        float sunset = dayNight != null ? Mathf.SmoothStep(0,1,Mathf.InverseLerp(dayNight.sunsetStart,dayNight.eveningStart,hour)) : 0;
        float night = dayNight != null ? Mathf.SmoothStep(0,1,Mathf.InverseLerp(dayNight.eveningStart,dayNight.nightEnd,hour)) : 0;
        Color ambient = dayNight != null ? Color.Lerp(Color.Lerp(Color.Lerp(Color.white,dayNight.dawnTint,dawn),dayNight.sunsetTint,sunset),dayNight.nightTint,night) : Color.white;
        visual.GetPropertyBlock(properties);
        properties.SetColor("_AmbientTint",ambient * Mathf.Lerp(.72f,.55f,night));
        properties.SetColor("_SpotTint",Color.Lerp(new Color(1,.92f,.76f),new Color(1,.79f,.53f),night));
        properties.SetFloat("_SpotIntensity",Mathf.Lerp(.55f,.9f,night));
        properties.SetVector("_LightCenter",new Vector4(lightCenter.x,lightCenter.y,0,0));
        properties.SetFloat("_LightRadius",lightRadius);
        properties.SetFloat("_NormalStrength",normalStrength);
        visual.SetPropertyBlock(properties);
    }

    /// <summary>비활성화 시 컴포넌트가 적용한 미리보기 속성을 해제합니다.</summary>
    private void OnDisable()
    {
        if (visual != null) visual.SetPropertyBlock(null);
    }
}
