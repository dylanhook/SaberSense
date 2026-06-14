// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Configuration;
using SaberSense.Core;
using SaberSense.GUI.Framework.Core;
using UnityEngine;
using VRUIControls;

namespace SaberSense.GUI.Menu.Controllers;

internal sealed partial class PreviewController
{
    public void BuildPreviewWindow(RectTransform mainCanvasRect,
    PhysicsRaycasterWithCache physicsRaycaster, ModSettings settings)
    {
        var (pwRect, pvBg) = BuildWindowFrame(mainCanvasRect, physicsRaycaster);
        var pvContent = BuildContentLayout(pvBg);
        BuildSettingsPanel(pvContent, pwRect, settings);
    }

    private (RectTransform pwRect, UIImage pvBg) BuildWindowFrame(
    RectTransform mainCanvasRect, PhysicsRaycasterWithCache physicsRaycaster)
    {
        var (windowGO, pwRect, pvBg) = FloatingWindowFactory.Build(
        mainCanvasRect, physicsRaycaster, FloatingWindowFactory.Side.Right, "PreviewWindow", "Pv");
        _previewWindowGO = windowGO;
        return (pwRect, pvBg);
    }

    private VBox BuildContentLayout(UIImage pvBg)
    {
        var pvContent = new VBox("PvContent").SetParent(pvBg.RectTransform);
        pvContent.SetAnchors(Vector2.zero, Vector2.one);
        pvContent.RectTransform.sizeDelta = Vector2.zero;
        pvContent.RectTransform.anchoredPosition = Vector2.zero;
        UnityEngine.Object.Destroy(pvContent.GameObject.GetComponent<UnityEngine.UI.ContentSizeFitter>());
        pvContent.SetPadding(UITheme.PreviewPad, UITheme.PreviewPad, UITheme.PreviewPad, UITheme.PreviewPad).SetSpacing(UITheme.PreviewSpacing);

        var pvHeader = new HBox("PvHeader").SetParent(pvContent.RectTransform);
        pvHeader.SetSpacing(UITheme.PreviewHeaderSpacing).AddLayoutElement(minHeight: UITheme.HeaderHeight, preferredHeight: UITheme.HeaderHeight, flexibleHeight: 0);
        var headerLayout = pvHeader.GameObject.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        if (headerLayout != null) headerLayout.childAlignment = TextAnchor.MiddleLeft;

        var pvDot = new UIImage("PvDot").SetColor(UITheme.Accent).SetParent(pvHeader.RectTransform)
        .AddLayoutElement(preferredWidth: UITheme.AccentBarWidth, preferredHeight: UITheme.LabelHeight);
        UITheme.TrackAccent(pvDot.ImageComponent);

        _previewTitleLabel = new UILabel("PvTitle", "PREVIEW").SetFontSize(UITheme.FontNormal).SetColor(UITheme.TextPrimary)
        .SetAlignment(TMPro.TextAlignmentOptions.MidlineLeft);
        _previewTitleLabel.TextComponent.enableAutoSizing = true;
        _previewTitleLabel.TextComponent.fontSizeMin = 2.0f;
        _previewTitleLabel.TextComponent.overflowMode = TMPro.TextOverflowModes.Overflow;
        _previewTitleLabel.SetParent(pvHeader.RectTransform).AddLayoutElement(flexibleWidth: 1);

        new UIImage("PvHdrSep").SetColor(UITheme.Divider)
        .SetParent(pvContent.RectTransform).AddLayoutElement(preferredHeight: UITheme.SeparatorHeight, flexibleWidth: 1);

        _saberPreview = new UISaberPreview("SaberPv");
        _coverService?.SetCaptureSource(size => _saberPreview?.CaptureSnapshot(size)!);
        _saberPreview.SetParent(pvContent.RectTransform).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        var timerBarContainer = new UIImage("PvTimerBg").SetColor(UITheme.Divider);
        timerBarContainer.SetParent(pvContent.RectTransform).AddLayoutElement(preferredHeight: UITheme.SeparatorHeight, flexibleWidth: 1);

        var timerFill = new UIImage("PvTimerFill").SetColor(UITheme.Accent);
        UITheme.TrackAccent(timerFill.ImageComponent);
        _timerFillRect = timerFill.RectTransform;
        _timerFillRect.SetParent(timerBarContainer.RectTransform, false);
        _timerFillRect.anchorMin = Vector2.zero;
        _timerFillRect.anchorMax = Vector2.zero;
        _timerFillRect.offsetMin = Vector2.zero;
        _timerFillRect.offsetMax = Vector2.zero;

        new UIImage("PvSepSpacer").SetColor(Color.clear).SetParent(pvContent.RectTransform).AddLayoutElement(preferredHeight: 0.5f, flexibleWidth: 1);

        return pvContent;
    }

