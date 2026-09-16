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
    private readonly RawImage[] counterExtensions = new RawImage[2];
    private readonly Dictionary<uint, GameObject> facilityVisuals = new Dictionary<uint, GameObject>();
    private readonly List<Graphic> facilityGraphics = new List<Graphic>();
    private bool containerOpen;
    private IReadOnlyDictionary<uint, (StoreStageVisual[] Visuals, Sprite Clock, Dictionary<uint, GameObject> Facilities)> prepared =
        new Dictionary<uint, (StoreStageVisual[] Visuals, Sprite Clock, Dictionary<uint, GameObject> Facilities)>();
    public uint AppliedStage { get; private set; }

    /// <summary>초기화 덮개 아래에서 3단계 모두 로드·검증한다. 취소/실패 시 표시에는 손대지 않는다.</summary>
    public async UniTask PrepareAsync(CancellationToken cancellationToken)
    {
        ValidateTargets();
        var tables = DataTableManager.Instance;
        var resources = tables.GetDB<ResourceDataTable>(DataTableType.Resource);
        var facilities = tables.GetDB<FacilityDataTable>(DataTableType.Facility).Rows;
        var next = new Dictionary<uint, (StoreStageVisual[] Visuals, Sprite Clock, Dictionary<uint, GameObject> Facilities)>();
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
            if (!resources.TryGetResource(row.ClockResourceIdx, out var clockResource))
                throw new InvalidOperationException($"StoreStage {row.Idx}: Resource {row.ClockResourceIdx} missing");
            var clock = await ResourceManager.Instance.LoadAssetAsync<Sprite>(clockResource.Path, cancellationToken);
            if (clock == null) throw new InvalidOperationException($"StoreStage {row.Idx}: {clockResource.Path} is not a Sprite");
            var facilityPrefabs = new Dictionary<uint, GameObject>();
            foreach (FacilityData facility in facilities.Values)
            {
                uint resourceIdx = facility.GetStageResourceIdx(row.StoreStage);
                if (resourceIdx == 0) continue;
                if (!resources.TryGetResource(resourceIdx, out var facilityResource))
                    throw new InvalidOperationException($"Facility {facility.Idx}: Resource {resourceIdx} missing");
                var facilityPrefab = await ResourceManager.Instance.LoadAssetAsync<GameObject>(facilityResource.Path, cancellationToken);
                if (facilityPrefab == null || facilityPrefab.GetComponent<RectTransform>() == null ||
                    facilityPrefab.GetComponentsInChildren<Graphic>(true).Length == 0)
                    throw new InvalidOperationException($"Facility {facility.Idx}: {facilityResource.Path} is not a visual prefab");
                facilityPrefabs.Add(facility.Idx, facilityPrefab);
            }
            next.Add(row.StoreStage, (visuals, clock, facilityPrefabs));
        }
        cancellationToken.ThrowIfCancellationRequested();
        prepared = next;
    }

    /// <summary>단계 변경은 외형을 교체하고 같은 단계에서는 설비 활성 상태만 갱신한다.</summary>
    /// <param name="stage">현재 가게 단계.</param>
    /// <param name="isFacilityActive">세션 권위의 설비 활성 판정.</param>
    /// <param name="sortingPanel">단계별 쏟기 이미지를 받을 기존 분류 패널.</param>
    public void Apply(uint stage, Func<uint, bool> isFacilityActive, SaleSortingPanel sortingPanel = null)
    {
        if (isFacilityActive == null) throw new ArgumentNullException(nameof(isFacilityActive));
        if (!prepared.TryGetValue(stage, out var stageAssets)) throw new InvalidOperationException($"StoreStage {stage}: assets not prepared");
        if (sortingPanel != null)
        {
            StoreStageVisual top = stageAssets.Visuals[(int)StoreStageVisual.Region.TopView];
            sortingPanel.SetContainerSprites(top.tiltedContainerSprite, top.emptyContainerSprite);
        }
        if (AppliedStage == stage)
        {
            this.refreshFacilityVisibility(stageAssets.Facilities, isFacilityActive);
            world.RefreshPresentation();
            return;
        }
        var visuals = stageAssets.Visuals;
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
        this.containerOpen = false;
        this.applyCounterExtensions(visuals[1].counterExtensions);
        frontTargets[5].sprite = visuals[1].images[5].sprite;
        frontTargets[6].sprite = stageAssets.Clock;
        StoreStageVisual.CopyRect(clockDigits, visuals[1].clockDigits);
        workbench.sprite = visuals[2].images[0].sprite;
        workbench.color = visuals[2].images[0].color;
        // Workbench 자식의 물리·입력 영역과 시계/상자 버튼 인스턴스는 유지한다.
        this.rebuildFacilities(stageAssets.Facilities, isFacilityActive);
        world.SetStageGraphics(frontTargets, workbench, counterExtensions, facilityGraphics);
        world.SetStageSmoke(visuals[0].smokeAnchors);
        AppliedStage = stage;
        world.RefreshPresentation();
    }

    /// <summary>현재 단계의 닫힌/열린 상자 Sprite만 교체한다.</summary>
    /// <param name="open">작업대 진입 상태이면 true.</param>
    public void SetContainerOpen(bool open)
    {
        this.containerOpen = open;
        if (this.AppliedStage == 0 || this.frontTargets == null || this.frontTargets.Length <= 5 || this.frontTargets[5] == null ||
            !this.prepared.TryGetValue(this.AppliedStage, out var stageAssets)) return;
        StoreStageVisual front = stageAssets.Visuals[(int)StoreStageVisual.Region.Front];
        this.frontTargets[5].sprite = open ? front.openContainerSprite : front.images[5].sprite;
    }

    /// <summary>상판 대상이 소유하는 두 확장면만 만들고 준비된 표시값을 적용한다.</summary>
    private void applyCounterExtensions(RawImage[] sources)
    {
        for (int i = 0; i < counterExtensions.Length; i++)
        {
            RawImage target = counterExtensions[i];
            if (sources.Length == 0)
            {
                if (target != null) target.enabled = false;
                continue;
            }
            if (target == null)
            {
                var item = new GameObject(i == 0 ? "CounterLeftExtension" : "CounterRightExtension",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                item.transform.SetParent(frontTargets[0].transform, false);
                target = item.GetComponent<RawImage>();
                counterExtensions[i] = target;
            }
            RawImage source = sources[i];
            StoreStageVisual.CopyRect(target.rectTransform, source.rectTransform);
            target.texture = source.texture;
            target.uvRect = source.uvRect;
            target.color = source.color;
            target.raycastTarget = false;
            target.enabled = source.enabled;
        }
    }

    /// <summary>이 컴포넌트가 생성한 확장면만 정리한다.</summary>
    private void OnDestroy()
    {
        foreach (RawImage extension in counterExtensions)
            if (extension != null) Destroy(extension.gameObject);
        foreach (GameObject facility in facilityVisuals.Values)
            if (facility != null) Destroy(facility);
    }

    /// <summary>이전 단계 설비를 숨긴 뒤 현재 단계의 표시 인스턴스를 소유한다.</summary>
    /// <param name="prefabs">설비 ID별 준비된 외형.</param>
    /// <param name="isActive">현재 활성 판정.</param>
    private void rebuildFacilities(IReadOnlyDictionary<uint, GameObject> prefabs, Func<uint, bool> isActive)
    {
        foreach (GameObject visual in facilityVisuals.Values)
            if (visual != null)
            {
                visual.SetActive(false);
                Destroy(visual);
            }
        facilityVisuals.Clear();
        facilityGraphics.Clear();
        Transform parent = frontTargets[0].transform.parent;
        int siblingIndex = frontTargets[0].transform.GetSiblingIndex() + 1;
        foreach (var pair in prefabs)
        {
            GameObject visual = Instantiate(pair.Value, parent, false);
            visual.name = $"Facility_{pair.Key}";
            visual.transform.SetSiblingIndex(siblingIndex++);
            foreach (Graphic graphic in visual.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
                facilityGraphics.Add(graphic);
            }
            visual.SetActive(isActive(pair.Key));
            facilityVisuals.Add(pair.Key, visual);
        }
    }

    /// <summary>단계가 같아도 다음 날 활성화된 설비를 기존 인스턴스에 반영한다.</summary>
    /// <param name="prefabs">현재 단계의 설비.</param>
    /// <param name="isActive">현재 활성 판정.</param>
    private void refreshFacilityVisibility(IReadOnlyDictionary<uint, GameObject> prefabs, Func<uint, bool> isActive)
    {
        foreach (uint facilityIdx in prefabs.Keys)
            if (facilityVisuals.TryGetValue(facilityIdx, out GameObject visual)) visual.SetActive(isActive(facilityIdx));
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
