using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>설비 구매·진행 버튼의 호버·누름·거절 반응. 구매 가능 여부는 Button과 상위 화면이 정하고 여기서는 표시만 한다.</summary>
[DisallowMultipleComponent]
public sealed class FacilityButtonFeedback : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private const float HoverScale = 1.12f;
    private const float PressScale = 0.92f;
    private const float AttentionScale = 1.035f;
    private const float HoverBrightness = 1.3f;
    private const float PressBrightness = 0.8f;
    private const float HoverVolume = 0.3f;
    private const float PressVolume = 0.55f;
    private const float RejectVolume = 0.55f;
    private const float RejectSoundSeconds = 0.4f;
    private const float UnlockVolume = 0.7f;
    private const float UnlockDelaySeconds = 0.3f;

    private static readonly Color LiftEdgeColor = new Color(0.17f, 0.05f, 0.04f, 1f);
    private static readonly Color LiftShadowColor = new Color(0f, 0f, 0f, 0.45f);

    private Button button;
    /// <summary>글자까지 그림에 박힌 구매 버튼의 원본 그림입니다. 진행 버튼처럼 자체 그림이 있으면 null입니다.</summary>
    private Image bakedArt;
    private RawImage liftImage;
    private Image liftShadow;
    /// <summary>실제로 커지고 눌리는 대상입니다.</summary>
    private RectTransform visual;
    /// <summary>밝기를 바꾸는 대상입니다.</summary>
    private Graphic tintTarget;
    private Color restColor;
    private Vector3 restScale = Vector3.one;
    private Tween scaleTween;
    private Tween shakeTween;
    private bool isPointerOver;
    private bool isPressed;
    private bool rejectsWhenDisabled;
    private bool wantsAttention;

    /// <summary>글자까지 팸플릿 그림에 박힌 버튼에 붙인다. 호버할 때만 그림의 해당 부분을 잘라 띄운다.</summary>
    /// <param name="button">투명한 구매 입력 영역입니다.</param>
    /// <param name="art">버튼이 그려진 팸플릿 그림입니다.</param>
    /// <returns>붙은 component입니다.</returns>
    public static FacilityButtonFeedback AttachToBakedArt(Button button, Image art)
    {
        var feedback = button.gameObject.AddComponent<FacilityButtonFeedback>();
        feedback.button = button;
        feedback.bakedArt = art;
        feedback.createLift();
        return feedback;
    }

    /// <summary>자체 그림이 있는 버튼에 붙인다.</summary>
    /// <param name="button">진행 버튼입니다.</param>
    /// <param name="image">버튼 그림입니다.</param>
    /// <returns>붙은 component입니다.</returns>
    public static FacilityButtonFeedback AttachToSprite(Button button, Image image)
    {
        var feedback = button.gameObject.AddComponent<FacilityButtonFeedback>();
        feedback.button = button;
        feedback.visual = image.rectTransform;
        feedback.tintTarget = image;
        feedback.restColor = image.color;
        feedback.restScale = image.rectTransform.localScale;
        return feedback;
    }

    /// <summary>숨겨질 때 진행 중인 표시를 지우고 원래 모습으로 돌린다.</summary>
    private void OnDisable()
    {
        isPointerOver = false;
        isPressed = false;
        scaleTween?.Kill();
        shakeTween?.Kill();
        applyRestLook();
    }

    /// <summary>비활성 상태의 버튼을 눌렀을 때 거절 반응을 낼지 정한다. 입력을 잃으면 호버 표시도 지운다.</summary>
    /// <param name="rejects">잔액 부족처럼 이유가 있어 눌리지 않는 상태인지 여부입니다.</param>
    public void SetRejectsWhenDisabled(bool rejects)
    {
        rejectsWhenDisabled = rejects;
        if (!button.interactable && (isPointerOver || isPressed))
        {
            isPressed = false;
            animateTo(Vector3.one, Color.white, 0.1f, Ease.OutQuad, true);
        }
    }

    /// <summary>구매할 수 있는 진행 버튼을 잔잔히 맥동시켜 눈에 띄게 한다.</summary>
    /// <param name="attention">맥동 여부입니다.</param>
    public void SetAttention(bool attention)
    {
        if (wantsAttention == attention) return;
        wantsAttention = attention;
        if (!isPointerOver && !isPressed) returnToIdle();
    }

    /// <summary>진행 버튼이 막 열린 순간을 튕김과 소리로 알린다.</summary>
    public void PlayUnlocked()
    {
        if (!isActiveAndEnabled) return;
        scaleTween?.Kill();
        visual.localScale = restScale;
        // 마지막 도장이 찍힌 뒤에 이어지도록 잠깐 기다린다.
        scaleTween = DOTween.Sequence()
            .AppendInterval(UnlockDelaySeconds)
            .AppendCallback(() => SoundManager.Instance?.PlaySfx(SoundKeys.TransactionSuccess, UnlockVolume))
            .Append(visual.DOPunchScale(restScale * 0.18f, 0.45f, 6, 0.6f))
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(returnToIdle);
    }

    /// <summary>구매 가능한 버튼만 띄우고 짧은 소리를 낸다.</summary>
    /// <param name="eventData">입력 정보입니다.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerOver = true;
        if (!button.interactable) return;
        showLift();
        animateTo(Vector3.one * HoverScale, Color.white * HoverBrightness, 0.12f, Ease.OutBack, false);
        SoundManager.Instance?.PlaySfx(SoundKeys.CalculatorButton, HoverVolume);
    }

    /// <summary>포인터가 나가면 평소 모습으로 돌아간다.</summary>
    /// <param name="eventData">입력 정보입니다.</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerOver = false;
        isPressed = false;
        returnToIdle();
    }

    /// <summary>구매 가능하면 눌리고, 이유가 있어 잠겼으면 거절 반응을 낸다.</summary>
    /// <param name="eventData">입력 정보입니다.</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!button.interactable)
        {
            if (rejectsWhenDisabled) playRejected();
            return;
        }
        isPressed = true;
        showLift();
        animateTo(Vector3.one * PressScale, Color.white * PressBrightness, 0.05f, Ease.OutQuad, false);
        SoundManager.Instance?.PlaySfx(SoundKeys.RegisterClunk, PressVolume);
    }

    /// <summary>손을 떼면 호버 모습으로 튀어 오른다.</summary>
    /// <param name="eventData">입력 정보입니다.</param>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isPressed) return;
        isPressed = false;
        if (isPointerOver && button.interactable)
            animateTo(Vector3.one * HoverScale, Color.white * HoverBrightness, 0.14f, Ease.OutBack, false);
        else
            returnToIdle();
    }

    /// <summary>평소 상태로 복귀한다. 맥동이 필요하면 다시 시작한다.</summary>
    private void returnToIdle()
    {
        if (!isActiveAndEnabled) return;
        if (wantsAttention)
        {
            scaleTween?.Kill();
            tintTarget.color = restColor;
            visual.localScale = restScale;
            scaleTween = visual.DOScale(restScale * AttentionScale, 0.55f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(gameObject);
            return;
        }
        animateTo(Vector3.one, Color.white, 0.1f, Ease.OutQuad, true);
    }

    /// <summary>크기와 밝기를 함께 옮긴다.</summary>
    /// <param name="scale">평소 크기에 곱할 크기입니다.</param>
    /// <param name="brightness">평소 색에 곱할 색입니다.</param>
    /// <param name="durationSeconds">시간입니다.</param>
    /// <param name="ease">가감속입니다.</param>
    /// <param name="hideLiftAfter">끝난 뒤 띄운 그림을 숨길지 여부입니다.</param>
    private void animateTo(Vector3 scale, Color brightness, float durationSeconds, Ease ease, bool hideLiftAfter)
    {
        if (!isActiveAndEnabled) return;
        scaleTween?.Kill();
        Vector3 targetScale = Vector3.Scale(restScale, scale);
        Color startColor = tintTarget.color;
        Color targetColor = restColor * brightness;
        targetColor.a = restColor.a;
        Sequence sequence = DOTween.Sequence()
            .Join(visual.DOScale(targetScale, durationSeconds).SetEase(ease))
            .Join(DOTween.To(() => 0f, progress => tintTarget.color = Color.Lerp(startColor, targetColor, progress), 1f, durationSeconds))
            .SetUpdate(true)
            .SetLink(gameObject);
        if (hideLiftAfter) sequence.OnComplete(hideLift);
        scaleTween = sequence;
    }

    /// <summary>눌리지 않는 이유가 있는 버튼을 좌우로 떨고 실패음을 낸다.</summary>
    private void playRejected()
    {
        shakeTween?.Kill();
        showLift();
        tintTarget.color = restColor * new Color(1f, 0.62f, 0.58f, 1f);
        Vector3 rest = visual.localPosition;
        shakeTween = DOTween.To(() => 0f, progress =>
            {
                float offset = Mathf.Sin(progress * Mathf.PI * 6f) * 6f * (1f - progress);
                visual.localPosition = rest + new Vector3(Mathf.Round(offset), 0f, 0f);
            }, 1f, 0.32f)
            .SetEase(Ease.Linear)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnKill(() =>
            {
                visual.localPosition = rest;
                if (!isPointerOver || !button.interactable) applyRestLook();
            });
        SoundManager.Instance?.PlaySfxForDuration(SoundKeys.TransactionFail, RejectSoundSeconds, RejectVolume);
    }

    /// <summary>띄운 그림과 크기·색을 평소 값으로 되돌린다.</summary>
    private void applyRestLook()
    {
        if (visual == null) return;
        visual.localScale = restScale;
        tintTarget.color = restColor;
        hideLift();
    }

    /// <summary>팸플릿의 버튼 그림을 잘라 올릴 그림과 그 아래 그림자를 만든다.</summary>
    private void createLift()
    {
        var rect = (RectTransform)transform;
        liftShadow = FacilityShopEffects.CreateImage("LiftShadow", rect, LiftShadowColor);
        stretch(liftShadow.rectTransform, new Vector2(3f, -5f));
        liftShadow.enabled = false;

        var liftObject = new GameObject("Lift", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(Outline));
        liftObject.transform.SetParent(rect, false);
        liftImage = liftObject.GetComponent<RawImage>();
        liftImage.raycastTarget = false;
        liftImage.texture = bakedArt.sprite.texture;
        liftImage.enabled = false;
        var edge = liftObject.GetComponent<Outline>();
        edge.effectColor = LiftEdgeColor;
        edge.effectDistance = new Vector2(2f, -2f);
        stretch(liftImage.rectTransform, Vector2.zero);

        visual = liftImage.rectTransform;
        tintTarget = liftImage;
        restColor = bakedArt.color;
        liftImage.color = restColor;
    }

    /// <summary>현재 배치 기준으로 버튼 자리의 그림을 맞춰 띄운다.</summary>
    private void showLift()
    {
        if (bakedArt == null) return;
        liftImage.uvRect = computeArtUv();
        liftImage.enabled = true;
        liftShadow.enabled = true;
    }

    /// <summary>띄운 그림을 숨긴다.</summary>
    private void hideLift()
    {
        if (bakedArt == null) return;
        liftImage.enabled = false;
        liftShadow.enabled = false;
    }

    /// <summary>버튼 사각형이 팸플릿 Sprite의 어느 픽셀을 덮는지 UV로 구한다.</summary>
    /// <returns>텍스처 기준 UV 영역입니다.</returns>
    private Rect computeArtUv()
    {
        RectTransform artRect = bakedArt.rectTransform;
        var corners = new Vector3[4];
        ((RectTransform)transform).GetWorldCorners(corners);
        Vector2 min = artRect.InverseTransformPoint(corners[0]);
        Vector2 max = artRect.InverseTransformPoint(corners[2]);
        Rect local = artRect.rect;
        Rect spriteRect = bakedArt.sprite.textureRect;
        Texture texture = bakedArt.sprite.texture;
        float xMin = (spriteRect.x + (min.x - local.x) / local.width * spriteRect.width) / texture.width;
        float xMax = (spriteRect.x + (max.x - local.x) / local.width * spriteRect.width) / texture.width;
        float yMin = (spriteRect.y + (min.y - local.y) / local.height * spriteRect.height) / texture.height;
        float yMax = (spriteRect.y + (max.y - local.y) / local.height * spriteRect.height) / texture.height;
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    /// <summary>자식을 부모 크기에 맞춘다.</summary>
    /// <param name="rect">대상입니다.</param>
    /// <param name="offset">위치 오프셋입니다.</param>
    private static void stretch(RectTransform rect, Vector2 offset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = offset;
    }
}
