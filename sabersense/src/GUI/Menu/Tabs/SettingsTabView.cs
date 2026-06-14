// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.App;
using SaberSense.Catalog;
using SaberSense.Configuration;
using SaberSense.Core.Logging;
using SaberSense.Core.Messaging;
using SaberSense.Customization;
using SaberSense.GUI.Framework.Core;
using SaberSense.GUI.Menu.Controllers;
using SaberSense.Profiles;
using SaberSense.Services;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Menu.Tabs;

internal sealed class SettingsTabView : IMenuTab
{
    public string Title => "Settings";

    public string IconPath => IconPaths.Gear;

    private readonly ModSettings _settings;
    private readonly InternalConfig _internalConfig;
    private readonly AppPaths _appPaths;
    private readonly IMessageBroker _broker;
    private readonly DefaultSaberProvider _defaultSaberProvider;
    private readonly IConfigStore _configManager;
    private readonly TrailSettingsController _trailController;
    private readonly SaberTransformController _transformController;
    private readonly PreviewSession _previewSession;
    private readonly SaberLoadout _loadout;
    private readonly IModLogger _log;

    private RectTransform _canvasRoot = null!;
    private readonly BindingScope _bindingScope = new();

    private GameObject _infoContent = null!;
    private GameObject _gameplayGroup = null!;
    private GameObject _renderingGroup = null!;

    private WorldModSection _worldModSection = null!;
    private ConfigManagementSection _configSection = null!;

    public SettingsTabView(
    ModSettings settings,
    InternalConfig internalConfig,
    AppPaths appPaths,
    IMessageBroker broker,
    DefaultSaberProvider defaultSaberProvider,
    IConfigStore configManager,
    TrailSettingsController trailController,
    SaberTransformController transformController,
    PreviewSession previewSession,
    SaberLoadout loadout,
    IModLogger log)
    {
        _settings = settings;
        _internalConfig = internalConfig;
        _appPaths = appPaths;
        _broker = broker;
        _defaultSaberProvider = defaultSaberProvider;
        _configManager = configManager;
        _trailController = trailController;
        _transformController = transformController;
        _previewSession = previewSession;
        _loadout = loadout;
        _log = log.ForSource(nameof(SettingsTabView));
    }

    public GameObject Build(MenuTabContext ctx)
    {
        _canvasRoot = ctx.CanvasRoot;
        _worldModSection = new WorldModSection(_settings, _bindingScope, ctx.CanvasRoot);
        _configSection = new ConfigManagementSection(
        _settings, _internalConfig, _appPaths, _broker,
        _configManager, _trailController, _transformController,
        _defaultSaberProvider, ctx.CanvasRoot, _log);

        var root = UILayoutFactory.TabRoot("SettingsTab", ctx.Parent);

        var columns = new HBox("SettingsCols").SetParent(root);
        UnityEngine.Object.Destroy(columns.GameObject.GetComponent<ContentSizeFitter>());
        columns.SetSpacing(UITheme.ColumnGap).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        BuildLeftColumn(columns.RectTransform);
        BuildRightColumn(columns.RectTransform);

        _configSection.SetPanelCallbacks(
        hideNormalPanels: () =>
        {
            _infoContent?.SetActive(false);
            _gameplayGroup?.SetActive(false);
            _renderingGroup?.SetActive(false);
        },
        showNormalPanels: () =>
        {
            _infoContent?.SetActive(true);
            _gameplayGroup?.SetActive(true);
            _renderingGroup?.SetActive(true);
        });

        return root.gameObject;
    }

    private void BuildLeftColumn(RectTransform parent)
    {
        var leftCol = new VBox("LeftCol").SetParent(parent).SetAlignment(TextAnchor.UpperLeft);
        UnityEngine.Object.Destroy(leftCol.GameObject.GetComponent<ContentSizeFitter>());
        leftCol.SetPadding(0, 0, 0, 0).SetSpacing(UITheme.GroupGap).AddLayoutElement(flexibleWidth: 1);

        var leftGroup = new UIGroupBox("Gameplay");
        leftGroup.SetParent(leftCol.RectTransform).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);
        _gameplayGroup = leftGroup.GameObject;

        BuildGameplaySection(leftGroup.Content);
        BuildRenderingSection(leftCol.RectTransform);
        _worldModSection.Build(leftGroup.Content);

