// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public class UIListCell : UIElement
{
    public UIImage Background { get; protected set; } = null!;
    public UIImage Icon { get; private set; } = null!;
    public UILabel Title { get; protected set; } = null!;
    public UILabel Subtitle { get; private set; } = null!;

    public Action? OnClicked;

    protected PointerEventHandler _eventHandler = null!;
    private Coroutine? _bgTween;
    protected bool _isSelected;
    protected Image _accentImage = null!;

    private readonly GameObject _heartContainer = null!;
    private readonly RectTransform _iconContainer = null!;
    private GameObject? _placeholderGO;

    private static Sprite? _heartSprite;

    protected UIListCell(string name) : base(name) { }

    public UIListCell(UIListCellData data) : base("Cell")
    {
        var layout = GameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.spacing = 2f;
        layout.padding = new RectOffset(1, 1, 1, 1);

        Background = new UIImage("Bg")
        .SetColor(UITheme.Surface);
        Background.RectTransform.SetParent(RectTransform, false);
        Background.SetAnchors(Vector2.zero, Vector2.one);
        Background.ImageComponent.raycastTarget = true;
        Background.AddComponent<LayoutElement>().ignoreLayout = true;

        var iconContainer = new GameObject("IconContainer");
        iconContainer.transform.SetParent(RectTransform, false);
        var iconRect = iconContainer.AddComponent<RectTransform>();
        _iconContainer = iconRect;
        var iconLayout = iconContainer.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = 8;
        iconLayout.preferredHeight = 8;

        if (data.Icon != null)
        {
            Icon = new UIImage("Icon")
            .SetSprite(data.Icon);
            Icon.RectTransform.SetParent(iconRect, false);
            Icon.SetAnchors(Vector2.zero, Vector2.one);
            Icon.RectTransform.offsetMin = new Vector2(0.5f, 0.5f);
            Icon.RectTransform.offsetMax = new Vector2(-0.5f, -0.5f);
            Icon.RectTransform.anchoredPosition = Vector2.zero;
            Icon.ImageComponent.preserveAspect = true;
            Icon.ImageComponent.raycastTarget = false;
        }
        else
        {
            Icon = new UIImage("Icon")
            .SetSprite(null);
            Icon.RectTransform.SetParent(iconRect, false);
            Icon.SetAnchors(Vector2.zero, Vector2.one);
            Icon.RectTransform.offsetMin = new Vector2(0.5f, 0.5f);
            Icon.RectTransform.offsetMax = new Vector2(-0.5f, -0.5f);
            Icon.RectTransform.anchoredPosition = Vector2.zero;
            Icon.ImageComponent.preserveAspect = true;
            Icon.ImageComponent.raycastTarget = false;
            Icon.GameObject.SetActive(false);
        }

        {
            _placeholderGO = new GameObject("Placeholder");
            _placeholderGO.transform.SetParent(iconRect, false);
            var phRect = _placeholderGO.AddComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero; phRect.anchorMax = Vector2.one;
            phRect.sizeDelta = Vector2.zero;

            var outline = new UIImage("Outline").SetColor(UITheme.SurfaceCell);
            outline.RectTransform.SetParent(phRect, false);
            outline.SetAnchors(Vector2.zero, Vector2.one);
            outline.RectTransform.sizeDelta = Vector2.zero;

            var inner = new UIImage("Inner").SetColor(UITheme.SurfaceDeep);
            inner.RectTransform.SetParent(outline.RectTransform, false);
            inner.SetAnchors(Vector2.zero, Vector2.one);
            inner.RectTransform.offsetMin = new Vector2(0.2f, 0.2f);
            inner.RectTransform.offsetMax = new Vector2(-0.2f, -0.2f);

            for (int i = 0; i < 2; i++)
            {
                var bar = new UIImage("XBar" + i).SetColor(UITheme.SurfaceCell);
                bar.RectTransform.SetParent(inner.RectTransform, false);
                bar.RectTransform.anchorMin = new Vector2(0.5f, 0f);
                bar.RectTransform.anchorMax = new Vector2(0.5f, 1f);
                bar.RectTransform.sizeDelta = new Vector2(1f, 0f);
                bar.RectTransform.anchoredPosition = Vector2.zero;
                bar.RectTransform.localRotation = Quaternion.Euler(0, 0, i == 0 ? 45f : -45f);
                bar.ImageComponent.raycastTarget = false;
            }

            var textColMiss = new GameObject("MissTextCol");
            textColMiss.transform.SetParent(inner.RectTransform, false);
            var tcRect = textColMiss.AddComponent<RectTransform>();
            tcRect.anchorMin = Vector2.zero; tcRect.anchorMax = Vector2.one; tcRect.sizeDelta = Vector2.zero;
            var tcLayout = textColMiss.AddComponent<VerticalLayoutGroup>();
            tcLayout.childControlHeight = true; tcLayout.childForceExpandHeight = false;
            tcLayout.childAlignment = TextAnchor.MiddleCenter;

            new UILabel("M", "MISSING").SetFontSize(2.5f).SetColor(UITheme.TextMuted)
            .SetAlignment(TMPro.TextAlignmentOptions.Center)
            .SetParent(tcRect).AddLayoutElement(preferredHeight: UITheme.LabelHeight);
            new UILabel("C", "COVER").SetFontSize(2.5f).SetColor(UITheme.TextMuted)
            .SetAlignment(TMPro.TextAlignmentOptions.Center)
            .SetParent(tcRect).AddLayoutElement(preferredHeight: UITheme.LabelHeight);

            _placeholderGO.SetActive(data.Icon == null);
        }

        var textCol = new GameObject("TextCol");
        textCol.transform.SetParent(RectTransform, false);
        textCol.AddComponent<RectTransform>();
        var textLayout = textCol.AddComponent<VerticalLayoutGroup>();
        textLayout.childControlHeight = true;
        textLayout.childForceExpandHeight = false;
        textLayout.spacing = 0;
        textCol.AddComponent<LayoutElement>().flexibleWidth = 1;

        Title = new UILabel("Title", data.Title)
        .SetFontSize(UITheme.FontNormal)
        .SetColor(UITheme.TextPrimary)
        .SetAlignment(TMPro.TextAlignmentOptions.Left);
        Title.RectTransform.SetParent(textCol.transform, false);
        Title.AddLayoutElement(flexibleWidth: 1, preferredHeight: UITheme.ButtonRowHeight);

        Subtitle = new UILabel("Sub", data.Subtitle ?? "")
        .SetFontSize(UITheme.FontSmall)
        .SetColor(UITheme.TextSecondary)
        .SetAlignment(TMPro.TextAlignmentOptions.Left);
        Subtitle.RectTransform.SetParent(textCol.transform, false);
        Subtitle.AddLayoutElement(flexibleWidth: 1, preferredHeight: UITheme.SectionLabelHeight);
        Subtitle.GameObject.SetActive(!string.IsNullOrEmpty(data.Subtitle));

        if (_heartSprite == null)
        _heartSprite = VectorSpriteGenerator.Generate(IconPaths.Heart, 64);

        _heartContainer = new GameObject("FavIconContainer");
        _heartContainer.transform.SetParent(RectTransform, false);
        var fiLayout = _heartContainer.AddComponent<LayoutElement>();
        fiLayout.preferredWidth = 4f;
        fiLayout.preferredHeight = 4f;
        fiLayout.flexibleWidth = 0;

        if (_heartSprite != null)
        {
            var favIcon = new UIImage("FavIcon").SetSprite(_heartSprite);
            favIcon.RectTransform.SetParent(_heartContainer.transform, false);
            favIcon.SetAnchors(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            favIcon.RectTransform.sizeDelta = new Vector2(4, 4);
            favIcon.RectTransform.anchoredPosition = Vector2.zero;
            favIcon.ImageComponent.preserveAspect = true;
            favIcon.ImageComponent.raycastTarget = false;
        }

        _heartContainer.SetActive(data.IsPinned);

        var accentBar = new UIImage("SelectBar")
        .SetColor(Color.clear);
        accentBar.RectTransform.SetParent(RectTransform, false);
        accentBar.RectTransform.anchorMin = Vector2.zero;
        accentBar.RectTransform.anchorMax = new Vector2(0, 1);
        accentBar.RectTransform.sizeDelta = new Vector2(1.5f, 0);
        accentBar.RectTransform.pivot = new Vector2(0, 0.5f);
        accentBar.ImageComponent.raycastTarget = false;
        accentBar.AddComponent<LayoutElement>().ignoreLayout = true;
        _accentImage = accentBar.ImageComponent;

        _eventHandler = Background.AddComponent<PointerEventHandler>();
        _eventHandler.OnClick = () => UICallbackGuard.Invoke(OnClicked!);
        _eventHandler.OnEnter = () =>
        {
            if (!_isSelected) AnimateBg(UITheme.SurfaceHover);
        };
        _eventHandler.OnExit = () =>
        {
            if (!_isSelected) AnimateBg(UITheme.Surface);
        };
    }

    public void SetClipRect(RectTransform clipRect)
    {
        if (_eventHandler != null) _eventHandler.ClipRect = clipRect;
    }

    public virtual void SetSelected(bool selected)
    {
        _isSelected = selected;
        Background.SetColor(selected ? UITheme.SurfaceHover : UITheme.Surface);
        if (_accentImage != null)
        _accentImage.color = selected ? UITheme.Accent : Color.clear;
    }

    protected void AnimateBg(Color target)
    {
        if (_bgTween != null && _eventHandler != null)
        _eventHandler.StopCoroutine(_bgTween);
        _bgTween = UITweener.FadeColor(_eventHandler!, Background.ImageComponent, target, UITheme.AnimFast);
    }

    public void Rebind(UIListCellData data)
    {
        Title?.SetText(data.Title);
        if (Subtitle is not null)
        {
            Subtitle.SetText(data.Subtitle ?? "");
            Subtitle.GameObject.SetActive(!string.IsNullOrEmpty(data.Subtitle));
        }
        if (data.Icon != null)
        {
            Icon?.SetSprite(data.Icon);
            Icon?.GameObject.SetActive(true);
            if (_placeholderGO != null) _placeholderGO.SetActive(false);
        }
        else
        {
            Icon?.GameObject.SetActive(false);
            if (_placeholderGO != null) _placeholderGO.SetActive(true);
        }

        if (_heartContainer != null) _heartContainer.SetActive(data.IsPinned);
    }

    public void SetIcon(Sprite sprite)
    {
        if (_iconContainer == null || sprite == null) return;

        Icon?.SetSprite(sprite);
        Icon?.GameObject.SetActive(true);
        if (_placeholderGO != null) _placeholderGO.SetActive(false);
    }
}

public class UISimpleCell : UIListCell
{
    public UISimpleCell(UIListCellData data) : base("SimpleCell")
    {
        var bg = new UIImage("Bg").SetColor(UITheme.ListboxBg);
        bg.RectTransform.SetParent(RectTransform, false);
        bg.SetAnchors(Vector2.zero, Vector2.one);
        bg.ImageComponent.raycastTarget = true;
        bg.AddComponent<LayoutElement>().ignoreLayout = true;
        Background = bg;

        var layout = GameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        layout.padding = new RectOffset(2, 1, 0, 0);

        Title = new UILabel("Title", data.Title)
        .SetFontSize(UITheme.FontSmall)
        .SetColor(UITheme.ListboxText)
        .SetAlignment(TMPro.TextAlignmentOptions.Left);
        Title.RectTransform.SetParent(RectTransform, false);
        Title.AddLayoutElement(flexibleWidth: 1);

        _eventHandler = bg.AddComponent<PointerEventHandler>();
        _eventHandler.OnClick = () => UICallbackGuard.Invoke(OnClicked!);
        _eventHandler.OnEnter = () =>
        {
            if (!_isSelected) AnimateBg(UITheme.ListboxHover);
        };
        _eventHandler.OnExit = () =>
        {
            if (!_isSelected) AnimateBg(UITheme.ListboxBg);
        };
    }

    public override void SetSelected(bool selected)
    {
        _isSelected = selected;

        Background.SetColor(selected ? UITheme.ListboxSelected : UITheme.ListboxBg);
        Title?.SetColor(selected ? UITheme.Accent : UITheme.ListboxText);
    }
}