// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public class UIModal : UIElement
{
    private const float BackdropAlpha = 0.6f;
    private const float BorderInset = 0.3f;
    private const float LayoutSpacing = 2f;
    private const int LayoutPadding = 3;
    private const float TitleHeight = 6f;
    private const float DividerHeight = 0.3f;
    private const float ButtonHeight = 4.5f;

    public UIImage Backdrop { get; }

    public UIImage Panel { get; }

    public UIImage PanelBg { get; }

    public UILabel TitleLabel { get; }

    public VBox ContentArea { get; }

    public HBox? ButtonsRow { get; private set; }

    public UIImage? ButtonsDivider { get; private set; }

    private readonly RectTransform _canvasRoot;

    public UIModal(string title, RectTransform canvasRoot, float width = 60, float height = 40) : base("Modal")
    {
        _canvasRoot = canvasRoot;

        Backdrop = new UIImage("Backdrop")
        .SetColor(new Color(0, 0, 0, BackdropAlpha));
        Backdrop.ImageComponent.raycastTarget = true;

        Panel = new UIImage("Panel").SetColor(UITheme.Border);
        Panel.RectTransform.SetParent(Backdrop.RectTransform, false);
        Panel.RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        Panel.RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        Panel.RectTransform.pivot = new Vector2(0.5f, 0.5f);
        Panel.RectTransform.sizeDelta = new Vector2(width, height);
        Panel.ImageComponent.type = Image.Type.Simple;
        Panel.ImageComponent.raycastTarget = true;

        PanelBg = new UIImage("PanelBg").SetColor(UITheme.Surface);
        PanelBg.RectTransform.SetParent(Panel.RectTransform, false);
        PanelBg.SetAnchors(Vector2.zero, Vector2.one);
        PanelBg.RectTransform.offsetMin = new Vector2(BorderInset, BorderInset);
        PanelBg.RectTransform.offsetMax = new Vector2(-BorderInset, -BorderInset);

        var panelLayout = PanelBg.GameObject.AddComponent<VerticalLayoutGroup>();
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;
        panelLayout.spacing = LayoutSpacing;
        panelLayout.padding = new RectOffset(LayoutPadding, LayoutPadding, LayoutPadding, LayoutPadding);

        TitleLabel = new UILabel("Title", title)
        .SetFontSize(UITheme.FontLarge)
        .SetColor(UITheme.TextPrimary)
        .SetAlignment(TMPro.TextAlignmentOptions.Left);
        TitleLabel.RectTransform.SetParent(PanelBg.RectTransform, false);
        TitleLabel.AddLayoutElement(preferredHeight: TitleHeight);

        var div = new UIImage("Div")
        .SetColor(UITheme.Divider);
        div.RectTransform.SetParent(PanelBg.RectTransform, false);
        div.AddLayoutElement(preferredHeight: DividerHeight);

        ContentArea = new VBox("Content");
        ContentArea.RectTransform.SetParent(PanelBg.RectTransform, false);
        ContentArea.SetSpacing(LayoutSpacing);
        ContentArea.AddLayoutElement(flexibleHeight: 1);

        Backdrop.SetActive(false);
    }

    public void Show()
    {
        foreach (var handler in Backdrop.GameObject.GetComponentsInChildren<PointerEventHandler>(true))
        handler.OnExit?.Invoke();

        Backdrop.RectTransform.SetParent(_canvasRoot, false);
        Backdrop.SetAnchors(Vector2.zero, Vector2.one);
        Backdrop.RectTransform.sizeDelta = Vector2.zero;
        Backdrop.RectTransform.SetAsLastSibling();
        Backdrop.SetActive(true);

        UIFocusManager.Instance.PushModal(Backdrop);
    }

    public void Hide()
    {
        UIFocusManager.Instance.PopModal(Backdrop);

        Backdrop.SetActive(false);
        Backdrop.RectTransform.SetParent(RectTransform, false);
    }

    public UIModal AddButtons(string confirmText, Action onConfirm, string cancelText = "Cancel", Action? onCancel = null)
    {
        ButtonsDivider = new UIImage("BtnDiv")
        .SetColor(UITheme.Divider);
        ButtonsDivider.RectTransform.SetParent(PanelBg.RectTransform, false);
        ButtonsDivider.AddLayoutElement(preferredHeight: DividerHeight);

        ButtonsRow = new HBox("Buttons");
        ButtonsRow.RectTransform.SetParent(PanelBg.RectTransform, false);
        ButtonsRow.SetSpacing(LayoutSpacing);
        ButtonsRow.LayoutGroup.childAlignment = TextAnchor.MiddleCenter;
        ButtonsRow.AddLayoutElement(minHeight: ButtonHeight, preferredHeight: ButtonHeight, flexibleHeight: 0);

        var cancelBtn = new BaseButton(cancelText, showAccent: false);
        cancelBtn.RectTransform.SetParent(ButtonsRow.RectTransform, false);
        cancelBtn.AddLayoutElement(preferredHeight: ButtonHeight, flexibleWidth: 1);
        cancelBtn.OnClick = () => { onCancel?.Invoke(); Hide(); };

        var confirmBtn = new BaseButton(confirmText, showAccent: false);
        confirmBtn.RectTransform.SetParent(ButtonsRow.RectTransform, false);
        confirmBtn.AddLayoutElement(preferredHeight: ButtonHeight, flexibleWidth: 1);
        confirmBtn.NormalSprite = UIGradient.AccentVert;
        confirmBtn.HoverSprite = UIGradient.AccentVert;
        confirmBtn.PressedSprite = UIGradient.AccentVert;
        confirmBtn.GradientOverlay.SetSprite(confirmBtn.NormalSprite);
        confirmBtn.Label.SetColor(Color.white);
        confirmBtn.OnClick = () => { onConfirm?.Invoke(); Hide(); };

        return this;
    }

    public override void Dispose()
    {
        if (IsDisposed) return;
        Hide();
        base.Dispose();
    }
}