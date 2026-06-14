// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public class UIToggle : UIElement
{
    private const float CheckboxSize = 1.33f;
    private const float FillInset = 0.166f;
    private static readonly Color BorderColor = new(0.04f, 0.04f, 0.04f, 1f);

    public UIImage Border { get; private set; } = null!;

    private bool _isOn;

    public bool IsOn => _isOn;
    private Action<bool>? _onValueChanged;
    private readonly PointerEventHandler _eventHandler;
    private readonly Image _borderImage;
    private readonly Image _checkImage;
    private readonly List<GameObject> _dependentElements = [];

    public UIToggle(string name = "Toggle", bool defaultValue = false) : base(name)
    {
        _isOn = defaultValue;

        var le = RectTransform.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = CheckboxSize;
        le.preferredHeight = CheckboxSize;
        le.minWidth = CheckboxSize;
        le.minHeight = CheckboxSize;
        le.flexibleWidth = 0;
        le.flexibleHeight = 0;

        var borderGO = new GameObject("TBorder");
        borderGO.transform.SetParent(RectTransform, false);
        var borR = borderGO.AddComponent<RectTransform>();
        borR.anchorMin = Vector2.zero;
        borR.anchorMax = Vector2.one;
        borR.sizeDelta = Vector2.zero;
        _borderImage = borderGO.AddComponent<Image>();
        _borderImage.material = UIMaterials.NoBloomMaterial;
        _borderImage.color = BorderColor;
        _borderImage.raycastTarget = true;

        var fillGO = new GameObject("TFill");
        fillGO.transform.SetParent(borR, false);
        var filR = fillGO.AddComponent<RectTransform>();
        filR.anchorMin = Vector2.zero;
        filR.anchorMax = Vector2.one;
        filR.offsetMin = new Vector2(FillInset, FillInset);
        filR.offsetMax = new Vector2(-FillInset, -FillInset);
        filR.sizeDelta = Vector2.zero;
        _checkImage = fillGO.AddComponent<Image>();
        _checkImage.material = UIMaterials.NoBloomMaterial;
        _checkImage.type = Image.Type.Simple;
        _checkImage.color = Color.white;
        _checkImage.sprite = _isOn ? UIGradient.AccentVert : UIGradient.TglUnchecked;
        _checkImage.raycastTarget = false;

        _eventHandler = _borderImage.gameObject.AddComponent<PointerEventHandler>();
        _eventHandler.OnClick = Toggle;
        _eventHandler.OnEnter = () =>
        {
            if (!_isOn) _checkImage.sprite = UIGradient.TglHover;
        };
        _eventHandler.OnExit = () =>
        {
            if (!_isOn) _checkImage.sprite = UIGradient.TglUnchecked;
        };
    }

    public UIToggle OnValueChanged(Action<bool> callback)
    {
        _onValueChanged += callback;
        return this;
    }

    public UIToggle SetValue(bool value)
    {
        _isOn = value;
        UpdateFillVisual();
        SyncDependentVisibility();
        return this;
    }

    public void InvokeToggle() => Toggle();

    private void Toggle()
    {
        _isOn = !_isOn;
        UpdateFillVisual();
        SyncDependentVisibility();
        UICallbackGuard.Invoke(_onValueChanged!, _isOn);
    }

    private void UpdateFillVisual()
    {
        _checkImage.sprite = _isOn ? UIGradient.AccentVert : UIGradient.TglUnchecked;
    }

    public UIToggle ControlsVisibility(GameObject target)
    {
        _dependentElements.Add(target);
        target.SetActive(_isOn);
        return this;
    }

    private void SyncDependentVisibility()
    {
        foreach (var go in _dependentElements)
        if (go != null) go.SetActive(_isOn);
    }
}