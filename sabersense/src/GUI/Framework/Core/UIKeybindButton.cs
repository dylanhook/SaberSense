// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Input;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

internal sealed class UIKeybindButton : UIElement
{
    private static readonly int MaxBindingIndex = (int)VrButtonBinding.RightStick;

    private readonly UILabel _label;

    private int _currentIndex;
    private bool _listening;
    private UIKeybindListener? _listener;
    private Action<int>? _onChanged;

    public UIKeybindButton(string name = "KeybindButton") : base(name)
    {
        _label = new UILabel("Text", "[None]")
        .SetFontSize(UITheme.FontSmall)
        .SetColor(UITheme.TextKeybind)
        .SetAlignment(TMPro.TextAlignmentOptions.Right);
        _label.RectTransform.SetParent(RectTransform, false);
        _label.SetAnchors(Vector2.zero, Vector2.one);
        _label.TextComponent.raycastTarget = false;

        var hitImage = RectTransform.gameObject.AddComponent<Image>();
        hitImage.color = new Color(0, 0, 0, 0);
        hitImage.raycastTarget = true;

        var handler = RectTransform.gameObject.AddComponent<PointerEventHandler>();
        handler.OnClick = HandleClick;
    }

    public static string GetButtonName(int index)
    => VrButtonBindings.DisplayName((VrButtonBinding)index);

    public UIKeybindButton SetValue(int index)
    {
        _currentIndex = Mathf.Clamp(index, 0, MaxBindingIndex);
        UpdateLabel();
        return this;
    }

    public UIKeybindButton OnValueChanged(Action<int> callback)
    {
        _onChanged = callback;
        return this;
    }

    private void HandleClick()
    {
        if (_listening)
        CancelListening();
        else
        StartListening();
    }

    private void StartListening()
    {
        _listening = true;
        _label.SetText("[...]");
        _label.SetColor(UITheme.TextKeybindActive);

        _listener = _label.GameObject.AddComponent<UIKeybindListener>();
        _listener.OnCaptured = idx =>
        {
            _listening = false;
            _listener = null;
            _currentIndex = idx;
            UpdateLabel();
            UICallbackGuard.Invoke(_onChanged!, idx);
        };
        _listener.OnCancelled = () =>
        {
            _listening = false;
            _listener = null;
            UpdateLabel();
        };
    }

    private void CancelListening()
    {
        _listener?.Cancel();
        _listener = null;
        _listening = false;
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        _label.SetText($"[{GetButtonName(_currentIndex)}]");
        _label.SetColor(UITheme.TextKeybind);
    }

    public override void Dispose()
    {
        if (_listening) CancelListening();
        base.Dispose();
    }
}