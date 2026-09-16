using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>GameUI 외형 로드 분기의 빈값 대체와 Sprite 소유 수명을 검사한다.</summary>
public sealed class CustomerAppearancePlaceholderTests
{
    /// <summary>딸의 빈 FK는 공유 사각형을 실제 Image에 연결하고 지정 FK 누락은 거부한다.</summary>
    /// <returns>화면 소유 Sprite 해제를 기다린다.</returns>
    [UnityTest]
    public IEnumerator DaughterBlankResourceReachesPortraitAndKeepsStrictExplicitReferences()
    {
        var root = new GameObject("DaughterPlaceholderFixture");
        var ui = root.AddComponent<GameUIController>();
        ui.enabled = false;
        var panel = new GameObject("DaughterPanel");
        panel.transform.SetParent(root.transform);
        panel.SetActive(false);
        var portrait = new GameObject("Portrait", typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
        portrait.transform.SetParent(panel.transform);
        var text = new GameObject("Dialogue", typeof(TMPro.TextMeshProUGUI)).GetComponent<TMPro.TextMeshProUGUI>();
        text.transform.SetParent(panel.transform);
        var presenter = panel.AddComponent<DaughterDialoguePresenter>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(DaughterDialoguePresenter).GetField("portrait", flags).SetValue(presenter, portrait);
        typeof(DaughterDialoguePresenter).GetField("speechBubble", flags).SetValue(presenter, portrait);
        typeof(DaughterDialoguePresenter).GetField("dialogue", flags).SetValue(presenter, text);
        try
        {
            var square = (Sprite)typeof(GameUIController).GetMethod("getPlaceholderSprite", flags).Invoke(ui, null);
            var texts = new TextDataTable();
            texts.LoadData("idx,text\n8192,Test dialogue");
            typeof(TextDataTable).GetMethod("Commit", flags).Invoke(texts, null);
            var factory = new ProgressViewDataFactory(new CustomerCatalog(new CustomerAppearanceDataTable(),
                new CustomerDispositionDataTable(), new ProductCategoryDataTable(), new ProductDataTable()),
                texts, new Dictionary<uint, Sprite>());
            var service = new DaughterDialogueService(new[] { new DaughterDialogueData { Idx = 16004, TextIdxs = new uint[] { 8192 } } },
                new[] { new DaughterAppearanceData { Idx = 17001, StartDay = 1 } }, new System.Random(1));
            var result = service.Select(1, 0);
            var view = factory.CreateDaughterDialogueViewData(result, null, square);
            Assert.That(view.Sprite, Is.SameAs(square));
            panel.SetActive(true);
            presenter.UpdateView(view);
            Assert.That(portrait.sprite, Is.SameAs(square));
            Assert.That(text.text, Is.EqualTo("Test dialogue"));
            Assert.Throws<System.InvalidOperationException>(() => factory.CreateDaughterDialogueViewData(result, null));
            var explicitService = new DaughterDialogueService(new[] { new DaughterDialogueData { Idx = 16004, TextIdxs = new uint[] { 8192 } } },
                new[] { new DaughterAppearanceData { Idx = 17001, StartDay = 1, ResourceIdx = 4201 } }, new System.Random(1));
            Assert.Throws<System.InvalidOperationException>(() => factory.CreateDaughterDialogueViewData(explicitService.Select(1, 0), null, square));
            Assert.That(factory.CreateDaughterDialogueViewData(explicitService.Select(1, 0),
                new Dictionary<uint, Sprite> { [4201] = square }).Sprite, Is.SameAs(square));
        }
        finally { Object.Destroy(root); }
        yield return null;
        yield return null;
        LogAssert.NoUnexpectedReceived();
    }

    /// <summary>빈값은 Addressables 없이 사각형을 공유하고 지정된 이미지는 그대로 사용한다.</summary>
    /// <returns>화면 파괴 후 Sprite 해제를 대기한다.</returns>
    [UnityTest]
    public IEnumerator BlankAppearanceUsesSharedSquareAndReleasesItWithView()
    {
        var root = new GameObject("AppearancePlaceholderFixture");
        var ui = root.AddComponent<GameUIController>();
        ui.enabled = false; // 부트스트랩 Start 대신 실제 이미지 로드 분기만 실행한다.
        var method = typeof(GameUIController).GetMethod("loadAppearanceSpriteAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        var cached = new Dictionary<uint, Sprite>();
        Sprite square = null;
        var real = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero, 100);
        try
        {
            var blank = new CustomerAppearanceData { Idx = 5001, ImageResourceIdx = null };
            square = ((UniTask<Sprite>)method.Invoke(ui, new object[] { blank, null, cached })).GetAwaiter().GetResult();
            var second = ((UniTask<Sprite>)method.Invoke(ui, new object[] { blank, null, cached })).GetAwaiter().GetResult();
            Assert.That(square, Is.Not.Null);
            Assert.That(second, Is.SameAs(square));
            Assert.That(square.texture, Is.SameAs(Texture2D.whiteTexture));
            Assert.That(square.rect.width, Is.EqualTo(square.rect.height));
            Assert.That(square.bounds.size.y, Is.GreaterThan(0), "Queue height scaling must remain finite.");
            cached.Add(4201, real);
            var specified = new CustomerAppearanceData { Idx = 5002, ImageResourceIdx = 4201 };
            var loaded = ((UniTask<Sprite>)method.Invoke(ui, new object[] { specified, null, cached })).GetAwaiter().GetResult();
            Assert.That(loaded, Is.SameAs(real));
        }
        finally { Object.Destroy(root); }
        yield return null;
        yield return null;
        Assert.That(square == null, Is.True, "Only the view-owned placeholder must be destroyed.");
        Assert.That(real != null, Is.True);
        Object.Destroy(real);
        LogAssert.NoUnexpectedReceived();
    }
}
