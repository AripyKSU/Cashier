using UnityEngine;
using UnityEngine.UI;

/// <summary>편집 가능한 UI의 최종 메시를 월드 렌더에 전달하며 문자와 입력은 그대로 유지합니다.</summary>
[ExecuteAlways]
public sealed class DystopiaPixelSource : BaseMeshEffect
{
    /// <summary>저해상도 렌더가 활성화된 동안만 원래 그래픽을 대체합니다.</summary>
    [System.NonSerialized] public bool capture;
    /// <summary>Shadow 등 앞선 효과가 반영된 최종 로컬 메시입니다.</summary>
    public Mesh CapturedMesh { get; private set; }

    /// <summary>원래 그래픽의 메시를 보관하고 이중 표시를 막습니다.</summary>
    /// <param name="vertices">uGUI가 구성한 현재 메시입니다.</param>
    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || !capture) return;
        if (CapturedMesh == null) { CapturedMesh = new Mesh { name = "Pixel source mesh", hideFlags = HideFlags.HideAndDontSave }; CapturedMesh.MarkDynamic(); }
        vertices.FillMesh(CapturedMesh);
        vertices.Clear();
    }

    /// <summary>렌더 경로를 바꿀 때만 그래픽 메시를 재구축합니다.</summary>
    /// <param name="value">월드 렌더 사용 여부입니다.</param>
    internal void SetCapture(bool value)
    {
        if (capture == value) return;
        capture = value;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    /// <summary>임시 메시를 해제하고 UI 효과의 기존 정리 절차를 수행합니다.</summary>
    protected override void OnDestroy()
    {
        if (CapturedMesh != null) DestroyImmediate(CapturedMesh);
        base.OnDestroy();
    }
}