        _configSection.BuildControlsPanel(leftCol.RectTransform);
    }

    private void BuildGameplaySection(RectTransform parent)
    {
        var swing = new ToggleRow("Swing extrapolation", c => c.SwingExtrapolation.Enabled) { Experimental = true };

        RenderRows(
        [
        new ToggleRow("Enable SaberSense", c => c.IsActive),
        new ToggleRow("Event manager", c => c.EnableEventManager),
        new ToggleRow("Randomize saber", c => c.RandomizeSaber),
        new CustomRow(BuildDefaultSaberToggle),
        new CustomRow(BuildPauseKeyRow),
        new CustomRow(BuildWarningMarkerRows),
        new ToggleRow("Hide platform", c => c.HidePlatform),
        new ToggleRow("Disable saber standby", c => c.KeepSabersOnFocusLoss),
        new CustomRow(BuildFloorCalibrationRows),
        swing,
        new SliderRow("Strength", c => c.SwingExtrapolation.Strength, 0, 100) { ParentToggle = swing },
        new ToggleRow("Extrapolate in-game only", c => c.SwingExtrapolation.GameOnly) { ParentToggle = swing },
        ], parent);
    }

    private abstract record SettingRow
    {
        public ToggleRow? ParentToggle { get; init; }
    }

    private sealed record ToggleRow(
    string Label,
    Expression<Func<ModSettings, bool>> Binding,
    Action<bool>? OnChanged = null) : SettingRow
    {
        public bool Experimental { get; init; }
    }

    private sealed record SliderRow(
    string Label,
    Expression<Func<ModSettings, float>> Binding,
    float Min,
    float Max,
    Action<float>? OnChanged = null) : SettingRow;

    private sealed record IntSliderRow(
    string Label,
    Expression<Func<ModSettings, int>> Binding,
    float Min,
    float Max,
    Action<int>? OnChanged = null) : SettingRow;

    private sealed record MultiDropdownRow(
    string ComboName,
    string Label,
    IReadOnlyList<string> Options,
    Expression<Func<ModSettings, List<int>>> Binding,
    Action<HashSet<int>>? OnChanged = null) : SettingRow;

    private sealed record CustomRow(Action<RectTransform> Build) : SettingRow;

    private void RenderRows(IReadOnlyList<SettingRow> rows, RectTransform parent)
    {
        var toggles = new Dictionary<ToggleRow, UIToggle>();

        foreach (var row in rows)
        {
            GameObject? rowObject = null;

            switch (row)
            {
                case ToggleRow t:
                {
                    var toggle = new UIToggle().Bind(_settings, t.Binding, _bindingScope, t.OnChanged);
                    rowObject = UILayoutFactory.CheckboxRow(t.Label, toggle, parent, t.Experimental);
                    toggles[t] = toggle;
                    break;
                }
                case SliderRow s:
                {
                    var slider = new UISlider().SetRange(s.Min, s.Max).Bind(_settings, s.Binding, _bindingScope, s.OnChanged);
                    rowObject = UILayoutFactory.SliderRow(s.Label, slider, parent);
                    break;
                }
                case IntSliderRow i:
                {
                    var slider = new UISlider().SetRange(i.Min, i.Max).BindInt(_settings, i.Binding, _bindingScope, i.OnChanged);
                    rowObject = UILayoutFactory.SliderRow(i.Label, slider, parent);
                    break;
                }
                case MultiDropdownRow m:
                {
                    var combo = new UIMultiComboBox(m.ComboName, _canvasRoot);
                    combo.SetOptions([.. m.Options]);
                    combo.BindList(_settings, m.Binding, _bindingScope, m.OnChanged);
                    rowObject = UILayoutFactory.DropdownRow(m.Label, combo, parent);
                    break;
                }
                case CustomRow c:
                {
                    c.Build(parent);
                    break;
                }
            }

            if (rowObject is not null && row.ParentToggle is { } parentToggle
            && toggles.TryGetValue(parentToggle, out var control))
            {
                control.ControlsVisibility(rowObject);
            }
        }
    }

    private void BuildDefaultSaberToggle(RectTransform parent)
    {
        var defaultSaberToggle = new UIToggle().Bind(_settings, c => c.ShowDefaultSaber, _bindingScope, val =>
        {
            if (val)
            {
                _defaultSaberProvider.Register();
            }
            else
            {
                if (IsDefaultSaberEquipped())
                {
                    _previewSession?.WipePreviews();
                    _loadout.Left.Equipped = null;
                    _loadout.Right.Equipped = null;
                }

                ClearDefaultSaberTrailReferences();
                _defaultSaberProvider.Unregister();
            }
            _broker?.Publish(new SettingsChangedMsg());
        });
        UILayoutFactory.CheckboxRow("Show default saber", defaultSaberToggle, parent);

        if (_settings.ShowDefaultSaber) _defaultSaberProvider?.Register();
        else _defaultSaberProvider?.Unregister();
    }

    private void BuildPauseKeyRow(RectTransform parent)
    {
        var pauseKeybind = new UIKeybindButton("PauseKeybind");
        pauseKeybind.BindInt(_settings, c => c.PauseKeyButton, scope: _bindingScope);
        var pauseToggle = new UIToggle().Bind(_settings, c => c.PauseKeyEnabled, scope: _bindingScope);
        UILayoutFactory.CheckboxKeybindRow("Override pause key", pauseToggle, pauseKeybind, parent);
    }

    private void BuildWarningMarkerRows(RectTransform parent)
    {
        var warningToggle = new UIToggle().Bind(_settings, c => c.WarningMarkerEnabled, scope: _bindingScope);
        UILayoutFactory.CheckboxRow("Warning markers", warningToggle, parent);

        var warningTypesCombo = new UIMultiComboBox("WarningTypesCombo", _canvasRoot);
        warningTypesCombo.SetOptions(["Resets", "Horizontals", "All notes"]);
        warningTypesCombo.BindList(_settings, c => c.WarningTypes, scope: _bindingScope);
        var goWarningTypes = UILayoutFactory.DropdownRow("Warning types", warningTypesCombo, parent);
        warningToggle.ControlsVisibility(goWarningTypes);

        var warningLayerCombo = new UIMultiComboBox("WarningLayerCombo", _canvasRoot);
        warningLayerCombo.SetOptions(["Top", "Middle", "Bottom"]);
        warningLayerCombo.BindList(_settings, c => c.WarningLayerFilter, scope: _bindingScope);
        var goWarningLayers = UILayoutFactory.DropdownRow("Layer filter", warningLayerCombo, parent);
        warningToggle.ControlsVisibility(goWarningLayers);
    }

    private void BuildFloorCalibrationRows(RectTransform parent)
    {
        var toggle = new UIToggle().Bind(_settings, c => c.FloorCalibrationEnabled, scope: _bindingScope);
        UILayoutFactory.CheckboxRow("Floor calibration", toggle, parent);

        var calibrateBtn = new BaseButton("Calibrate floor").SetParent(parent)
        .AddLayoutElement(preferredHeight: UITheme.ActionRowHeight, flexibleWidth: 1);
        calibrateBtn.OnClick = OnCalibrateFloor;
        toggle.ControlsVisibility(calibrateBtn.GameObject);
    }

    private void BuildRenderingSection(RectTransform parentCol)
    {
        var renderingGroup = new UIGroupBox("Rendering");
        renderingGroup.SetParent(parentCol).AddLayoutElement(flexibleWidth: 1, preferredHeight: 26);
        _renderingGroup = renderingGroup.GameObject;

        var desktopVisCombo = new UIMultiComboBox("DesktopVisCombo", _canvasRoot);
        desktopVisCombo.SetOptions(SaberSense.Gameplay.ViewFeatureRegistry.GetAllLabels());
        if (_settings is not null) desktopVisCombo.BindList(_settings, c => c.Visibility.Desktop, scope: _bindingScope);
        UILayoutFactory.DropdownRow("Desktop view", desktopVisCombo, renderingGroup.Content);

        var hmdVisCombo = new UIMultiComboBox("HmdVisCombo", _canvasRoot);
        hmdVisCombo.SetOptions(SaberSense.Gameplay.ViewFeatureRegistry.GetAllLabels());
        if (_settings is not null) hmdVisCombo.BindList(_settings, c => c.Visibility.Hmd, scope: _bindingScope);
        UILayoutFactory.DropdownRow("HMD view", hmdVisCombo, renderingGroup.Content);
    }

    private void OnCalibrateFloor()
    {
        var left = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
        var right = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);

        bool hasLeft = left.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out var lp);
        bool hasRight = right.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out var rp);

        if (!hasLeft && !hasRight) return;

        float floorY;
        if (hasLeft && hasRight) floorY = Mathf.Min(lp.y, rp.y);
        else floorY = hasLeft ? lp.y : rp.y;

        const float controllerThickness = 0.035f;
        float calibration = -(floorY - controllerThickness);

        if (_settings is not null)
        {
            _settings.FloorCalibrationY = calibration;
            SaberSense.Patches.FloorCalibrationPatch.ApplyCalibration(calibration);
        }
    }

    private void BuildRightColumn(RectTransform parent)
    {
        var rightCol = new VBox("RightCol").SetParent(parent);
        UnityEngine.Object.Destroy(rightCol.GameObject.GetComponent<ContentSizeFitter>());
        rightCol.SetSpacing(UITheme.GroupGap).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        var infoGroup = new UIGroupBox("Information");
        infoGroup.SetParent(rightCol.RectTransform).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        var actionKeybind = new UIKeybindButton("ActionKeybind");
        actionKeybind.BindInt(_settings, c => c.ActionKeyButton, scope: _bindingScope);
        UILayoutFactory.KeybindRow("Action key", actionKeybind, infoGroup.Content);

        var accentRow = new HBox("AccentColorRow").SetParent(infoGroup.Content);
        accentRow.SetSpacing(UITheme.ColumnGap).SetPadding(0, 0, 0, 0).AddLayoutElement(preferredHeight: UITheme.LabelHeight, flexibleWidth: 1);
        accentRow.LayoutGroup.childAlignment = TextAnchor.MiddleLeft;
        accentRow.LayoutGroup.childForceExpandHeight = false;
        new UILabel("AccentL", "Accent color").SetFontSize(UITheme.FontSmall).SetColor(UITheme.TextLabel)
        .SetAlignment(TMPro.TextAlignmentOptions.Left).SetParent(accentRow.RectTransform).AddLayoutElement(flexibleWidth: 1, preferredHeight: UITheme.LabelHeight);
        var accentPicker = new UIColorPicker("AccentCP", _canvasRoot)
        .SetColor(_settings?.AccentColor ?? UITheme.Accent)
        .OnColorChanged(c =>
        {
            UITheme.SetAccentLive(c);
        })
        .OnCommit(c =>
        {
            UITheme.SetAccent(c);
            if (_settings is not null) _settings.AccentColor = c;
        });
        accentPicker.SetParent(accentRow.RectTransform).AddLayoutElement(preferredWidth: UITheme.SwatchWidth, preferredHeight: UITheme.SwatchHeight);

        if (_settings is not null)
        {
            _bindingScope.Add(_settings!, (_, e) =>
            {
                if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName.StartsWith("Accent"))
                accentPicker.SetColor(_settings.AccentColor);
            });
        }
        accentPicker.SetResetColor(UITheme.DefaultAccent);
        var discordBtn = new BaseButton("Discord").SetParent(infoGroup.Content).AddLayoutElement(preferredHeight: UITheme.ActionRowHeight, flexibleWidth: 1);
        discordBtn.OnClick = () => OpenExternal(SaberSense.App.ExternalLinks.Discord);

        new UILabel("Sp2", "").SetParent(infoGroup.Content).AddLayoutElement(flexibleHeight: 1);

        var getMoreBtn = new BaseButton("Get more sabers").SetParent(infoGroup.Content).AddLayoutElement(preferredHeight: UITheme.ActionRowHeight, flexibleWidth: 1);
        getMoreBtn.OnClick = () => OpenExternal(SaberSense.App.ExternalLinks.ModelSaber);

        new UILabel("Sp6", "").SetParent(infoGroup.Content).AddLayoutElement(flexibleHeight: 1);

        var configBtn = new BaseButton("Configuration").SetParent(infoGroup.Content).AddLayoutElement(preferredHeight: UITheme.ActionRowHeight, flexibleWidth: 1);
        configBtn.OnClick = () => _configSection.Show();

        new UILabel("Sp5b", "").SetParent(infoGroup.Content).AddLayoutElement(flexibleHeight: 1);

        new UILabel("Credit", "mod by youtube.com/dylanhook")
        .SetFontSize(UITheme.FontSmall).SetColor(UITheme.TextVersion)
        .SetAlignment(TMPro.TextAlignmentOptions.Center)
        .SetParent(infoGroup.Content).AddLayoutElement(preferredHeight: UITheme.SectionLabelHeight);

        _infoContent = infoGroup.GameObject;

        _configSection.BuildConfigPanel(rightCol.RectTransform);
    }

    private void OpenExternal(string path)
    {
        try { System.Diagnostics.Process.Start(path); }
        catch (Exception ex) { _log.Debug($"Open external failed: {ex.Message}"); }
    }

    public void Dispose()
    {
        _bindingScope.Dispose();
        _configSection.Dispose();
    }

    private bool IsDefaultSaberEquipped()
    {
        var entry = _previewSession?.ActiveEntry;
        if (entry is null) return false;
        return entry.LeftPiece?.Asset?.RelativePath == DefaultSaberProvider.DefaultSaberPath;
    }

    private void ClearDefaultSaberTrailReferences()
    {
        bool cleared = false;
        cleared |= ClearTrailIfDefault(_loadout.Left);
        cleared |= ClearTrailIfDefault(_loadout.Right);

        if (cleared)
        _broker?.Publish(new SaberSense.Core.Messaging.PreviewSaberChangedMsg(_previewSession?.ActiveEntry!));

        static bool ClearTrailIfDefault(SaberProfile profile)
        {
            if (profile?.Customization?.TrailSettings is { } ts
            && ts.OriginAssetPath == DefaultSaberProvider.DefaultSaberPath)
            {
                profile.Customization.TrailSettings = null;
                return true;
            }
            return false;
        }
    }
}