    private void BuildSettingsPanel(VBox pvContent, RectTransform pwRect, ModSettings settings)
    {
        var pvSettingsPanel = new UIGroupBox("Settings");
        pvSettingsPanel.SetParent(pvContent.RectTransform).AddLayoutElement(flexibleWidth: 1);

        var bloomToggle = new UIToggle().Bind(settings, c => c.Editor.Bloom, _bindingScope, val => { _saberPreview?.SetBloom(val); });
        UILayoutFactory.CheckboxRow("Bloom", bloomToggle, pvSettingsPanel.Content, experimental: true);
        _saberPreview?.SetBloom(settings.Editor.Bloom);

        var trailToggle = new UIToggle().Bind(settings, c => c.Editor.DisplayTrails, _bindingScope, val =>
        {
            _saberPreview?.SetDisplayTrails(val);
            SetTrailVisualizerVisible(val);
        });
        UILayoutFactory.CheckboxRow("Display trails", trailToggle, pvSettingsPanel.Content);
        _saberPreview?.SetDisplayTrails(settings.Editor.DisplayTrails);
        SetTrailVisualizerVisible(settings.Editor.DisplayTrails);

        var rotSpeedSlider = new UISlider().SetRange(-100, 100).Bind(settings, c => c.Editor.RotationSpeed, _bindingScope, val =>
        {
            _saberPreview?.SetRotation(settings.Editor.Rotation, val);
        });
        rotSpeedSlider.SetLabelFormatter(v => $"{Mathf.RoundToInt(v)}%");

        var rotToggle = new UIToggle().Bind(settings, c => c.Editor.Rotation, _bindingScope, val =>
        {
            _saberPreview?.SetRotation(val, settings.Editor.RotationSpeed);
        });
        UILayoutFactory.CheckboxRow("Rotation", rotToggle, pvSettingsPanel.Content);
        var rotSpeedRow = UILayoutFactory.SliderRow("Rotation amount", rotSpeedSlider, pvSettingsPanel.Content);
        rotToggle.ControlsVisibility(rotSpeedRow);
        _saberPreview?.SetRotation(settings.Editor.Rotation, settings.Editor.RotationSpeed);

        _previewModeCombo = new UIComboBox("PreviewModeCombo", pwRect)
        .SetOptions(["Automatic", "Left saber", "Right saber"])
        .SetSelected(settings.Editor.SaberPreviewMode)
        .SetOpenUpward()
        .OnSelect((idx, _) =>
        {
            settings.Editor.SaberPreviewMode = idx;
            _isAutoMode = idx == 0;
            _autoSwitchTimer = 0f;
            if (idx == 1)
            {
                _previewSession.FocusedHand = SaberHand.Left;
                ShowFocusedSaber();
            }
            else if (idx == 2)
            {
                _previewSession.FocusedHand = SaberHand.Right;
                ShowFocusedSaber();
            }
            UpdateTimerBar();
        });
        UILayoutFactory.DropdownRow("Saber preview", _previewModeCombo, pvSettingsPanel.Content);

        _isAutoMode = settings.Editor.SaberPreviewMode == 0;
        if (settings.Editor.SaberPreviewMode == 1)
        _previewSession.FocusedHand = SaberHand.Left;
        else if (settings.Editor.SaberPreviewMode == 2)
        _previewSession.FocusedHand = SaberHand.Right;
        UpdateTimerBar();

        pvSettingsPanel.SizeToContent();
        pvSettingsPanel.GameObject.SetActive(false);

        var pvSettingsBtn = new BaseButton("Preview settings");
        pvSettingsBtn.SetParent(pvContent.RectTransform).AddLayoutElement(flexibleWidth: 1, minHeight: UITheme.ActionRowHeight, preferredHeight: UITheme.ActionRowHeight);
        pvSettingsBtn.OnClick = () =>
        {
            bool expanding = !pvSettingsPanel.GameObject.activeSelf;
            pvSettingsPanel.GameObject.SetActive(expanding);
            pvSettingsBtn.SetText(expanding ? "Close settings" : "Preview settings");
        };
    }
}