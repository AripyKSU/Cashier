using UnityEngine;
using UnityEngine.UI;

/// <summary>가계부의 글자와 구분선을 사용자 공책 이미지의 좌우 페이지 원근에 맞춥니다.</summary>
public sealed class DystopiaLedgerPage : BaseMeshEffect
{
    /// <summary>기존 정산 항목의 좌상단 좌표를 소유하는 UI 영역입니다.</summary>
    private RectTransform pageSpace;
    /// <summary>오른쪽 정산 페이지이면 true, 왼쪽 장사 기록이면 false입니다.</summary>
    private bool isRightPage;
    /// <summary>화면 소유자가 지정한 원본 배경 픽셀 단위의 인쇄 여백입니다.</summary>
    private Vector2 pageInset;

    /// <summary>기존 항목 좌표를 유지하면서 표시 메시만 페이지 안으로 투영합니다.</summary>
    /// <param name="space">정산 항목의 부모 좌표계입니다.</param>
    /// <param name="rightPage">오른쪽 페이지 여부입니다.</param>
    /// <param name="inset">원본 배경 기준 가로·세로 인쇄 여백입니다.</param>
    public void Configure(RectTransform space, bool rightPage, Vector2 inset)
    {
        pageSpace = space;
        isRightPage = rightPage;
        pageInset = new Vector2(Mathf.Clamp(inset.x,0,20),Mathf.Clamp(inset.y,0,20));
        graphic.SetVerticesDirty();
    }

    /// <summary>각 정점을 위가 좁고 아래가 넓은 공책 페이지에 대응시킵니다.</summary>
    /// <param name="vertices">원래 글자 또는 구분선의 UI 정점입니다.</param>
    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || pageSpace == null) return;
        var vertex = new UIVertex();
        for (int i = 0; i < vertices.currentVertCount; i++)
        {
            vertices.PopulateUIVertex(ref vertex,i);
            Vector3 point = pageSpace.InverseTransformPoint(transform.TransformPoint(vertex.position));
            float u = (point.x - (isRightPage ? 548 : 92)) / 350;
            float v = (-point.y - 94) / 480;
            // 334x188 원본에서 제본과 테두리를 피한 인쇄 영역입니다. 아래쪽 폭이 더 넓습니다.
            float left = (isRightPage ? 183 : Mathf.LerpUnclamped(111,96,v)) + pageInset.x;
            float right = (isRightPage ? Mathf.LerpUnclamped(245,259,v) : 170) - pageInset.x;
            Vector3 projected = new Vector3(Mathf.LerpUnclamped(left,right,u)*1280/334,-Mathf.LerpUnclamped(72+pageInset.y,164-pageInset.y,v)*720/188,point.z);
            vertex.position = transform.InverseTransformPoint(pageSpace.TransformPoint(projected));
            vertices.SetUIVertex(vertex,i);
        }
    }
}
