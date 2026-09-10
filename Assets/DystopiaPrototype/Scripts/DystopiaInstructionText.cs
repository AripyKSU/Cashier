using UnityEngine;
using UnityEngine.UI;

/// <summary>작은 원화 좌표의 지침서 글자를 충분한 해상도로 생성하며 배치는 보존합니다.</summary>
[RequireComponent(typeof(Text))]
public sealed class DystopiaInstructionText : BaseMeshEffect
{
    /// <summary>확대용 글자 정점을 재사용하는 생성기입니다.</summary>
    private readonly TextGenerator generator = new TextGenerator();
    /// <summary>글자 하나의 정점 버퍼입니다.</summary>
    private readonly UIVertex[] quad = new UIVertex[4];

    /// <summary>Transform이나 글자 크기를 변경하지 않고 고해상도 글리프 메시로 교체합니다.</summary>
    /// <param name="vertices">동일한 영역에 다시 그릴 텍스트 메시입니다.</param>
    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive()) return;
        var label = graphic as Text;
        if (label == null || label.font == null || !label.font.dynamic) return;
        var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
        // 5~8px에서 굵은 한글을 만든 뒤 확대하면 내부 획이 합쳐지므로,
        // 원래 영역은 유지한 채 폰트 아틀라스에 최소 32px 글리프를 요청합니다.
        settings.scaleFactor = Mathf.Max(settings.scaleFactor, 32f / Mathf.Max(1, label.fontSize));
        settings.fontStyle = FontStyle.Normal;
        generator.PopulateWithErrors(label.text, settings, gameObject);
        var generated = generator.verts;
        vertices.Clear();
        for (int i = 0; i < generated.Count; i++)
        {
            int index = i & 3;
            quad[index] = generated[i];
            quad[index].position /= settings.scaleFactor;
            if (index == 3) vertices.AddUIVertexQuad(quad);
        }
    }

    /// <summary>런타임 UI가 제거될 때 네이티브 글자 생성기 메모리를 반환합니다.</summary>
    protected override void OnDestroy()
    {
        ((System.IDisposable)generator).Dispose();
        base.OnDestroy();
    }
}
