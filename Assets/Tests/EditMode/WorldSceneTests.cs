using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>실제 prefab 경계와 화면비·단일 색상 합성을 검증한다. UX 판정은 하지 않는다.</summary>
public sealed class WorldSceneTests
{
    /// <summary>기존 Main의 직렬화 Image 계약과 새 월드 Transform 계약은 별도 타입으로 공존한다.</summary>
    [Test]
    public void LegacyAndWorldQueueKeepSeparateSerializedContracts()
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        Assert.That(typeof(CustomerQueueView).GetField("counterAppearance", flags).FieldType, Is.EqualTo(typeof(Image)));
        Assert.That(typeof(CustomerQueueView).GetField("visualRoot", flags).FieldType, Is.EqualTo(typeof(RectTransform)));
        Assert.That(typeof(CustomerWorldQueueView).GetField("counter", flags).FieldType, Is.EqualTo(typeof(Transform)));
        var ui = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GameUI/GameUI.prefab");
        var presenter = new SerializedObject(ui.GetComponentInChildren<CustomerPresenter>(true));
        Assert.That(presenter.FindProperty("appearanceImage").objectReferenceValue, Is.Null);
        Assert.That(presenter.FindProperty("temporaryGenderText").objectReferenceValue, Is.Not.Null);
    }

    /// <summary>종횡비와 pixelRect 오프셋이 바뀌어도 viewport의 같은 점으로 대응한다.</summary>
    /// <param name="width">화면 폭.</param><param name="height">화면 높이.</param>
    [TestCase(1600, 900)]
    [TestCase(1200, 900)]
    public void ScreenPointUsesCameraViewport(int width, int height)
    {
        var go = new GameObject("WorldCamera", typeof(Camera));
        var target = new RenderTexture(width + 74, height + 42, 0);
        try
        {
            var camera = go.GetComponent<Camera>(); camera.orthographic = true;
            camera.targetTexture = target;
            camera.pixelRect = new Rect(37, 21, width, height);
            Assert.That(Vector2.Distance(camera.pixelRect.size, new Vector2(width, height)), Is.LessThan(.001f));
            Vector3 expected = camera.ViewportToWorldPoint(new Vector3(.25f, .75f, 10));
            Assert.That(Vector3.Distance(WorldSceneView.ScreenToWorld(camera, new Vector2(37 + width * .25f, 21 + height * .75f), 10), expected), Is.LessThan(.0001f));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(target); }
    }

    /// <summary>퇴장과 환경색이 서로 덮어쓰지 않고 alpha 경계를 보존한다.</summary>
    [Test]
    public void ExitTintAndAlphaHaveOneComposition()
    {
        Color tint = new Color(.72f, .72f, .72f, 1);
        Assert.That(CustomerWorldQueueView.ComposeColor(tint, 0, 1, 1), Is.EqualTo(tint));
        Assert.That(CustomerWorldQueueView.ComposeColor(tint, .5f, .5f, .5f), Is.EqualTo(new Color(.36f, .36f, .36f, .25f)));
        Assert.That(CustomerWorldQueueView.ComposeColor(tint, 1, 0, 1), Is.EqualTo(new Color(0, 0, 0, 0)));
        Assert.That(CustomerWorldQueueView.ComposeColor(tint, 0, 1, 0).a, Is.Zero);
    }

    /// <summary>실제 prefab에는 Canvas/Image가 없고 10개 슬롯·Sprite material이 연결된다.</summary>
    [Test]
    public void WorldPrefabHasNoCanvasOrUiImages()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/CustomerWorld.prefab");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<Canvas>(true), Is.Empty);
        Assert.That(prefab.GetComponentsInChildren<Image>(true), Is.Empty);
        Assert.That(prefab.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(29));
        var queue = new SerializedObject(prefab.GetComponent<CustomerWorldQueueView>());
        Assert.That(queue.FindProperty("slots").arraySize, Is.EqualTo(CustomerQueue.Capacity));
        for (int i = 0; i < CustomerQueue.Capacity; i++)
            Assert.That(queue.FindProperty("slots").GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null);
        foreach (var renderer in prefab.GetComponentsInChildren<SpriteRenderer>(true))
        {
            Assert.That(renderer.sprite, Is.Not.Null);
            Assert.That(renderer.sharedMaterial, Is.Not.Null);
            Assert.That(renderer.sharedMaterial.shader.isSupported, Is.True);
            Assert.That(ShaderUtil.GetShaderMessages(renderer.sharedMaterial.shader), Is.Empty);
            Assert.That(renderer.sprite.texture, Is.Not.Null);
        }
        var ui = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GameUI/GameUI.prefab");
        Assert.That(ui.GetComponentsInChildren<TimeOfDayUIController>(true), Is.Empty);
        var front = ui.GetComponentInChildren<SaleSortingPanel>(true).FrontView;
        Assert.That(front.Find("Customer/Appearance").GetComponent<Image>(), Is.Null);
        Assert.That(front.GetSiblingIndex(), Is.LessThan(front.parent.Find("SaleSortingUI").GetSiblingIndex()));
        Assert.That(new SerializedObject(front.Find("DialoguePanel").GetComponent<Canvas>()).FindProperty("m_OverrideSorting").boolValue, Is.False);
    }

    /// <summary>안개는 명시적 Sprite 시간·독립 FullRect 자산을 쓰고 prototype 로직을 실행하지 않는다.</summary>
    [Test]
    public void EffectsKeepSpriteTimeAndSourceBoundaries()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/CustomerWorld.prefab");
        var world = new SerializedObject(prefab.GetComponent<WorldSceneView>());
        Assert.That(world.FindProperty("timedEffects").arraySize, Is.EqualTo(4));
        Assert.That(world.FindProperty("smokeFrames").arraySize, Is.EqualTo(4));
        Assert.That(world.FindProperty("guards").arraySize, Is.EqualTo(2));
        var birds = (SpriteRenderer)world.FindProperty("timedEffects").GetArrayElementAtIndex(0).objectReferenceValue;
        Assert.That(birds.sprite.rect.size, Is.EqualTo(new Vector2(birds.sprite.texture.width, birds.sprite.texture.height)));
        foreach (var behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            Assert.That(behaviour.GetType().Name.StartsWith("Dystopia"), Is.False);
        for (int i = 1; i < 4; i++)
        {
            var renderer = (SpriteRenderer)world.FindProperty("timedEffects").GetArrayElementAtIndex(i).objectReferenceValue;
            Assert.That(renderer.sharedMaterial.GetFloat("_UseUI"), Is.Zero);
            Assert.That(renderer.sharedMaterial.GetFloat("_UsePresentationTime"), Is.EqualTo(1));
            Assert.That(renderer.sprite.vertices.Length, Is.EqualTo(4));
            Assert.That(renderer.sprite.rect.size, Is.EqualTo(new Vector2(renderer.sprite.texture.width, renderer.sprite.texture.height)));
        }
        var uiFog = AssetDatabase.LoadAssetAtPath<Material>("Assets/DystopiaPrototype/Art/FogBack.mat");
        Assert.That(uiFog.GetFloat("_UseUI"), Is.EqualTo(1));
        Assert.That(uiFog.GetFloat("_UsePresentationTime"), Is.Zero);
    }
}
