// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public abstract class UIDropdownBase : UIElement
{
    private readonly UIImage _buttonFill;
    private readonly UILabel _buttonLabel;

    private bool _isOpen;
    private List<string> _options = [];

    private readonly GameObject _backdropGO;
    private readonly GameObject _popupGO;
    private readonly RectTransform _popupRect;

    private readonly RectTransform? _canvasRoot;
    private readonly PopupOwnerGuard _guard;

    protected UILabel ButtonLabel => _buttonLabel;

    protected RectTransform? CanvasRoot => _canvasRoot;

    protected RectTransform PopupRect => _popupRect;

    protected GameObject PopupGO => _popupGO;

    protected IReadOnlyList<string> Options => _options;

    protected readonly List<Image> ItemBackgrounds = [];

    protected Transform? ItemContainer => _popupRect.childCount > 0 ? _popupRect.GetChild(0) : null;

    protected UIDropdownBase(string name, RectTransform? canvasRoot, float labelFontSize,
    string initialLabel, string backdropName, string popupName) : base(name)
    {
        _canvasRoot = canvasRoot;

        var le = RectTransform.gameObject.AddComponent<LayoutElement>();
        le.minHeight = UISizes.ButtonHeight;
        le.preferredHeight = UISizes.ButtonHeight;

        var buttonBorder = new UIImage("Border")
        .SetColor(UITheme.Border)
        .SetParent(this, false);
        buttonBorder.SetAnchors(Vector2.zero, Vector2.one);
        buttonBorder.ImageComponent.raycastTarget = true;

        _buttonFill = new UIImage("Fill");
        _buttonFill.RectTransform.SetParent(buttonBorder.RectTransform, false);
        _buttonFill.SetAnchors(Vector2.zero, Vector2.one);
        _buttonFill.RectTransform.offsetMin = new Vector2(0.2f, 0.2f);
        _buttonFill.RectTransform.offsetMax = new Vector2(-0.2f, -0.2f);
        _buttonFill.ImageComponent.raycastTarget = false;
        _buttonFill.SetSprite(UIGradient.CmbNormal);
        _buttonFill.ImageComponent.type = Image.Type.Simple;
        _buttonFill.ImageComponent.color = Color.white;

        _buttonLabel = new UILabel("Text", initialLabel)
        .SetFontSize(labelFontSize)
        .SetColor(UITheme.TextMuted)
        .SetAlignment(TMPro.TextAlignmentOptions.Left);
        _buttonLabel.RectTransform.SetParent(buttonBorder.RectTransform, false);
        _buttonLabel.SetAnchors(Vector2.zero, Vector2.one);
        _buttonLabel.RectTransform.offsetMin = new Vector2(1, 0);
        _buttonLabel.RectTransform.offsetMax = new Vector2(-3, 0);
        _buttonLabel.TextComponent.raycastTarget = false;

        var arrow = new UILabel("Arrow", "▼")
        .SetFontSize(UITheme.FontSmall)
        .SetColor(UITheme.TextMuted);
        arrow.RectTransform.SetParent(buttonBorder.RectTransform, false);
        arrow.SetAnchors(new Vector2(0.85f, 0), Vector2.one);
        arrow.TextComponent.raycastTarget = false;

        var handler = buttonBorder.AddComponent<PointerEventHandler>();
        handler.OnClick = ToggleDropdown;
        handler.OnEnter = () => _buttonFill.SetSprite(UIGradient.CmbHover);
        handler.OnExit = () => { if (!_isOpen) _buttonFill.SetSprite(UIGradient.CmbNormal); };

        _backdropGO = UIPopupHelper.CreateBackdrop(backdropName, _canvasRoot ?? RectTransform, RectTransform, Close);
        _popupGO = UIPopupHelper.CreatePopupContainer(popupName, _canvasRoot ?? RectTransform, RectTransform, out _popupRect, out _);

        _guard = RectTransform.gameObject.AddComponent<PopupOwnerGuard>();
        _guard.Register(_backdropGO);
        _guard.Register(_popupGO);
    }

    private void ToggleDropdown()
    {
        if (_isOpen) Close();
        else Open();
    }

    protected void Open()
    {
        if (_canvasRoot == null) return;
        _isOpen = true;
        _buttonFill.SetSprite(UIGradient.CmbHover);

        OnOpening();

        float dropH = (_options.Count * UISizes.DropdownItemHeight) + UISizes.BorderOuter;

        _backdropGO.transform.SetParent(_canvasRoot, false);
        var bRect = _backdropGO.GetComponent<RectTransform>();
        bRect.anchorMin = Vector2.zero;
        bRect.anchorMax = Vector2.one;
        bRect.sizeDelta = Vector2.zero;

        _popupGO.transform.SetParent(_canvasRoot, false);
        _popupRect.anchorMin = new Vector2(0.5f, 0.5f);
        _popupRect.anchorMax = new Vector2(0.5f, 0.5f);
        PositionPopup(dropH);

        _backdropGO.SetActive(true);
        _popupGO.SetActive(true);
        _backdropGO.transform.SetAsLastSibling();
        _popupGO.transform.SetAsLastSibling();

        OnOpened();
    }

    protected void Close()
    {
        _isOpen = false;
        _buttonFill.SetSprite(UIGradient.CmbNormal);
        _backdropGO.SetActive(false);
        _popupGO.SetActive(false);
        _backdropGO.transform.SetParent(RectTransform, false);
        _popupGO.transform.SetParent(RectTransform, false);

        OnClosed();
    }

    protected virtual void OnOpening() { }

    protected abstract void PositionPopup(float dropH);

    protected virtual void OnOpened() { }

    protected virtual void OnClosed() { }

    protected abstract void OnItemClicked(int index);

    protected void SetOptionsCore(List<string> options)
    {
        _options = options;
        RebuildItems();
    }

    protected void RebuildItems()
    {
        var container = ItemContainer;
        if (container == null) return;

        for (int i = container.childCount - 1; i >= 0; i--)
        UnityEngine.Object.Destroy(container.GetChild(i).gameObject);
        ItemBackgrounds.Clear();

        for (int i = 0; i < _options.Count; i++)
        BuildItemRow(container, i, _options[i]);
    }

    private void BuildItemRow(Transform container, int index, string text)
    {
        var itemGO = new GameObject("Item_" + index);
        itemGO.transform.SetParent(container, false);
        itemGO.AddComponent<RectTransform>();
        itemGO.AddComponent<LayoutElement>().preferredHeight = UISizes.DropdownItemHeight;

        var itemBg = itemGO.AddComponent<Image>();
        itemBg.material = UIMaterials.NoBloomMaterial;
        itemBg.color = UITheme.SurfaceLight;
        itemBg.raycastTarget = true;
        ItemBackgrounds.Add(itemBg);

        var itemLabel = new UILabel("Lbl", text)
        .SetFontSize(UITheme.FontSmall)
        .SetColor(UITheme.TextSecondary)
        .SetAlignment(TMPro.TextAlignmentOptions.Left);
        itemLabel.RectTransform.SetParent(itemGO.transform, false);
        itemLabel.SetAnchors(Vector2.zero, Vector2.one);
        itemLabel.RectTransform.offsetMin = new Vector2(1, 0);
        itemLabel.RectTransform.offsetMax = new Vector2(-1, 0);
        itemLabel.TextComponent.raycastTarget = false;

        var handler = itemGO.AddComponent<PointerEventHandler>();
        handler.OnEnter = () => itemBg.color = UITheme.SurfacePressed;
        handler.OnExit = () => itemBg.color = UITheme.SurfaceLight;
        handler.OnClick = () => OnItemClicked(index);
    }

    public override void Dispose()
    {
        if (IsDisposed) return;
        Close();
        base.Dispose();
    }
}