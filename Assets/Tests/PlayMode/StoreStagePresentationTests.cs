#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>실제 가게 자산의 표시 API와 Editor Addressables 로드를 게임 부트와 분리해 검사한다.</summary>
public sealed class StoreStagePresentationTests
{
    private GameObject root;
    private StoreStagePresentation presentation;
    private StoreStageVisual front;
    private WorldSceneView world;
    private ResourceManager resources;
    private Dictionary<uint, (StoreStageVisual[] Visuals, Sprite Clock, Dictionary<uint, GameObject> Facilities)> prepared;

    /// <summary>실제 Prefab의 복사본만 사용하며 소스 자산과 사용자 씬은 변경하지 않는다.</summary>
    [SetUp]
    public void SetUp()
    {
        Assert.That(ResourceManager.Instance, Is.Null, "Run in the Test Runner scene.");
        root = new GameObject("StoreStageApiFixture");
        world = root.AddComponent<WorldSceneView>();
        presentation = root.AddComponent<StoreStagePresentation>();
        prepared = new Dictionary<uint, (StoreStageVisual[], Sprite, Dictionary<uint, GameObject>)>();
        var facilityRows = Util.ParseFromCSV<FacilityData>(File.ReadAllText("Assets/Datas/FacilityData.csv"));
        var resourceRows = Util.ParseFromCSV<ResourceData>(File.ReadAllText("Assets/Datas/ResourceData.csv")).ToDictionary(row => row.Idx);
        foreach (var row in Util.ParseFromCSV<StoreStageData>(File.ReadAllText("Assets/Datas/StoreStageData.csv")))
        {
            var visuals = new[] { "World", "Front", "TopView" }.Select(region =>
                AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/StoreStage/StoreStage{row.StoreStage}{region}.prefab")
                    .GetComponent<StoreStageVisual>()).ToArray();
            var clock = AssetDatabase.LoadAllAssetsAtPath($"Assets/Textures/UI/Dystopia/{resourceRows[row.ClockResourceIdx].Path}.png").OfType<Sprite>().Single();
            var facilities = facilityRows.Where(facility => facility.GetStageResourceIdx(row.StoreStage) != 0).ToDictionary(facility => facility.Idx,
                facility => AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/StoreStage/Facilities/{resourceRows[facility.GetStageResourceIdx(row.StoreStage)].Path}.prefab"));
            prepared.Add(row.StoreStage, (visuals, clock, facilities));
        }
        front = Object.Instantiate(prepared[1].Visuals[1], root.transform);
        var worldVisual = Object.Instantiate(prepared[1].Visuals[0], root.transform);
        var top = Object.Instantiate(prepared[1].Visuals[2], root.transform);
        setField(presentation, "world", world);
        setField(presentation, "worldTargets", worldVisual.worldLayers);
        setField(presentation, "frontTargets", front.images);
        setField(presentation, "workbench", top.images[0]);
        setField(presentation, "clockDigits", front.clockDigits);
        setField(presentation, "prepared", prepared);
    }

    /// <summary>테스트가 만든 객체와 로드 핸들만 해제한다.</summary>
    /// <returns>지연 파괴가 끝날 때까지 대기한다.</returns>
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (resources != null) { resources.ReleaseAll(); Object.Destroy(resources.gameObject); }
        Object.Destroy(root);
        yield return null;
        LogAssert.NoUnexpectedReceived();
    }

