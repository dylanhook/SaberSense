// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public class UIGroupBox : UIElement
{
    public RectTransform Content { get; private set; }

    public UIGroupBox(string title, string name = "GroupBox") : base(name)
    {
        float textStartX = -1;
        float textWidth = -1;

        var titleLabel = new UILabel("GroupTitle", title)
        .SetFontSize(UITheme.FontSmall)
        .SetColor(UITheme.TextLabel);
        titleLabel.TextComponent.fontStyle = TMPro.FontStyles.Bold;
        titleLabel.TextComponent.alignment = TMPro.TextAlignmentOptions.Left;

        if (!string.IsNullOrEmpty(title))
        {
            titleLabel.RectTransform.SetParent(RectTransform, false);
            titleLabel.RectTransform.anchorMin = new Vector2(0, 1);
            titleLabel.RectTransform.anchorMax = new Vector2(0, 1);
            titleLabel.RectTransform.pivot = new Vector2(0, 0.5f);

            textStartX = 4f;
            textWidth = titleLabel.TextComponent.GetPreferredValues(title).x;

            titleLabel.RectTransform.anchoredPosition = new Vector2(textStartX, 0);
            titleLabel.RectTransform.sizeDelta = new Vector2(textWidth, 4);

            textStartX -= 0.8f;
            textWidth += 1.6f;
        }

        var bg = new UIImage("GroupBg")
        .SetColor(UITheme.SurfaceInner);
        bg.RectTransform.SetParent(RectTransform, false);
        bg.SetAnchors(Vector2.zero, Vector2.one);
        bg.RectTransform.offsetMin = new Vector2(0.4f, 0.4f);
        bg.RectTransform.offsetMax = new Vector2(-0.4f, -0.4f);

        UIBorderUtils.DrawBorderLines("Outer", RectTransform, UITheme.Border, 0f, 0.2f, textStartX, textWidth);
        UIBorderUtils.DrawBorderLines("Inner", RectTransform, UITheme.Divider, 0.2f, 0.2f, textStartX, textWidth);

        if (!string.IsNullOrEmpty(title)) titleLabel.RectTransform.SetAsLastSibling();

        var viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(bg.RectTransform, false);
        var viewportRect = viewportGO.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(4, 2);
        viewportRect.offsetMax = new Vector2(-5, -4);
        var maskImg = viewportGO.AddComponent<Image>();
        maskImg.color = Color.white;
        maskImg.material = UIMaterials.NoBloomMaterial;
        maskImg.raycastTarget = true;
        var mask = viewportGO.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportRect, false);
        Content = contentGO.AddComponent<RectTransform>();
        Content.anchorMin = new Vector2(0, 1);
        Content.anchorMax = Vector2.one;
        Content.pivot = new Vector2(0.5f, 1f);
        Content.sizeDelta = Vector2.zero;

        var contentLayout = contentGO.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 2f;
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;

        var sizeFitter = contentGO.AddComponent<ContentSizeFitter>();
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        var scrollRect = bg.GameObject.AddComponent<VRSafeScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = Content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 5f;

        var scrollbarGO = new GameObject("Scrollbar");
        scrollbarGO.transform.SetParent(bg.RectTransform, false);
        var scrollbarRect = scrollbarGO.AddComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1, 0);
        scrollbarRect.anchorMax = Vector2.one;
        scrollbarRect.sizeDelta = new Vector2(1, 0);
        scrollbarRect.anchoredPosition = Vector2.zero;
        scrollbarRect.pivot = new Vector2(1, 0.5f);

        scrollbarRect.offsetMin = new Vector2(scrollbarRect.offsetMin.x, 0.15f);
        scrollbarRect.offsetMax = new Vector2(scrollbarRect.offsetMax.x, -0.3f);

        var scrollbarBg = scrollbarGO.AddComponent<Image>();
        scrollbarBg.color = UITheme.SurfaceHover;
        scrollbarBg.material = UIMaterials.NoBloomMaterial;
        scrollbarBg.raycastTarget = true;

        var hitPad = new GameObject("ScrollHitPad");
        hitPad.transform.SetParent(scrollbarGO.transform, false);
        var hitPadRect = hitPad.AddComponent<RectTransform>();
        hitPadRect.anchorMin = Vector2.zero;
        hitPadRect.anchorMax = Vector2.one;
        hitPadRect.offsetMin = new Vector2(-3f, 0);
        hitPadRect.offsetMax = Vector2.zero;
        var hitPadImg = hitPad.AddComponent<Image>();
        hitPadImg.color = new Color(0, 0, 0, 0);
        hitPadImg.material = UIMaterials.NoBloomMaterial;
        hitPadImg.raycastTarget = true;

        var handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(scrollbarGO.transform, false);
        var handleRect = handleGO.AddComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.sizeDelta = Vector2.zero;
        handleRect.anchoredPosition = Vector2.zero;
        handleRect.offsetMin = new Vector2(0.15f, 0);
        handleRect.offsetMax = new Vector2(-0.15f, 0);

        var handleImg = handleGO.AddComponent<Image>();
        handleImg.color = UITheme.ScrollHandle;
        handleImg.material = UIMaterials.NoBloomMaterial;

        var dummyHandleGO = new GameObject("DummyHandle");
        dummyHandleGO.transform.SetParent(scrollbarGO.transform, false);
        var dummyHandleRect = dummyHandleGO.AddComponent<RectTransform>();
        dummyHandleRect.anchorMin = Vector2.zero;
        dummyHandleRect.anchorMax = Vector2.one;
        dummyHandleRect.sizeDelta = Vector2.zero;

        var scrollbar = scrollbarGO.AddComponent<VRSafeScrollbar>();
        scrollbar.handleRect = dummyHandleRect;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = handleImg;
        scrollbar.transition = Selectable.Transition.None;
        scrollbar.SetVisibleHandle(handleRect);

        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scrollRect.verticalScrollbarSpacing = 0;

        var guard = bg.GameObject.AddComponent<ContentGuard>();
        guard.Init(scrollRect, viewportRect, Content);

        GameObject.AddComponent<CanvasAttachGuard>();
    }

    public UIGroupBox SizeToContent()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(Content);
        float contentHeight = LayoutUtility.GetPreferredHeight(Content);

        const float chromeHeight = 0.4f + 0.4f + 2f + 4f;
        float totalHeight = contentHeight + chromeHeight;

        var le = RectTransform.GetComponent<LayoutElement>() ?? RectTransform.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = totalHeight;
        return this;
    }
}