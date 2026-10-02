using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>표현 트윈의 중단·재활성·완료 이벤트와 시간 기준을 검증합니다. 화면 품질 검증은 아닙니다.</summary>
public sealed class PresentationTweenTests
{
    private GameObject root;
    private Sprite sprite;
    private float originalTimeScale;

    /// <summary>테스트 소유 화면과 임시 Sprite만 준비합니다.</summary>
    [SetUp]
    public void SetUp()
    {
        originalTimeScale = Time.timeScale;
        root = new GameObject("PresentationTweenTests", typeof(RectTransform), typeof(Canvas));
        sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f);
    }

    /// <summary>자기 객체를 제거해 남은 트윈 callback 오류도 확인합니다.</summary>
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = originalTimeScale;
        Object.Destroy(root);
        Object.Destroy(sprite);
        yield return null;
        LogAssert.NoUnexpectedReceived();
    }

    /// <summary>완료 강제 호출과 중단 뒤 재진입에서도 한 요청의 이벤트는 한 번만 발생합니다.</summary>
    [UnityTest]
    public IEnumerator LedgerCancelAndCompleteDoNotDuplicateNotification()
    {
        var view = child("Ledger").AddComponent<DailySettlementLedgerView>();
        var left = child("Left").AddComponent<TextMeshProUGUI>();
        var right = child("Right").AddComponent<TextMeshProUGUI>();
        set(view, "leftPageText", left); set(view, "rightPageText", right);
        set(view, "charactersPerSecond", 30f); set(view, "pageIntervalSeconds", .03f);
        int completed = 0;
        view.OnPresentationCompleted += () => completed++;
        Time.timeScale = 0f;
        view.Present(new DailySettlementLedgerText("ABC", "DEF"));
        view.CompleteImmediately(); view.CompleteImmediately();
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(completed, Is.EqualTo(1));
        view.Present(new DailySettlementLedgerText("ABCDEFGHIJ", "KLMNOP"));
        view.gameObject.SetActive(false);
        yield return new WaitForSecondsRealtime(.4f);
        Assert.That(completed, Is.EqualTo(1), "Kill must not complete a cancelled presentation.");
        view.gameObject.SetActive(true);
        view.Present(new DailySettlementLedgerText("AB", "CD"));
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(completed, Is.EqualTo(2));
        Assert.That(right.maxVisibleCharacters, Is.GreaterThanOrEqualTo(2));
    }

    /// <summary>감독관의 scaled fade는 차단 중 멈추며 취소된 퇴장은 모델 완료를 알리지 않습니다.</summary>
    [UnityTest]
    public IEnumerator InspectorBlockCancelAndReenterPreserveExitContract()
    {
        var obj = child("Inspector"); obj.SetActive(false);
        var portrait = child("Portrait").AddComponent<Image>(); portrait.sprite = sprite;
        var text = child("Dialogue").AddComponent<TextMeshProUGUI>();
        var group = child("Group").AddComponent<CanvasGroup>();
        var button = child("Next").AddComponent<Button>();
        var view = obj.AddComponent<InspectorPresenter>();
        set(view, "portrait", portrait); set(view, "dialogue", text);
        set(view, "dialogueGroup", group); set(view, "nextButton", button); set(view, "fadeSeconds", .08f);
        obj.SetActive(true);
        int exits = 0;
        view.ExitCompleted += _ => exits++;
        bool blocked = true;
        var entering = (InspectorEventSnapshot)System.Activator.CreateInstance(typeof(InspectorEventSnapshot),
            BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { 1u, null, 0, InspectorEventPhase.Dialogue }, null);
        var leaving = (InspectorEventSnapshot)System.Activator.CreateInstance(typeof(InspectorEventSnapshot),
            BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { 1u, null, 0, InspectorEventPhase.AwaitingExit }, null);
        view.Present(entering, "ABC", sprite, true, () => blocked);
        yield return new WaitForSecondsRealtime(.15f);
        Assert.That(group.alpha, Is.Zero);
        blocked = false; Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(.15f);
        Assert.That(group.alpha, Is.Zero);
        Time.timeScale = 1f;
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(button.interactable, Is.True);
        view.Present(leaving, "ABC", sprite, true, () => blocked);
        obj.SetActive(false);
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(exits, Is.Zero);
        obj.SetActive(true);
        view.Present(leaving, "ABC", sprite, true, () => blocked);
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(exits, Is.EqualTo(1));
    }

    /// <summary>딸 타이핑은 일시정지에서도 완료되고 재요청·비활성화는 완료를 중복 전송하지 않습니다.</summary>
    [UnityTest]
    public IEnumerator DaughterCancelReplayAndUnscaledCompletion()
    {
        var obj = child("Daughter"); obj.SetActive(false);
        var portrait = child("Portrait").AddComponent<Image>();
        var bubble = child("Bubble").AddComponent<Image>();
        var text = child("Text").AddComponent<TextMeshProUGUI>();
        var view = obj.AddComponent<DaughterDialoguePresenter>();
        set(view, "portrait", portrait); set(view, "speechBubble", bubble); set(view, "dialogue", text);
        set(view, "charactersPerSecond", 30f);
        obj.SetActive(true);
        Vector2 rest = portrait.rectTransform.anchoredPosition;
        int completed = 0;
        view.OnPresentationCompleted += () => completed++;
        view.UpdateView(new DaughterDialogueViewData(1, "ABCDEFGHIJK", sprite));
        view.Present(); obj.SetActive(false);
        yield return new WaitForSecondsRealtime(.4f);
        Assert.That(completed, Is.Zero);
        obj.SetActive(true); Time.timeScale = 0f;
        view.UpdateView(new DaughterDialogueViewData(1, "ABC", sprite));
        view.Present();
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(completed, Is.EqualTo(1));
        Assert.That(portrait.rectTransform.anchoredPosition, Is.EqualTo(rest));
        view.Present();
        yield return null;
        Assert.That(completed, Is.EqualTo(1));
    }

    /// <summary>도장 중단은 완료로 세지 않고 재생 완료만 한 번 통지합니다.</summary>
    [UnityTest]
    public IEnumerator StampCancellationRestoresTransformAndCanReplay()
    {
        var obj = child("Stamp"); obj.SetActive(false);
        var image = obj.AddComponent<Image>();
        var view = obj.AddComponent<ReputationStampPresenter>();
        set(view, "stampImage", image);
        foreach (string field in new[] { "notoriousSprite", "unpopularSprite", "neutralSprite", "popularSprite", "excellentSprite" })
            set(view, field, sprite);
        set(view, "durationSeconds", .12f);
        obj.SetActive(true);
        int completed = 0;
        view.OnPresentationCompleted += () => completed++;
        view.UpdateView(1, 0); view.Present();
        yield return null;
        obj.SetActive(false);
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(completed, Is.Zero);
        Assert.That(image.rectTransform.localScale, Is.EqualTo(Vector3.one));
        obj.SetActive(true); Time.timeScale = 0f; view.Present();
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(completed, Is.EqualTo(1));
        Assert.That(image.rectTransform.localScale, Is.EqualTo(Vector3.one));
    }

    /// <summary>먼지의 차단·취소와 10개 이미지 재사용을 검사합니다.</summary>
    [UnityTest]
    public IEnumerator DustBlockStopAndReplayReuseImages()
    {
        var obj = child("Dust");
        var view = obj.AddComponent<LandingDustEffect>();
        set(view, "durationSeconds", .12f);
        bool blocked = true;
        view.SetPresentationBlockQuery(() => blocked);
        view.Play(Vector2.zero);
        var images = obj.GetComponentsInChildren<Image>();
        Assert.That(images.Length, Is.EqualTo(10));
        float alpha = images[0].color.a;
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(view.IsPlaying, Is.True);
        Assert.That(images[0].color.a, Is.EqualTo(alpha));
        view.Stop();
        Assert.That(view.IsPlaying, Is.False);
        Assert.That(images[0].color.a, Is.Zero);
        blocked = false; Time.timeScale = 0f;
        view.Play(Vector2.one);
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(view.IsPlaying, Is.False);
        CollectionAssert.AreEqual(images, obj.GetComponentsInChildren<Image>());
        Assert.That(images[0].color.a, Is.Zero);
    }

    /// <summary>테스트 소유 Canvas 아래에만 UI 객체를 생성합니다.</summary>
    private GameObject child(string name)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(root.transform, false);
        return obj;
    }

    /// <summary>공개 계약을 늘리지 않고 테스트 fixture의 직렬화 필드를 연결합니다.</summary>
    private static void set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
