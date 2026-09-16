using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>가게 외형 프리팹의 명시적 슬롯. 진행 상태나 입력 컴포넌트는 포함하지 않는다.</summary>
public sealed class StoreStageVisual : MonoBehaviour
{
    /// <summary>서로 다른 좌표계의 외형 묶음.</summary>
    public enum Region { World, Front, TopView }
    /// <summary>배경 여섯 레이어는 기존 WorldSceneView의 시간대 역할을 유지한다.</summary>
    public Region region;
    /// <summary>World: 원경/새벽/석양/밤/도시등/중경 순서.</summary>
    public SpriteRenderer[] worldLayers = Array.Empty<SpriteRenderer>();
    /// <summary>같은 배경에 맞춘 좌우 굴뚝 연기의 기준 배치.</summary>
    public Transform[] smokeAnchors = Array.Empty<Transform>();
    /// <summary>Front: 계산대/천장/좌기둥/우기둥/천장등/상자/시계/하부장. TopView: 작업대.</summary>
    public Image[] images = Array.Empty<Image>();
    /// <summary>Front의 작업대 진입 때 기존 닫힌 상자 슬롯에 표시할 열린 상자.</summary>
    public Sprite openContainerSprite;
    /// <summary>TopView에서 물품을 쏟는 동안 사용할 상자 이미지.</summary>
    public Sprite tiltedContainerSprite;
    /// <summary>TopView에서 쏟기를 마치고 퇴장할 때 사용할 상자 이미지.</summary>
    public Sprite emptyContainerSprite;
    /// <summary>Front 상판의 좌우 연장면. Left, Right 순서이며 Rect·Texture·UV를 전달한다.</summary>
    public RawImage[] counterExtensions = Array.Empty<RawImage>();
    /// <summary>Front의 기존 시계 숫자가 따를 표시창 배치.</summary>
    public RectTransform clockDigits;

    /// <summary>세트의 타입과 모든 슬롯을 적용 전에 검사한다.</summary>
    public void Validate(Region expected)
    {
        if (region != expected) throw new InvalidOperationException($"{name}: expected {expected}, got {region}");
        if (region == Region.World)
        {
            if (worldLayers == null || worldLayers.Length != 6 || smokeAnchors == null || smokeAnchors.Length != 2)
                throw new InvalidOperationException($"{name}: background/smoke slots missing");
            foreach (var layer in worldLayers)
                if (layer == null || layer.sprite == null) throw new InvalidOperationException($"{name}: background Sprite missing");
            foreach (var anchor in smokeAnchors) if (anchor == null) throw new InvalidOperationException($"{name}: smoke anchor missing");
        }
        else
        {
            if (region == Region.TopView && (tiltedContainerSprite == null || emptyContainerSprite == null))
                throw new InvalidOperationException($"{name}: pouring container sprites missing");
            if (images == null || images.Length != (region == Region.Front ? 8 : 1)) throw new InvalidOperationException($"{name}: image slots missing");
            for (int i = 0; i < images.Length; i++)
                if (images[i] == null || (images[i].enabled && images[i].sprite == null && !(region == Region.Front && i == 6)))
                    throw new InvalidOperationException($"{name}: visible Sprite missing");
            if (region == Region.Front && (clockDigits == null || openContainerSprite == null
                || counterExtensions == null || (counterExtensions.Length != 0 && counterExtensions.Length != 2)))
                throw new InvalidOperationException($"{name}: front container/extension slots missing");
            if (region == Region.Front)
                foreach (var extension in counterExtensions)
                    if (extension == null || extension.texture == null)
                        throw new InvalidOperationException($"{name}: counter extension missing");
        }
    }

    /// <summary>入力・子オブジェクトを保持して表示矩形だけをコピーする。</summary>
    public static void CopyRect(RectTransform target, RectTransform source)
    {
        target.anchorMin = source.anchorMin; target.anchorMax = source.anchorMax; target.pivot = source.pivot;
        target.sizeDelta = source.sizeDelta; target.anchoredPosition3D = source.anchoredPosition3D;
        target.localScale = source.localScale; target.localRotation = source.localRotation;
    }
}
