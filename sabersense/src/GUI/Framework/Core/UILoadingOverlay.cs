// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

internal sealed class UILoadingOverlay : UIElement
{
    private const float BackdropAlpha = 0.55f;
    private const float PanelWidth = 48f;
    private const float PanelHeight = 12f;
    private const float BgInset = 0.4f;
    private const float BarHeight = 0.6f;
    private const float BarPad = 1f;

    private readonly RectTransform _canvasRoot;
    private readonly UIImage _backdrop;
    private readonly UILabel _phaseLabel;
    private readonly Image _barFillImg;
    private readonly RectTransform _barFill;
    private float _progress;

    public bool IsVisible { get; private set; }

    public UILoadingOverlay(RectTransform canvasRoot) : base("LoadingOverlay")
    {
        _canvasRoot = canvasRoot;

        _backdrop = new UIImage("LoadingBackdrop")
        .SetColor(new Color(0, 0, 0, BackdropAlpha));
        _backdrop.ImageComponent.raycastTarget = true;
        UIPopupHelper.SetupPopupCanvas(_backdrop.GameObject, _canvasRoot, UIZLayer.LoadingOverlay);

        var panel = new GameObject("LoadingPanel");
        panel.transform.SetParent(_backdrop.RectTransform, false);
        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        const string title = "Loading";
        var titleLabel = new UILabel("LoadingTitle", title)
        .SetFontSize(UITheme.FontSmall)
        .SetColor(UITheme.TextLabel);
        titleLabel.TextComponent.fontStyle = TMPro.FontStyles.Bold;
        titleLabel.TextComponent.alignment = TMPro.TextAlignmentOptions.Left;
        titleLabel.RectTransform.SetParent(panelRect.transform, false);
        titleLabel.RectTransform.anchorMin = new Vector2(0, 1);
        titleLabel.RectTransform.anchorMax = new Vector2(0, 1);
        titleLabel.RectTransform.pivot = new Vector2(0, 0.5f);

        float textStartX = 4f;
        float textWidth = titleLabel.TextComponent.GetPreferredValues(title).x;
        titleLabel.RectTransform.anchoredPosition = new Vector2(textStartX, 0);
        titleLabel.RectTransform.sizeDelta = new Vector2(textWidth, 4);
        textStartX -= 0.8f;
        textWidth += 1.6f;

        var bg = new UIImage("PanelBg").SetColor(UITheme.SurfaceInner);
        bg.RectTransform.SetParent(panelRect.transform, false);
        bg.SetAnchors(Vector2.zero, Vector2.one);
        bg.RectTransform.offsetMin = new Vector2(BgInset, BgInset);
        bg.RectTransform.offsetMax = new Vector2(-BgInset, -BgInset);

        UIBorderUtils.DrawBorderLines("Outer", panelRect, UITheme.Border, 0f, 0.2f, textStartX, textWidth);
        UIBorderUtils.DrawBorderLines("Inner", panelRect, UITheme.Divider, 0.2f, 0.2f, textStartX, textWidth);
        titleLabel.RectTransform.SetAsLastSibling();

        _phaseLabel = new UILabel("PhaseLabel", "Preparing...")
        .SetFontSize(UITheme.FontNormal)
        .SetColor(UITheme.TextPrimary)
        .SetAlignment(TMPro.TextAlignmentOptions.Left);
        _phaseLabel.RectTransform.SetParent(bg.RectTransform, false);
        _phaseLabel.SetAnchors(Vector2.zero, Vector2.one);
        _phaseLabel.RectTransform.offsetMin = new Vector2(2f, BarHeight + BarPad + 0.5f);
        _phaseLabel.RectTransform.offsetMax = new Vector2(-2f, -2f);

        var barTrack = new UIImage("BarTrack").SetColor(UITheme.SurfaceDark);
        barTrack.RectTransform.SetParent(bg.RectTransform, false);
        barTrack.RectTransform.anchorMin = Vector2.zero;
        barTrack.RectTransform.anchorMax = new Vector2(1, 0);
        barTrack.RectTransform.offsetMin = new Vector2(BarPad, BarPad);
        barTrack.RectTransform.offsetMax = new Vector2(-BarPad, BarPad + BarHeight);

        var barFillEl = new UIImage("BarFill").SetColor(UITheme.Accent);
        _barFillImg = barFillEl.ImageComponent;
        UITheme.TrackAccent(_barFillImg);
        barFillEl.RectTransform.SetParent(barTrack.RectTransform, false);
        barFillEl.RectTransform.anchorMin = Vector2.zero;
        barFillEl.RectTransform.anchorMax = new Vector2(0, 1);
        barFillEl.RectTransform.offsetMin = Vector2.zero;
        barFillEl.RectTransform.offsetMax = Vector2.zero;
        _barFill = barFillEl.RectTransform;

        _backdrop.SetActive(false);
    }

    public async Task ShowAsync(string saberName = "")
    {
        _progress = 0f;
        UpdateBar();
        SetPhase("Preparing...");

        _backdrop.RectTransform.SetParent(_canvasRoot, false);
        _backdrop.SetAnchors(Vector2.zero, Vector2.one);
        _backdrop.RectTransform.sizeDelta = Vector2.zero;
        _backdrop.RectTransform.SetAsLastSibling();
        _backdrop.SetActive(true);
        IsVisible = true;

        await Task.Yield();
        await Task.Yield();
    }

    public void SetPhase(string text, float progress = -1f)
    {
        _phaseLabel.SetText(text);
        if (progress >= 0f)
        {
            _progress = Mathf.Clamp01(progress);
            UpdateBar();
        }
    }

    public void Hide()
    {
        IsVisible = false;
        _backdrop.SetActive(false);
        _backdrop.RectTransform.SetParent(RectTransform, false);
    }

    public override void Dispose()
    {
        if (IsDisposed) return;
        UITheme.UntrackAccent(_barFillImg);
        Hide();
        base.Dispose();
    }

    private void UpdateBar() => _barFill.anchorMax = new Vector2(_progress, 1);
}