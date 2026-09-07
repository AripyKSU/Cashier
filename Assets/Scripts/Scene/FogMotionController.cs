using UnityEngine;

/// <summary>
/// 별도 PNG Full Rect 안개 Sprite의 파라미터를 전용 MaterialPropertyBlock으로 설정합니다.
/// 이 컴포넌트가 활성화된 동안 Renderer의 property block을 소유하고 비활성화 시 이전 값을 복원합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class FogMotionController : MonoBehaviour
{
    /// <summary>전용 셰이더 판별 이름입니다. Material은 자동 생성하거나 교체하지 않습니다.</summary>
    private const string ShaderName = "Cashier/2D/PixelFog";
    private static readonly int FlowSpeedXId = Shader.PropertyToID("_FlowSpeedX");
    private static readonly int FlowSpeedYId = Shader.PropertyToID("_FlowSpeedY");
    private static readonly int NoiseScale1Id = Shader.PropertyToID("_NoiseScale1");
    private static readonly int NoiseSpeed1Id = Shader.PropertyToID("_NoiseSpeed1");
    private static readonly int NoiseStrength1Id = Shader.PropertyToID("_NoiseStrength1");
    private static readonly int NoiseScale2Id = Shader.PropertyToID("_NoiseScale2");
    private static readonly int NoiseSpeed2Id = Shader.PropertyToID("_NoiseSpeed2");
    private static readonly int NoiseStrength2Id = Shader.PropertyToID("_NoiseStrength2");
    private static readonly int SwirlStrengthId = Shader.PropertyToID("_SwirlStrength");
    private static readonly int SwirlScaleId = Shader.PropertyToID("_SwirlScale");
    private static readonly int SwirlSpeedId = Shader.PropertyToID("_SwirlSpeed");
    private static readonly int AlphaMinId = Shader.PropertyToID("_AlphaMin");
    private static readonly int AlphaMaxId = Shader.PropertyToID("_AlphaMax");
    private static readonly int AlphaNoiseStrengthId = Shader.PropertyToID("_AlphaNoiseStrength");
    private static readonly int PixelSizeId = Shader.PropertyToID("_PixelSize");
    private static readonly int DistortionStrengthId = Shader.PropertyToID("_DistortionStrength");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    private static readonly int SeedId = Shader.PropertyToID("_Seed");

    /// <summary>수평 흐름, 초당 Sprite 폭. 양수는 오른쪽입니다.</summary>
    [SerializeField] private float flowSpeedX = 0.1f;
    /// <summary>수직 흐름, 초당 Sprite 높이. 양수는 위쪽입니다.</summary>
    [SerializeField] private float flowSpeedY = 0.015f;
    /// <summary>첫 번째 노이즈의 Sprite UV 공간 주파수입니다.</summary>
    [SerializeField, Min(0.01f)] private float noiseScale1 = 4f;
    /// <summary>첫 번째 노이즈의 초당 진행 속도입니다.</summary>
    [SerializeField] private float noiseSpeed1 = 0.65f;
    /// <summary>첫 번째 노이즈 왜곡량, 원본 픽셀 단위입니다.</summary>
    [SerializeField, Range(0, 3)] private float noiseStrength1 = 1.2f;
    /// <summary>두 번째 노이즈의 Sprite UV 공간 주파수입니다.</summary>
    [SerializeField, Min(0.01f)] private float noiseScale2 = 11f;
    /// <summary>두 번째 노이즈의 초당 진행 속도입니다.</summary>
    [SerializeField] private float noiseSpeed2 = 1.1f;
    /// <summary>두 번째 노이즈 왜곡량, 원본 픽셀 단위입니다.</summary>
    [SerializeField, Range(0, 3)] private float noiseStrength2 = 0.7f;
    /// <summary>국소 curl의 왜곡량, 원본 픽셀 단위입니다.</summary>
    [SerializeField, Range(0, 3)] private float swirlStrength = 1.4f;
    /// <summary>국소 소용돌이 공간 주파수입니다. 높일수록 작은 영역이 늘어납니다.</summary>
    [SerializeField, Min(0.01f)] private float swirlScale = 8f;
    /// <summary>소용돌이 노이즈의 초당 진행 속도입니다.</summary>
    [SerializeField] private float swirlSpeed = 0.8f;
    /// <summary>노이즈로 변조할 최소 alpha 배율입니다.</summary>
    [SerializeField, Range(0.05f, 1)] private float alphaMin = 0.45f;
    /// <summary>노이즈로 변조할 최대 alpha 배율입니다.</summary>
    [SerializeField, Range(0.05f, 1)] private float alphaMax = 0.8f;
    /// <summary>Alpha 변조 혼합률입니다. 0이면 원본 alpha 배율을 유지합니다.</summary>
    [SerializeField, Range(0, 1)] private float alphaNoiseStrength = 0.65f;
    /// <summary>샘플 격자 간격, 원본 픽셀 단위이며 셰이더에서 정수로 반올림합니다.</summary>
    [SerializeField, Range(1, 8)] private float pixelSize = 1f;
    /// <summary>전체 왜곡 배율입니다. 합성 벡터는 셰이더에서 최대 3픽셀로 제한합니다.</summary>
    [SerializeField, Range(0, 3)] private float distortionStrength = 1f;
    /// <summary>SpriteRenderer.color alpha와 곱해지는 전체 불투명도입니다.</summary>
    [SerializeField, Range(0, 1)] private float opacity = 0.65f;
    /// <summary>패턴의 재현 가능한 기본 seed입니다. 같은 seed와 설정이면 같은 패턴입니다.</summary>
    [SerializeField, Min(0)] private int randomSeed = 1;
    /// <summary>켜면 인스턴스 ID를 섞어 같은 Material과 기본 seed도 개체별로 다르게 표시합니다.</summary>
    [SerializeField] private bool useInstanceSeed = true;

    // Renderer와 두 block은 컴포넌트 수명 동안 재사용하며 매 프레임 할당하지 않습니다.
    private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock fogBlock;
    private MaterialPropertyBlock previousBlock;
    // 현재 활성 구간에서 원래 Renderer block을 확보했는지 나타냅니다.
    private bool hasPreviousBlock;

    /// <summary>기존 block을 보존하고 이 Sprite에 설정을 한 번 적용합니다.</summary>
    private void OnEnable()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (fogBlock == null) fogBlock = new MaterialPropertyBlock();
        if (previousBlock == null) previousBlock = new MaterialPropertyBlock();
        spriteRenderer.GetPropertyBlock(previousBlock);
        hasPreviousBlock = true;
        ApplyParameters();
    }

    /// <summary>활성 컴포넌트의 Inspector 변경을 반영하며 비활성 개체는 건드리지 않습니다.</summary>
    private void OnValidate()
    {
        if (isActiveAndEnabled && hasPreviousBlock) ApplyParameters();
    }

    /// <summary>이 활성 구간 이전의 block을 복원하고 소유권을 반환합니다.</summary>
    private void OnDisable()
    {
        if (hasPreviousBlock && spriteRenderer != null)
            spriteRenderer.SetPropertyBlock(previousBlock.isEmpty ? null : previousBlock);
        hasPreviousBlock = false;
    }

    /// <summary>
    /// Inspector 값을 Renderer에 적용합니다. 런타임에서 값을 바꾸거나 Sprite/Material을 바꾼 뒤 다시 호출합니다.
    /// Atlas, 부분 rect, Tight mesh, Sliced/Tiled Sprite는 UV wrap 범위를 보장하지 못하므로 적용하지 않습니다.
    /// </summary>
    [ContextMenu("Apply Fog Parameters")]
    public void ApplyParameters()
    {
        if (!isActiveAndEnabled || !hasPreviousBlock) return;
        Sprite sprite = spriteRenderer.sprite;
        Material material = spriteRenderer.sharedMaterial;
        if (material == null || material.shader == null || material.shader.name != ShaderName)
        {
            Debug.LogWarning("FogMotionController requires an explicitly assigned Cashier/2D/PixelFog material.", this);
            return;
        }
        if (sprite == null || sprite.packed || spriteRenderer.drawMode != SpriteDrawMode.Simple ||
            sprite.rect != new Rect(0, 0, sprite.texture.width, sprite.texture.height) ||
            sprite.vertices.Length != 4 ||
            Vector2.Distance((Vector2)sprite.bounds.size, sprite.rect.size / sprite.pixelsPerUnit) > 0.001f)
        {
            Debug.LogWarning("FogMotionController requires a standalone PNG, Full Rect mesh and Simple SpriteRenderer. Do not pack this sprite in an atlas.", this);
            return;
        }
        spriteRenderer.GetPropertyBlock(fogBlock);
        fogBlock.SetFloat(FlowSpeedXId, flowSpeedX);
        fogBlock.SetFloat(FlowSpeedYId, flowSpeedY);
        fogBlock.SetFloat(NoiseScale1Id, noiseScale1);
        fogBlock.SetFloat(NoiseSpeed1Id, noiseSpeed1);
        fogBlock.SetFloat(NoiseStrength1Id, noiseStrength1);
        fogBlock.SetFloat(NoiseScale2Id, noiseScale2);
        fogBlock.SetFloat(NoiseSpeed2Id, noiseSpeed2);
        fogBlock.SetFloat(NoiseStrength2Id, noiseStrength2);
        fogBlock.SetFloat(SwirlStrengthId, swirlStrength);
        fogBlock.SetFloat(SwirlScaleId, swirlScale);
        fogBlock.SetFloat(SwirlSpeedId, swirlSpeed);
        fogBlock.SetFloat(AlphaMinId, alphaMin);
        fogBlock.SetFloat(AlphaMaxId, alphaMax);
        fogBlock.SetFloat(AlphaNoiseStrengthId, alphaNoiseStrength);
        fogBlock.SetFloat(PixelSizeId, pixelSize);
        fogBlock.SetFloat(DistortionStrengthId, distortionStrength);
        fogBlock.SetFloat(OpacityId, opacity);
        // 지역 hash만 사용하여 gameplay Random 상태를 소비하지 않습니다.
        uint seed = unchecked((uint)randomSeed);
        if (useInstanceSeed) seed ^= unchecked((uint)GetInstanceID()) * 2654435761u;
        fogBlock.SetFloat(SeedId, seed % 4096u);
        spriteRenderer.SetPropertyBlock(fogBlock);
    }
}
