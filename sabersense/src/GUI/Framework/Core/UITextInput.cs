// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public class UITextInput : UIElement, IKeyboardTarget
{
    private const string CursorChar = "_";
    private const float BorderInset = 0.2f;
    private const float TextPaddingLeft = 1f;
    private const float ClearButtonMargin = -5f;
    private const float ClearButtonWidth = 4f;

    public string Text { get; private set; } = "";

    private readonly RectTransform _canvasRoot;
    private readonly UILabel _textLabel;
    private readonly UIImage _clearBtn;
    private readonly string _placeholderText;

    private Action<string>? _onTextChanged;
    private UIVirtualKeyboard? _keyboard;
    private bool _isFocused;

    public UITextInput(string name, string placeholder, RectTransform canvasRoot) : base(name)
    {
        _canvasRoot = canvasRoot;
        _placeholderText = placeholder ?? "Type here...";

        var outerBorder = new UIImage("OuterBorder").SetColor(UITheme.Border);
        outerBorder.RectTransform.SetParent(RectTransform, false);
        outerBorder.SetAnchors(Vector2.zero, Vector2.one);
        outerBorder.RectTransform.sizeDelta = Vector2.zero;
        outerBorder.ImageComponent.raycastTarget = true;

        var innerBorder = new UIImage("InnerBorder").SetColor(UITheme.Divider);
        innerBorder.RectTransform.SetParent(outerBorder.RectTransform, false);
        innerBorder.SetAnchors(Vector2.zero, Vector2.one);
        innerBorder.RectTransform.offsetMin = new Vector2(BorderInset, BorderInset);
        innerBorder.RectTransform.offsetMax = new Vector2(-BorderInset, -BorderInset);
        innerBorder.ImageComponent.raycastTarget = false;

        var fill = new UIImage("Fill").SetColor(UITheme.SurfaceSubtle);
        fill.RectTransform.SetParent(innerBorder.RectTransform, false);
        fill.SetAnchors(Vector2.zero, Vector2.one);
        fill.RectTransform.offsetMin = new Vector2(BorderInset, BorderInset);
        fill.RectTransform.offsetMax = new Vector2(-BorderInset, -BorderInset);
        fill.ImageComponent.raycastTarget = false;

        var viewport = new GameObject("TextViewport");
        var vpRect = viewport.AddComponent<RectTransform>();
        vpRect.SetParent(fill.RectTransform, false);
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = new Vector2(TextPaddingLeft, 0);
        vpRect.offsetMax = new Vector2(ClearButtonMargin, 0);
        var maskImg = viewport.AddComponent<Image>();
        maskImg.color = Color.white;
        maskImg.material = UIMaterials.NoBloomMaterial;
        maskImg.raycastTarget = false;
        var mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        _textLabel = new UILabel("InputText", _placeholderText);
        _textLabel.RectTransform.SetParent(vpRect, false);
        _textLabel.SetAnchors(Vector2.zero, Vector2.one);
        _textLabel.RectTransform.sizeDelta = Vector2.zero;
        _textLabel.SetFontSize(UITheme.FontSmall);
        _textLabel.SetColor(UITheme.TextPlaceholder);
        _textLabel.SetAlignment(TMPro.TextAlignmentOptions.Left);
        _textLabel.TextComponent.enableAutoSizing = false;
        _textLabel.TextComponent.overflowMode = TMPro.TextOverflowModes.Masking;

        var clearContainer = new GameObject("ClearContainer");
        var ccRect = clearContainer.AddComponent<RectTransform>();
        ccRect.SetParent(fill.RectTransform, false);
        ccRect.anchorMin = new Vector2(1, 0);
        ccRect.anchorMax = new Vector2(1, 1);
        ccRect.pivot = new Vector2(1, 0.5f);
        ccRect.sizeDelta = new Vector2(ClearButtonWidth, 0);
        ccRect.anchoredPosition = Vector2.zero;

        _clearBtn = new UIImage("ClearBtn").SetColor(Color.clear);
        _clearBtn.RectTransform.SetParent(ccRect, false);
        _clearBtn.SetAnchors(Vector2.zero, Vector2.one);
        _clearBtn.RectTransform.sizeDelta = Vector2.zero;
        _clearBtn.ImageComponent.raycastTarget = true;

        var clearLabel = new UILabel("ClearX", "x");
        clearLabel.RectTransform.SetParent(_clearBtn.RectTransform, false);
        clearLabel.SetAnchors(Vector2.zero, Vector2.one);
        clearLabel.RectTransform.sizeDelta = Vector2.zero;
        clearLabel.SetFontSize(UITheme.FontSmall);
        clearLabel.SetColor(UITheme.TextPlaceholder);
        clearLabel.SetAlignment(TMPro.TextAlignmentOptions.Center);
        clearLabel.TextComponent.enableAutoSizing = false;
        clearLabel.TextComponent.raycastTarget = false;

        _clearBtn.GameObject.SetActive(false);

        var clearHandler = _clearBtn.AddComponent<PointerEventHandler>();
        clearHandler.OnClick = () => SetText("");

        var barHandler = outerBorder.AddComponent<PointerEventHandler>();
        barHandler.OnClick = OpenKeyboard;
    }

    public UITextInput OnTextChanged(Action<string> callback)
    {
        _onTextChanged = callback;
        return this;
    }

    public string GetText() => Text;

    public UITextInput SetText(string text)
    {
        Text = text ?? "";
        UpdateDisplay();
        UICallbackGuard.Invoke(_onTextChanged!, Text);
        return this;
    }

    public void AppendChar(char c)
    {
        Text += c;
        UpdateDisplay();
        UICallbackGuard.Invoke(_onTextChanged!, Text);
    }

    public void Backspace()
    {
        if (Text.Length is 0) return;
        Text = Text[..^1];
        UpdateDisplay();
        UICallbackGuard.Invoke(_onTextChanged!, Text);
    }

    public void SetFocused(bool focused)
    {
        _isFocused = focused;
        UpdateDisplay();
    }

    public void CloseKeyboard()
    {
        SetFocused(false);
    }

    private void UpdateDisplay()
    {
        bool empty = string.IsNullOrEmpty(Text);

        if (empty && !_isFocused)
        {
            _textLabel.SetText(_placeholderText);
            _textLabel.SetColor(UITheme.TextPlaceholder);
        }
        else
        {
            string display = _isFocused ? Text + CursorChar : Text;
            _textLabel.SetText(display);
            _textLabel.SetColor(UITheme.TextSecondary);
        }

        _clearBtn.GameObject.SetActive(!empty);
    }

    private void OpenKeyboard()
    {
        if (_keyboard is null)
        _keyboard = new UIVirtualKeyboard(this, _canvasRoot);
        _keyboard.Show();
    }
}