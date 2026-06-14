// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public class PointerEventHandler : MonoBehaviour,
IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
IPointerDownHandler, IPointerUpHandler
{
    public Action? OnEnter;

    public Action? OnExit;

    public Action? OnClick;

    public Action? OnDown;

    public Action? OnUp;

    public Action<PointerEventData>? OnClickEvent;

    public UIElement? Owner;

    public RectTransform? ClipRect;

    private bool IsBlocked => Owner != null && UIFocusManager.Instance.IsInputBlocked(Owner);

    private void Start()
    {
        if (ClipRect == null)
        {
            var mask = GetComponentInParent<Mask>();
            if (mask != null)
            ClipRect = mask.rectTransform;
        }
    }

    private bool IsOutsideClip(PointerEventData eventData)
    {
        if (ClipRect == null) return false;

        var cam = eventData.pointerCurrentRaycast.module != null
        ? eventData.pointerCurrentRaycast.module.eventCamera
        : eventData.pressEventCamera;
        return !RectTransformUtility.RectangleContainsScreenPoint(ClipRect, eventData.position, cam);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (IsBlocked) return;
        OnEnter?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData) => OnExit?.Invoke();

    public void OnPointerClick(PointerEventData eventData)
    {
        if (IsBlocked || IsOutsideClip(eventData)) return;
        OnClick?.Invoke();
        OnClickEvent?.Invoke(eventData);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (IsBlocked || IsOutsideClip(eventData)) return;
        OnDown?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData) => OnUp?.Invoke();
}

public class BaseButton : UIElement
{
    private const float DefaultHeight = 4.5f;
    private const float OuterBorderInset = 0.2f;
    private const float InnerFillInset = 0.2f;
    private const float IconPadding = 2f;

    public UIImage Background { get; }

    public UIImage GradientOverlay { get; }

    public UIImage IconImage { get; }

    public UILabel Label { get; }

    public bool Interactable { get; set; } = true;

    public Sprite NormalSprite { get; set; } = UIGradient.BtnNormal;

    public Sprite HoverSprite { get; set; } = UIGradient.BtnHover;

    public Sprite PressedSprite { get; set; } = UIGradient.BtnPressed;

    public Action? OnClick;

    private readonly PointerEventHandler _eventHandler;

    public BaseButton(string text = "Button", bool showAccent = true) : base("Button")
    {
        var le = RectTransform.gameObject.AddComponent<LayoutElement>();
        le.minHeight = DefaultHeight;
        le.preferredHeight = DefaultHeight;

        Background = new UIImage("Background")
        .SetColor(UITheme.Border);
        Background.RectTransform.SetParent(RectTransform, false);
        Background.SetAnchors(Vector2.zero, Vector2.one);
        Background.ImageComponent.raycastTarget = true;

        var innerFill = new UIImage("InnerFill")
        .SetColor(UITheme.InnerBorder);
        innerFill.RectTransform.SetParent(RectTransform, false);
        innerFill.SetAnchors(Vector2.zero, Vector2.one);
        innerFill.RectTransform.offsetMin = new Vector2(OuterBorderInset, OuterBorderInset);
        innerFill.RectTransform.offsetMax = new Vector2(-OuterBorderInset, -OuterBorderInset);
        innerFill.ImageComponent.raycastTarget = false;

        GradientOverlay = new UIImage("Gradient");
        GradientOverlay.RectTransform.SetParent(innerFill.RectTransform, false);
        GradientOverlay.SetAnchors(Vector2.zero, Vector2.one);
        GradientOverlay.RectTransform.offsetMin = new Vector2(InnerFillInset, InnerFillInset);
        GradientOverlay.RectTransform.offsetMax = new Vector2(-InnerFillInset, -InnerFillInset);
        GradientOverlay.ImageComponent.raycastTarget = false;
        GradientOverlay.SetSprite(NormalSprite);
        GradientOverlay.ImageComponent.type = Image.Type.Simple;
        GradientOverlay.ImageComponent.color = Color.white;

        Label = new UILabel("Text", text);
        Label.RectTransform.SetParent(RectTransform, false);
        Label.SetAnchors(Vector2.zero, Vector2.one);
        Label.SetFontSize(UITheme.FontSmall);
        Label.SetColor(UITheme.TextPrimary);
        Label.TextComponent.fontStyle = TMPro.FontStyles.Bold;
        Label.TextComponent.alignment = TMPro.TextAlignmentOptions.Center;
        Label.TextComponent.raycastTarget = false;

        IconImage = new UIImage("IconImage");
        IconImage.RectTransform.SetParent(RectTransform, false);
        IconImage.SetAnchors(Vector2.zero, Vector2.one);
        IconImage.RectTransform.offsetMin = new Vector2(IconPadding, IconPadding);
        IconImage.RectTransform.offsetMax = new Vector2(-IconPadding, -IconPadding);
        IconImage.ImageComponent.raycastTarget = false;
        IconImage.ImageComponent.preserveAspect = true;
        IconImage.GameObject.SetActive(false);

        _eventHandler = Background.AddComponent<PointerEventHandler>();
        _eventHandler.Owner = this;
        _eventHandler.OnEnter = HandleEnter;
        _eventHandler.OnExit = HandleExit;
        _eventHandler.OnDown = HandleDown;
        _eventHandler.OnUp = HandleUp;
        _eventHandler.OnClick = HandleClick;
    }

    public BaseButton SetIcon(Sprite iconSprite)
    {
        if (iconSprite == null) return this;
        IconImage.SetSprite(iconSprite);
        IconImage.GameObject.SetActive(true);
        Label.GameObject.SetActive(false);
        return this;
    }

    public BaseButton SetText(string text)
    {
        Label.SetText(text);
        return this;
    }

    private void HandleEnter()
    {
        if (!Interactable) return;
        GradientOverlay.SetSprite(HoverSprite);
    }

    private void HandleExit()
    {
        if (!Interactable) return;
        GradientOverlay.SetSprite(NormalSprite);
    }

    private void HandleDown()
    {
        if (!Interactable) return;
        GradientOverlay.SetSprite(PressedSprite);
    }

    private void HandleUp()
    {
        if (!Interactable) return;
        GradientOverlay.SetSprite(HoverSprite);
    }

    private void HandleClick()
    {
        if (!Interactable) return;
        UICallbackGuard.Invoke(OnClick!);

        GradientOverlay.SetSprite(NormalSprite);
    }
}