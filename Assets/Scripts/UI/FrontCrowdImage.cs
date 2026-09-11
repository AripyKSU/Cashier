using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

/// <summary>
/// 원경의 분리된 군중 행(Row)을 세밀한 정점 변위 메시로 렌더링하여
/// 프레임 할당 없이 유기적인 대기 출렁임 움직임을 구현하는 UI Image 컴포넌트입니다.
/// </summary>
public sealed class FrontCrowdImage : Image
{
    private const int Columns = 256;
    private static readonly float[] Heights = { 0f, 0.36f, 0.54f, 1f };

    [Tooltip("군중 행 인덱스 (0: 후열, 1: 중열, 2: 전열)")]
    [SerializeField] private int rowIndex;

    private float elapsedSeconds;

    /// <summary>
    /// 군중 행 인덱스를 반환하거나 설정합니다.
    /// </summary>
    public int RowIndex
    {
        get => this.rowIndex;
        set => this.rowIndex = value;
    }

    /// <summary>
    /// 현재 군중 행의 인덱스를 설정합니다 (0: 후열, 1: 중열, 2: 전열).
    /// </summary>
    /// <param name="index">군중 행 인덱스(0~2)</param>
    public void Configure(int index)
    {
        this.rowIndex = index;
    }

    /// <summary>
    /// 누적된 연출 경과 시간을 반영하여 정점 변위를 갱신합니다.
    /// </summary>
    /// <param name="seconds">일시정지를 제외한 누적 초</param>
    public void Animate(float seconds)
    {
        this.elapsedSeconds = seconds;
        this.SetVerticesDirty();
    }

    /// <summary>
    /// 원본 UV를 보존하면서 가로 256개 열의 어깨/머리 정점을 조화 진동 함수로 변위시킵니다.
    /// </summary>
    /// <param name="vh">uGUI 정점 헬퍼</param>
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (this.sprite == null) return;

        Rect rect = this.GetPixelAdjustedRect();
        Vector4 uv = DataUtility.GetOuterUV(this.sprite);

        for (int y = 0; y < Heights.Length; y++)
        {
            float v = Heights[y];
            // 하단(발밑)은 고정하고 어깨(0.36)부터 머리(1.0) 높이에만 변위 가중치를 둡니다.
            float weight = Mathf.InverseLerp(0.36f, 0.50f, v);

            for (int x = 0; x <= Columns; x++)
            {
                float u = (float)x / Columns;
                float person = u * (34 - this.rowIndex * 9);
                int left = Mathf.FloorToInt(person);
                float blend = Mathf.SmoothStep(0f, 1f, person - left);
                Vector2 shift = Vector2.Lerp(this.calculateMotion(left), this.calculateMotion(left + 1), blend) * weight;

                var vertex = UIVertex.simpleVert;
                vertex.color = this.color;
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

    /// <summary>
    /// 각 인물 위치에 따른 속도, 진폭, 위상을 계산하여 가로/세로 오프셋을 반환합니다.
    /// </summary>
    /// <param name="person">행 내부 인물 위치 번호</param>
    /// <returns>픽셀 단위 변위 벡터</returns>
    private Vector2 calculateMotion(int person)
    {
        float seed = person * 2.39996f + this.rowIndex * 1.73f;
        float speed = 0.65f + Mathf.Repeat(person * 0.371f + this.rowIndex * 0.217f, 1f) * 0.65f;
        float phase = this.elapsedSeconds * speed + seed;
        float rowPhase = this.elapsedSeconds * (0.48f + this.rowIndex * 0.19f) + this.rowIndex * 2.1f;

        return new Vector2(
            Mathf.Sin(rowPhase) * 0.7f + Mathf.Sin(phase) * (1f + this.rowIndex * 0.25f),
            Mathf.Sin(rowPhase * 0.79f) * 0.5f + Mathf.Sin(phase * 0.83f + seed) * (1.2f + this.rowIndex * 0.4f)
        );
    }
}
