// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public interface IKeyboardTarget
{
    void AppendChar(char c);
    void Backspace();
    void SetFocused(bool focused);
    void CloseKeyboard();
}

public class UIVirtualKeyboard : UIElement
{
    private const float PanelWidth = 60f;
    private const float PanelHeight = 25f;
    private const float BorderInset = 0.2f;
    private const float KeyRowSpacing = 0.5f;
    private const float KeyRowHeight = 3.5f;
    private const float LayoutPadding = 1f;
    private const float BackdropAlpha = 0.6f;
    private const float ToggleKeyWidth = 6f;
    private const float DoneKeyWidth = 8f;

    private const string BackspaceSymbol = "\u232B";
    private const string SpaceLabel = "Space";
    private const string DoneLabel = "Done";
    private const string NumberModeLabel = "123";
    private const string LetterModeLabel = "ABC";

    private static readonly string[] LetterRows =
    {
        "QWERTYUIOP",
        "ASDFGHJKL",
        "ZXCVBNM"
    };

    private static readonly string[] NumberRows =
    {
        "1234567890",
        "-=[];',./",
        "!@#$%^&*"
    };

    private readonly IKeyboardTarget _target;
    private readonly RectTransform _canvasRoot;
    private readonly UIImage _backdrop;
    private readonly UIImage _panel;
    private readonly GameObject _lettersGO;
    private readonly GameObject _numbersGO;

    private bool _showingNumbers;

    public UIVirtualKeyboard(IKeyboardTarget target, RectTransform canvasRoot) : base("VirtualKeyboard")
    {
        _target = target;
        _canvasRoot = canvasRoot;

        _backdrop = new UIImage("KBBackdrop")
        .SetColor(new Color(0, 0, 0, BackdropAlpha));
        _backdrop.ImageComponent.raycastTarget = true;
        _backdrop.AddComponent<PointerEventHandler>().OnClick = Hide;

        _panel = new UIImage("KBPanel")
        .SetColor(UITheme.Border);
        _panel.RectTransform.SetParent(_backdrop.RectTransform, false);
        _panel.RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _panel.RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _panel.RectTransform.pivot = new Vector2(0.5f, 0.5f);
        _panel.RectTransform.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        _panel.ImageComponent.raycastTarget = true;
        _panel.AddComponent<PointerEventHandler>();

        var innerBorder = new UIImage("KBInner")
        .SetColor(UITheme.Divider);
        innerBorder.RectTransform.SetParent(_panel.RectTransform, false);
        innerBorder.SetAnchors(Vector2.zero, Vector2.one);
        innerBorder.RectTransform.offsetMin = new Vector2(BorderInset, BorderInset);
        innerBorder.RectTransform.offsetMax = new Vector2(-BorderInset, -BorderInset);
        innerBorder.ImageComponent.raycastTarget = false;

        var bg = new UIImage("KBBg")
        .SetColor(UITheme.Surface);
        bg.RectTransform.SetParent(innerBorder.RectTransform, false);
        bg.SetAnchors(Vector2.zero, Vector2.one);
        bg.RectTransform.offsetMin = new Vector2(BorderInset, BorderInset);
        bg.RectTransform.offsetMax = new Vector2(-BorderInset, -BorderInset);
        bg.ImageComponent.raycastTarget = false;

        var mainLayout = new VBox("KBLayout");
        mainLayout.RectTransform.SetParent(bg.RectTransform, false);
        mainLayout.SetAnchors(Vector2.zero, Vector2.one);
        mainLayout.RectTransform.sizeDelta = Vector2.zero;
        UnityEngine.Object.Destroy(mainLayout.GameObject.GetComponent<ContentSizeFitter>());
        mainLayout.SetSpacing(KeyRowSpacing).SetPadding(
        (int)LayoutPadding, (int)LayoutPadding,
        (int)LayoutPadding, (int)LayoutPadding);

        _lettersGO = BuildKeyGrid("Letters", LetterRows, mainLayout.RectTransform);

        _numbersGO = BuildKeyGrid("Numbers", NumberRows, mainLayout.RectTransform);
        _numbersGO.SetActive(false);

        BuildBottomRow(mainLayout.RectTransform);

        _backdrop.GameObject.SetActive(false);

        UIPopupHelper.SetupPopupCanvas(_backdrop.GameObject, _canvasRoot, UIZLayer.Keyboard);
    }

