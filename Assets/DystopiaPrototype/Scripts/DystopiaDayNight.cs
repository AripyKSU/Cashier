using UnityEngine;
using UnityEngine.UI;

/// <summary>저장된 배경·빛 레이어를 영업 시간에 맞춰 혼합하며 편집 시간 미리보기를 제공합니다.</summary>
[ExecuteAlways]
public sealed class DystopiaDayNight : MonoBehaviour
{
    /// <summary>시간 권위인 기존 거래 화면입니다.</summary>
    public DystopiaTopDownTest clock;
    /// <summary>활성화 시 단순 색 곱셈 대신 표면 조명을 적용합니다.</summary>
    public DystopiaPixelStage pixelStage;
    /// <summary>기존 점심 배경 위에 겹치는 시간대별 아트입니다.</summary>
    public Image dawn, evening, cityLights, counterLight;
    /// <summary>15~18시에 강해지고 밤 배경 아래에 유지되는 석양 이미지입니다.</summary>
    public Image sunset;
    /// <summary>석양이 시작되는 시각입니다.</summary>
    public float sunsetStart = 15;
    /// <summary>석양 절정의 환경 색입니다.</summary>
    public Color sunsetTint = new Color(1f, .72f, .49f);
    /// <summary>Scene에서 위치·크기를 편집하는 탐조등입니다.</summary>
    public Image leftBeam, rightBeam;
    /// <summary>문자 UI를 제외한 환경 및 손님 이미지입니다.</summary>
    public Graphic[] environment, people;
    /// <summary>편집 중에만 사용하는 시간 미리보기입니다.</summary>
    public bool preview;
    [Range(9,21)] public float previewHour = 9;
    /// <summary>아침·점심·저녁 전환 시간이며 단위는 시입니다.</summary>
    public float dawnStart = 9, dayStart = 12, eveningStart = 18, nightEnd = 21;
    /// <summary>환경의 시간대별 색입니다. 점심은 원본 색을 그대로 사용합니다.</summary>
    public Color dawnTint = new Color(1f,.90f,.80f), nightTint = new Color(.38f,.43f,.56f);
    /// <summary>손님과 가판의 가독성을 위한 야간 최소 밝기입니다.</summary>
    [Range(0,1)] public float peopleBrightness = .72f;
    /// <summary>야간 도시·탐조등·가판 빛의 최대 불투명도입니다.</summary>
    [Range(0,1)] public float cityIntensity = .7f, beamIntensity = .11f, counterIntensity = .12f;
    /// <summary>탐조등 중심 각도·좌우 이동 폭입니다. 위치와 크기는 RectTransform에서 편집합니다.</summary>
    public float leftAngle = 14, rightAngle = 166, sweepDegrees = 14;

    /// <summary>일반 UI 경로를 갱신하며 픽셀 렌더 사용 시에는 렌더 직전 갱신에 맡깁니다.</summary>
    private void LateUpdate()
    {
        if (pixelStage != null && pixelStage.IsRendering) return;
        RefreshTime();
    }

    /// <summary>표시 시계와 같은 영업 시간을 읽어 배경 혼합과 조명에 함께 전달합니다.</summary>
    internal void RefreshTime()
    {
        if (!Application.isPlaying && !preview) { Restore(); return; }
        float hour = Application.isPlaying && clock != null ? clock.BusinessMinute / 60f : previewHour;
        ApplyHour(hour);
    }

    /// <summary>시간대 경계에서 연속적으로 배경과 조명 강도를 혼합합니다.</summary>
    /// <param name="hour">09~21시 영업 시간입니다.</param>
    public void ApplyHour(float hour)
    {
        float morning = 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(dawnStart,dayStart,hour));
        float night = Mathf.SmoothStep(0,1,Mathf.InverseLerp(eveningStart,nightEnd,hour));
        float sunsetBlend = Mathf.SmoothStep(0,1,Mathf.InverseLerp(sunsetStart,eveningStart,hour));
        Alpha(sunset,sunsetBlend);
        Alpha(dawn,morning); Alpha(evening,night); Alpha(cityLights,night*cityIntensity);
        Alpha(leftBeam,night*beamIntensity); Alpha(rightBeam,night*beamIntensity); Alpha(counterLight,night*counterIntensity);
        Color tint=Color.Lerp(Color.Lerp(Color.Lerp(Color.white,dawnTint,morning),sunsetTint,sunsetBlend),nightTint,night);
        bool lit = pixelStage != null && pixelStage.IsRendering;
        if (lit) pixelStage.SetTimeWeights(morning,sunsetBlend,night);
        Tint(environment,lit ? Color.white : tint); Tint(people,lit ? Color.white : Color.Lerp(Color.white,new Color(peopleBrightness,peopleBrightness,peopleBrightness),night));
        float phase=hour*6;
        if(leftBeam!=null)leftBeam.rectTransform.localRotation=Quaternion.Euler(0,0,leftAngle+Mathf.Sin(phase)*sweepDegrees);
        if(rightBeam!=null)rightBeam.rectTransform.localRotation=Quaternion.Euler(0,0,rightAngle+Mathf.Sin(phase*.83f+2)*sweepDegrees);
    }

    /// <summary>기존 이미지의 Inspector 색을 보존하고 Renderer에만 색을 곱합니다.</summary>
    private static void Tint(Graphic[] graphics,Color color)
    { if(graphics!=null)foreach(var graphic in graphics)if(graphic!=null)graphic.canvasRenderer.SetColor(color); }
    /// <summary>원본 색을 유지하면서 레이어 불투명도를 설정합니다.</summary>
    private static void Alpha(Image image,float alpha)
    { if(image!=null) image.canvasRenderer.SetColor(new Color(1,1,1,alpha)); }
    /// <summary>편집 미리보기 종료 또는 비활성화 시 원래 낮 화면으로 복원합니다.</summary>
    private void Restore()
    { if (pixelStage != null) pixelStage.SetTimeWeights(0,0,0); Alpha(dawn,0);Alpha(sunset,0);Alpha(evening,0);Alpha(cityLights,0);Alpha(leftBeam,0);Alpha(rightBeam,0);Alpha(counterLight,0);Tint(environment,Color.white);Tint(people,Color.white); }
    /// <summary>시간 연출을 비활성화할 때 어두운 색이 남지 않도록 복원합니다.</summary>
    private void OnDisable() { Restore(); }
}
