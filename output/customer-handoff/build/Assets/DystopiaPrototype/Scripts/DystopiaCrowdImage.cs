using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

/// <summary>분리된 군중 한 줄을 연속 메시로 표시해 인물 사이에 틈 없이 미세하게 움직입니다.</summary>
public sealed class DystopiaCrowdImage : Image
{
    // 원본 가로 실루엣을 약 6.5픽셀 간격으로 나누며 세로는 발밑·어깨 경계만 사용합니다.
    private const int Columns = 256;
    private static readonly float[] Heights = { 0, .36f, .54f, 1 };
    // 화면이 소유하는 일시정지 가능한 시간과 뒤에서부터 세는 줄 번호입니다.
    private float seconds;
    private int rowIndex;

    /// <summary>줄마다 다른 주기와 인물 간격을 선택합니다.</summary>
    /// <param name="index">뒤·중간·앞줄에 대응하는 0부터 2까지의 번호입니다.</param>
    public void Configure(int index) { rowIndex = index; }

    /// <summary>거래 난수와 무관한 시각 연출 시간으로 메시를 갱신합니다.</summary>
    /// <param name="elapsedSeconds">일시정지를 제외한 누적 초입니다.</param>
    public void Animate(float elapsedSeconds)
    {
        seconds = elapsedSeconds;
        SetVerticesDirty();
    }

    /// <summary>원본 UV를 유지하고 줄·인물 위치별 변위를 연결한 메시를 만듭니다.</summary>
    /// <param name="vh">uGUI에 전달할 이미지 메시입니다.</param>
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (sprite == null) return;
        Rect rect = GetPixelAdjustedRect();
        Vector4 uv = DataUtility.GetOuterUV(sprite);
        for (int y = 0; y < Heights.Length; y++)
        {
            float v = Heights[y];
            // 아래쪽은 고정하고 머리·어깨 높이에만 움직임을 줍니다.
            float weight = Mathf.InverseLerp(.36f, .50f, v);
            for (int x = 0; x <= Columns; x++)
            {
                float u = (float)x / Columns;
                float person = u * (34 - rowIndex * 9);
                int left = Mathf.FloorToInt(person);
                float blend = Mathf.SmoothStep(0, 1, person - left);
                Vector2 shift = Vector2.Lerp(Motion(left), Motion(left + 1), blend) * weight;
                var vertex = UIVertex.simpleVert;
                vertex.color = color;
                vertex.position = new Vector3(rect.xMin + u * rect.width + shift.x, rect.yMin + v * rect.height + shift.y);
                vertex.uv0 = new Vector2(Mathf.Lerp(uv.x, uv.z, u), Mathf.Lerp(uv.y, uv.w, v));
                vh.AddVert(vertex);
                if (y == 0 || x == 0) continue;
                int current = y * (Columns + 1) + x;
                vh.AddTriangle(current - Columns - 2, current, current - Columns - 1);
                vh.AddTriangle(current - Columns - 2, current - 1, current);
            }
        }
    }

    /// <summary>같은 줄에서도 인물 위치마다 다른 속도·진폭·위상을 계산합니다.</summary>
    /// <param name="person">줄 안의 인물 위치 번호입니다.</param>
    /// <returns>기준 해상도의 픽셀 단위 가로·세로 변위입니다.</returns>
    private Vector2 Motion(int person)
    {
        float seed = person * 2.39996f + rowIndex * 1.73f;
        float speed = .65f + Mathf.Repeat(person * .371f + rowIndex * .217f, 1) * .65f;
        float phase = seconds * speed + seed;
        float rowPhase = seconds * (.48f + rowIndex * .19f) + rowIndex * 2.1f;
        return new Vector2(Mathf.Sin(rowPhase) * .7f + Mathf.Sin(phase) * (1 + rowIndex * .25f),
            Mathf.Sin(rowPhase * .79f) * .5f + Mathf.Sin(phase * .83f + seed) * (1.2f + rowIndex * .4f));
    }
}
