using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>외형 자산을 초기화 중 준비하고 단계 변경 시 기존 표시 슬롯에 함께 적용한다.</summary>
public sealed class StoreStagePresentation : MonoBehaviour
{
    [SerializeField] private WorldSceneView world;
    [SerializeField] private SpriteRenderer[] worldTargets;
    [SerializeField] private Image[] frontTargets;
    [SerializeField] private Image workbench;
    [SerializeField] private RectTransform clockDigits;
    private readonly Dictionary<uint, StoreStageVisual[]> prepared = new Dictionary<uint, StoreStageVisual[]>();
    public uint AppliedStage { get; private set; }

    /// <summary>초기화 덮개 아래에서 3단계 모두 로드·검증한다. 취소/실패 시 표시에는 손대지 않는다.</summary>
    public async UniTask PrepareAsync(CancellationToken cancellationToken)
    {
        ValidateTargets();
        var tables = DataTableManager.Instance;
        var resources = tables.GetDB<ResourceDataTable>(DataTableType.Resource);
        var next = new Dictionary<uint, StoreStageVisual[]>();
        foreach (var row in tables.GetDB<StoreStageDataTable>(DataTableType.StoreStage).Rows.Values)
        {
            var visuals = new StoreStageVisual[3];
            var ids = new[] { row.WorldPrefabResourceIdx, row.FrontPrefabResourceIdx, row.TopViewPrefabResourceIdx };
            for (int i = 0; i < ids.Length; i++)
            {
                if (!resources.TryGetResource(ids[i], out var resource)) throw new InvalidOperationException($"StoreStage {row.Idx}: Resource {ids[i]} missing");
                var prefab = await ResourceManager.Instance.LoadAssetAsync<GameObject>(resource.Path, cancellationToken);
                visuals[i] = prefab.GetComponent<StoreStageVisual>();
                if (visuals[i] == null) throw new InvalidOperationException($"StoreStage {row.Idx}: {resource.Path} has no StoreStageVisual");
                visuals[i].Validate((StoreStageVisual.Region)i);
            }
            next.Add(row.StoreStage, visuals);
        }
        cancellationToken.ThrowIfCancellationRequested();
        prepared.Clear();
        foreach (var entry in next) prepared.Add(entry.Key, entry.Value);
    }

    /// <summary>게임 상태를 변경하지 않고 외형만 적용한다. 같은 단계의 반복 갱신은 생략한다.</summary>
    public void Apply(uint stage)
    {
        if (AppliedStage == stage) return;
        if (!prepared.TryGetValue(stage, out var visuals)) throw new InvalidOperationException($"StoreStage {stage}: assets not prepared");
        ValidateTargets();
        for (int i = 0; i < visuals.Length; i++) visuals[i].Validate((StoreStageVisual.Region)i);
        for (int i = 0; i < worldTargets.Length; i++)
        {
            var target = worldTargets[i]; var source = visuals[0].worldLayers[i];
            target.sprite = source.sprite;
            target.transform.localPosition = source.transform.localPosition;
            target.transform.localRotation = source.transform.localRotation;
            target.transform.localScale = source.transform.localScale;
        }
        for (int i = 0; i < frontTargets.Length; i++)
        {
            var target = frontTargets[i]; var source = visuals[1].images[i];
            StoreStageVisual.CopyRect(target.rectTransform, source.rectTransform);
            target.sprite = source.sprite; target.color = source.color;
            target.preserveAspect = source.preserveAspect;
            target.useSpriteMesh = source.useSpriteMesh;
            target.enabled = source.enabled;
        }
        StoreStageVisual.CopyRect(clockDigits, visuals[1].clockDigits);
        workbench.sprite = visuals[2].images[0].sprite;
        workbench.color = visuals[2].images[0].color;
        // Workbench 자식의 물리·입력 영역과 시계/상자 버튼 인스턴스는 유지한다.
        world.SetStageGraphics(frontTargets, workbench);
        world.SetStageSmoke(visuals[0].smokeAnchors);
        AppliedStage = stage;
        world.RefreshPresentation();
    }

    /// <summary>한 슬롯이라도 없으면 부분 교체 전에 거부한다.</summary>
    private void ValidateTargets()
    {
        if (world == null || workbench == null || clockDigits == null || worldTargets == null || worldTargets.Length != 6 || frontTargets == null || frontTargets.Length != 8)
            throw new InvalidOperationException("StoreStage: scene bindings missing");
        foreach (var target in worldTargets) if (target == null) throw new InvalidOperationException("StoreStage: world target missing");
        foreach (var target in frontTargets) if (target == null) throw new InvalidOperationException("StoreStage: front target missing");
    }
}
