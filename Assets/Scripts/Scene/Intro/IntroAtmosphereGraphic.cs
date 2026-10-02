using UnityEngine;
using UnityEngine.UI;

/// <summary>원본 그림 위의 작은 하늘 영역 또는 냄비 김만 그리는 인트로 전용 UI.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class IntroAtmosphereGraphic : MaskableGraphic
{
    /// <summary>원본 하늘을 이동시키거나 기존 연기 텍스처를 옅게 띄운다.</summary>
    public enum AtmosphereKind { Clouds, Steam }

    [SerializeField] private Image artwork;
    [SerializeField] private Texture2D steamTexture;
    [SerializeField] private AtmosphereKind effect;
    [SerializeField, Min(0)] private int cutIndex;
    [Tooltip("원본 그림의 왼쪽 아래를 기준으로 한 픽셀 영역. 위치와 크기는 Inspector에서 조정한다.")]
    [SerializeField] private Rect region;
    [Tooltip("원본 픽셀 단위. 구름은 좌우 이동 폭, 김은 흔들림과 상승 거리.")]
    [SerializeField] private Vector2 travelPixels;
    [SerializeField, Min(1f)] private float cycleSeconds = 80f;
    [SerializeField, Min(1f)] private float featherPixels = 18f;
    private bool showing;
    private float elapsed;

    /// <summary>원본 하늘 또는 기존 픽셀 연기만 샘플링한다.</summary>
    public override Texture mainTexture => effect == AtmosphereKind.Clouds
        ? (artwork != null ? artwork.mainTexture : Texture2D.whiteTexture)
        : (steamTexture != null ? steamTexture : Texture2D.whiteTexture);

    /// <summary>현재 컷에 해당할 때만 효과를 처음부터 표시한다.</summary>
    /// <param name="index">0부터 시작하는 그림 번호. -1은 정리.</param>
    public void ShowCut(int index)
    {
        showing = index == cutIndex;
        elapsed = 0f;
        SetAllDirty();
    }

    /// <summary>기존 재생기의 일시 정지 가능한 시간을 공유한다.</summary>
    /// <param name="delta">확인창에서는 0인 실제 시간 증분.</param>
    public void Tick(float delta)
    {
        if (!showing || delta <= 0f) return;
        elapsed += delta;
        SetVerticesDirty();
    }

    /// <summary>비활성화 이후 효과가 남거나 재시작 때 이어지지 않게 한다.</summary>
    protected override void OnDisable()
    {
        showing = false;
        elapsed = 0f;
        base.OnDisable();
    }

    /// <summary>기존 이미지의 비율 유지와 같은 좌표계로 효과를 그린다.</summary>
    /// <param name="mesh">이번 프레임의 UI 메시.</param>
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (!showing || artwork == null || artwork.sprite == null) return;
        Vector2 sourceSize = artwork.sprite.rect.size;
        Rect bounds = rectTransform.rect;
        float scale = Mathf.Min(bounds.width / sourceSize.x, bounds.height / sourceSize.y);
        Vector2 origin = bounds.center - sourceSize * scale * .5f;
        if (effect == AtmosphereKind.Clouds) drawClouds(mesh, sourceSize, origin, scale);
        else if (steamTexture != null) drawSteam(mesh, origin, scale);
    }

    /// <summary>가장자리는 원본과 일치시키고 하늘 내부만 몇 픽셀 이동한다.</summary>
    private void drawClouds(VertexHelper mesh, Vector2 sourceSize, Vector2 origin, float scale)
    {
        int columns = Mathf.Max(2, Mathf.CeilToInt(region.width / 16f));
        int rows = Mathf.Max(2, Mathf.CeilToInt(region.height / 16f));
        float drift = Mathf.Round(Mathf.Sin(elapsed * Mathf.PI * 2f / cycleSeconds) * travelPixels.x);
        Vector4 uv = UnityEngine.Sprites.DataUtility.GetOuterUV(artwork.sprite);
        for (int y = 0; y <= rows; y++)
        for (int x = 0; x <= columns; x++)
        {
            Vector2 local = new Vector2(region.width * x / columns, region.height * y / rows);
            Vector2 pixel = region.position + local;
            float edge = Mathf.Min(Mathf.Min(local.x, region.width-local.x), Mathf.Min(local.y, region.height-local.y));
            Color tint = color; tint.a *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / featherPixels));
            Vector2 sample = new Vector2(Mathf.Lerp(uv.x,uv.z,(pixel.x-drift)/sourceSize.x),
                Mathf.Lerp(uv.y,uv.w,pixel.y/sourceSize.y));
            mesh.AddVert(origin + pixel * scale, tint, sample);
            if (x == 0 || y == 0) continue;
            int n = y * (columns+1) + x;
            mesh.AddTriangle(n-columns-2,n-columns-1,n);
            mesh.AddTriangle(n-columns-2,n,n-1);
        }
    }

    /// <summary>위상이 다른 세 가닥을 냄비 영역 안에서 올라오며 사라지게 한다.</summary>
    private void drawSteam(VertexHelper mesh, Vector2 origin, float scale)
    {
        for (int i = 0; i < 3; i++)
        {
            float phase = Mathf.Repeat(elapsed / cycleSeconds + i / 3f, 1f);
            float envelope = Mathf.Sin(phase * Mathf.PI);
            float width = region.width * (.21f + phase * .17f);
            float height = region.height * (.48f + phase * .22f);
            float sway = Mathf.Sin(phase * Mathf.PI * 2f + i * 2f) * travelPixels.x;
            Vector2 center = new Vector2(region.x + region.width * (.25f + i * .25f) + sway,
                region.y + height*.5f + phase * travelPixels.y);
            Color tint = color; tint.a *= envelope * envelope;
            // 원본 픽셀 윤곽을 유지하며 얇은 김으로 겹친다.
            Vector2 min = origin + (center-new Vector2(width,height)*.5f) * scale;
            Vector2 max = min + new Vector2(width,height)*scale;
            int n = mesh.currentVertCount;
            mesh.AddVert(new Vector3(min.x,min.y),tint,new Vector2(0,0));
            mesh.AddVert(new Vector3(min.x,max.y),tint,new Vector2(0,1));
            mesh.AddVert(new Vector3(max.x,max.y),tint,new Vector2(1,1));
            mesh.AddVert(new Vector3(max.x,min.y),tint,new Vector2(1,0));
            mesh.AddTriangle(n,n+1,n+2); mesh.AddTriangle(n,n+2,n+3);
        }
    }
}