    /// <summary>같은 단계 활성 갱신과 단계 교체에서 대상 입력 인스턴스와 상자 상태를 보존한다.</summary>
    /// <returns>이전 단계의 지연 파괴를 대기한다.</returns>
    [UnityTest]
    public IEnumerator ApplyRefreshesFacilitiesAndResetsContainerAcrossStages()
    {
        Image counter = front.images[0], box = front.images[5], clock = front.images[6];
        presentation.Apply(1, _ => false);
        var first = front.transform.Find("Facility_12001").gameObject;
        Assert.That(first.activeSelf, Is.False);
        presentation.Apply(1, id => id == 12001);
        Assert.That(front.transform.Find("Facility_12001").gameObject, Is.SameAs(first));
        Assert.That(first.activeSelf, Is.True);
        Assert.That(front.transform.Find("Facility_12002").gameObject.activeSelf, Is.False);
        presentation.SetContainerOpen(true);
        Assert.That(box.sprite, Is.SameAs(prepared[1].Visuals[1].openContainerSprite));
        presentation.Apply(2, _ => true);
        Assert.That(first.activeSelf, Is.False, "Retired stage must disappear before delayed Destroy.");
        Assert.That(box.sprite, Is.SameAs(prepared[2].Visuals[1].images[5].sprite));
        yield return null;
        Assert.That(first == null, Is.True);
        var facilities = front.GetComponentsInChildren<Image>(true).Where(image => image.name.StartsWith("Facility_")).ToArray();
        Assert.That(facilities.Length, Is.EqualTo(4));
        Assert.That(facilities.All(image => !image.raycastTarget && image.gameObject.activeSelf), Is.True);
        Assert.That(front.transform.Find("Facility_12003").GetSiblingIndex(), Is.EqualTo(counter.transform.GetSiblingIndex() + 1));
        Assert.That(facilities.OrderBy(image => image.transform.GetSiblingIndex()).Select(image => image.name),
            Is.EqualTo(new[] { "Facility_12003", "Facility_12004", "Facility_12001", "Facility_12002" }));
        Assert.That(counter.GetComponentsInChildren<RawImage>().Count(image => image.enabled), Is.EqualTo(2));
        var tinted = (Graphic[])typeof(WorldSceneView).GetField("counterGraphics", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(world);
        Assert.That(facilities.All(image => tinted.Contains(image)), Is.True);
        presentation.Apply(3, _ => true);
        yield return null;
        Assert.That(front.GetComponentsInChildren<Image>(true).Count(image => image.name.StartsWith("Facility_")), Is.EqualTo(6));
        Assert.That(front.GetComponentsInChildren<Image>(true).Where(image => image.name.StartsWith("Facility_"))
                .OrderBy(image => image.transform.GetSiblingIndex()).Select(image => image.name),
            Is.EqualTo(new[] { "Facility_12005", "Facility_12006", "Facility_12003", "Facility_12004", "Facility_12001", "Facility_12002" }));
        presentation.Apply(1, _ => false);
        yield return null;
        Assert.That(counter.GetComponentsInChildren<RawImage>().All(image => !image.enabled), Is.True);
        Assert.That(front.images[0], Is.SameAs(counter));
        Assert.That(front.images[5], Is.SameAs(box));
        Assert.That(front.images[6], Is.SameAs(clock));
    }

    /// <summary>단계별 Sprite를 기존 쏟기·퇴장 경로에 전달하고 대상의 배치를 보존한다.</summary>
    /// <returns>단계별 쏟기와 퇴장 연출을 기다린다.</returns>
    [UnityTest]
    public IEnumerator StagePouringSpritesReachExistingImageAcrossStages()
    {
        var panel = root.AddComponent<SaleSortingPanel>();
        var image = new GameObject("PouringContainer", typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(root.transform);
        setField(panel, "containerImage", image);
        setField(panel, "pourSeconds", 0.02f);
        var position = image.rectTransform.anchoredPosition;
        var size = image.rectTransform.sizeDelta;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (uint stage in new uint[] { 1, 2, 3, 1 })
        {
            presentation.Apply(stage, _ => false, panel);
            var top = prepared[stage].Visuals[2];
            Assert.That(top.tiltedContainerSprite, Is.Not.Null);
            Assert.That(top.emptyContainerSprite, Is.Not.Null);
            Assert.Throws<ArgumentNullException>(() => panel.SetContainerSprites(top.tiltedContainerSprite, null));
            var flow = (IEnumerator)typeof(SaleSortingPanel).GetMethod("playEntryFlow", flags).Invoke(panel, null);
            Assert.That(flow.MoveNext(), Is.True);
            Assert.That(image.sprite, Is.SameAs(top.tiltedContainerSprite));
            bool sawEmpty = false;
            while (flow.MoveNext())
            {
                if (image.color.a < 1) { sawEmpty = true; Assert.That(image.sprite, Is.SameAs(top.emptyContainerSprite)); }
                yield return flow.Current;
            }
            Assert.That(sawEmpty, Is.True);
            Assert.That(image.gameObject.activeSelf, Is.False);
            Assert.That(image.rectTransform.anchoredPosition, Is.EqualTo(position));
            Assert.That(image.rectTransform.sizeDelta, Is.EqualTo(size));
            Assert.That(image.rectTransform.localRotation, Is.EqualTo(Quaternion.identity));
        }
    }

    /// <summary>상자 대상 파괴 후 알림과 표시 컴포넌트 종료가 다른 객체를 제거하지 않는다.</summary>
    /// <returns>프레임 끝의 소유 객체 해제를 대기한다.</returns>
    [UnityTest]
    public IEnumerator DestructionClearsOwnedVisualsAndLateContainerNotificationIsSafe()
    {
        presentation.Apply(2, _ => true);
        var owned = front.GetComponentsInChildren<Graphic>(true).Where(graphic => graphic is RawImage || graphic.name.StartsWith("Facility_")).ToArray();
        Object.Destroy(front.images[5].gameObject);
        yield return null;
        Assert.DoesNotThrow(() => presentation.SetContainerOpen(false));
        Object.Destroy(presentation);
        yield return null;
        yield return null;
        Assert.That(owned.All(graphic => graphic == null), Is.True);
        Assert.That(front.images[0] != null && front.images[6] != null, Is.True);
    }

    /// <summary>현재 CSV가 선택한 실제 Prefab·시계를 기존 ResourceManager로 로드한다.</summary>
    /// <returns>Editor Addressables 비동기 로드와 해제를 대기한다.</returns>
    [UnityTest]
    public IEnumerator ActualStageAndFacilityAddressesLoadThroughResourceManager()
    {
        resources = new GameObject("StoreResourceFixture").AddComponent<ResourceManager>();
        yield return wait(resources.InitAsync().AsTask());
        var rows = Util.ParseFromCSV<ResourceData>(File.ReadAllText("Assets/Datas/ResourceData.csv")).ToDictionary(row => row.Idx);
        var prefabs = new HashSet<uint>();
        var clocks = new HashSet<uint>();
        foreach (var row in Util.ParseFromCSV<StoreStageData>(File.ReadAllText("Assets/Datas/StoreStageData.csv")))
        {
            prefabs.UnionWith(new[] { row.WorldPrefabResourceIdx, row.FrontPrefabResourceIdx, row.TopViewPrefabResourceIdx });
            clocks.Add(row.ClockResourceIdx);
        }
        foreach (var row in Util.ParseFromCSV<FacilityData>(File.ReadAllText("Assets/Datas/FacilityData.csv")))
            prefabs.UnionWith(new[] { row.Stage1ResourceIdx, row.Stage2ResourceIdx, row.Stage3ResourceIdx }.Where(id => id != 0));
        foreach (uint id in prefabs)
        {
            var task = resources.LoadAssetAsync<GameObject>(rows[id].Path).AsTask();
            yield return wait(task);
            Assert.That(task.Result, Is.Not.Null, rows[id].Path);
            var visual = task.Result.GetComponent<StoreStageVisual>();
            if (visual != null && visual.region == StoreStageVisual.Region.TopView)
                Assert.DoesNotThrow(() => visual.Validate(StoreStageVisual.Region.TopView));
        }
        foreach (uint id in clocks)
        {
            var task = resources.LoadAssetAsync<Sprite>(rows[id].Path).AsTask();
            yield return wait(task);
            Assert.That(task.Result, Is.Not.Null, rows[id].Path);
        }
        Assert.That(prefabs.Count, Is.EqualTo(21));
        Assert.That(clocks.Count, Is.EqualTo(3));
    }

    /// <summary>실제 준비 API가 잘못된 설비 순서를 거부하고 이전 준비·표시 상태를 보존한다.</summary>
    /// <returns>CSV와 실제 자산 준비 완료 대기.</returns>
    [UnityTest]
    public IEnumerator PrepareRejectsInvalidFacilityOrderWithoutPublishingPartialState()
    {
        resources = new GameObject("StoreResourceFixture").AddComponent<ResourceManager>();
        yield return wait(resources.InitAsync().AsTask());
        var tables = root.AddComponent<DataTableManager>();
        yield return wait(tables.EnsureDataLoadedAsync().AsTask());
        yield return wait(presentation.PrepareAsync(default).AsTask());
        presentation.Apply(1, _ => true);
        var field = typeof(StoreStagePresentation).GetField("prepared", BindingFlags.NonPublic | BindingFlags.Instance);
        object published = field.GetValue(presentation);
        Sprite box = front.images[5].sprite;
        Assert.That(box, Is.Not.Null);
        var source = resources.GetResource<GameObject>("StoreStage3Front").GetComponent<StoreStageVisual>();
        uint[] original = source.facilityDrawOrder;
        try
        {
            // 공유 로드 결과는 메모리에서만 잠시 바꾸고 반드시 복원한다. 자산 저장/dirty 처리는 하지 않는다.
            foreach (uint[] invalid in new[] { new uint[] { 12005, 12005 }, new uint[] { 12005 }, new uint[] { uint.MaxValue } })
            {
                source.facilityDrawOrder = invalid;
                Task task = presentation.PrepareAsync(default).AsTask();
                float deadline = Time.realtimeSinceStartup + 30;
                while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(task.IsCompleted, Is.True, "Stage preparation timeout");
                Assert.That(Assert.Throws<InvalidOperationException>(() => task.GetAwaiter().GetResult()).Message,
                    Does.Contain("facility draw order"));
                Assert.That(field.GetValue(presentation), Is.SameAs(published));
                Assert.That(presentation.AppliedStage, Is.EqualTo(1));
                Assert.That(front.images[5].sprite, Is.SameAs(box));
            }
        }
        finally { source.facilityDrawOrder = original; }
        yield return wait(presentation.PrepareAsync(default).AsTask());
        Assert.That(field.GetValue(presentation), Is.Not.SameAs(published));
        presentation.Apply(3, _ => true);
        Assert.That(presentation.AppliedStage, Is.EqualTo(3));
    }

    /// <summary>테스트 준비 상태만 주입하고 제품 API를 통해 동작을 검사한다.</summary>
    /// <param name="target">테스트 대상.</param><param name="name">필드 이름.</param><param name="value">준비 값.</param>
    private static void setField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

    /// <summary>로드 실패를 관찰하며 무한 대기를 막는다.</summary>
    /// <param name="task">검사할 로드.</param><returns>완료 또는 시간 제한까지 대기한다.</returns>
    private static IEnumerator wait(Task task)
    {
        float deadline = Time.realtimeSinceStartup + 30;
        while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(task.IsCompleted, Is.True, "Resource load timeout");
        task.GetAwaiter().GetResult();
    }
}
#endif
