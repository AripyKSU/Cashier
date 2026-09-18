using System;
using UnityEngine;
using static CustomerWorldQueueView;

public class WorldVisit : MonoBehaviour
{
    [SerializeField] private SpriteRenderer appearance = null;
    [SerializeField] private SpriteRenderer reaction = null;

    public Visual ToVisual(CustomerVisit visit, Transform visualRoot, Transform start, Sprite sprite, Texture2D normalTexture, WorldSceneView world, CustomerPortraitLayout layout, WorldQueueSpeech speech, float phase)
    {
        transform.SetParent(visualRoot, false);
        transform.position = start.position;
        appearance.sprite = sprite;
        appearance.transform.localScale = Vector3.one * (layout.DisplayHeight / sprite.bounds.size.y);
        Vector3 bodyBounds = sprite.bounds.min;
        appearance.transform.localPosition = -Vector3.Scale(new Vector3(sprite.bounds.center.x, bodyBounds.y, 0), appearance.transform.localScale);
        var bodyProperties = new MaterialPropertyBlock();
        world.ApplyCustomerLighting(bodyProperties);
        bodyProperties.SetTexture("_NormalMap", normalTexture);
        bodyProperties.SetFloat("_NormalStrength", 1f);
        bodyProperties.SetFloat("_Surface", 2f);
        appearance.SetPropertyBlock(bodyProperties);
        appearance.color = Color.clear;
        reaction.sortingOrder = 220;
        reaction.gameObject.SetActive(false);

        Visual visual = new Visual
        {
            Root = transform,
            Body = appearance,
            BodyProperties = bodyProperties,
            Reaction = reaction,
            Speech = speech,
            SpeechTMP = speech.TMP,
            Phase = phase,
            Attributes = visit.Attributes,
            DisplayHeight = layout.DisplayHeight,
            RisePixels = layout.RisePixels,
            BreathPeriod = 2.9f * Mathf.Lerp(.88f, 1.12f, Mathf.Repeat(phase, 1))
        };

        return visual;
    }
}