    public void Show()
    {
        _showingNumbers = false;
        _lettersGO.SetActive(true);
        _numbersGO.SetActive(false);

        foreach (var handler in _backdrop.GameObject.GetComponentsInChildren<PointerEventHandler>(true))
        handler.OnExit?.Invoke();

        _backdrop.RectTransform.SetParent(_canvasRoot, false);
        _backdrop.SetAnchors(Vector2.zero, Vector2.one);
        _backdrop.RectTransform.sizeDelta = Vector2.zero;
        _backdrop.RectTransform.SetAsLastSibling();
        _backdrop.GameObject.SetActive(true);

        UIFocusManager.Instance.PushModal(_backdrop);
        _target.SetFocused(true);
    }

    public void Hide()
    {
        UIFocusManager.Instance.PopModal(_backdrop);
        _backdrop.GameObject.SetActive(false);
        _backdrop.RectTransform.SetParent(RectTransform, false);
        _target.CloseKeyboard();
    }

    private GameObject BuildKeyGrid(string name, string[] rows, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();

        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = KeyRowSpacing;

        go.AddComponent<LayoutElement>().flexibleHeight = 1;

        foreach (var row in rows)
        BuildKeyRow(row, go.transform);

        return go;
    }

    private void BuildKeyRow(string keys, Transform parent)
    {
        var row = new HBox("Row");
        row.RectTransform.SetParent(parent, false);
        row.SetSpacing(KeyRowSpacing);
        row.AddLayoutElement(preferredHeight: KeyRowHeight, flexibleWidth: 1);
        row.LayoutGroup.childAlignment = TextAnchor.MiddleCenter;

        foreach (char c in keys)
        {
            char ch = c;
            var key = MakeKey(ch.ToString(), preferredWidth: 0);
            key.RectTransform.SetParent(row.RectTransform, false);
            key.AddLayoutElement(flexibleWidth: 1, preferredHeight: KeyRowHeight);
            key.OnClick = () => _target.AppendChar(char.ToLower(ch));
        }
    }

    private void BuildBottomRow(Transform parent)
    {
        var bottomRow = new HBox("BottomRow");
        bottomRow.RectTransform.SetParent(parent, false);
        bottomRow.SetSpacing(KeyRowSpacing);
        bottomRow.AddLayoutElement(preferredHeight: KeyRowHeight, flexibleWidth: 1);
        bottomRow.LayoutGroup.childAlignment = TextAnchor.MiddleCenter;

        var toggleBtn = MakeKey(NumberModeLabel, ToggleKeyWidth);
        toggleBtn.RectTransform.SetParent(bottomRow.RectTransform, false);
        toggleBtn.OnClick = () =>
        {
            _showingNumbers = !_showingNumbers;
            _lettersGO.SetActive(!_showingNumbers);
            _numbersGO.SetActive(_showingNumbers);
            toggleBtn.Label.SetText(_showingNumbers ? LetterModeLabel : NumberModeLabel);
        };

        var spaceBtn = MakeKey(SpaceLabel, preferredWidth: 0);
        spaceBtn.RectTransform.SetParent(bottomRow.RectTransform, false);
        spaceBtn.AddLayoutElement(flexibleWidth: 1, preferredHeight: KeyRowHeight);
        spaceBtn.OnClick = () => _target.AppendChar(' ');

        var bkspBtn = MakeKey(BackspaceSymbol, ToggleKeyWidth);
        bkspBtn.RectTransform.SetParent(bottomRow.RectTransform, false);
        bkspBtn.OnClick = () => _target.Backspace();

        var doneBtn = MakeKey(DoneLabel, DoneKeyWidth);
        doneBtn.RectTransform.SetParent(bottomRow.RectTransform, false);
        doneBtn.NormalSprite = UIGradient.AccentVert;
        doneBtn.HoverSprite = UIGradient.AccentVert;
        doneBtn.PressedSprite = UIGradient.AccentVert;
        doneBtn.GradientOverlay.SetSprite(doneBtn.NormalSprite);
        doneBtn.Label.SetColor(Color.white);
        doneBtn.OnClick = Hide;
    }

    private static BaseButton MakeKey(string label, float preferredWidth)
    {
        var btn = new BaseButton(label, showAccent: false);
        btn.Label.SetFontSize(UITheme.FontSmall);
        if (preferredWidth > 0)
        btn.AddLayoutElement(preferredWidth: preferredWidth, preferredHeight: KeyRowHeight);
        return btn;
    }

    public override void Dispose()
    {
        if (IsDisposed) return;
        Hide();
        base.Dispose();
    }
}